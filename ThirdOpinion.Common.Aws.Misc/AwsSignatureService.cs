using System.Net.Http.Headers;
using System.Text;
using Amazon.Runtime;
using AwsSignatureVersion4;
using Microsoft.Extensions.Logging;

namespace ThirdOpinion.Common.Aws.Misc;

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

    public async Task<HttpRequestMessage> SignRequestAsync(
        HttpRequestMessage request,
        string service,
        string region,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the request body if present
            string? requestBody = null;
            if (request.Content != null)
                requestBody = await request.Content.ReadAsStringAsync(cancellationToken);

            return await SignRequestWithBodyAsync(request, requestBody ?? string.Empty, service,
                region, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to sign AWS request for service {Service} in region {Region}", service,
                region);
            throw;
        }
    }

    public async Task<HttpRequestMessage> SignRequestWithBodyAsync(
        HttpRequestMessage request,
        string requestBody,
        string service,
        string region,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Signing request for {Service} in region {Region}", service, region);

            // Get immutable credentials
            ImmutableCredentials? immutableCredentials = await _credentials.GetCredentialsAsync();

            // Log AWS credential info for debugging (mask sensitive data)
            string maskedAccessKey = immutableCredentials.AccessKey.Length > 8
                ? $"{immutableCredentials.AccessKey.Substring(0, 8)}...{immutableCredentials.AccessKey.Substring(immutableCredentials.AccessKey.Length - 4)}"
                : "***";
            bool hasToken = !string.IsNullOrEmpty(immutableCredentials.Token);
            // WARNING: Never log secret keys or tokens in production!
            // _logger.LogInformation($"Token: {immutableCredentials.Token}");
            // _logger.LogInformation($"SecretKey: {immutableCredentials.SecretKey}");
            _logger.LogInformation(
                "AWS Credentials - AccessKeyId: {AccessKey}, HasSessionToken: {HasToken}, Profile: {Profile}, Expiration: {Expiration}",
                maskedAccessKey,
                hasToken,
                Environment.GetEnvironmentVariable("AWS_PROFILE") ?? "default",
                Environment.GetEnvironmentVariable("AWS_CREDENTIAL_EXPIRATION") ?? "not set");

            // Create the signer - AwsSignatureHandlerSettings takes credentials directly
            var signer = new AwsSignatureHandlerSettings(
                region,
                service,
                immutableCredentials);

            // Build the canonical URI from the request
            // AWS Signature V4 REQUIRES $ to be encoded as %24 in the canonical request
            string canonicalUri = request.RequestUri?.AbsolutePath ?? "/";

            // CRITICAL: AWS Signature V4 requires $ to be encoded as %24
            // If the path contains unencoded $, we must encode it for signature calculation
            if (canonicalUri.Contains("$") && !canonicalUri.Contains("%24"))
            {
                _logger.LogInformation(
                    "Encoding $ to %24 for AWS signature calculation: {OriginalPath}",
                    canonicalUri);
                canonicalUri = canonicalUri.Replace("$", "%24");
            }
            else if (canonicalUri.Contains("%24"))
            {
                // Already encoded, use as-is for signature
                _logger.LogInformation(
                    "Path already contains %24 (encoded $), using for signature: {Path}",
                    canonicalUri);
            }

            string queryString = request.RequestUri?.Query ?? string.Empty;
            if (queryString.StartsWith("?")) queryString = queryString.Substring(1);

            // Properly encode query parameters for AWS Signature V4
            queryString = EncodeQueryStringForAwsSignature(queryString);

            _logger.LogInformation(
                "Request URL components - CanonicalUri: {CanonicalUri}, QueryString: {QueryString}, FullUrl: {FullUrl}",
                canonicalUri, queryString, request.RequestUri?.ToString());

            // Create the signed headers
            var httpMethod = request.Method.ToString();
            var headers = new Dictionary<string, string>();

            // Add existing headers from the request
            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
                headers[header.Key.ToLowerInvariant()] = string.Join(",", header.Value);

            // Add content headers if present
            if (request.Content?.Headers != null)
                foreach (KeyValuePair<string, IEnumerable<string>> header in
                         request.Content.Headers)
                    headers[header.Key.ToLowerInvariant()] = string.Join(",", header.Value);

            // Add host header if not present
            if (!headers.ContainsKey("host") && request.RequestUri != null)
                headers["host"] = request.RequestUri.Host;

            // Sign the request using the AwsSignatureVersion4 package
            var httpRequestMessage = new HttpRequestMessage(request.Method, request.RequestUri);

            // Create HttpClient with the signature handler
            using var httpClient = new HttpClient(new AwsSignatureHandler(signer)
            {
                InnerHandler = new HttpClientHandler()
            });

            // Copy headers to the new request
            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
                httpRequestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);

            // Set content if present
            if (!string.IsNullOrEmpty(requestBody))
            {
                httpRequestMessage.Content = new StringContent(requestBody, Encoding.UTF8);

                // Preserve original Content-Type if it exists, otherwise use application/json
                if (request.Content?.Headers?.ContentType != null)
                    httpRequestMessage.Content.Headers.ContentType
                        = request.Content.Headers.ContentType;
                else
                    httpRequestMessage.Content.Headers.ContentType
                        = new MediaTypeHeaderValue("application/json");
            }
            else if (request.Content != null)
            {
                httpRequestMessage.Content = request.Content;
            }

            // The AwsSignatureHandler will automatically add the required AWS SigV4 headers
            // when the request is sent through the HttpClient

            // Since we need to return a signed request without sending it,
            // we'll manually add the signature headers
            DateTime dateTime = DateTime.UtcNow;
            var dateStamp = dateTime.ToString("yyyyMMdd");
            var amzDate = dateTime.ToString("yyyyMMddTHHmmssZ");

            // Add required AWS headers to both the request and the headers dictionary for signing
            headers["x-amz-date"] = amzDate;
            httpRequestMessage.Headers.Add("X-Amz-Date", amzDate);

            if (!string.IsNullOrEmpty(immutableCredentials.Token))
            {
                headers["x-amz-security-token"] = immutableCredentials.Token;
                httpRequestMessage.Headers.Add("X-Amz-Security-Token", immutableCredentials.Token);
            }

            // Calculate the content hash
            string contentHash = AWS4Signer.ComputeHash(requestBody ?? string.Empty);

            // Only add X-Amz-Content-Sha256 header for non-GET requests
            // AWS console doesn't send this header for GET requests
            if (request.Method != HttpMethod.Get)
            {
                headers["x-amz-content-sha256"] = contentHash;
                httpRequestMessage.Headers.Add("X-Amz-Content-Sha256", contentHash);
            }

            // Create canonical request and sign it
            string authorizationHeader = CreateAuthorizationHeader(
                httpMethod,
                canonicalUri,
                queryString,
                headers,
                contentHash,
                immutableCredentials,
                service,
                region,
                dateStamp,
                amzDate);

            httpRequestMessage.Headers.TryAddWithoutValidation("Authorization",
                authorizationHeader);

            _logger.LogDebug("Successfully signed request for {Service}", service);
            return httpRequestMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to sign request with body for service {Service} in region {Region}",
                service, region);
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

        _logger.LogDebug("Available headers for signing: {Headers}",
            string.Join(", ", headers.Select(h => $"{h.Key}={h.Value}")));

        // Always include host header
        if (headers.ContainsKey("host"))
            headersToSign.Add(new KeyValuePair<string, string>("host", headers["host"]));

        // Include x-amz-date header
        if (headers.ContainsKey("x-amz-date"))
            headersToSign.Add(
                new KeyValuePair<string, string>("x-amz-date", headers["x-amz-date"]));

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

        _logger.LogDebug(
            "AWS Signature Headers - Canonical: {CanonicalHeaders}, Signed: {SignedHeaders}",
            canonicalHeaders.Replace("\n", "\\n"), signedHeaders);

        // Create the canonical request
        var canonicalRequest
            = $"{httpMethod}\n{canonicalUri}\n{queryString}\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";

        _logger.LogInformation("Canonical Request:\n{CanonicalRequest}", canonicalRequest);
        _logger.LogDebug(
            "Canonical Request Components - Method: {Method}, URI: {URI}, Query: {Query}, PayloadHash: {PayloadHash}",
            httpMethod, canonicalUri, queryString, payloadHash);

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

        _logger.LogInformation("Generated Authorization Header: {AuthorizationHeader}",
            authorizationHeader);

        // Log the string to sign for debugging
        _logger.LogDebug("String to Sign:\n{StringToSign}", stringToSign);

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

            _logger.LogDebug(
                "Encoded query string for AWS signature: Original={Original}, Encoded={Encoded}",
                queryString, result);

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