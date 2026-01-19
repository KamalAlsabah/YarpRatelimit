using RedisRateLimiting;
using StackExchange.Redis;
using System;
using System.Threading.RateLimiting;

namespace ReverseProxy.RateLimiting.Domain.Strategies
{
    public sealed class TokenBucketStrategy : RateLimitStrategy
    {
        private readonly TokenBucketConfig _config;
        private readonly IConnectionMultiplexer _connection;

        public TokenBucketStrategy(TokenBucketConfig config, IConnectionMultiplexer connection)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public override RateLimitPartition<string> CreatePartition(string key)
        {
            return RedisRateLimitPartition.GetTokenBucketRateLimiter(
                key,
                _ => new RedisTokenBucketRateLimiterOptions
                {
                    TokenLimit = _config.TokenLimit,
                    TokensPerPeriod = _config.TokensPerPeriod,
                    ReplenishmentPeriod = _config.ReplenishmentPeriod,
                    ConnectionMultiplexerFactory = () => _connection
                });
        }
    }

    public sealed class TokenBucketConfig
    {
        public int TokenLimit { get; }
        public int TokensPerPeriod { get; }
        public TimeSpan ReplenishmentPeriod { get; }

        public TokenBucketConfig(int tokenLimit, int tokensPerPeriod, TimeSpan replenishmentPeriod)
        {
            TokenLimit = tokenLimit;
            TokensPerPeriod = tokensPerPeriod;
            ReplenishmentPeriod = replenishmentPeriod;
        }
    }
}
