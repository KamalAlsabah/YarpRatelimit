using Microsoft.Extensions.Options;
using ReverseProxy.RateLimiting.Domain.Rules;
using ReverseProxy.RateLimiting.Domain.Strategies;
using ReverseProxy.RateLimiting.Integration.Configuration;
using StackExchange.Redis;
using System.Collections.Immutable;
using System.Linq;

namespace ReverseProxy.RateLimiting.Integration
{
    public sealed class ConfigurationFromSettingsProvider : IRateLimitConfigurationProvider
    {
        private readonly IOptions<RateLimitSettingsOptions> _options;
        private readonly IConnectionMultiplexer _connection;
        private RateLimitConfiguration _cachedConfig;
        private readonly object _lockObject = new object();

        public ConfigurationFromSettingsProvider(
            IOptions<RateLimitSettingsOptions> options,
            IConnectionMultiplexer connection)
        {
            _options = options;
            _connection = connection;
        }

        public RateLimitConfiguration GetConfiguration()
        {
            if (_cachedConfig != null)
                return _cachedConfig;

            lock (_lockObject)
            {
                if (_cachedConfig != null)
                    return _cachedConfig;

                var settings = _options.Value;

                var whitelistRules = ConvertWhitelistRules(settings.WhitelistRules ?? new());
                var routeRules = ConvertRouteRules(settings.RouteRules ?? new());
                var tenantRules = ConvertTenantRules(settings.TenantRules ?? new());

                var globalDefault = new WhitelistStrategy() as RateLimitStrategy;
                if (settings.GlobalDefault != null)
                {
                    globalDefault = BuildStrategy(settings.GlobalDefault);
                }

                _cachedConfig = new RateLimitConfiguration(
                    whitelistRules,
                    routeRules,
                    tenantRules,
                    globalDefault);

                return _cachedConfig;
            }
        }

        private ImmutableList<WhitelistRule> ConvertWhitelistRules(System.Collections.Generic.List<WhitelistRuleSettings> settings)
        {
            var builder = ImmutableList.CreateBuilder<WhitelistRule>();

            foreach (var setting in settings)
            {
                builder.Add(new WhitelistRule(
                    setting.Name ?? "Whitelist",
                    setting.Description ?? "",
                    setting.IpAddresses?.ToImmutableHashSet() ?? ImmutableHashSet<string>.Empty,
                    setting.TenantIds?.ToImmutableHashSet() ?? ImmutableHashSet<int>.Empty,
                    setting.ClientIds?.ToImmutableHashSet() ?? ImmutableHashSet<string>.Empty,
                    (setting.EndpointPatterns ?? new()).ToImmutableList(),
                    setting.IsEnabled ?? setting.Enabled ?? true
                ));
            }

            return builder.ToImmutable();
        }

        private ImmutableList<RouteRule> ConvertRouteRules(System.Collections.Generic.List<RouteRuleSettings> settings)
        {
            var builder = ImmutableList.CreateBuilder<RouteRule>();

            foreach (var setting in settings)
            {
                var strategy = BuildStrategy(setting.StrategySettings ?? setting.Strategy);

                var priority = ParseRoutePriority(setting.Priority);

                builder.Add(new RouteRule(
                    setting.Name ?? "Route",
                    setting.Description ?? "",
                    setting.RouteId,
                    priority,
                    strategy,
                    setting.IsEnabled ?? setting.Enabled ?? true
                ));
            }

            return builder.ToImmutable();
        }

        private ImmutableList<TenantRule> ConvertTenantRules(System.Collections.Generic.List<TenantRuleSettings> settings)
        {
            var builder = ImmutableList.CreateBuilder<TenantRule>();
            var index = 0;

            foreach (var setting in settings)
            {
                var strategy = BuildStrategy(setting.StrategySettings ?? setting.Strategy);
                var priority = ParseTenantPriority(setting.Priority, settings.Count - index);

                builder.Add(new TenantRule(

                    setting.Name ?? "Tenant",
                    setting.Description ?? "",
                    setting.TenantIds?.ToImmutableHashSet() ?? ImmutableHashSet<int>.Empty,
                    setting.ClientIds?.ToImmutableHashSet() ?? ImmutableHashSet<string>.Empty,
                    strategy,
                    setting.IsEnabled ?? setting.Enabled ?? true,
                    priority
                ));
                
                index++;
            }

            return builder.ToImmutable().OrderByDescending(r => r.Priority).ToImmutableList();
        }

        private RateLimitStrategy BuildStrategy(StrategySettings settings)
        {
            if (settings == null)
                return new WhitelistStrategy();

            var windowSeconds = settings.WindowSeconds ?? ParseTimeSpanSeconds(settings.Window);
            var replenishmentSeconds = settings.ReplenishmentPeriodSeconds ?? ParseTimeSpanSeconds(settings.ReplenishmentPeriod);

            return settings.Type switch
            {
                "TokenBucket" => (RateLimitStrategy)new TokenBucketStrategy(
                    new TokenBucketConfig(
                        settings.TokenLimit ?? 100,
                        settings.TokensPerPeriod ?? 10,
                        System.TimeSpan.FromSeconds(replenishmentSeconds ?? 60)),
                    _connection),

                "Concurrency" => (RateLimitStrategy)new ConcurrencyStrategy(
                    new ConcurrencyConfig(
                        settings.PermitLimit ?? 10,
                        settings.QueueLimit ?? 20),
                    _connection),

                "FixedWindow" => (RateLimitStrategy)new FixedWindowStrategy(
                    new FixedWindowConfig(
                        settings.PermitLimit ?? 10,
                        System.TimeSpan.FromSeconds(windowSeconds ?? 60)),
                    _connection),

                "SlidingWindow" => (RateLimitStrategy)new SlidingWindowStrategy(
                    new SlidingWindowConfig(
                        settings.PermitLimit ?? 10,
                        System.TimeSpan.FromSeconds(windowSeconds ?? 60)),
                    _connection),

                _ => new WhitelistStrategy()
            };
        }

        private static RuleResolutionPriority ParseRoutePriority(string priority)
        {
            if (string.IsNullOrWhiteSpace(priority))
                return RuleResolutionPriority.RouteWins;

            return priority.Equals("TenantWins", System.StringComparison.OrdinalIgnoreCase)
                ? RuleResolutionPriority.TenantWins
                : RuleResolutionPriority.RouteWins;
        }

        private static int ParseTenantPriority(string priority, int fallback)
        {
            if (string.IsNullOrWhiteSpace(priority))
                return fallback;

            if (int.TryParse(priority, out var value))
                return value;

            return fallback;
        }

        private static int? ParseTimeSpanSeconds(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (System.TimeSpan.TryParse(value, out var ts))
                return (int)ts.TotalSeconds;

            return null;
        }
    }

    public sealed class RateLimitSettingsOptions
    {
        public System.Collections.Generic.List<WhitelistRuleSettings> WhitelistRules { get; set; }
        public System.Collections.Generic.List<RouteRuleSettings> RouteRules { get; set; }
        public System.Collections.Generic.List<TenantRuleSettings> TenantRules { get; set; }
        public StrategySettings GlobalDefault { get; set; }
    }

    public sealed class WhitelistRuleSettings
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public System.Collections.Generic.List<string> IpAddresses { get; set; }
        public System.Collections.Generic.List<int> TenantIds { get; set; }
        public System.Collections.Generic.List<string> ClientIds { get; set; }
        public System.Collections.Generic.List<string> EndpointPatterns { get; set; }
        public bool? IsEnabled { get; set; }
        public bool? Enabled { get; set; }
    }

    public sealed class RouteRuleSettings
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string RouteId { get; set; }
        public string Priority { get; set; }
        public StrategySettings StrategySettings { get; set; }
        public StrategySettings Strategy { get; set; }
        public bool? IsEnabled { get; set; }
        public bool? Enabled { get; set; }
    }

    public sealed class TenantRuleSettings
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public System.Collections.Generic.List<int> TenantIds { get; set; }
        public System.Collections.Generic.List<string> ClientIds { get; set; }
        public StrategySettings StrategySettings { get; set; }
        public StrategySettings Strategy { get; set; }
        public bool? IsEnabled { get; set; }
        public bool? Enabled { get; set; }
        public string Priority { get; set; }
    }

    public sealed class StrategySettings
    {
        public string Type { get; set; }
        public int? TokenLimit { get; set; }
        public int? TokensPerPeriod { get; set; }
        public int? ReplenishmentPeriodSeconds { get; set; }
        public string ReplenishmentPeriod { get; set; }
        public int? PermitLimit { get; set; }
        public int? QueueLimit { get; set; }
        public int? WindowSeconds { get; set; }
        public string Window { get; set; }
    }
}
