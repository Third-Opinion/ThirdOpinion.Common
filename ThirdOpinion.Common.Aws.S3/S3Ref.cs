using System.Text.RegularExpressions;

namespace ThirdOpinion.Common.Aws.S3;

public static class Helpers
{
    public const string S3ArnStringPattern =
        "^arn:aws:(s3):((?:[a-z0-9-]*)?):((?:[0-9]{12})?):([a-z0-9][a-z0-9.-]*)\\/(.*)?$";

    public const string S3BucketArnStringPattern =
        "^arn:aws:(s3):((?:[a-z0-9-]*)?):((?:[0-9]{12})?):([a-z0-9][a-z0-9.-]*)$";

    public static readonly Regex S3ArnPattern = new(
        S3ArnStringPattern,
        RegexOptions.Compiled);

    public static readonly Regex S3BucketArnPattern = new(
        S3BucketArnStringPattern,
        RegexOptions.Compiled);

    public static bool IsValidS3Arn(this string? arn)
    {
        return !string.IsNullOrWhiteSpace(arn) && 
               (S3ArnPattern.IsMatch(arn) || S3BucketArnPattern.IsMatch(arn));
    }
}

public class S3Ref
{
    private static readonly Regex S3ObjectUrlRegex = new(
        @"^https:\/\/([^.]+)\.([^.]+)\.([^.]+)\.amazonaws\.com\/(.+)$",
        RegexOptions.Compiled);

    private static readonly Regex S3EndpointUriRegex = new(
        @"^https:\/\/([^.]+)\.([^.]+)\.amazonaws\.com\/([^\/]+)\/(.+)$",
        RegexOptions.Compiled);

    public S3Ref(string bucket, string key, string? region, string? accountId)
    {
        Bucket = bucket;
        Key = key;
        Region = region;
        AccountId = accountId;
        if (key.Contains('/'))
        {
            string[] keyParts = key.Split('/');
            FileName = keyParts.Length > 1 ? key.Split('/').Last() : null;
        }
        else
        {
            FileName = key;
        }
    }

    public S3Ref(string arn)
    {
        S3Ref parsedArn = ParseArn(arn);

        Bucket = parsedArn.Bucket;
        Key = parsedArn.Key;
        Region = parsedArn.Region;
        AccountId = parsedArn.AccountId;
        FileName = parsedArn.FileName;
    }

    public string Bucket { get; }
    public string Key { get; }
    public string? Region { get; }
    public string? AccountId { get; }
    public string? FileName { get; }

    public override string ToString()
    {
        return $"arn:aws:s3:{Region}:{AccountId}:{Bucket}/{Key}";
    }

    public string ToUri()
    {
        return $"https://{Bucket}.{Region}.amazonaws.com/{Key}";
    }

    public string ToS3Path()
    {
        return $"{Bucket}/{Key}";
    }

    public string ToArn()
    {
        return $"arn:aws:s3:{Region}:{AccountId}:{Bucket}/{Key}";
    }

    //https://s3.eu-west-2.amazonaws.com/test-bucket/folder/file.json   
    public string ToS3EndpointUri()
    {
        return $"https://s3.{Region}.amazonaws.com/{Key}";
    }

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

    //https://s3.us-east-1.amazonaws.com/pf-ehr-int-ue1-ambient-scribe-data/bcaa1394-1896-4492-80c0-0eb6138cd1ca_test_3/transcript.json
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

    //https://pf-ehr-int-ue1-ambient-scribe-data.s3.us-east-1.amazonaws.com/llm-judge-test-cases/test-case_530/96299.wav

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