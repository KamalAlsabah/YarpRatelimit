using ReverseProxy.RateLimiting.Domain.Matchers;
using System;

namespace ReverseProxy.RateLimiting.Infrastructure
{
    public sealed class EndpointPatternMatcher : IEndpointPatternMatcher
    {
        public bool Matches(string pattern, string httpMethod, string path)
        {
            if (pattern == "*")
                return true;

            var parts = pattern.Split(':', 2);

            if (parts.Length == 2)
                return MatchesMethodAndPath(parts[0], parts[1], httpMethod, path);

            return MatchesPath(pattern, path);
        }

        private static bool MatchesMethodAndPath(string patternMethod, string patternPath, string method, string path)
        {
            if (patternMethod != "*" &&
                !string.Equals(patternMethod, method, StringComparison.OrdinalIgnoreCase))
                return false;

            return MatchesPath(patternPath, path);
        }

        private static bool MatchesPath(string pattern, string path)
        {
            if (pattern == "*")
                return true;

            if (string.Equals(pattern, path, StringComparison.OrdinalIgnoreCase))
                return true;

            if (pattern.EndsWith("/*"))
            {
                var prefix = pattern[..^2];
                return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            if (pattern.StartsWith("*/"))
            {
                var suffix = pattern[1..];
                return path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
    }
}
