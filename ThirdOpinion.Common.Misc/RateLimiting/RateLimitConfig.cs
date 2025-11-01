using System.ComponentModel.DataAnnotations;

namespace ThirdOpinion.Common.Misc.RateLimiting;

/// <summary>
///     Configuration for rate limiting
/// </summary>
public class RateLimitConfig
{
    /// <summary>
    ///     Service name for this rate limiter
    /// </summary>
    [Required]
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    ///     Maximum calls per second
    /// </summary>
    [Range(0.01, 10000)]
    public double CallsPerSecond { get; set; }

    /// <summary>
    ///     Burst size (maximum tokens that can be accumulated)
    ///     If not specified, defaults to CallsPerSecond * 2
    /// </summary>
    [Range(1, 10000)]
    public int? BurstSize { get; set; }

    /// <summary>
    ///     Whether rate limiting is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets the actual burst size to use
    /// </summary>
    public int GetBurstSize()
    {
        return BurstSize ?? Math.Max(1, (int)Math.Ceiling(CallsPerSecond * 2));
    }

    /// <summary>
    ///     Creates a rate limit config from per-minute rate
    /// </summary>
    public static RateLimitConfig FromPerMinute(string serviceName, int callsPerMinute)
    {
        return new RateLimitConfig
        {
            ServiceName = serviceName,
            CallsPerSecond = callsPerMinute / 60.0,
            Enabled = callsPerMinute > 0
        };
    }
}