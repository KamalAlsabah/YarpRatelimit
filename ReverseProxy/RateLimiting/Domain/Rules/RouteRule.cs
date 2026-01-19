using ReverseProxy.RateLimiting.Domain.Strategies;
using System;

namespace ReverseProxy.RateLimiting.Domain.Rules
{
    public sealed class RouteRule
    {
        public string Name { get; }
        public string Description { get; }
        public string RouteId { get; }
        public RuleResolutionPriority Priority { get; }
        public RateLimitStrategy Strategy { get; }
        public bool IsEnabled { get; }

        public RouteRule(
            string name,
            string description,
            string routeId,
            RuleResolutionPriority priority,
            RateLimitStrategy strategy,
            bool isEnabled = true)
        {
            Name = name;
            Description = description;
            RouteId = routeId;
            Priority = priority;
            Strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
            IsEnabled = isEnabled;
        }
    }

    public enum RuleResolutionPriority
    {
        RouteWins,
        TenantWins
    }
}
