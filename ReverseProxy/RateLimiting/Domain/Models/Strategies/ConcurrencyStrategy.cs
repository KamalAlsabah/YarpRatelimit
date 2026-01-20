using RedisRateLimiting;
using StackExchange.Redis;
using System;
using System.Threading.RateLimiting;

namespace ReverseProxy.RateLimiting.Domain.Models.Strategies
{
    public sealed class ConcurrencyStrategy : RateLimitStrategy
    {
        private readonly ConcurrencyConfig _config;
        private readonly IConnectionMultiplexer _connection;

        public ConcurrencyStrategy(ConcurrencyConfig config, IConnectionMultiplexer connection)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public override RateLimitPartition<string> CreatePartition(string key)
        {
            return RedisRateLimitPartition.GetConcurrencyRateLimiter(
                key,
                _ => new RedisConcurrencyRateLimiterOptions
                {
                    PermitLimit = _config.PermitLimit,
                    QueueLimit = _config.QueueLimit,
                    ConnectionMultiplexerFactory = () => _connection
                });
        }
    }

    public sealed class ConcurrencyConfig
    {
        public int PermitLimit { get; }
        public int QueueLimit { get; }

        public ConcurrencyConfig(int permitLimit, int queueLimit)
        {
            PermitLimit = permitLimit;
            QueueLimit = queueLimit;
        }
    }
}
