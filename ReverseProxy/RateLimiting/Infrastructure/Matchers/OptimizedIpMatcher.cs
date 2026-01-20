using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace ReverseProxy.RateLimiting.Infrastructure.Matchers
{
    /// <summary>
    /// Pre-computed CIDR range for fast IP matching
    /// </summary>
    public sealed class CidrRange
    {
        public IPAddress BaseAddress { get; }
        public int PrefixLength { get; }
        public byte[] BaseBytes { get; }
        public int FullBytes { get; }
        public int RemainderBits { get; }
        public int RemainderMask { get; }

        public CidrRange(string cidr)
        {
            var parts = cidr.Split('/');
            if (parts.Length != 2)
                throw new ArgumentException($"Invalid CIDR format: {cidr}");

            if (!IPAddress.TryParse(parts[0], out var baseAddress))
                throw new ArgumentException($"Invalid IP address in CIDR: {parts[0]}");

            if (!int.TryParse(parts[1], out var prefixLength))
                throw new ArgumentException($"Invalid prefix length in CIDR: {parts[1]}");

            int maxBits = baseAddress.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
            if (prefixLength < 0 || prefixLength > maxBits)
                throw new ArgumentException($"Invalid prefix length: {prefixLength}");

            BaseAddress = baseAddress;
            PrefixLength = prefixLength;
            BaseBytes = baseAddress.GetAddressBytes();
            FullBytes = prefixLength / 8;
            RemainderBits = prefixLength % 8;
            RemainderMask = RemainderBits > 0 ? (0xFF << (8 - RemainderBits)) : 0;
        }

        public bool Contains(IPAddress address)
        {
            if (address.AddressFamily != BaseAddress.AddressFamily)
                return false;

            var addressBytes = address.GetAddressBytes();

            // Check full bytes
            for (int i = 0; i < FullBytes; i++)
            {
                if (addressBytes[i] != BaseBytes[i])
                    return false;
            }

            // Check remainder bits
            if (RemainderBits > 0)
            {
                if ((addressBytes[FullBytes] & RemainderMask) != (BaseBytes[FullBytes] & RemainderMask))
                    return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Optimized IP pattern set with pre-computed CIDR ranges and caching
    /// </summary>
    public sealed class IpPatternSet
    {
        private readonly bool _matchAny;
        private readonly IPAddress[] _exactIps;
        private readonly CidrRange[] _cidrRanges;
        
        // Cache for IP match results to avoid repeated calculations
        private readonly ConcurrentDictionary<string, bool> _matchCache;
        private const int MaxCacheSize = 1000; // Limit cache size

        public IpPatternSet(System.Collections.Immutable.ImmutableHashSet<string> patterns)
        {
            if (patterns.IsEmpty)
            {
                _matchAny = true;
                _exactIps = Array.Empty<IPAddress>();
                _cidrRanges = Array.Empty<CidrRange>();
                _matchCache = new ConcurrentDictionary<string, bool>();
                return;
            }

            var exactIpList = new System.Collections.Generic.List<IPAddress>();
            var cidrList = new System.Collections.Generic.List<CidrRange>();

            foreach (var pattern in patterns)
            {
                if (pattern == "*")
                {
                    _matchAny = true;
                    _exactIps = Array.Empty<IPAddress>();
                    _cidrRanges = Array.Empty<CidrRange>();
                    _matchCache = new ConcurrentDictionary<string, bool>();
                    return;
                }

                if (pattern.Contains('/'))
                {
                    try
                    {
                        cidrList.Add(new CidrRange(pattern));
                    }
                    catch
                    {
                        // Skip invalid CIDR patterns
                    }
                }
                else if (IPAddress.TryParse(pattern, out var ip))
                {
                    exactIpList.Add(ip);
                }
            }

            _matchAny = false;
            _exactIps = exactIpList.ToArray();
            _cidrRanges = cidrList.ToArray();
            _matchCache = new ConcurrentDictionary<string, bool>();
        }

        public bool Matches(string ipAddress)
        {
            if (_matchAny)
                return true;

            // Check cache first
            if (_matchCache.TryGetValue(ipAddress, out var cachedResult))
                return cachedResult;

            if (!IPAddress.TryParse(ipAddress, out var address))
                return false;

            bool result = MatchesInternal(address);

            // Add to cache if not too large
            if (_matchCache.Count < MaxCacheSize)
            {
                _matchCache.TryAdd(ipAddress, result);
            }

            return result;
        }

        private bool MatchesInternal(IPAddress address)
        {
            // Check exact IPs - O(n) but typically small
            foreach (var ip in _exactIps)
            {
                if (ip.Equals(address))
                    return true;
            }

            // Check CIDR ranges - O(n) but pre-computed
            foreach (var cidr in _cidrRanges)
            {
                if (cidr.Contains(address))
                    return true;
            }

            return false;
        }
    }
}
