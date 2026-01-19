using ReverseProxy.RateLimiting.Domain.Matchers;
using System;
using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;

namespace ReverseProxy.RateLimiting.Infrastructure.Matching
{
    public sealed class IpAddressMatcher : IIpAddressMatcher
    {
        public bool Matches(ImmutableHashSet<string> allowedPatterns, string ipAddress)
        {
            if (allowedPatterns.IsEmpty)
                return true;

            if (!IPAddress.TryParse(ipAddress, out var clientAddress))
                return false;

            foreach (var pattern in allowedPatterns)
            {
                if (pattern == "*")
                    return true;

                if (pattern.Contains('/'))
                {
                    if (IsInCidrRange(pattern, clientAddress))
                        return true;
                }
                else if (IPAddress.TryParse(pattern, out var ruleAddress) && ruleAddress.Equals(clientAddress))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInCidrRange(string cidr, IPAddress address)
        {
            var parts = cidr.Split('/');
            if (parts.Length != 2)
                return false;

            if (!IPAddress.TryParse(parts[0], out var baseAddress))
                return false;

            if (!int.TryParse(parts[1], out var prefixLength))
                return false;

            if (address.AddressFamily != baseAddress.AddressFamily)
                return false;

            int maxBits = address.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
            if (prefixLength < 0 || prefixLength > maxBits)
                return false;

            var addressBytes = address.GetAddressBytes();
            var baseBytes = baseAddress.GetAddressBytes();

            int fullBytes = prefixLength / 8;
            int remainingBits = prefixLength % 8;

            for (int i = 0; i < fullBytes; i++)
                if (addressBytes[i] != baseBytes[i])
                    return false;

            if (remainingBits > 0)
            {
                int mask = 0xFF << 8 - remainingBits;
                if ((addressBytes[fullBytes] & mask) != (baseBytes[fullBytes] & mask))
                    return false;
            }

            return true;
        }
    }
}
