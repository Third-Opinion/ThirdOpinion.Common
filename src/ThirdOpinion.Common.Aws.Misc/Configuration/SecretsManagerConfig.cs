using System.ComponentModel.DataAnnotations;

namespace ThirdOpinion.Common.Aws.Misc.Configuration;

public class SecretsManagerConfig
{
    /// <summary>
    /// The name or ARN of the secret in AWS Secrets Manager
    /// </summary>
    [Required]
    public string SecretName { get; set; } = string.Empty;

    /// <summary>
    /// AWS region where the secret is stored
    /// </summary>
    [Required]
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// Cache TTL in minutes for retrieved secrets
    /// </summary>
    [Range(1, 1440)] // 1 minute to 24 hours
    public int CacheTtlMinutes { get; set; } = 60;

    /// <summary>
    /// Whether to enable caching of secrets
    /// </summary>
    public bool EnableCaching { get; set; } = true;
}