using System;
using System.Diagnostics;

namespace ReverseProxy.RateLimiting.Infrastructure.Monitoring
{
    /// <summary>
    /// Performance metrics for rate limiting operations
    /// </summary>
    public interface IRateLimitMetrics
    {
        void RecordResolutionTime(TimeSpan duration);
        void RecordCacheHit();
        void RecordCacheMiss();
        void RecordRejection(string partitionKey);
        void RecordAllowed(string partitionKey);
        PerformanceSnapshot GetSnapshot();
    }

    public sealed class PerformanceSnapshot
    {
        public long TotalRequests { get; set; }
        public long CacheHits { get; set; }
        public long CacheMisses { get; set; }
        public long Rejections { get; set; }
        public double AverageResolutionTimeMs { get; set; }
        public double MaxResolutionTimeMs { get; set; }
        public double CacheHitRate => TotalRequests > 0 ? (double)CacheHits / TotalRequests : 0;
        public double RejectionRate => TotalRequests > 0 ? (double)Rejections / TotalRequests : 0;
    }

    /// <summary>
    /// Default in-memory metrics collector
    /// </summary>
    public sealed class RateLimitMetrics : IRateLimitMetrics
    {
        private long _totalRequests;
        private long _cacheHits;
        private long _cacheMisses;
        private long _rejections;
        private long _totalResolutionTicks;
        private long _maxResolutionTicks;
        private readonly object _lock = new object();

        public void RecordResolutionTime(TimeSpan duration)
        {
            var ticks = duration.Ticks;
            
            lock (_lock)
            {
                _totalRequests++;
                _totalResolutionTicks += ticks;
                
                if (ticks > _maxResolutionTicks)
                    _maxResolutionTicks = ticks;
            }
        }

        public void RecordCacheHit()
        {
            System.Threading.Interlocked.Increment(ref _cacheHits);
        }

        public void RecordCacheMiss()
        {
            System.Threading.Interlocked.Increment(ref _cacheMisses);
        }

        public void RecordRejection(string partitionKey)
        {
            System.Threading.Interlocked.Increment(ref _rejections);
        }

        public void RecordAllowed(string partitionKey)
        {
            // Could track per-partition metrics here
        }

        public PerformanceSnapshot GetSnapshot()
        {
            lock (_lock)
            {
                var avgTicks = _totalRequests > 0 ? (double)_totalResolutionTicks / _totalRequests : 0;
                
                return new PerformanceSnapshot
                {
                    TotalRequests = _totalRequests,
                    CacheHits = _cacheHits,
                    CacheMisses = _cacheMisses,
                    Rejections = _rejections,
                    AverageResolutionTimeMs = TimeSpan.FromTicks((long)avgTicks).TotalMilliseconds,
                    MaxResolutionTimeMs = TimeSpan.FromTicks(_maxResolutionTicks).TotalMilliseconds
                };
            }
        }
    }

    /// <summary>
    /// Stopwatch helper for measuring operation duration
    /// </summary>
    public readonly struct PerformanceTimer : IDisposable
    {
        private readonly IRateLimitMetrics _metrics;
        private readonly Stopwatch _stopwatch;

        public PerformanceTimer(IRateLimitMetrics metrics)
        {
            _metrics = metrics;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _metrics?.RecordResolutionTime(_stopwatch.Elapsed);
        }
    }
}
