using Amazon.S3;
using Amazon.S3.Model;

namespace ThirdOpinion.Common.Aws.S3;

public class S3UrlGenerator(IAmazonS3 s3Client)
{
    public async Task<string> GeneratePreSignedUrl(string s3Arn, TimeSpan expiration)
    {
        S3Ref s3Ref = S3Ref.ParseArn(s3Arn); //Let this throw

        var request = new GetPreSignedUrlRequest
        {
            BucketName = s3Ref.Bucket,
            Key = s3Ref.Key,
            Expires = DateTime.UtcNow.Add(expiration)
        };

        string? x = await s3Client.GetPreSignedURLAsync(request);
        return x;
    }

    private (string bucketName, string objectKey) ParseS3Arn(string arn)
    {
        S3Ref s3Ref = S3Ref.ParseArn(arn); //Let this throw

        string[] parts = arn.Split(":::");

        string bucketAndKey = parts[1];
        int firstSlashIndex = bucketAndKey.IndexOf('/');

        if (firstSlashIndex == -1)
            throw new ArgumentException("ARN does not contain an object key", nameof(arn));

        string bucketName = bucketAndKey.Substring(0, firstSlashIndex);
        string objectKey = bucketAndKey.Substring(firstSlashIndex + 1);

        return (bucketName, objectKey);
    }
}