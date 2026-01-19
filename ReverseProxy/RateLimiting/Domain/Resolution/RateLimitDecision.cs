using ReverseProxy.RateLimiting.Domain.Strategies;
using System;

namespace ReverseProxy.RateLimiting.Domain.Resolution
{
    public sealed class RateLimitDecision
    {
        public RateLimitStrategy Strategy { get; }
        public bool IsRouteRule { get; }

        public RateLimitDecision(RateLimitStrategy strategy, bool isRouteRule)
        {
            Strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
            IsRouteRule = isRouteRule;
        }
    }
}
