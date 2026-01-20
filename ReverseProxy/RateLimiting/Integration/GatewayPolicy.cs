#nullable enable

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using RedisRateLimiting.AspNetCore;
using ReverseProxy.RateLimiting.Domain.Matchers;
using ReverseProxy.RateLimiting.Domain.Models;
using ReverseProxy.RateLimiting.Domain.Resolution;
using ReverseProxy.RateLimiting.Infrastructure.Monitoring;
using ReverseProxy.RateLimiting.Integration.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace ReverseProxy
{
    public sealed class GatewayPolicy : IRateLimiterPolicy<string>
    {
        private readonly IRequestActorResolver _actorResolver;
        private readonly IRateLimitRuleOrchestrator _orchestrator;
        private readonly IRateLimitConfigurationProvider _configProvider;
        private readonly IRateLimitMetrics? _metrics;

        private readonly Func<OnRejectedContext, CancellationToken, ValueTask> _onRejected =
            (context, token) =>
            {
                context.HttpContext.Response.StatusCode = 429;
                context.HttpContext.Items["RateLimitedAt"] = DateTime.UtcNow;
                return ValueTask.CompletedTask;
            };

        public GatewayPolicy(
            IRequestActorResolver actorResolver,
            IRateLimitRuleOrchestrator orchestrator,
            IRateLimitConfigurationProvider configProvider,
            IRateLimitMetrics? metrics = null)
        {
            _actorResolver = actorResolver;
            _orchestrator = orchestrator;
            _configProvider = configProvider;
            _metrics = metrics;
        }

        public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => async (context, ct) =>
        {
            // Record rejection in metrics
            if (_metrics != null)
            {
                // Get partition key from HttpContext.Items (set in GetPartition)
                var partitionKey = context.HttpContext.Items["RateLimitPartitionKey"] as string ?? "unknown";
                _metrics.RecordRejection(partitionKey);
            }
            
            await RateLimitMetadata.OnRejected(context.HttpContext, context.Lease, ct);
        };

        public RateLimitPartition<string> GetPartition(HttpContext httpContext)
        {
            // Track performance if metrics are enabled
            using (_metrics != null ? new PerformanceTimer(_metrics) : default)
            {
                var actor = _actorResolver.Resolve(httpContext);
                
                var routeId = httpContext.GetEndpoint()?.Metadata.GetMetadata<Yarp.ReverseProxy.Model.RouteModel>()?.Config.RouteId;
                var path = httpContext.Request.Path.Value ?? string.Empty;
                var method = httpContext.Request.Method;

                var resolutionContext = new RuleResolutionContext(actor, routeId, path, method, httpContext);
                var config = _configProvider.GetConfiguration();

                // Use optimized indexed cache resolution
                var decision = _orchestrator.ResolveWithCache(
                    config.IndexedCache,
                    config.GlobalDefault,
                    resolutionContext);

                var partitionKey = BuildPartitionKey(resolutionContext, decision);
                
                // Store partition key in HttpContext.Items for rejection tracking
                httpContext.Items["RateLimitPartitionKey"] = partitionKey;

                return decision.Strategy.CreatePartition(partitionKey);
            }
        }

        private static string BuildPartitionKey(RuleResolutionContext context, RateLimitDecision decision)
        {
            // Optimized key building with minimal allocations
            if (decision.IsRouteRule)
            {
                return $"route:{context.RouteId}";
            }

            var actor = context.Actor;

            // Use string interpolation for better performance than StringBuilder for simple cases
            if (actor.TenantId.HasValue)
            {
                if (!string.IsNullOrEmpty(actor.ClientId))
                {
                    if (!string.IsNullOrEmpty(actor.ActorId))
                    {
                        return $"tenant:{actor.TenantId}:client:{actor.ClientId}:user:{actor.ActorId}";
                    }
                    return $"tenant:{actor.TenantId}:client:{actor.ClientId}";
                }
                
                if (!string.IsNullOrEmpty(actor.ActorId))
                {
                    return $"tenant:{actor.TenantId}:user:{actor.ActorId}";
                }
                
                return $"tenant:{actor.TenantId}";
            }

            if (!string.IsNullOrEmpty(actor.ClientId))
            {
                if (!string.IsNullOrEmpty(actor.ActorId))
                {
                    return $"client:{actor.ClientId}:user:{actor.ActorId}";
                }
                return $"client:{actor.ClientId}";
            }

            if (!string.IsNullOrEmpty(actor.ActorId))
            {
                return $"user:{actor.ActorId}";
            }

            return "anonymous:ip";
        }
    }
}

