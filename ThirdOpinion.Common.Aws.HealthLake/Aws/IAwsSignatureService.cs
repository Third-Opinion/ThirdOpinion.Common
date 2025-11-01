namespace ThirdOpinion.Common.Aws.HealthLake.Aws;

/// <summary>
///     Service for signing AWS requests
/// </summary>
public interface IAwsSignatureService
{
    /// <summary>
    ///     Signs an HTTP request with AWS credentials
    /// </summary>
    Task SignRequestAsync(HttpRequestMessage request, string service, string region);
}