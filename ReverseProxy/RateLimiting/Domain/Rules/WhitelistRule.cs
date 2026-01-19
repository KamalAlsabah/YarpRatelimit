using ReverseProxy.RateLimiting.Domain.Strategies;
using System;
using System.Collections.Immutable;

namespace ReverseProxy.RateLimiting.Domain.Rules
{
    public sealed class WhitelistRule
    {
        public string Name { get; }
        public string Description { get; }
        public ImmutableHashSet<string> IpAddresses { get; }
        public ImmutableHashSet<int> TenantIds { get; }
        public ImmutableHashSet<string> ClientIds { get; }
        public ImmutableList<string> EndpointPatterns { get; }
        public bool IsEnabled { get; }

        public WhitelistRule(
            string name,
            string description,
            ImmutableHashSet<string> ipAddresses,
            ImmutableHashSet<int> tenantIds,
            ImmutableHashSet<string> clientIds,
            ImmutableList<string> endpointPatterns,
            bool isEnabled = true)
        {
            Name = name;
            Description = description;
            IpAddresses = ipAddresses ?? throw new ArgumentNullException(nameof(ipAddresses));
            TenantIds = tenantIds ?? throw new ArgumentNullException(nameof(tenantIds));
            ClientIds = clientIds ?? throw new ArgumentNullException(nameof(clientIds));
            EndpointPatterns = endpointPatterns ?? throw new ArgumentNullException(nameof(endpointPatterns));
            IsEnabled = isEnabled;
        }
    }
}
