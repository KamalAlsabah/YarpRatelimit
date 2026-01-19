using System.Collections.Immutable;

namespace ReverseProxy.RateLimiting.Domain.Matchers
{
    public interface IIpAddressMatcher
    {
        bool Matches(ImmutableHashSet<string> allowedPatterns, string ipAddress);
    }
}
