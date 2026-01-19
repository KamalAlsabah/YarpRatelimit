#nullable enable

using ReverseProxy.RateLimiting.Domain;
using ReverseProxy.RateLimiting.Domain.Resolution;
using ReverseProxy.RateLimiting.Domain.Rules;
using ReverseProxy.RateLimiting.Domain.Strategies;
using ReverseProxy.RateLimiting.Domain.Matchers;

namespace ReverseProxy.RateLimiting.Infrastructure.Resolution
{
    public sealed class TenantRuleResolver : ITenantRuleResolver
    {
        public bool TryResolve(TenantRule rule, RuleResolutionContext context, out RateLimitStrategy? strategy)
        {
            strategy = null;

            if (!rule.IsEnabled)
                return false;

            if (!ActorMatcher.TenantMatches(rule.TenantIds, context.Actor.TenantId))
                return false;

            if (!ActorMatcher.ClientMatches(rule.ClientIds, context.Actor.ClientId))
                return false;

            strategy = rule.Strategy;
            return true;
        }
    }
}
