using System.ComponentModel.DataAnnotations;

namespace ThirdOpinion.Common.AthenaEhr;

public class AthenaConfig
{
    [Required]
    [Url]
    public string BaseUrl { get; set; } = string.Empty;

    [Required]
    public string PracticeId { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public int RateLimitPerMinute { get; set; } = 60;

    public int RequestTimeoutSeconds { get; set; } = 30;

    public bool UseTokenCache { get; set; } = true;

    public int TokenCacheMinutes { get; set; } = 30;
}