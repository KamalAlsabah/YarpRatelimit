using ReverseProxy.RateLimiting.Domain.Rules;
using ReverseProxy.RateLimiting.Domain.Strategies;
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
    }
}
