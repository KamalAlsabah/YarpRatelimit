#nullable enable

using ReverseProxy.RateLimiting.Domain.Resolution;
using ReverseProxy.RateLimiting.Domain.Models.Rules;
using ReverseProxy.RateLimiting.Domain.Models.Strategies;
using ReverseProxy.RateLimiting.Domain.Models;
using ReverseProxy.RateLimiting.Domain.Helpers;

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
