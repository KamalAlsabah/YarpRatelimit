#nullable enable

using Microsoft.AspNetCore.Http;

namespace ReverseProxy.RateLimiting.Domain.Models
{
    public sealed class RequestActor
    {
        public string ActorId { get; }
        public int? TenantId { get; }
        public string? ClientId { get; }

        public RequestActor(string actorId, int? tenantId, string? clientId)
        {
            ActorId = actorId;
            TenantId = tenantId;
            ClientId = clientId;
        }
    }

    public sealed class RuleResolutionContext
    {
        public RequestActor Actor { get; }
        public string? RouteId { get; }
        public string Path { get; }
        public string HttpMethod { get; }
        public HttpContext HttpContext { get; }

        public RuleResolutionContext(RequestActor actor, string? routeId, string path, string httpMethod, HttpContext httpContext)
        {
            Actor = actor;
            RouteId = routeId;
            Path = path;
            HttpMethod = httpMethod;
            HttpContext = httpContext;
        }
    }
}
