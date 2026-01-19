using System.Collections.Immutable;

namespace ReverseProxy.RateLimiting.Domain.Matchers
{
    public static class ActorMatcher
    {
        public static bool TenantMatches(ImmutableHashSet<int> tenantIds, int? tenantId)
        {
            if (tenantIds.IsEmpty)
                return true;

            if (!tenantId.HasValue)
                return false;

            return tenantIds.Contains(tenantId.Value);
        }

        public static bool ClientMatches(ImmutableHashSet<string> clientIds, string? clientId)
        {
            if (clientIds.IsEmpty)
                return true;

            if (string.IsNullOrEmpty(clientId))
                return false;

            return clientIds.Contains(clientId) || clientIds.Contains("*");
        }
    }
}