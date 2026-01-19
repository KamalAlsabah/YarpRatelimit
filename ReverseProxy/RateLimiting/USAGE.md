# How to install the rate limiting module

## Quick steps for a new project
1) Add Redis connection in settings:
```json
{
  "ConnectionStrings": { "Redis": "localhost:6379" }
}
```

2) Add a `RateLimitOptions` block in `appsettings.json` (or `appsettings.Development.json`). Minimal example:
```json
"RateLimitOptions": {
  "GlobalDefault": { "Type": "TokenBucket", "TokenLimit": 100, "TokensPerPeriod": 10, "ReplenishmentPeriod": "00:00:10" },
  "WhitelistRules": [],
  "RouteRules": [],
  "TenantRules": []
}
```

3) In `Startup` or `Program` (.NET 8 minimal hosting):
```csharp
// Create Redis connection once
var redisOptions = ConfigurationOptions.Parse(configuration.GetConnectionString("Redis"));
var redis = ConnectionMultiplexer.Connect(redisOptions);

// Register services
services.Configure<RateLimitSettingsOptions>(configuration.GetSection("RateLimitOptions"));
services.AddSingleton<IRateLimitConfigurationProvider, ConfigurationFromSettingsProvider>();
services.AddRateLimitServices(redis);

// YARP
services.AddReverseProxy().LoadFromConfig(configuration.GetSection("ReverseProxy"));
```

4) In the middleware pipeline:
```csharp
app.UseRouting();
app.UseRateLimiter();
app.UseEndpoints(endpoints =>
{
    endpoints.MapGet("/", () => "OK").RequireRateLimiting("gateway-policy");
    endpoints.MapReverseProxy().RequireRateLimiting("gateway-policy");
});
```

5) Ensure `RouteRules[].RouteId` matches the `RouteId` in YARP settings (`ReverseProxy:Routes`).

6) Run the gateway:
```bash
cd ReverseProxy
DOTNET_ENVIRONMENT=Development dotnet run
```

## Quick notes
- Flow: Whitelist → Route → Tenant → GlobalDefault.
- If a RouteRule wins the partition key is `route:{RouteId}`; if a TenantRule wins it is `tenant:{id}:client:{client}:user:{actor}` or `anonymous:ip` when identity is missing.
- Whitelist rules support IP/CIDR/wildcard, path patterns with `*`, and HTTP methods like `GET:/health`.
- Empty lists mean “any”; client "*" means any client.
