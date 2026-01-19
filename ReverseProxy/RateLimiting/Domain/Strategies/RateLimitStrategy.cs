using System.Threading.RateLimiting;

namespace ReverseProxy.RateLimiting.Domain.Strategies
{
    public abstract class RateLimitStrategy
    {
        public abstract RateLimitPartition<string> CreatePartition(string key);
    }
}
