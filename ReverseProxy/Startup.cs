using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReverseProxy.RateLimiting.Extensions;
using ReverseProxy.RateLimiting.Integration;
using ReverseProxy.RateLimiting.Integration.Configuration;
using StackExchange.Redis;

namespace ReverseProxy
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            // Add CORS
            services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", builder =>
                {
                    builder.AllowAnyOrigin()
                           .AllowAnyMethod()
                           .AllowAnyHeader();
                });
            });

            var redisConnectionString = _configuration.GetConnectionString("Redis") ?? "localhost:6379";
            var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
            var redisConnection = ConnectionMultiplexer.Connect(redisOptions);

            services
                .AddReverseProxy()
                .LoadFromConfig(_configuration.GetSection("ReverseProxy"));

            services.Configure<RateLimitSettingsOptions>(_configuration.GetSection("RateLimitOptions"));
            services.AddSingleton<IRateLimitConfigurationProvider, ConfigurationFromSettingsProvider>();
            services.AddRateLimitServices(redisConnection);
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // Use Forwarded Headers middleware BEFORE routing
            app.UseForwardedHeaders();

            // Add CORS middleware
            app.UseCors("AllowAll");

            app.UseRouting();

            // Manual rate limiting enforcement using RedisRateLimiting
            app.UseRateLimiter();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapReverseProxy().RequireRateLimiting("gateway-policy");
            });
        }
    }
}
