using System.Threading.RateLimiting;

namespace ReverseProxy.RateLimiting.Domain.Models.Strategies
{
    public sealed class WhitelistStrategy : RateLimitStrategy
    {
        public override RateLimitPartition<string> CreatePartition(string key)
        {
            return RateLimitPartition.GetNoLimiter(key);
        }
    }
}
