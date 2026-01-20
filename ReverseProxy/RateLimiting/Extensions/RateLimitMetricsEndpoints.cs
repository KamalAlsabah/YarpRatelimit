using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ReverseProxy.RateLimiting.Infrastructure.Monitoring;
using System.Text.Json;

namespace ReverseProxy.RateLimiting.Extensions
{
    public static class RateLimitMetricsEndpoints
    {
        /// <summary>
        /// Maps endpoints for rate limit metrics and diagnostics
        /// </summary>
        public static IEndpointRouteBuilder MapRateLimitMetrics(this IEndpointRouteBuilder endpoints, string pattern = "/_metrics/ratelimit")
        {
            endpoints.MapGet(pattern, async (HttpContext context, IRateLimitMetrics? metrics) =>
            {
                if (metrics == null)
                {
                    context.Response.StatusCode = 503;
                    await context.Response.WriteAsJsonAsync(new { error = "Metrics not enabled" });
                    return;
                }

                var snapshot = metrics.GetSnapshot();
                
                await context.Response.WriteAsJsonAsync(new
                {
                    totalRequests = snapshot.TotalRequests,
                    cacheHits = snapshot.CacheHits,
                    cacheMisses = snapshot.CacheMisses,
                    cacheHitRate = $"{snapshot.CacheHitRate:P2}",
                    rejections = snapshot.Rejections,
                    rejectionRate = $"{snapshot.RejectionRate:P2}",
                    averageResolutionTimeMs = $"{snapshot.AverageResolutionTimeMs:F3}",
                    maxResolutionTimeMs = $"{snapshot.MaxResolutionTimeMs:F3}",
                    timestamp = System.DateTime.UtcNow
                }, new JsonSerializerOptions { WriteIndented = true });
            })
            .WithName("RateLimitMetrics")
            .WithTags("Diagnostics");

            return endpoints;
        }
    }
}
