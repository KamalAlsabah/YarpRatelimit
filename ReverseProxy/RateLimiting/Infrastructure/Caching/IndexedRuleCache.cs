#nullable enable

using ReverseProxy.RateLimiting.Domain.Models.Rules;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace ReverseProxy.RateLimiting.Infrastructure.Caching
{
    /// <summary>
    /// Pre-indexed rule cache for O(1)/O(log n) lookups instead of O(n) scans
    /// - Route index now maps RouteId -> immutable array of RouteRule candidates sorted by explicit priority (deterministic)
    /// - Tenant index avoids per-request allocations by exposing precomputed arrays and an efficient candidate iterator
    /// </summary>
    public sealed partial class IndexedRuleCache
    {
        // Route-based index: RouteId -> RouteRule[] (sorted by deterministic heuristic)
        private readonly IReadOnlyDictionary<string, RouteRule[]> _routeIndex;

        // Tenant-based index: TenantId -> TenantRule[] (pre-sorted by Priority desc)
        private readonly IReadOnlyDictionary<int, TenantRule[]> _tenantIndex;

        // Client-based index: ClientId -> TenantRule[] (pre-sorted)
        private readonly IReadOnlyDictionary<string, TenantRule[]> _clientIndex;

        // Rules that match any tenant/client
        private readonly TenantRule[] _anyTenantRules;
        private readonly TenantRule[] _anyClientRules;

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

            _routeIndex = BuildRouteIndex(routeRules);
            (_tenantIndex, _clientIndex, _anyTenantRules, _anyClientRules) = BuildTenantIndexes(tenantRules);
        }

        private static IReadOnlyDictionary<string, RouteRule[]> BuildRouteIndex(ImmutableList<RouteRule> routeRules)
        {
            var dict = new Dictionary<string, List<RouteRule>>(System.StringComparer.OrdinalIgnoreCase);

            foreach (var rule in routeRules.Where(r => r.IsEnabled && !string.IsNullOrWhiteSpace(r.RouteId)))
            {
                if (!dict.TryGetValue(rule.RouteId, out var list))
                {
                    list = new List<RouteRule>();
                    dict[rule.RouteId] = list;
                }

                list.Add(rule);
            }

            // Sort each bucket deterministically. Current heuristic: RouteWins preferred, then leave original order via stable sort by Priority enum then index.
            var result = new Dictionary<string, RouteRule[]>(System.StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in dict)
            {
                // RouteRule.Priority is enum RouteWins/TenantWins. We want deterministic order: RouteWins first, then TenantWins.
                // Within same priority, preserve original order (stable). Use OrderBy with key and ToArray.
                var arr = kvp.Value
                            .OrderByDescending(r => r.Priority == RuleResolutionPriority.RouteWins)
                            .ToArray();

                result[kvp.Key] = arr;
            }

            return result;
        }

        private static (
            IReadOnlyDictionary<int, TenantRule[]> tenantIndex,
            IReadOnlyDictionary<string, TenantRule[]> clientIndex,
            TenantRule[] anyTenantRules,
            TenantRule[] anyClientRules
        ) BuildTenantIndexes(ImmutableList<TenantRule> tenantRules)
        {
            var tenantIndex = new Dictionary<int, List<TenantRule>>();
            var clientIndex = new Dictionary<string, List<TenantRule>>(System.StringComparer.OrdinalIgnoreCase);
            var anyTenantList = new List<TenantRule>();
            var anyClientList = new List<TenantRule>();

            // Precompute enabled tenant rules into buckets
            foreach (var rule in tenantRules.Where(r => r.IsEnabled))
            {
                if (rule.TenantIds.IsEmpty)
                {
                    anyTenantList.Add(rule);
                }
                else
                {
                    foreach (var tid in rule.TenantIds)
                    {
                        if (!tenantIndex.TryGetValue(tid, out var list))
                        {
                            list = new List<TenantRule>();
                            tenantIndex[tid] = list;
                        }
                        list.Add(rule);
                    }
                }

                if (rule.ClientIds.IsEmpty || rule.ClientIds.Contains("*"))
                {
                    anyClientList.Add(rule);
                }
                else
                {
                    foreach (var cid in rule.ClientIds)
                    {
                        if (!clientIndex.TryGetValue(cid, out var list))
                        {
                            list = new List<TenantRule>();
                            clientIndex[cid] = list;
                        }
                        list.Add(rule);
                    }
                }
            }

            // Convert lists to arrays sorted by descending priority (higher first). Arrays minimize GC pressure vs ImmutableList in hot-path.
            var tIndex = new Dictionary<int, TenantRule[]>();
            foreach (var kvp in tenantIndex)
            {
                var arr = kvp.Value.OrderByDescending(r => r.Priority).ToArray();
                tIndex[kvp.Key] = arr;
            }

            var cIndex = new Dictionary<string, TenantRule[]>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in clientIndex)
            {
                var arr = kvp.Value.OrderByDescending(r => r.Priority).ToArray();
                cIndex[kvp.Key] = arr;
            }

            var anyT = anyTenantList.OrderByDescending(r => r.Priority).ToArray();
            var anyC = anyClientList.OrderByDescending(r => r.Priority).ToArray();

            return (tIndex, cIndex, anyT, anyC);
        }

        /// <summary>
        /// Try get best RouteRule for a route id. Returns first candidate in pre-sorted bucket or null.
        /// </summary>
        public bool TryGetRouteRule(string? routeId, out RouteRule? rule)
        {
            rule = null;
            if (string.IsNullOrWhiteSpace(routeId))
                return false;

            if (_routeIndex.TryGetValue(routeId, out var arr) && arr.Length > 0)
            {
                // Choose the first candidate deterministically (pre-sorted)
                rule = arr[0];
                return true;
            }

            return false;
        }

        /// <summary>
        /// Efficiently enumerate tenant rule candidates in priority order without allocating per-request lists.
        /// Caller should break early on first match.
        /// </summary>
        public IEnumerable<TenantRule> GetTenantRuleCandidates(int? tenantId, string? clientId)
        {
            // 1. Exact tenant
            if (tenantId.HasValue && _tenantIndex.TryGetValue(tenantId.Value, out var tenantArr))
            {
                foreach (var r in tenantArr)
                    yield return r;
            }

            // 2. Exact client
            if (!string.IsNullOrWhiteSpace(clientId) && _clientIndex.TryGetValue(clientId, out var clientArr))
            {
                foreach (var r in clientArr)
                    yield return r;
            }

            // 3. Any tenant rules
            foreach (var r in _anyTenantRules)
                yield return r;

            // 4. Any client rules
            foreach (var r in _anyClientRules)
                yield return r;
        }

        /// <summary>
        /// Fast check whether any tenant rule candidates exist for given actor without allocating enumerables.
        /// Used by metrics to determine cache hit/miss.
        /// </summary>
        public bool HasTenantCandidates(int? tenantId, string? clientId)
        {
            if (tenantId.HasValue && _tenantIndex.TryGetValue(tenantId.Value, out var tenantArr) && tenantArr.Length > 0)
                return true;

            if (!string.IsNullOrWhiteSpace(clientId) && _clientIndex.TryGetValue(clientId!, out var clientArr) && clientArr.Length > 0)
                return true;

            if (_anyTenantRules.Length > 0)
                return true;

            if (_anyClientRules.Length > 0)
                return true;

            return false;
        }
    }
}
