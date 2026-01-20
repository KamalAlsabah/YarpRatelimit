
#nullable enable

using Microsoft.AspNetCore.Http;
using ReverseProxy.RateLimiting.Domain.Matchers;
using ReverseProxy.RateLimiting.Domain.Models;
using System;
using System.Linq;
using System.Security.Claims;

namespace ReverseProxy.RateLimiting.Infrastructure.Matchers
{
    public sealed class RequestActorResolver : IRequestActorResolver
    {
        private const string CacheKey = "_RateLimiting_RequestActor";

        public RequestActor Resolve(HttpContext context)
        {
            // Check cache first to avoid re-resolution
            if (context.Items.TryGetValue(CacheKey, out var cached) && cached is RequestActor actor)
            {
                return actor;
            }

            // Single-pass resolution to minimize lookups
            var (actorId, tenantId, clientId) = ResolveAll(context);
            var result = new RequestActor(actorId, tenantId, clientId);
            
            // Cache for the lifetime of this request
            context.Items[CacheKey] = result;
            return result;
        }

        private static (string actorId, int? tenantId, string? clientId) ResolveAll(HttpContext context)
        {
            // Single iteration through claims collection
            string? userId = null;
            string? tenantIdClaim = null;
            string? clientIdClaim = null;

            if (context.User?.Identity?.IsAuthenticated == true)
            {
                foreach (var claim in context.User.Claims)
                {
                    switch (claim.Type)
                    {
                        case ClaimTypes.NameIdentifier:
                            userId = claim.Value;
                            break;
                        case "tenantId":
                            tenantIdClaim = claim.Value;
                            break;
                        case "clientId":
                            clientIdClaim = claim.Value;
                            break;
                    }

                    // Early exit if all found
                    if (userId != null && tenantIdClaim != null && clientIdClaim != null)
                        break;
                }
            }

            // Resolve ActorId (fallback to IP if no userId)
            string actorId;
            if (!string.IsNullOrWhiteSpace(userId))
            {
                actorId = userId;
            }
            else
            {
                actorId = ResolveIpAddress(context);
            }

            // Resolve TenantId (claims first, then header fallback)
            int? tenantId = null;
            var tenantIdValue = tenantIdClaim ?? context.Request.Headers["Abp-TenantId"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(tenantIdValue) && int.TryParse(tenantIdValue, out var parsedTenantId))
            {
                tenantId = parsedTenantId;
            }

            // Resolve ClientId (claims first, then header fallback)
            var clientId = clientIdClaim ?? context.Request.Headers["X-Client-Id"].FirstOrDefault();

            return (actorId, tenantId, clientId);
        }

        private static string ResolveIpAddress(HttpContext context)
        {
            // Check X-Forwarded-For header first
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            
            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                // Use Span to avoid string allocation during split
                var span = forwardedFor.AsSpan();
                var commaIndex = span.IndexOf(',');
                
                if (commaIndex > 0)
                {
                    return span.Slice(0, commaIndex).Trim().ToString();
                }
                
                return span.Trim().ToString();
            }

            // Fallback to connection IP
            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}
