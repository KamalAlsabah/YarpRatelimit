namespace ReverseProxy.RateLimiting.Domain.Matchers
{
    public interface IEndpointPatternMatcher
    {
        bool Matches(string pattern, string httpMethod, string path);
    }
}
