#nullable enable

using ReverseProxy.RateLimiting.Domain.Models;
using ReverseProxy.RateLimiting.Domain.Models.Rules;
using ReverseProxy.RateLimiting.Domain.Models.Strategies;

namespace ReverseProxy.RateLimiting.Domain.Resolution
{
    public interface IWhitelistRuleResolver
    {
        bool TryResolve(WhitelistRule rule, RuleResolutionContext context, out RateLimitStrategy? strategy);
    }

    public interface IRouteRuleResolver
    {
        bool TryResolve(RouteRule rule, RuleResolutionContext context, out RateLimitStrategy? strategy);
    }

    public interface ITenantRuleResolver
    {
        bool TryResolve(TenantRule rule, RuleResolutionContext context, out RateLimitStrategy? strategy);
    }

}
