#nullable enable

using ReverseProxy.RateLimiting.Domain.Models.Rules;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace ReverseProxy.RateLimiting.Infrastructure.Caching
{
    /// <summary>
    /// Pre-indexed rule cache for O(1)/O(log n) lookups instead of O(n) scans
    /// </summary>
    public sealed class IndexedRuleCache
    {
        // Route-based index: RouteId -> RouteRule
        private readonly IReadOnlyDictionary<string, RouteRule> _routeIndex;

        // Tenant-based index: TenantId -> List of matching TenantRules (pre-sorted by priority)
        private readonly IReadOnlyDictionary<int, ImmutableList<TenantRule>> _tenantIndex;

        // Client-based index for quick lookup
        private readonly IReadOnlyDictionary<string, ImmutableList<TenantRule>> _clientIndex;

        // Fallback: Rules that match "any" tenant/client (empty sets)
        private readonly ImmutableList<TenantRule> _anyTenantRules;
        private readonly ImmutableList<TenantRule> _anyClientRules;

        // Original lists for fallback scenarios
        public ImmutableList<WhitelistRule> WhitelistRules { get; }
        public ImmutableList<RouteRule> RouteRules { get; }
        public ImmutableList<TenantRule> TenantRules { get; }

        public IndexedRuleCache(
            ImmutableList<WhitelistRule> whitelistRules,
            ImmutableList<RouteRule> routeRules,
            ImmutableList<TenantRule> tenantRules)
        {
            WhitelistRules = whitelistRules;
            RouteRules = routeRules;
            TenantRules = tenantRules;

            // Build route index
            _routeIndex = BuildRouteIndex(routeRules);

            // Build tenant indexes
            (_tenantIndex, _clientIndex, _anyTenantRules, _anyClientRules) = BuildTenantIndexes(tenantRules);
        }

        private static IReadOnlyDictionary<string, RouteRule> BuildRouteIndex(ImmutableList<RouteRule> routeRules)
        {
            var index = new Dictionary<string, RouteRule>(System.StringComparer.OrdinalIgnoreCase);

            foreach (var rule in routeRules)
            {
                if (rule.IsEnabled && !string.IsNullOrWhiteSpace(rule.RouteId))
                {
                    // First enabled rule wins for each RouteId
                    if (!index.ContainsKey(rule.RouteId))
                    {
                        index[rule.RouteId] = rule;
                    }
                }
            }

            return index;
        }

        private static (
            IReadOnlyDictionary<int, ImmutableList<TenantRule>> tenantIndex,
            IReadOnlyDictionary<string, ImmutableList<TenantRule>> clientIndex,
            ImmutableList<TenantRule> anyTenantRules,
            ImmutableList<TenantRule> anyClientRules
        ) BuildTenantIndexes(ImmutableList<TenantRule> tenantRules)
        {
            var tenantIndex = new Dictionary<int, List<TenantRule>>();
            var clientIndex = new Dictionary<string, List<TenantRule>>(System.StringComparer.OrdinalIgnoreCase);
            var anyTenantList = new List<TenantRule>();
            var anyClientList = new List<TenantRule>();

            foreach (var rule in tenantRules.Where(r => r.IsEnabled))
            {
                // Index by tenant IDs
                if (rule.TenantIds.IsEmpty)
                {
                    // Matches any tenant
                    anyTenantList.Add(rule);
                }
                else
                {
                    foreach (var tenantId in rule.TenantIds)
                    {
                        if (!tenantIndex.ContainsKey(tenantId))
                        {
                            tenantIndex[tenantId] = new List<TenantRule>();
                        }
                        tenantIndex[tenantId].Add(rule);
                    }
                }

                // Index by client IDs
                if (rule.ClientIds.IsEmpty || rule.ClientIds.Contains("*"))
                {
                    // Matches any client
                    anyClientList.Add(rule);
                }
                else
                {
                    foreach (var clientId in rule.ClientIds)
                    {
                        if (!clientIndex.ContainsKey(clientId))
                        {
                            clientIndex[clientId] = new List<TenantRule>();
                        }
                        clientIndex[clientId].Add(rule);
                    }
                }
            }

            // Convert to immutable and sort by priority (highest first)
            var immutableTenantIndex = tenantIndex.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.OrderByDescending(r => r.Priority).ToImmutableList()
            );

            var immutableClientIndex = clientIndex.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.OrderByDescending(r => r.Priority).ToImmutableList()
            );

            return (
                immutableTenantIndex,
                immutableClientIndex,
                anyTenantList.OrderByDescending(r => r.Priority).ToImmutableList(),
                anyClientList.OrderByDescending(r => r.Priority).ToImmutableList()
            );
        }

        /// <summary>
        /// O(1) lookup for route rule by RouteId
        /// </summary>
        public bool TryGetRouteRule(string? routeId, out RouteRule? rule)
        {
            rule = null;
            if (string.IsNullOrWhiteSpace(routeId))
                return false;

            return _routeIndex.TryGetValue(routeId, out rule);
        }

        /// <summary>
        /// Fast lookup for tenant rules - returns pre-sorted candidates
        /// </summary>
        public IEnumerable<TenantRule> GetTenantRuleCandidates(int? tenantId, string? clientId)
        {
            // Collect candidates from all indexes
            var candidates = new List<TenantRule>();

            // 1. Exact tenant match
            if (tenantId.HasValue && _tenantIndex.TryGetValue(tenantId.Value, out var tenantRules))
            {
                candidates.AddRange(tenantRules);
            }

            // 2. Exact client match
            if (!string.IsNullOrWhiteSpace(clientId) && _clientIndex.TryGetValue(clientId, out var clientRules))
            {
                candidates.AddRange(clientRules);
            }

            // 3. Any tenant rules
            candidates.AddRange(_anyTenantRules);

            // 4. Any client rules
            candidates.AddRange(_anyClientRules);

            // Remove duplicates and return sorted by priority (highest first)
            return candidates
                .Distinct()
                .OrderByDescending(r => r.Priority);
        }
    }
}
