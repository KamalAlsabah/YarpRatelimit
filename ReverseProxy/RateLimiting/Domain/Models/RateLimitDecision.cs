using ReverseProxy.RateLimiting.Domain.Models.Strategies;
using System;

namespace ReverseProxy.RateLimiting.Domain.Models
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
