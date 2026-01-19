# Rate Limiting Module

## What it does
- Applies gateway rate limits via YARP + ASP.NET Core rate limiting
- Supports route-based rules, tenant/client rules, and whitelisting
- Resolves the effective rule per request, then builds a partition key for the limiter

## Quick start (short)
1) Set `ConnectionStrings:Redis` and a minimal `RateLimitOptions` block in appsettings (e.g. `appsettings.Development.json`).

```json
{
  "ConnectionStrings": { "Redis": "localhost:6379" },
  "RateLimitOptions": {
    "GlobalDefault": { "Type": "TokenBucket", "TokenLimit": 100, "TokensPerPeriod": 10, "ReplenishmentPeriod": "00:00:10" },
    "WhitelistRules": [],
    "RouteRules": [],
    "TenantRules": []
  }
}
```

2) Services are already wired in startup: rate limit config binding, Redis connection, `AddRateLimitServices`, and the gateway policy registration live in [ReverseProxy/Startup.cs](ReverseProxy/Startup.cs#L24-L67) and [ReverseProxy/RateLimiting/Extensions/RateLimitServiceExtensions.cs](ReverseProxy/RateLimiting/Extensions/RateLimitServiceExtensions.cs#L17-L40).
3) Ensure YARP routes use `RequireRateLimiting("gateway-policy")` (already applied to `MapReverseProxy` in [ReverseProxy/Startup.cs](ReverseProxy/Startup.cs#L58-L67)) and that each `RouteRule.RouteId` matches the YARP `RouteId` in `ReverseProxy` config.
4) Run the gateway (`dotnet run` in `ReverseProxy/`); requests will be evaluated through the whitelist → route → tenant flow using the configured strategies.

## Resolution flow (per request)
1) **Whitelist rules** (short-circuit): first enabled whitelist match returns `WhitelistStrategy` and skips throttling.
2) **Route rules**: first enabled rule whose `RouteId` equals the incoming route.
3) **Tenant rules**: highest-priority enabled rule that matches tenant+client.
4) If both route and tenant matched: pick by `RouteWins` vs `TenantWins` on the matched route rule.
5) Otherwise fallback: route -> tenant -> global default.

## Partition keys
- If RouteRule wins: `route:{RouteId}` (actor ignored)
- Otherwise: `tenant:{id}:client:{client}:user:{actor}` (or `anonymous:ip` when missing)

## Request actor extraction
- TenantId: `X-Tenant-Id` header (int), nullable if missing.
- ClientId: `X-Client-Id` header (string), nullable if missing.
- ActorId: JWT `sub` claim (user id), nullable if missing.
- IP: `HttpContext.Connection.RemoteIpAddress` (used by whitelist IPs).
- Missing values are treated as null; empty collections in rules mean “match any” per dimension.

## Disabled rules
- Any rule with `Enabled: false` is skipped entirely (whitelist, route, tenant).
- If all matching candidates are disabled, resolution falls back to global default.

## Error handling
- A RouteRule that wins requires a non-null RouteId; otherwise configuration error should be fixed (the code throws when RouteId is null and route rule wins).
- Malformed IPs in requests simply fail whitelist IP match and continue resolution.

## Performance characteristics
- Tenant rules are pre-sorted once at startup by priority; per-request cost is O(n) scan to first match (no per-request sort).
- Whitelist/route/tenant matching short-circuits as soon as a match is found according to the resolution flow.
- Partition keys are built deterministically (route-based keys ignore actor to keep per-route buckets stable).

## Redis / storage
- Uses StackExchange.Redis via `ConnectionStrings:Redis` for distributed counters (TokenBucket/Fixed/Sliding/Concurrency). Ensure the connection string is set in appsettings.json.
- If Redis is unavailable, underlying rate limiter will surface errors per Microsoft rate limiting middleware behavior; ensure Redis is reachable in production.

## Strategies
- TokenBucket: Type, TokenLimit, TokensPerPeriod, ReplenishmentPeriod
- FixedWindow: Type, Window, PermitLimit
- SlidingWindow: Type, Window, PermitLimit
- Concurrency: Type, PermitLimit, QueueLimit
- Whitelist: Type (used for bypass)

## Configuration (appsettings.json)
Path: `RateLimitOptions`
- GlobalDefault: one strategy used when nothing else matches
- WhitelistRules[]: Name, Description, Enabled, IpAddresses[], EndpointPatterns[], TenantIds[], ClientIds[]
- RouteRules[]: Name, Description, RouteId, Priority (`RouteWins`|`TenantWins`), Enabled, Strategy
- TenantRules[]: Name, Description, TenantIds[], ClientIds[], Priority (int, higher wins), Enabled, Strategy

Notes:
- Route rule requires non-null RouteId when it wins; otherwise it is a config error
- Whitelist short-circuits all limits
- Tenant priorities: if omitted, implicit descending priority by order
- Tenant rules are pre-sorted at load; no per-request sort

## How whitelists are matched
- **IPs**: exact IP, CIDR, or `*` (any IP).
- **EndpointPatterns**: `*`, prefix `path/*`, suffix `*/suffix`, bare `/health`, or `METHOD:/path` (method can be `*`).
- **TenantIds / ClientIds**: empty = any. For clients, "*" also means any. (Tenants use empty set for “any”.)
- All specified dimensions are ANDed: only if every non-empty dimension matches.
- First enabled whitelist rule that matches wins and bypasses rate limiting entirely.

## How route rules are matched
- Matches when `RouteId` equals the YARP route id of the request.
- Only the first enabled matching route rule is used.
- **Priority resolution** when both route and tenant rules match:
  - `RouteWins`: Route strategy always wins (even if tenant has custom rule).
  - `TenantWins`: Tenant strategy wins **only when tenant rule exists**; otherwise route wins.
- This allows tenant-specific overrides while falling back to route limits for tenants without custom rules.

## How tenant rules are matched
- Must satisfy both:
  - Tenant: `TenantIds` empty → any tenant; else must contain the request tenant id.
  - Client: `ClientIds` empty → any client; or contains the request client id; or contains "*" (any client).
- Among enabled matches, highest `Priority` wins (implicit by order if unset).
- Tenant rules are pre-sorted at load; no per-request sorting.

## Example outcomes
- **Whitelist hit** (IP 127.0.0.1 + `/health`): immediately bypasses; no counters incremented.
- **Route rule wins** (`RouteWins`): partition key `route:service1-api`, actor ignored.
- **Tenant rule wins** (`TenantWins` + tenant has custom rule): partition key includes tenant/client (e.g., `tenant:4:client:crm`).
- **Route fallback** (`TenantWins` + no tenant rule for this tenant): uses route strategy with partition `route:service2-api`.
- **No matches**: uses `GlobalDefault` strategy.

## How to use
- Ensure services registered via RateLimitServiceExtensions (already wired)
- Use GatewayPolicy as the rate limiter policy for endpoints/YARP
- Set YARP route IDs to match RouteRules
- Populate RateLimitOptions in appsettings.json with rules/strategies
- Build and run; orchestrator uses loaded config

## Sample 
"RateLimitOptions": {
    "GlobalDefault": {
      "Type": "TokenBucket",
      "TokenLimit": 100,
      "TokensPerPeriod": 10,
      "ReplenishmentPeriod": "00:00:10"
    },
    "WhitelistRules": [
      {
        "Name": "Localhost & Health Checks",
        "Description": "Bypass all rate limiting for localhost and health checks",
        "Enabled": true,
        "IpAddresses": [ "127.0.0.1", "::1", "localhost" ],
        "EndpointPatterns": [ "/health", "GET:/health" ],
        "TenantIds": [],
        "ClientIds": []
      },
      {
        "Name": "Premium Tenants - Full Access",
        "Description": "Unlimited access for premium tenants 88, 99, 100",
        "Enabled": true,
        "IpAddresses": [],
        "EndpointPatterns": [],
        "TenantIds": [ 88, 99, 100 ],
        "ClientIds": [ "*" ]
      },
      {
        "Name": "Internal CRM Clients",
        "Description": "Unlimited access for internal CRM clients from specific IPs",
        "Enabled": true,
        "IpAddresses": [ "192.168.1.0/24", "10.0.0.0/8" ],
        "EndpointPatterns": [],
        "TenantIds": [],
        "ClientIds": [ "crm", "internal-api" ]
      }
    ],
    "RouteRules": [
      {
        "Name": "Service1 API - Premium Route",
        "Description": "High throughput limit for service1 API",
        "RouteId": "service1-api",
        "Priority": "RouteWins",
        "Enabled": true,
        "Strategy": {
          "Type": "TokenBucket",
          "TokenLimit": 150,
          "TokensPerPeriod": 15,
          "ReplenishmentPeriod": "00:00:10"
        }
      },
      {
        "Name": "Service2 API - Restrictive Route",
        "Description": "Lower limit for service2 API due to backend constraints",
        "RouteId": "service2-api",
        "Priority": "TenantWins",
        "Enabled": true,
        "Strategy": {
          "Type": "FixedWindow",
          "Window": "00:01:00",
          "PermitLimit": 50
        }
      }
    ],
    "TenantRules": [
      {
        "Name": "Premium Tenants - Concurrency",
        "Description": "Allow high concurrency for premium tenants (1-4)",
        "Enabled": true,
        "TenantIds": [ 1, 2, 3, 4 ],
        "ClientIds": [ "*" ],
        "Strategy": {
          "Type": "Concurrency",
          "PermitLimit": 30,
          "QueueLimit": 20
        }
      },
      {
        "Name": "Premium Tenants CRM - Concurrency",
        "Description": "Allow highest concurrency for premium CRM clients",
        "Enabled": true,
        "TenantIds": [ 4 ],
        "ClientIds": [ "crm" ],
        "Priority": 1000,
        "Strategy": {
          "Type": "Concurrency",
          "PermitLimit": 100,
          "QueueLimit": 90
        }
      },
      {
        "Name": "Standard Tenants - Fixed Window",
        "Description": "Standard rate limit with fixed window for tenants 5, 122, 45, 23",
        "Enabled": true,
        "TenantIds": [ 5, 122, 45, 23 ],
        "ClientIds": [ "crm", "airports" ],
        "Priority": 200,
        "Strategy": {
          "Type": "FixedWindow",
          "Window": "00:01:00",
          "PermitLimit": 100
        }
      },
      {
        "Name": "Enterprise Tenants - Sliding Window",
        "Description": "Flexible sliding window for enterprise tenants",
        "Enabled": true,
        "TenantIds": [ 6, 21, 32, 35 ],
        "ClientIds": [ "crm", "web-app", "*" ],
        "Priority": 150,
        "Strategy": {
          "Type": "SlidingWindow",
          "Window": "00:01:00",
          "PermitLimit": 500
        }
      },
      {
        "Name": "Basic Tenants - Conservative",
        "Description": "Conservative rate limit for basic tenants",
        "Enabled": true,
        "TenantIds": [ 10, 11, 12, 13 ],
        "ClientIds": [ "*" ],
        "Priority": 50,
        "Strategy": {
          "Type": "TokenBucket",
          "TokenLimit": 50,
          "TokensPerPeriod": 10,
          "ReplenishmentPeriod": "00:00:10"
        }
      }
    ]
  }
