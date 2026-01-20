#nullable enable

using Microsoft.AspNetCore.Http;
using ReverseProxy.RateLimiting.Domain.Models;

namespace ReverseProxy.RateLimiting.Domain.Matchers
{
    public interface IRequestActorResolver
    {
        RequestActor Resolve(HttpContext context);
    }
}
