#nullable enable

using ReverseProxy.RateLimiting.Domain.Models;
using ReverseProxy.RateLimiting.Domain.Models.Rules;
using ReverseProxy.RateLimiting.Domain.Models.Strategies;
using ReverseProxy.RateLimiting.Domain.Resolution;

namespace ReverseProxy.RateLimiting.Infrastructure.Resolution
{
    public sealed class RouteRuleResolver : IRouteRuleResolver
    {
        public bool TryResolve(RouteRule rule, RuleResolutionContext context, out RateLimitStrategy? strategy)
        {
            strategy = null;

            if (!rule.IsEnabled)
                return false;

            if (!string.Equals(rule.RouteId, context.RouteId, System.StringComparison.OrdinalIgnoreCase))
                return false;

            strategy = rule.Strategy;
            return true;
        }
    }
}
