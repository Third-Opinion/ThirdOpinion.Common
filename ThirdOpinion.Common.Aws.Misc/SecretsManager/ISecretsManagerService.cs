namespace ThirdOpinion.Common.Aws.Misc.SecretsManager;

/// <summary>
///     Interface for retrieving secrets from AWS Secrets Manager
/// </summary>
public interface ISecretsManagerService : IDisposable
{
    /// <summary>
    ///     Retrieves a secret from AWS Secrets Manager and parses it as a dictionary
    /// </summary>
    /// <param name="secretName">The name or ARN of the secret to retrieve</param>
    /// <param name="region">The AWS region where the secret is stored</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary containing the parsed secret values</returns>
    Task<Dictionary<string, string>> GetSecretAsync(
        string secretName,
        string region,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Retrieves a secret from AWS Secrets Manager using cached configuration
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary containing the parsed secret values</returns>
    Task<Dictionary<string, string>> GetSecretAsync(CancellationToken cancellationToken = default);
}