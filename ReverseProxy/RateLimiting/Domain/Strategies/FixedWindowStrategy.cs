using RedisRateLimiting;
using StackExchange.Redis;
using System;
using System.Threading.RateLimiting;

namespace ReverseProxy.RateLimiting.Domain.Strategies
{
    public sealed class FixedWindowStrategy : RateLimitStrategy
    {
        private readonly FixedWindowConfig _config;
        private readonly IConnectionMultiplexer _connection;

        public FixedWindowStrategy(FixedWindowConfig config, IConnectionMultiplexer connection)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public override RateLimitPartition<string> CreatePartition(string key)
        {
            return RedisRateLimitPartition.GetFixedWindowRateLimiter(
                key,
                _ => new RedisFixedWindowRateLimiterOptions
                {
                    PermitLimit = _config.PermitLimit,
                    Window = _config.Window,
                    ConnectionMultiplexerFactory = () => _connection
                });
        }
    }

    public sealed class FixedWindowConfig
    {
        public int PermitLimit { get; }
        public TimeSpan Window { get; }

        public FixedWindowConfig(int permitLimit, TimeSpan window)
        {
            PermitLimit = permitLimit;
            Window = window;
        }
    }
}
