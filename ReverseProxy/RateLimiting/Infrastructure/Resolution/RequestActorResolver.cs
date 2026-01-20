
using Microsoft.AspNetCore.Http;
using ReverseProxy.RateLimiting.Domain.Models;
using ReverseProxy.RateLimiting.Domain.Resolution;
using System;
using System.Linq;
using System.Security.Claims;

namespace ReverseProxy.RateLimiting.Infrastructure.Resolution
{
    public sealed class RequestActorResolver : IRequestActorResolver
    {
        public RequestActor Resolve(HttpContext context)
        {
            var actorId = ResolveActorId(context);
            var tenantId = ResolveTenantId(context);
            var clientId = ResolveClientId(context);

            return new RequestActor(actorId, tenantId, clientId);
        }

        private static string ResolveActorId(HttpContext context)
        {
            //forwardedFor just for test now 
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            string actorIp;

            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                actorIp = forwardedFor.Split(',')[0].Trim();
            }
            else
            {
                actorIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            }

            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return string.IsNullOrWhiteSpace(userId) ? actorIp : userId;
        }

        private static int? ResolveTenantId(HttpContext context)
        {
            var tenantIdValue = context.User.FindFirst("tenantId")?.Value ??
                                context.Request.Headers["Abp-TenantId"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(tenantIdValue))
                return null;

            return int.TryParse(tenantIdValue, out var id) ? id : null;
        }

        private static string ResolveClientId(HttpContext context)
        {
            return context.User.FindFirst("clientId")?.Value ??
                   context.Request.Headers["X-Client-Id"].FirstOrDefault();
        }
    }
}
