namespace ThirdOpinion.Common.Cognito;

/// <summary>
///     Configuration options for global application settings including Cognito and tenant configuration
/// </summary>
public class GlobalAppSettingsOptions
{
    /// <summary>
    ///     Gets or sets the prefix used for database table names
    /// </summary>
    public string TablePrefix { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the AWS Cognito configuration options
    /// </summary>
    public CognitoOptions Cognito { get; set; } = new();

    /// <summary>
    ///     Gets or sets the tenant-specific configuration options
    /// </summary>
    public TenantOptions Tenants { get; set; } = new();

    /// <summary>
    ///     Configuration options for tenant-specific settings
    /// </summary>
    public class TenantOptions
    {
        /// <summary>
        ///     Gets or sets the mapping of tenant identifiers to their associated groups
        /// </summary>
        public Dictionary<string, List<string>> TenantGroups { get; set; } = new();
    }

    /// <summary>
    ///     Configuration options for AWS Cognito authentication
    /// </summary>
    public class CognitoOptions
    {
        /// <summary>
        ///     Gets or sets the AWS region where Cognito is hosted
        /// </summary>
        public string Region { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the Cognito client ID for authentication
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the Cognito authority URL for JWT validation
        /// </summary>
        public string Authority { get; set; } = string.Empty;
    }
}