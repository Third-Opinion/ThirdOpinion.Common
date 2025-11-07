using System.Net.Http.Headers;
using System.Text;
using Amazon.Runtime;
using AwsSignatureVersion4;
using Microsoft.Extensions.Logging;

namespace ThirdOpinion.Common.Aws.HealthLake.Aws;

/// <summary>
///     Implementation of AWS Signature Version 4 signing for HTTP requests
/// </summary>
public class AwsSignatureService : IAwsSignatureService
{
    private readonly AWSCredentials _credentials;
    private readonly ILogger<AwsSignatureService> _logger;

    public AwsSignatureService(
        ILogger<AwsSignatureService> logger,
        AWSCredentials credentials)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
    }

    public async Task SignRequestAsync(HttpRequestMessage request, string service, string region)
    {
        try
        {
            _logger.LogDebug("Signing request for {Service} in region {Region}", service, region);

            // Get immutable credentials
            ImmutableCredentials immutableCredentials = await _credentials.GetCredentialsAsync();

            // Get the request body if present
            string? requestBody = null;
            if (request.Content != null)
                requestBody = await request.Content.ReadAsStringAsync();

            // Calculate the content hash
            string contentHash = AWS4Signer.ComputeHash(requestBody ?? string.Empty);

            // Build the canonical URI
            string canonicalUri = request.RequestUri?.AbsolutePath ?? "/";

            // Parse query string
            string queryString = request.RequestUri?.Query ?? string.Empty;
            if (queryString.StartsWith("?")) queryString = queryString.Substring(1);

            // Encode query string for AWS Signature V4
            queryString = EncodeQueryStringForAwsSignature(queryString);

            // Create headers dictionary
            var headers = new Dictionary<string, string>();

            // Add existing headers from the request
            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
                headers[header.Key.ToLowerInvariant()] = string.Join(",", header.Value);

            // Add content headers if present
            if (request.Content?.Headers != null)
                foreach (KeyValuePair<string, IEnumerable<string>> header in request.Content.Headers)
                    headers[header.Key.ToLowerInvariant()] = string.Join(",", header.Value);

            // Add host header if not present
            if (!headers.ContainsKey("host") && request.RequestUri != null)
                headers["host"] = request.RequestUri.Host;

            // Calculate date
            DateTime dateTime = DateTime.UtcNow;
            var dateStamp = dateTime.ToString("yyyyMMdd");
            var amzDate = dateTime.ToString("yyyyMMddTHHmmssZ");

            // Add required AWS headers
            headers["x-amz-date"] = amzDate;
            request.Headers.Add("X-Amz-Date", amzDate);

            if (!string.IsNullOrEmpty(immutableCredentials.Token))
            {
                headers["x-amz-security-token"] = immutableCredentials.Token;
                request.Headers.Add("X-Amz-Security-Token", immutableCredentials.Token);
            }

            // Only add X-Amz-Content-Sha256 header for non-GET requests
            if (request.Method != HttpMethod.Get)
            {
                headers["x-amz-content-sha256"] = contentHash;
                request.Headers.Add("X-Amz-Content-Sha256", contentHash);
            }

            // Create canonical request and sign it
            string authorizationHeader = CreateAuthorizationHeader(
                request.Method.ToString(),
                canonicalUri,
                queryString,
                headers,
                contentHash,
                immutableCredentials,
                service,
                region,
                dateStamp,
                amzDate);

            request.Headers.TryAddWithoutValidation("Authorization", authorizationHeader);

            _logger.LogDebug("Successfully signed request for {Service}", service);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to sign AWS request for service {Service} in region {Region}", service, region);
            throw;
        }
    }

    private string CreateAuthorizationHeader(
        string httpMethod,
        string canonicalUri,
        string queryString,
        Dictionary<string, string> headers,
        string payloadHash,
        ImmutableCredentials credentials,
        string service,
        string region,
        string dateStamp,
        string amzDate)
    {
        // Create the canonical headers - only include essential headers for signing
        var headersToSign = new List<KeyValuePair<string, string>>();

        // Always include host header
        if (headers.ContainsKey("host"))
            headersToSign.Add(new KeyValuePair<string, string>("host", headers["host"]));

        // Include x-amz-date header
        if (headers.ContainsKey("x-amz-date"))
            headersToSign.Add(new KeyValuePair<string, string>("x-amz-date", headers["x-amz-date"]));

        // Include x-amz-security-token if present
        if (headers.ContainsKey("x-amz-security-token"))
            headersToSign.Add(new KeyValuePair<string, string>("x-amz-security-token",
                headers["x-amz-security-token"]));

        // Include x-amz-content-sha256 if present (only added for non-GET requests)
        if (headers.ContainsKey("x-amz-content-sha256"))
            headersToSign.Add(new KeyValuePair<string, string>("x-amz-content-sha256",
                headers["x-amz-content-sha256"]));

        List<KeyValuePair<string, string>> sortedHeaders
            = headersToSign.OrderBy(h => h.Key).ToList();
        string canonicalHeaders
            = string.Join("\n", sortedHeaders.Select(h => $"{h.Key}:{h.Value.Trim()}")) + "\n";
        string signedHeaders = string.Join(";", sortedHeaders.Select(h => h.Key));

        // Create the canonical request
        var canonicalRequest
            = $"{httpMethod}\n{canonicalUri}\n{queryString}\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";

        _logger.LogDebug("Canonical Request:\n{CanonicalRequest}", canonicalRequest);

        // Create the string to sign
        var algorithm = "AWS4-HMAC-SHA256";
        var credentialScope = $"{dateStamp}/{region}/{service}/aws4_request";
        string hashedCanonicalRequest = AWS4Signer.ComputeHash(canonicalRequest);
        var stringToSign = $"{algorithm}\n{amzDate}\n{credentialScope}\n{hashedCanonicalRequest}";

        // Calculate the signature
        byte[] signingKey
            = AWS4Signer.GetSignatureKey(credentials.SecretKey, dateStamp, region, service);
        string signature = AWS4Signer.HmacSha256(stringToSign, signingKey);

        // Create the authorization header
        var authorizationHeader
            = $"{algorithm} Credential={credentials.AccessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";

        _logger.LogDebug("Generated Authorization Header: {AuthorizationHeader}",
            authorizationHeader);

        return authorizationHeader;
    }

    /// <summary>
    ///     Properly encodes query string parameters for AWS Signature V4 calculation
    /// </summary>
    private string EncodeQueryStringForAwsSignature(string queryString)
    {
        if (string.IsNullOrEmpty(queryString)) return string.Empty;

        try
        {
            // Parse query parameters
            var parameters = new List<KeyValuePair<string, string>>();
            string[] pairs = queryString.Split('&', StringSplitOptions.RemoveEmptyEntries);

            foreach (string pair in pairs)
            {
                string[] parts = pair.Split('=', 2);
                string key = parts[0];
                string value = parts.Length > 1 ? parts[1] : string.Empty;

                // URL encode both key and value according to AWS Signature V4 requirements
                string encodedKey = Uri.EscapeDataString(key);
                string encodedValue = Uri.EscapeDataString(value);

                // AWS Signature V4 specific encoding: encode $ to %24
                encodedKey = encodedKey.Replace("$", "%24");
                encodedValue = encodedValue.Replace("$", "%24");

                parameters.Add(new KeyValuePair<string, string>(encodedKey, encodedValue));
            }

            // Sort parameters lexicographically by key (required for AWS Signature V4)
            parameters.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));

            // Rebuild query string
            IEnumerable<string> encodedPairs = parameters.Select(p => $"{p.Key}={p.Value}");
            string result = string.Join("&", encodedPairs);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to encode query string for AWS signature, using original: {QueryString}",
                queryString);
            return queryString;
        }
    }
}
