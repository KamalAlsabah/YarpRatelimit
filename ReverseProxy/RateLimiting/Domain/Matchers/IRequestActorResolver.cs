#nullable enable

using Microsoft.AspNetCore.Http;

namespace ReverseProxy.RateLimiting.Domain.Matchers
{
    public interface IRequestActorResolver
    {
        RequestActor Resolve(HttpContext context);
    }
}
