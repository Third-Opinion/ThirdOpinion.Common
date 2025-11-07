using ThirdOpinion.Common.Aws.S3;

namespace ThirdOpinion.Common.Aws.Tests.S3;

public class tS3RefTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var s3Ref = new S3Ref("test-bucket", "path/to/file.txt", "us-east-2", "123456789012");

        // Assert
        s3Ref.Bucket.ShouldBe("test-bucket");
        s3Ref.Key.ShouldBe("path/to/file.txt");
        s3Ref.Region.ShouldBe("us-east-2");
        s3Ref.AccountId.ShouldBe("123456789012");
        s3Ref.FileName.ShouldBe("file.txt");
    }

    [Fact]
    public void Constructor_WithKeyWithoutSlash_SetsFileNameAsKey()
    {
        // Arrange & Act
        var s3Ref = new S3Ref("test-bucket", "file.txt", "us-east-2", "123456789012");

        // Assert
        s3Ref.FileName.ShouldBe("file.txt");
    }

    [Fact]
    public void Constructor_WithValidArn_ParsesCorrectly()
    {
        // Arrange
        var arn = "arn:aws:s3:us-east-2:123456789012:test-bucket/path/to/file.txt";

        // Act
        var s3Ref = new S3Ref(arn);

        // Assert
        s3Ref.Bucket.ShouldBe("test-bucket");
        s3Ref.Key.ShouldBe("path/to/file.txt");
        s3Ref.Region.ShouldBe("us-east-2");
        s3Ref.AccountId.ShouldBe("123456789012");
        s3Ref.FileName.ShouldBe("file.txt");
    }

    [Fact]
    public void ToString_ReturnsCorrectArnFormat()
    {
        // Arrange
        var s3Ref = new S3Ref("test-bucket", "path/to/file.txt", "us-west-2", "987654321098");

        // Act
        var result = s3Ref.ToString();

        // Assert
        result.ShouldBe("arn:aws:s3:us-west-2:987654321098:test-bucket/path/to/file.txt");
    }

    [Fact]
    public void ToUri_ReturnsCorrectS3Uri()
    {
        // Arrange
        var s3Ref = new S3Ref("my-bucket", "documents/report.pdf", "eu-west-1", null);

        // Act
        string result = s3Ref.ToUri();

        // Assert
        result.ShouldBe("https://my-bucket.eu-west-1.amazonaws.com/documents/report.pdf");
    }

    [Fact]
    public void ToS3Path_ReturnsCorrectPath()
    {
        // Arrange
        var s3Ref = new S3Ref("data-bucket", "analytics/2023/report.csv", "us-east-2",
            "123456789012");

        // Act
        string result = s3Ref.ToS3Path();

        // Assert
        result.ShouldBe("data-bucket/analytics/2023/report.csv");
    }

    [Fact]
    public void ToArn_ReturnsCorrectArnFormat()
    {
        // Arrange
        var s3Ref = new S3Ref("backup-bucket", "backups/database.sql", "ap-southeast-1",
            "111222333444");

        // Act
        string result = s3Ref.ToArn();

        // Assert
        result.ShouldBe(
            "arn:aws:s3:ap-southeast-1:111222333444:backup-bucket/backups/database.sql");
    }

    [Fact]
    public void ToS3EndpointUri_ReturnsCorrectEndpointFormat()
    {
        // Arrange
        var s3Ref = new S3Ref("logs-bucket", "application/2023/12/app.log", "us-west-1", null);

        // Act
        string result = s3Ref.ToS3EndpointUri();

        // Assert
        result.ShouldBe("https://s3.us-west-1.amazonaws.com/application/2023/12/app.log");
    }

    [Fact]
    public void ParseArn_ValidArn_ReturnsCorrectS3Ref()
    {
        // Arrange
        var arn = "arn:aws:s3:eu-central-1:555666777888:media-bucket/videos/training.mp4";

        // Act
        S3Ref result = S3Ref.ParseArn(arn);

        // Assert
        result.Bucket.ShouldBe("media-bucket");
        result.Key.ShouldBe("videos/training.mp4");
        result.Region.ShouldBe("eu-central-1");
        result.AccountId.ShouldBe("555666777888");
        result.FileName.ShouldBe("training.mp4");
    }

    [Fact]
    public void ParseArn_ArnWithEmptyRegion_ParsesCorrectly()
    {
        // Arrange
        var arn = "arn:aws:s3:::global-bucket/data/file.txt";

        // Act
        S3Ref result = S3Ref.ParseArn(arn);

        // Assert
        result.Bucket.ShouldBe("global-bucket");
        result.Key.ShouldBe("data/file.txt");
        result.Region.ShouldBeNull();
        result.AccountId.ShouldBeNull();
    }

    [Fact]
    public void ParseArn_NullOrEmptyArn_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => S3Ref.ParseArn(null!));
        Should.Throw<ArgumentException>(() => S3Ref.ParseArn(""));
        Should.Throw<ArgumentException>(() => S3Ref.ParseArn("   "));
    }

    [Fact]
    public void ParseArn_InvalidArnFormat_ThrowsArgumentException()
    {
        // Arrange
        var invalidArn = "invalid-arn-format";

        // Act & Assert
        var exception
            = Should.Throw<ArgumentException>(() => S3Ref.ParseArn(invalidArn));
        exception.Message.ShouldBe("Invalid S3 ARN format (Parameter 'arn')");
    }

    [Fact]
    public void ParseArn_NonS3Arn_ThrowsArgumentException()
    {
        // Arrange
        var nonS3Arn = "arn:aws:ec2:us-east-2:123456789012:instance/i-1234567890abcdef0";

        // Act & Assert
        var exception
            = Should.Throw<ArgumentException>(() => S3Ref.ParseArn(nonS3Arn));
        exception.Message.ShouldBe("Invalid S3 ARN format (Parameter 'arn')");
    }

    [Fact]
    public void ParseObjectUri_ValidS3Uri_ReturnsCorrectS3Ref()
    {
        // Arrange
        var uri = "https://my-bucket.s3.us-east-2.amazonaws.com/folder/document.pdf";

        // Act
        S3Ref result = S3Ref.ParseObjectUri(uri);

        // Assert
        result.Bucket.ShouldBe("my-bucket");
        result.Key.ShouldBe("folder/document.pdf");
        result.Region.ShouldBe("us-east-2");
        result.AccountId.ShouldBeNull();
        result.FileName.ShouldBe("document.pdf");
    }

    [Fact]
    public void ParseObjectUri_InvalidUri_ThrowsArgumentException()
    {
        // Arrange
        var invalidUri = "https://example.com/not-s3";

        // Act & Assert
        var exception
            = Should.Throw<ArgumentException>(() => S3Ref.ParseObjectUri(invalidUri));
        exception.Message.ShouldBe("Invalid S3 URI format (Parameter 'objectUri')");
    }

    [Fact]
    public void ParseEndpointUri_ValidEndpointUri_ReturnsCorrectS3Ref()
    {
        // Arrange
        var uri = "https://s3.us-west-2.amazonaws.com/test-bucket/data/file.json";

        // Act
        S3Ref result = S3Ref.ParseEndpointUri(uri);

        // Assert
        result.Bucket.ShouldBe("test-bucket");
        result.Key.ShouldBe("data/file.json");
        result.Region.ShouldBe("us-west-2");
        result.AccountId.ShouldBeNull();
        result.FileName.ShouldBe("file.json");
    }

    [Fact]
    public void ParseEndpointUri_InvalidEndpointUri_ThrowsArgumentException()
    {
        // Arrange
        var invalidUri = "https://invalid-endpoint.com/bucket/key";

        // Act & Assert
        var exception
            = Should.Throw<ArgumentException>(() => S3Ref.ParseEndpointUri(invalidUri));
        exception.Message.ShouldBe("Invalid S3 URI format (Parameter 'fileUri')");
    }

    [Fact]
    public void TryParseObjectUrl_ValidUrl_ReturnsTrue()
    {
        // Arrange
        var validUrl = "https://bucket-name.s3.eu-west-1.amazonaws.com/path/file.txt";

        // Act
        bool success = S3Ref.TryParseObjectUrl(validUrl, out S3Ref? result);

        // Assert
        success.ShouldBeTrue();
        result.ShouldNotBeNull();
        result.Bucket.ShouldBe("bucket-name");
        result.Key.ShouldBe("path/file.txt");
    }

    [Fact]
    public void TryParseObjectUrl_InvalidUrl_ReturnsFalse()
    {
        // Arrange
        var invalidUrl = "https://invalid-url.com";

        // Act
        bool success = S3Ref.TryParseObjectUrl(invalidUrl, out S3Ref? result);

        // Assert
        success.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public void TryParseArn_ValidArn_ReturnsTrue()
    {
        // Arrange
        var validArn = "arn:aws:s3:us-east-2:123456789012:my-bucket/my-key";

        // Act
        bool success = S3Ref.TryParseArn(validArn, out S3Ref? result);

        // Assert
        success.ShouldBeTrue();
        result.ShouldNotBeNull();
        result.Bucket.ShouldBe("my-bucket");
        result.Key.ShouldBe("my-key");
    }

    [Fact]
    public void TryParseArn_InvalidArn_ReturnsFalse()
    {
        // Arrange
        var invalidArn = "invalid-arn";

        // Act
        bool success = S3Ref.TryParseArn(invalidArn, out S3Ref? result);

        // Assert
        success.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public void TryParseEndpointUri_ValidUri_ReturnsTrue()
    {
        // Arrange
        var validUri = "https://s3.ap-southeast-2.amazonaws.com/bucket/key";

        // Act
        bool success = S3Ref.TryParseEndpointUri(validUri, out S3Ref? result);

        // Assert
        success.ShouldBeTrue();
        result.ShouldNotBeNull();
        result.Bucket.ShouldBe("bucket");
        result.Key.ShouldBe("key");
    }

    [Fact]
    public void TryParseEndpointUri_InvalidUri_ReturnsFalse()
    {
        // Arrange
        var invalidUri = "https://invalid.com/path";

        // Act
        bool success = S3Ref.TryParseEndpointUri(invalidUri, out S3Ref? result);

        // Assert
        success.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public void IsValidS3Arn_ValidS3Arn_ReturnsTrue()
    {
        // Arrange
        var validArn = "arn:aws:s3:us-east-2:123456789012:bucket/key";

        // Act
        bool result = validArn.IsValidS3Arn();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsValidS3Arn_ValidS3BucketArn_ReturnsTrue()
    {
        // Arrange
        var validBucketArn = "arn:aws:s3:us-east-2:123456789012:bucket";

        // Act
        bool result = validBucketArn.IsValidS3Arn();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsValidS3Arn_InvalidArn_ReturnsFalse()
    {
        // Arrange
        var invalidArn = "not-an-arn";

        // Act
        bool result = invalidArn.IsValidS3Arn();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsValidS3Arn_NullOrEmpty_ReturnsFalse()
    {
        // Act & Assert
        ((string)null!).IsValidS3Arn().ShouldBeFalse();
        "".IsValidS3Arn().ShouldBeFalse();
        "   ".IsValidS3Arn().ShouldBeFalse();
    }

    [Fact]
    public void FileName_ExtractedCorrectlyFromKey()
    {
        // Test various key patterns
        var testCases = new[]
        {
            ("file.txt", "file.txt"),
            ("folder/file.txt", "file.txt"),
            ("deep/nested/path/document.pdf", "document.pdf"),
            ("path/with/no/extension", "extension"),
            ("", null)
        };

        foreach ((string? key, string? expectedFileName) in testCases)
        {
            // Arrange
            var s3Ref = new S3Ref("bucket", key, "region", "account");

            // Act & Assert
            s3Ref.FileName.ShouldBe(expectedFileName);
        }
    }

    [Fact]
    public void Constructor_WithNullOrEmptyValues_HandlesGracefully()
    {
        // Test with null values
        var s3RefWithNulls = new S3Ref("bucket", "key", null, null);
        s3RefWithNulls.Bucket.ShouldBe("bucket");
        s3RefWithNulls.Key.ShouldBe("key");
        s3RefWithNulls.Region.ShouldBeNull();
        s3RefWithNulls.AccountId.ShouldBeNull();

        // Test with empty key
        var s3RefWithEmptyKey = new S3Ref("bucket", "", "region", "account");
        s3RefWithEmptyKey.Key.ShouldBe("");
        s3RefWithEmptyKey.FileName.ShouldBeNull();
    }
}