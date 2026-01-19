using RedisRateLimiting;
using StackExchange.Redis;
using System;
using System.Threading.RateLimiting;

namespace ReverseProxy.RateLimiting.Domain.Strategies
{
    public sealed class SlidingWindowStrategy : RateLimitStrategy
    {
        private readonly SlidingWindowConfig _config;
        private readonly IConnectionMultiplexer _connection;

        public SlidingWindowStrategy(SlidingWindowConfig config, IConnectionMultiplexer connection)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public override RateLimitPartition<string> CreatePartition(string key)
        {
            return RedisRateLimitPartition.GetSlidingWindowRateLimiter(
                key,
                _ => new RedisSlidingWindowRateLimiterOptions
                {
                    PermitLimit = _config.PermitLimit,
                    Window = _config.Window,
                    ConnectionMultiplexerFactory = () => _connection
                });
        }
    }

    public sealed class SlidingWindowConfig
    {
        public int PermitLimit { get; }
        public TimeSpan Window { get; }

        public SlidingWindowConfig(int permitLimit, TimeSpan window)
        {
            PermitLimit = permitLimit;
            Window = window;
        }
    }
}
