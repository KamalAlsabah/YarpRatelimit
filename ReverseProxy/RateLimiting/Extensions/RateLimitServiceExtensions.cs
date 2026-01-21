using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using RedisRateLimiting.AspNetCore;
using ReverseProxy.RateLimiting.Domain.Matchers;
using ReverseProxy.RateLimiting.Domain.Resolution;
using ReverseProxy.RateLimiting.Infrastructure.Matchers;
using ReverseProxy.RateLimiting.Infrastructure.Monitoring;
using ReverseProxy.RateLimiting.Infrastructure.Resolution;
using StackExchange.Redis;
using System.Threading.Tasks;

namespace ReverseProxy.RateLimiting.Extensions
{
    public static class RateLimitServiceExtensions
    {
        public static IServiceCollection AddRateLimitServices(
            this IServiceCollection services,
            IConnectionMultiplexer redisConnection,
            bool enableMetrics = true)
        {
            services.AddSingleton(redisConnection);

            // Core services
            services.AddSingleton<IRequestActorResolver, RequestActorResolver>();
            services.AddSingleton<IEndpointPatternMatcher, EndpointPatternMatcher>();
            services.AddSingleton<IIpAddressMatcher, IpAddressMatcher>();

            services.AddSingleton<IWhitelistRuleResolver, WhitelistRuleResolver>();
            services.AddSingleton<IRouteRuleResolver, RouteRuleResolver>();
            services.AddSingleton<ITenantRuleResolver, TenantRuleResolver>();

            // Performance monitoring (optional) - register metrics before orchestrator so it can be injected
            if (enableMetrics)
            {
                services.AddSingleton<IRateLimitMetrics, RateLimitMetrics>();
            }

            // Orchestrator depends optionally on IRateLimitMetrics; register after metrics registration to ensure DI can resolve it
            services.AddSingleton<IRateLimitRuleOrchestrator, RateLimitRuleOrchestrator>();

            services.AddSingleton<GatewayPolicy>();

            services.AddRateLimiter(options =>
            {
                options.AddPolicy<string, GatewayPolicy>("gateway-policy");
                options.OnRejected = (context, ct) => RateLimitMetadata.OnRejected(context.HttpContext, context.Lease, ct);
            });

            return services;
        }
    }
}

