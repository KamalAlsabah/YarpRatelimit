#nullable enable

using System;

namespace ReverseProxy.RateLimiting.Domain.Matchers
{
    /// <summary>
    /// Lightweight match result used in hot-path matchers.
    /// Contains a boolean result and a specificity score used to choose the most specific match deterministically.
    /// </summary>
    public readonly struct MatchResult : IEquatable<MatchResult>
    {
        public readonly bool IsMatch;
        /// <summary>
        /// Higher values indicate more specific matches. Comparisons should prefer larger Specificity.
        /// </summary>
        public readonly int Specificity;

        public MatchResult(bool isMatch, int specificity = 0)
        {
            IsMatch = isMatch;
            Specificity = isMatch ? specificity : 0;
        }

        public static implicit operator MatchResult(bool value) => new MatchResult(value, value ? 1 : 0);

        public override bool Equals(object? obj) => obj is MatchResult other && Equals(other);
        public bool Equals(MatchResult other) => IsMatch == other.IsMatch && Specificity == other.Specificity;
        public override int GetHashCode() => HashCode.Combine(IsMatch, Specificity);

        public static MatchResult NoMatch => new MatchResult(false, 0);
        public static MatchResult Match(int specificity) => new MatchResult(true, specificity);
    }
}
