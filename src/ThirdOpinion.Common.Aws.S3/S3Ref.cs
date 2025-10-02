using System.Text.RegularExpressions;

namespace ThirdOpinion.Common.Aws.S3;

/// <summary>
/// Helper methods and constants for working with S3 ARNs and patterns.
/// </summary>
public static class Helpers
{
    /// <summary>
    /// Regular expression pattern for validating S3 ARN format including object key.
    /// </summary>
    public const string S3ArnStringPattern =
        "^arn:aws:(s3):((?:[a-z0-9-]*)?):((?:[0-9]{12})?):([a-z0-9][a-z0-9.-]*)\\/(.*)?$";

    /// <summary>
    /// Regular expression pattern for validating S3 bucket ARN format without object key.
    /// </summary>
    public const string S3BucketArnStringPattern =
        "^arn:aws:(s3):((?:[a-z0-9-]*)?):((?:[0-9]{12})?):([a-z0-9][a-z0-9.-]*)$";

    /// <summary>
    /// Compiled regular expression for validating S3 ARN format including object key.
    /// </summary>
    public static readonly Regex S3ArnPattern = new(
        S3ArnStringPattern,
        RegexOptions.Compiled);

    /// <summary>
    /// Compiled regular expression for validating S3 bucket ARN format without object key.
    /// </summary>
    public static readonly Regex S3BucketArnPattern = new(
        S3BucketArnStringPattern,
        RegexOptions.Compiled);

    /// <summary>
    /// Validates whether the given string is a valid S3 ARN.
    /// </summary>
    /// <param name="arn">The ARN string to validate.</param>
    /// <returns>True if the ARN is valid; otherwise, false.</returns>
    public static bool IsValidS3Arn(this string? arn)
    {
        return !string.IsNullOrWhiteSpace(arn) && 
               (S3ArnPattern.IsMatch(arn) || S3BucketArnPattern.IsMatch(arn));
    }
}

/// <summary>
/// Represents a reference to an S3 object, including bucket, key, region, and account information.
/// </summary>
public class S3Ref
{
    private static readonly Regex S3ObjectUrlRegex = new(
        @"^https:\/\/([^.]+)\.([^.]+)\.([^.]+)\.amazonaws\.com\/(.+)$",
        RegexOptions.Compiled);

    private static readonly Regex S3EndpointUriRegex = new(
        @"^https:\/\/([^.]+)\.([^.]+)\.amazonaws\.com\/([^\/]+)\/(.+)$",
        RegexOptions.Compiled);

    /// <summary>
    /// Initializes a new instance of the S3Ref class with the specified parameters.
    /// </summary>
    /// <param name="bucket">The S3 bucket name.</param>
    /// <param name="key">The S3 object key.</param>
    /// <param name="region">The AWS region (optional).</param>
    /// <param name="accountId">The AWS account ID (optional).</param>
    public S3Ref(string bucket, string key, string? region, string? accountId)
    {
        Bucket = bucket;
        Key = key;
        Region = region;
        AccountId = accountId;
        if (string.IsNullOrEmpty(key))
        {
            FileName = null;
        }
        else if (key.Contains('/'))
        {
            string lastPart = key.Split('/').Last();
            FileName = string.IsNullOrEmpty(lastPart) ? null : lastPart;
        }
        else
        {
            FileName = key;
        }
    }

    /// <summary>
    /// Initializes a new instance of the S3Ref class by parsing an S3 ARN.
    /// </summary>
    /// <param name="arn">The S3 ARN to parse.</param>
    public S3Ref(string arn)
    {
        S3Ref parsedArn = ParseArn(arn);

        Bucket = parsedArn.Bucket;
        Key = parsedArn.Key;
        Region = parsedArn.Region;
        AccountId = parsedArn.AccountId;
        FileName = parsedArn.FileName;
    }

    /// <summary>
    /// Gets the S3 bucket name.
    /// </summary>
    public string Bucket { get; }
    /// <summary>
    /// Gets the S3 object key.
    /// </summary>
    public string Key { get; }
    /// <summary>
    /// Gets the AWS region, if available.
    /// </summary>
    public string? Region { get; }
    /// <summary>
    /// Gets the AWS account ID, if available.
    /// </summary>
    public string? AccountId { get; }
    /// <summary>
    /// Gets the file name extracted from the object key, if available.
    /// </summary>
    public string? FileName { get; }

    /// <summary>
    /// Returns the S3 ARN representation of this reference.
    /// </summary>
    /// <returns>The S3 ARN as a string.</returns>
    public override string ToString()
    {
        return $"arn:aws:s3:{Region}:{AccountId}:{Bucket}/{Key}";
    }

    /// <summary>
    /// Converts this S3 reference to an S3 object URI.
    /// </summary>
    /// <returns>The S3 object URI.</returns>
    public string ToUri()
    {
        return $"https://{Bucket}.{Region}.amazonaws.com/{Key}";
    }

    /// <summary>
    /// Converts this S3 reference to an S3 path format (bucket/key).
    /// </summary>
    /// <returns>The S3 path as bucket/key.</returns>
    public string ToS3Path()
    {
        return $"{Bucket}/{Key}";
    }

    /// <summary>
    /// Converts this S3 reference to an ARN format.
    /// </summary>
    /// <returns>The S3 ARN.</returns>
    public string ToArn()
    {
        return $"arn:aws:s3:{Region}:{AccountId}:{Bucket}/{Key}";
    }

    /// <summary>
    /// Converts this S3 reference to an S3 endpoint URI format.
    /// </summary>
    /// <returns>The S3 endpoint URI.</returns>
    public string ToS3EndpointUri()
    {
        return $"https://s3.{Region}.amazonaws.com/{Key}";
    }

    /// <summary>
    /// Parses an S3 ARN string into an S3Ref object.
    /// </summary>
    /// <param name="arn">The S3 ARN to parse.</param>
    /// <returns>A new S3Ref instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the ARN format is invalid.</exception>
    public static S3Ref ParseArn(string arn)
    {
        if (string.IsNullOrWhiteSpace(arn))
            throw new ArgumentException("ARN cannot be null or empty", nameof(arn));

        Match match = Helpers.S3ArnPattern.Match(arn);
        if (!match.Success)
            throw new ArgumentException("Invalid S3 ARN format", nameof(arn));

        if (match.Groups[1].Value != "s3")
            throw new ArgumentException("Invalid S3 ARN format", nameof(arn));

        string? region = string.IsNullOrWhiteSpace(match.Groups[2].Value)
            ? null
            : match.Groups[2].Value;
        string? accountId = string.IsNullOrWhiteSpace(match.Groups[3].Value)
            ? null
            : match.Groups[3].Value;

        string bucket = match.Groups[4].Value;
        string key = match.Groups[5].Value;

        return new S3Ref(bucket, key, region, accountId);
    }

    /// <summary>
    /// Parses an S3 object URI into an S3Ref object.
    /// </summary>
    /// <param name="objectUri">The S3 object URI to parse.</param>
    /// <returns>A new S3Ref instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the URI format is invalid.</exception>
    public static S3Ref ParseObjectUri(string objectUri)
    {
        if (string.IsNullOrWhiteSpace(objectUri))
            throw new ArgumentException("URI cannot be null or empty", nameof(objectUri));

        Match match = S3ObjectUrlRegex.Match(objectUri);
        if (!match.Success)
            throw new ArgumentException("Invalid S3 URI format", nameof(objectUri));

        if (match.Groups[2].Value != "s3")
            throw new ArgumentException("Invalid S3 URI format", nameof(objectUri));

        string? region = string.IsNullOrWhiteSpace(match.Groups[3].Value)
            ? null
            : match.Groups[3].Value;

        return new S3Ref(match.Groups[1].Value, match.Groups[4].Value, region, null);
    }

    /// <summary>
    /// Parses an S3 endpoint URI into an S3Ref object.
    /// </summary>
    /// <param name="fileUri">The S3 endpoint URI to parse.</param>
    /// <returns>A new S3Ref instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the URI format is invalid.</exception>
    public static S3Ref ParseEndpointUri(string fileUri)
    {
        if (string.IsNullOrWhiteSpace(fileUri))
            throw new ArgumentException("URI cannot be null or empty", nameof(fileUri));

        Match match = S3EndpointUriRegex.Match(fileUri);
        if (!match.Success)
            throw new ArgumentException("Invalid S3 URI format", nameof(fileUri));

        if (match.Groups[1].Value != "s3")
            throw new ArgumentException("Invalid S3 URI format", nameof(fileUri));

        return new S3Ref(match.Groups[3].Value, match.Groups[4].Value, match.Groups[2].Value, null);
    }

    /// <summary>
    /// Attempts to parse an S3 object URL into an S3Ref object.
    /// </summary>
    /// <param name="objectUrl">The S3 object URL to parse.</param>
    /// <param name="result">The parsed S3Ref object, or null if parsing failed.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParseObjectUrl(string objectUrl, out S3Ref? result)
    {
        result = null;

        try
        {
            result = ParseObjectUri(objectUrl);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to parse an S3 ARN into an S3Ref object.
    /// </summary>
    /// <param name="arn">The S3 ARN to parse.</param>
    /// <param name="result">The parsed S3Ref object, or null if parsing failed.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParseArn(string arn, out S3Ref? result)
    {
        result = null;

        try
        {
            result = ParseArn(arn);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to parse an S3 endpoint URI into an S3Ref object.
    /// </summary>
    /// <param name="fileUri">The S3 endpoint URI to parse.</param>
    /// <param name="result">The parsed S3Ref object, or null if parsing failed.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParseEndpointUri(string fileUri, out S3Ref? result)
    {
        result = null;

        try
        {
            result = ParseEndpointUri(fileUri);
            return true;
        }
        catch
        {
            return false;
        }
    }
}