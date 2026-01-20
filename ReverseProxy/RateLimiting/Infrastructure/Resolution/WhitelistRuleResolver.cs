#nullable enable

using ReverseProxy.RateLimiting.Domain.Helpers;
using ReverseProxy.RateLimiting.Domain.Matchers;
using ReverseProxy.RateLimiting.Domain.Models;
using ReverseProxy.RateLimiting.Domain.Models.Rules;
using ReverseProxy.RateLimiting.Domain.Models.Strategies;
using ReverseProxy.RateLimiting.Domain.Resolution;

namespace ReverseProxy.RateLimiting.Infrastructure.Resolution
{
    public sealed class WhitelistRuleResolver : IWhitelistRuleResolver
    {
        private readonly IEndpointPatternMatcher _endpointMatcher;
        private readonly IIpAddressMatcher _ipMatcher;

        public WhitelistRuleResolver(IEndpointPatternMatcher endpointMatcher, IIpAddressMatcher ipMatcher)
        {
            _endpointMatcher = endpointMatcher;
            _ipMatcher = ipMatcher;
        }

        public bool TryResolve(WhitelistRule rule, RuleResolutionContext context, out RateLimitStrategy? strategy)
        {
            strategy = null;

            if (!rule.IsEnabled)
                return false;

            if (!MatchesIp(rule, context))
                return false;

            if (!ActorMatcher.TenantMatches(rule.TenantIds, context.Actor.TenantId))
                return false;

            if (!ActorMatcher.ClientMatches(rule.ClientIds, context.Actor.ClientId))
                return false;

            if (!MatchesEndpoint(rule, context.HttpMethod, context.Path))
                return false;

            strategy = new WhitelistStrategy();
            return true;
        }

        private bool MatchesIp(WhitelistRule rule, RuleResolutionContext context)
        {
            if (rule.IpAddresses.IsEmpty)
                return true;

            var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return _ipMatcher.Matches(rule.IpAddresses, ip);
        }

        private bool MatchesEndpoint(WhitelistRule rule, string method, string path)
        {
            if (rule.EndpointPatterns.IsEmpty)
                return true;

            foreach (var pattern in rule.EndpointPatterns)
            {
                if (_endpointMatcher.Matches(pattern, method, path))
                    return true;
            }

            return false;
        }
    }
}
