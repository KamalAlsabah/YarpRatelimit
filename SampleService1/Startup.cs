using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;

namespace SampleService1
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                // Health check - ultra-fast endpoint
                endpoints.MapGet("/health", async context =>
                {
                    logger.LogInformation("[Service1] Health check from {RemoteIP}", 
                        context.Connection.RemoteIpAddress);
                    
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        status = "healthy",
                        service = "SampleService1",
                        timestamp = DateTime.UtcNow,
                        uptime = GC.GetTotalMemory(false) / (1024 * 1024)
                    });
                });

                // Fast endpoint - returns data
                endpoints.MapGet("/data", async context =>
                {
                    var requestId = context.Request.Headers["X-Request-ID"].ToString() ?? Guid.NewGuid().ToString();
                    logger.LogInformation("[Service1] Data request {RequestId} from {RemoteIP}", 
                        requestId, context.Connection.RemoteIpAddress);
                    
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        id = requestId,
                        service = "SampleService1",
                        data = new[] 
                        { 
                            new { name = "Record1", value = 100 },
                            new { name = "Record2", value = 200 },
                            new { name = "Record3", value = 300 }
                        },
                        timestamp = DateTime.UtcNow
                    });
                });

                // Slow endpoint - simulates long-running operation (for concurrency testing)
                endpoints.MapGet("/slow", async context =>
                {
                    var delayMs = 3000; // 3 seconds
                    var requestId = context.Request.Headers["X-Request-ID"].ToString() ?? Guid.NewGuid().ToString();
                    
                    logger.LogInformation("[Service1] Slow request {RequestId} started (delay: {DelayMs}ms) from {RemoteIP}", 
                        requestId, delayMs, context.Connection.RemoteIpAddress);
                    
                    var sw = Stopwatch.StartNew();
                    await Task.Delay(delayMs);
                    sw.Stop();
                    
                    logger.LogInformation("[Service1] Slow request {RequestId} completed in {ElapsedMs}ms", 
                        requestId, sw.ElapsedMilliseconds);
                    
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        id = requestId,
                        service = "SampleService1",
                        message = "Slow operation completed",
                        processingTimeMs = sw.ElapsedMilliseconds,
                        timestamp = DateTime.UtcNow
                    });
                });

                // CPU-heavy endpoint - simulates computation
                endpoints.MapGet("/cpu-heavy", async context =>
                {
                    var iterations = 1000000000; // 1 billion iterations
                    var requestId = context.Request.Headers["X-Request-ID"].ToString() ?? Guid.NewGuid().ToString();
                    
                    logger.LogInformation("[Service1] CPU-heavy request {RequestId} started (iterations: {Iterations}) from {RemoteIP}", 
                        requestId, iterations, context.Connection.RemoteIpAddress);
                    
                    var sw = Stopwatch.StartNew();
                    long result = 0;
                    
                    // CPU-bound work
                    for (long i = 0; i < iterations; i++)
                    {
                        result += i % 7; // Simple calculation that can't be optimized away
                    }
                    
                    sw.Stop();
                    logger.LogInformation("[Service1] CPU-heavy request {RequestId} completed in {ElapsedMs}ms", 
                        requestId, sw.ElapsedMilliseconds);
                    
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        id = requestId,
                        service = "SampleService1",
                        message = "CPU-heavy operation completed",
                        processingTimeMs = sw.ElapsedMilliseconds,
                        result = result,
                        timestamp = DateTime.UtcNow
                    });
                });

                // Test endpoint - for rate limit testing
                endpoints.MapGet("/test", async context =>
                {
                    var requestId = context.Request.Headers["X-Request-ID"].ToString() ?? Guid.NewGuid().ToString();
                    logger.LogInformation("[Service1] Test request {RequestId} from {RemoteIP}", 
                        requestId, context.Connection.RemoteIpAddress);
                    
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        id = requestId,
                        service = "SampleService1",
                        endpoint = "test",
                        message = "Test endpoint response",
                        timestamp = DateTime.UtcNow
                    });
                });

                // Default endpoint
                endpoints.MapGet("/", async context =>
                {
                    logger.LogInformation("[Service1] Root endpoint from {RemoteIP}", 
                        context.Connection.RemoteIpAddress);
                    
                    await context.Response.WriteAsync("SampleService1 - Available endpoints: /health, /data, /test, /slow, /cpu-heavy");
                });
            });
        }
    }
}
