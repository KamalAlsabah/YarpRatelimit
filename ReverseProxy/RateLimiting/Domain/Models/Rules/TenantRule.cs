using ReverseProxy.RateLimiting.Domain.Models.Strategies;
using System;
using System.Collections.Immutable;

namespace ReverseProxy.RateLimiting.Domain.Models.Rules
{
    public sealed class TenantRule
    {
        public string Name { get; }
        public string Description { get; }
        public ImmutableHashSet<int> TenantIds { get; }
        public ImmutableHashSet<string> ClientIds { get; }
        public RateLimitStrategy Strategy { get; }
        public bool IsEnabled { get; }
        public int Priority { get; }

        public TenantRule(
            string name,
            string description,
            ImmutableHashSet<int> tenantIds,
            ImmutableHashSet<string> clientIds,
            RateLimitStrategy strategy,
            bool isEnabled = true,
            int priority = 0)
        {
            Name = name;
            Description = description;
            TenantIds = tenantIds ?? throw new ArgumentNullException(nameof(tenantIds));
            ClientIds = clientIds ?? throw new ArgumentNullException(nameof(clientIds));
            Strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
            IsEnabled = isEnabled;
            Priority = priority;
        }
    }
}
