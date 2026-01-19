#nullable enable

using ReverseProxy.RateLimiting.Domain;
using ReverseProxy.RateLimiting.Domain.Resolution;
using ReverseProxy.RateLimiting.Domain.Rules;
using ReverseProxy.RateLimiting.Domain.Strategies;
using System.Collections.Immutable;

namespace ReverseProxy.RateLimiting.Infrastructure.Resolution
{
    public sealed class RateLimitRuleOrchestrator : IRateLimitRuleOrchestrator
    {
        private readonly IWhitelistRuleResolver _whitelistResolver;
        private readonly IRouteRuleResolver _routeResolver;
        private readonly ITenantRuleResolver _tenantResolver;

        public RateLimitRuleOrchestrator(
            IWhitelistRuleResolver whitelistResolver,
            IRouteRuleResolver routeResolver,
            ITenantRuleResolver tenantResolver)
        {
            _whitelistResolver = whitelistResolver;
            _routeResolver = routeResolver;
            _tenantResolver = tenantResolver;
        }

        public RateLimitDecision Resolve(
            ImmutableList<WhitelistRule> whitelistRules,
            ImmutableList<RouteRule> routeRules,
            ImmutableList<TenantRule> tenantRules,
            RateLimitStrategy globalDefault,
            RuleResolutionContext context)
        {
            foreach (var rule in whitelistRules)
            {
                if (_whitelistResolver.TryResolve(rule, context, out var strategy) && strategy != null)
                    return new RateLimitDecision(strategy, false);
            }

            RateLimitStrategy? routeStrategy = null;
            RouteRule? matchedRouteRule = null;

            foreach (var rule in routeRules)
            {
                if (_routeResolver.TryResolve(rule, context, out var strategy) && strategy != null)
                {
                    routeStrategy = strategy;
                    matchedRouteRule = rule;
                    break;
                }
            }

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

    }
}
