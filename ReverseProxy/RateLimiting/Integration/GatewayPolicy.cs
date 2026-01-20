#nullable enable

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using RedisRateLimiting.AspNetCore;
using ReverseProxy.RateLimiting.Domain.Matchers;
using ReverseProxy.RateLimiting.Domain.Models;
using ReverseProxy.RateLimiting.Domain.Resolution;
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
            IRateLimitConfigurationProvider configProvider)
        {
            _actorResolver = actorResolver;
            _orchestrator = orchestrator;
            _configProvider = configProvider;
        }

        public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => (context, ct) => RateLimitMetadata.OnRejected(context.HttpContext, context.Lease, ct);

        public RateLimitPartition<string> GetPartition(HttpContext httpContext)
        {
            var actor = _actorResolver.Resolve(httpContext);
            
            var routeId = httpContext.GetEndpoint()?.Metadata.GetMetadata<Yarp.ReverseProxy.Model.RouteModel>()?.Config.RouteId;
            var path = httpContext.Request.Path.Value ?? string.Empty;
            var method = httpContext.Request.Method;

            var resolutionContext = new RuleResolutionContext(actor, routeId, path, method, httpContext);
            var config = _configProvider.GetConfiguration();

            var decision = _orchestrator.Resolve(
                config.WhitelistRules,
                config.RouteRules,
                config.TenantRules,
                config.GlobalDefault,
                resolutionContext);

            var partitionKey = BuildPartitionKey(resolutionContext, decision);

            return decision.Strategy.CreatePartition(partitionKey);
        }

        private static string BuildPartitionKey(RuleResolutionContext context, RateLimitDecision decision)
        {
            var keyParts = new StringBuilder();
            if (decision.IsRouteRule)
            {
                return keyParts.Append($"route:{ context.RouteId}").ToString();
            }
            var actor = context.Actor;

            if (actor.TenantId.HasValue)
                keyParts.Append($"tenant:{actor.TenantId}");

            if (!string.IsNullOrEmpty(actor.ClientId))
            {
                if (keyParts.Length > 0) keyParts.Append(":");
                keyParts.Append($"client:{actor.ClientId}");
            }

            if (!string.IsNullOrEmpty(actor.ActorId))
            {
                if (keyParts.Length > 0) keyParts.Append(":");
                keyParts.Append($"user:{actor.ActorId}");
            }

            if (keyParts.Length == 0)
                keyParts.Append("anonymous:ip");

            return keyParts.ToString();
        }
    }
}

