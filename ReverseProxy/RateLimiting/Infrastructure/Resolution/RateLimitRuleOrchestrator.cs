#nullable enable

using ReverseProxy.RateLimiting.Domain.Models;
using ReverseProxy.RateLimiting.Domain.Models.Rules;
using ReverseProxy.RateLimiting.Domain.Models.Strategies;
using ReverseProxy.RateLimiting.Domain.Resolution;
using ReverseProxy.RateLimiting.Infrastructure.Caching;
using ReverseProxy.RateLimiting.Infrastructure.Monitoring;
using System.Collections.Immutable;
using System.Linq;

namespace ReverseProxy.RateLimiting.Infrastructure.Resolution
{
    public sealed class RateLimitRuleOrchestrator : IRateLimitRuleOrchestrator
    {
        private readonly IWhitelistRuleResolver _whitelistResolver;
        private readonly IRouteRuleResolver _routeResolver;
        private readonly ITenantRuleResolver _tenantResolver;
        private readonly IRateLimitMetrics? _metrics;

        public RateLimitRuleOrchestrator(
            IWhitelistRuleResolver whitelistResolver,
            IRouteRuleResolver routeResolver,
            ITenantRuleResolver tenantResolver,
            IRateLimitMetrics? metrics = null)
        {
            _whitelistResolver = whitelistResolver;
            _routeResolver = routeResolver;
            _tenantResolver = tenantResolver;
            _metrics = metrics;
        }

        public RateLimitDecision Resolve(
            ImmutableList<WhitelistRule> whitelistRules,
            ImmutableList<RouteRule> routeRules,
            ImmutableList<TenantRule> tenantRules,
            RateLimitStrategy globalDefault,
            RuleResolutionContext context)
        {
            // Whitelist check - still sequential but typically small (< 10 rules)
            foreach (var rule in whitelistRules)
            {
                if (_whitelistResolver.TryResolve(rule, context, out var strategy) && strategy != null)
                    return new RateLimitDecision(strategy, false);
            }

            RateLimitStrategy? routeStrategy = null;
            RouteRule? matchedRouteRule = null;

            // Route lookup - sequential (could be optimized but typically few rules)
            foreach (var rule in routeRules)
            {
                if (_routeResolver.TryResolve(rule, context, out var strategy) && strategy != null)
                {
                    routeStrategy = strategy;
                    matchedRouteRule = rule;
                    break;
                }
            }

            // Tenant lookup - sequential but pre-sorted by priority
            RateLimitStrategy? tenantStrategy = null;
            foreach (var rule in tenantRules)
            {
                if (_tenantResolver.TryResolve(rule, context, out var strategy) && strategy != null)
                {
                    tenantStrategy = strategy;
                    break;
                }
            }

            if (routeStrategy != null && matchedRouteRule != null && tenantStrategy != null )
            {
                return matchedRouteRule.Priority == RuleResolutionPriority.RouteWins
                    ? new RateLimitDecision(routeStrategy, true)
                    : new RateLimitDecision(tenantStrategy, false);
            }

            if (routeStrategy != null)
                return new RateLimitDecision(routeStrategy, true);

            if (tenantStrategy != null)
                return new RateLimitDecision(tenantStrategy, false);

            return new RateLimitDecision(globalDefault, false);
        }

        /// <summary>
        /// Optimized resolution using pre-indexed cache for O(1) lookups
        /// </summary>
        public RateLimitDecision ResolveWithCache(
            IndexedRuleCache cache,
            RateLimitStrategy globalDefault,
            RuleResolutionContext context)
        {
            // 1. Whitelist check (still sequential - typically small)
            foreach (var rule in cache.WhitelistRules)
            {
                if (_whitelistResolver.TryResolve(rule, context, out var strategy) && strategy != null)
                    return new RateLimitDecision(strategy, false);
            }

            // 2. Route lookup - O(1) dictionary lookup
            RateLimitStrategy? routeStrategy = null;
            RouteRule? matchedRouteRule = null;

            // Pre-check cache presence to deterministically record hit/miss
            bool cacheHit = false;

            // Try get route rule (fast)
            cache.TryGetRouteRule(context.RouteId, out var routeRule);
            if (routeRule != null)
            {
                cacheHit = true;
                if (_routeResolver.TryResolve(routeRule, context, out var rStrategy) && rStrategy != null)
                {
                    routeStrategy = rStrategy;
                    matchedRouteRule = routeRule;
                }
            }

            // 3. Tenant lookup - efficient, pre-filtered and pre-sorted candidates without per-request allocations
            RateLimitStrategy? tenantStrategy = null;

            // Determine if cache produced tenant candidates without allocating
            if (cache.HasTenantCandidates(context.Actor.TenantId, context.Actor.ClientId))
            {
                cacheHit = true;
            }

            var tenantCandidates = cache.GetTenantRuleCandidates(context.Actor.TenantId, context.Actor.ClientId);

            foreach (var rule in tenantCandidates)
            {
                if (_tenantResolver.TryResolve(rule, context, out var tStrategy) && tStrategy != null)
                {
                    tenantStrategy = tStrategy;
                    break; // First match wins (already sorted by priority)
                }
            }

            // update metrics about cache usage
            if (_metrics != null)
            {
                if (cacheHit)
                    _metrics.RecordCacheHit();
                else
                    _metrics.RecordCacheMiss();
            }

            // 4. Priority resolution
            if (routeStrategy != null && matchedRouteRule != null && tenantStrategy != null)
            {
                return matchedRouteRule.Priority == RuleResolutionPriority.RouteWins
                    ? new RateLimitDecision(routeStrategy, true)
                    : new RateLimitDecision(tenantStrategy, false);
            }

            if (routeStrategy != null)
                return new RateLimitDecision(routeStrategy, true);

            if (tenantStrategy != null)
                return new RateLimitDecision(tenantStrategy, false);

            return new RateLimitDecision(globalDefault, false);
        }
    }
}
