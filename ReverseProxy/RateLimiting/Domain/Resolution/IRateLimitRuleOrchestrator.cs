using ReverseProxy.RateLimiting.Domain.Models;
using ReverseProxy.RateLimiting.Domain.Models.Rules;
using ReverseProxy.RateLimiting.Domain.Models.Strategies;
using ReverseProxy.RateLimiting.Infrastructure.Caching;
using System.Collections.Immutable;

namespace ReverseProxy.RateLimiting.Domain.Resolution
{
    public interface IRateLimitRuleOrchestrator
    {
        RateLimitDecision Resolve(
            ImmutableList<WhitelistRule> whitelistRules,
            ImmutableList<RouteRule> routeRules,
            ImmutableList<TenantRule> tenantRules,
            RateLimitStrategy globalDefault,
            RuleResolutionContext context);

        /// <summary>
        /// Optimized resolution using pre-indexed cache for O(1) lookups
        /// </summary>
        RateLimitDecision ResolveWithCache(
            IndexedRuleCache cache,
            RateLimitStrategy globalDefault,
            RuleResolutionContext context);
    }
}
