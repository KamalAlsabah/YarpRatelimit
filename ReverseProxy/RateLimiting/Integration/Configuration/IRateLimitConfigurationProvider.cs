using ReverseProxy.RateLimiting.Domain.Models.Rules;
using ReverseProxy.RateLimiting.Domain.Models.Strategies;
using ReverseProxy.RateLimiting.Infrastructure.Caching;
using System.Collections.Immutable;

namespace ReverseProxy.RateLimiting.Integration.Configuration
{
    public interface IRateLimitConfigurationProvider
    {
        RateLimitConfiguration GetConfiguration();
    }

    public sealed class RateLimitConfiguration
    {
        public ImmutableList<WhitelistRule> WhitelistRules { get; }
        public ImmutableList<RouteRule> RouteRules { get; }
        public ImmutableList<TenantRule> TenantRules { get; }
        public RateLimitStrategy GlobalDefault { get; }
        
        /// <summary>
        /// Pre-indexed cache for fast O(1)/O(log n) lookups
        /// </summary>
        public IndexedRuleCache IndexedCache { get; }

        public RateLimitConfiguration(
            ImmutableList<WhitelistRule> whitelistRules,
            ImmutableList<RouteRule> routeRules,
            ImmutableList<TenantRule> tenantRules,
            RateLimitStrategy globalDefault)
        {
            WhitelistRules = whitelistRules ?? ImmutableList<WhitelistRule>.Empty;
            RouteRules = routeRules ?? ImmutableList<RouteRule>.Empty;
            TenantRules = tenantRules ?? ImmutableList<TenantRule>.Empty;
            GlobalDefault = globalDefault;
            
            // Build indexed cache for performance
            IndexedCache = new IndexedRuleCache(WhitelistRules, RouteRules, TenantRules);
        }
    }
}
