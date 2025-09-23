using ThirdOpinion.Common.Aws.S3;
namespace ThirdOpinion.Common.UnitTests.Models;

public class S3RefTests
{
    [Theory]
    [InlineData("arn:aws:s3:us-east-1:123456789012:my-bucket/path/to/file.txt",
        "arn:aws:s3:us-east-1:123456789012:my-bucket/path/to/file.txt")]
    [InlineData("arn:aws:s3:eu-west-2:987654321098:test-bucket/folder/document.pdf",
        "arn:aws:s3:eu-west-2:987654321098:test-bucket/folder/document.pdf")]
    [InlineData("arn:aws:s3:ap-south-1:111222333444:data-bucket/logs/2023/01/log.json",
        "arn:aws:s3:ap-south-1:111222333444:data-bucket/logs/2023/01/log.json")]
    public void ToString_WithDataRows_ReturnsCorrectArnFormat(string input, string expected)
    {
        // Arrange
        var s3Ref = new S3Ref(input);

        // Act
        var result = s3Ref.ToString();

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("arn:aws:s3:us-east-1:123456789012:my-bucket/path/to/file.txt",
        "https://my-bucket.us-east-1.amazonaws.com/path/to/file.txt")]
    [InlineData("arn:aws:s3:eu-west-2:987654321098:test-bucket/folder/document.pdf",
        "https://test-bucket.eu-west-2.amazonaws.com/folder/document.pdf")]
    [InlineData("arn:aws:s3:ap-south-1:111222333444:data-bucket/logs/2023/01/log.json",
        "https://data-bucket.ap-south-1.amazonaws.com/logs/2023/01/log.json")]
    public void ToUri_WithDataRows_ReturnsCorrectS3Uri(string input, string expected)
    {
        // Arrange
        var s3Ref = new S3Ref(input);

        // Act
        var result = s3Ref.ToUri();

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("arn:aws:s3:us-east-1:123456789012:my-bucket/path/to/file.txt",
        "my-bucket/path/to/file.txt")]
    [InlineData("arn:aws:s3:eu-west-2:987654321098:test-bucket/folder/document.pdf",
        "test-bucket/folder/document.pdf")]
    [InlineData("arn:aws:s3:ap-south-1:111222333444:data-bucket/logs/2023/01/log.json",
        "data-bucket/logs/2023/01/log.json")]
    public void ToS3Path_WithDataRows_ReturnsCorrectPath(string input, string expected)
    {
        // Arrange
        var s3Ref = new S3Ref(input);

        // Act
        var result = s3Ref.ToS3Path();

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("arn:aws:s3:us-east-1:123456789012:my-bucket/path/to/file.txt",
        "arn:aws:s3:us-east-1:123456789012:my-bucket/path/to/file.txt")]
    [InlineData("arn:aws:s3:eu-west-2:987654321098:test-bucket/folder/document.pdf",
        "arn:aws:s3:eu-west-2:987654321098:test-bucket/folder/document.pdf")]
    [InlineData("arn:aws:s3:ap-south-1:111222333444:data-bucket/logs/2023/01/log.json",
        "arn:aws:s3:ap-south-1:111222333444:data-bucket/logs/2023/01/log.json")]
    public void ToArn_WithDataRows_ReturnsCorrectArnFormat(string input, string expected)
    {
        // Arrange
        var s3Ref = new S3Ref(input);

        // Act
        var result = s3Ref.ToArn();

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("arn:aws:s3:us-east-1:123456789012:my-bucket/path/to/file.txt",
        "https://s3.us-east-1.amazonaws.com/path/to/file.txt")]
    [InlineData("arn:aws:s3:eu-west-2:987654321098:test-bucket/folder/document.pdf",
        "https://s3.eu-west-2.amazonaws.com/folder/document.pdf")]
    [InlineData("arn:aws:s3:ap-south-1:111222333444:data-bucket/logs/2023/01/log.json",
        "https://s3.ap-south-1.amazonaws.com/logs/2023/01/log.json")]
    public void ToS3EndpointUri_WithDataRows_ReturnsCorrectEndpointUri(string input,
        string expected)
    {
        // Arrange
        var s3Ref = new S3Ref(input);

        // Act
        var result = s3Ref.ToS3EndpointUri();

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void ToString_ReturnsCorrectArnFormat()
    {
        // Arrange
        var s3Ref = new S3Ref("arn:aws:s3:us-east-1:123456789012:my-bucket/path/to/file.txt");

        // Act
        var result = s3Ref.ToString();

        // Assert
        result.ShouldBe("arn:aws:s3:us-east-1:123456789012:my-bucket/path/to/file.txt");
    }

    [Fact]
    public void ToUri_ReturnsCorrectS3Uri()
    {
        // Arrange
        var s3Ref = new S3Ref("arn:aws:s3:us-east-1:123456789012:my-bucket/path/to/file.txt");

        // Act
        var result = s3Ref.ToUri();

        // Assert
        result.ShouldBe("https://my-bucket.us-east-1.amazonaws.com/path/to/file.txt");
    }

    [Fact]
    public void ToS3Path_ReturnsCorrectPath()
    {
        // Arrange
        var s3Ref = new S3Ref("arn:aws:s3:us-east-1:123456789012:my-bucket/path/to/file.txt");

        // Act
        var result = s3Ref.ToS3Path();

        // Assert
        result.ShouldBe("my-bucket/path/to/file.txt");
    }

    [Fact]
    public void ToArn_ReturnsCorrectArnFormat()
    {
        // Arrange
        var s3Ref = new S3Ref("arn:aws:s3:us-east-1:123456789012:my-bucket/path/to/file.txt");

        // Act
        var result = s3Ref.ToArn();

        // Assert
        result.ShouldBe("arn:aws:s3:us-east-1:123456789012:my-bucket/path/to/file.txt");
    }

    [Fact]
    public void ToS3EndpointUri_ReturnsCorrectEndpointUri()
    {
        // Arrange
        var s3Ref = new S3Ref("arn:aws:s3:us-east-1:123456789012:my-bucket/path/to/file.txt");

        // Act
        var result = s3Ref.ToS3EndpointUri();

        // Assert
        result.ShouldBe("https://s3.us-east-1.amazonaws.com/path/to/file.txt");
    }


    [Theory]
    [InlineData("https://s3.us-east-1.amazonaws.com/my-bucket/path/to/file.txt", true, "my-bucket",
        "path/to/file.txt",
        "us-east-1")]
    [InlineData("https://s3.eu-west-2.amazonaws.com/test-bucket/folder/file.json", true, "test-bucket",
        "folder/file.json",
        "eu-west-2")]
    [InlineData("https://s3.us-west-1.amazonaws.com/data-bucket/document.pdf", true, "data-bucket",
        "document.pdf",
        "us-west-1")]
    [InlineData("invalid-uri", false, null, null, null)]
    [InlineData("", false, null, null, null)]
    [InlineData(null, false, null, null, null)]
    [InlineData("https://example.com/not-s3", false, null, null, null)]
    public void TryParseFileUri_ReturnsExpectedResult(string fileUri,
        bool expectedResult,
        string? expectedBucket,
        string? expectedKey,
        string? expectedRegion)
    {
        // Act
        var success = S3Ref.TryParseEndpointUri(fileUri, out var result);

        // Assert
        success.ShouldBe(expectedResult);

        if (expectedResult)
        {
            result.ShouldNotBeNull();
            result!.Bucket.ShouldBe(expectedBucket);
            result.Key.ShouldBe(expectedKey);
            result.Region.ShouldBe(expectedRegion);
        }
        else
        {
            result.ShouldBeNull();
        }
    }


    [Theory]
    [InlineData("arn:aws:s3:::my-bucket/path/to/file.txt", "my-bucket", "path/to/file.txt", null, null,
        "file.txt")]
    [InlineData("arn:aws:s3:us-east-1::test-bucket/folder/file.json", "test-bucket",
        "folder/file.json", "us-east-1", null,
        "file.json")]
    [InlineData("arn:aws:s3:us-east-1:358692710224:test-bucket/file.json", "test-bucket", "file.json",
        "us-east-1",
        "358692710224", "file.json")]
    [InlineData("arn:aws:s3::358692710224:test-bucket/folder/file.json", "test-bucket",
        "folder/file.json", null,
        "358692710224", "file.json")]
    public void Parse_ValidArn_ReturnsCorrectS3Arn(string arn,
        string expectedBucket,
        string expectedKey,
        string? expectedRegion,
        string? expectedAccountId,
        string expectedFilename)
    {
        // Act
        var result = S3Ref.ParseArn(arn);

        // Assert
        result.Bucket.ShouldBe(expectedBucket);
        result.Key.ShouldBe(expectedKey);
        result.Region.ShouldBe(expectedRegion);
        result.FileName.ShouldBe(expectedFilename);
        result.ToString().ShouldBe(arn);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Parse_NullOrEmptyArn_ThrowsArgumentException(string arn)
    {
        // Act & Assert
        Action act = () => S3Ref.ParseArn(arn!);
        act.ShouldThrow<ArgumentException>()
            .Message.ShouldContain("ARN cannot be null or empty");
    }

    [Theory]
    [InlineData("invalid-arn")]
    [InlineData("arn:aws:invalid:::my-bucket/file.txt")]
    [InlineData("arn:aws:s3:::my-bucket")] // Missing key
    [InlineData("arn:aws:foo:::my-bucket/path/to/file.txt")] //Not S3
    public void Parse_InvalidArnFormat_ThrowsArgumentException(string arn)
    {
        // Act & Assert
        Action act = () => S3Ref.ParseArn(arn);
        act.ShouldThrow<ArgumentException>()
            .Message.ShouldContain("Invalid S3 ARN format");
    }

    [Theory]
    [InlineData("https://my-bucket.s3.us-east-1.amazonaws.com/path/to/file.txt", "my-bucket",
        "path/to/file.txt",
        "us-east-1", "file.txt")]
    [InlineData("https://test-bucket.s3.eu-west-2.amazonaws.com/folder/file.json", "test-bucket",
        "folder/file.json",
        "eu-west-2", "file.json")]
    [InlineData("https://my-bucket.s3.us-east-1.amazonaws.com/path", "my-bucket", "path", "us-east-1",
        "path")]
    // [InlineData("https://s3.us-east-1.amazonaws.com/pf-ehr-int-ue1-ambient-scribe-data/bcaa1394-1896-4492-80c0-0eb6138cd1ca_test_3/transcript.json", "my-bucket", "file.txt", "us-east-1", "file.txt")]
    public void TryParseUri_ValidUri_ReturnsTrueAndCorrectS3Arn(string uri,
        string expectedBucket,
        string expectedKey,
        string expectedRegion,
        string expectedFilename)
    {
        // Act
        var result = S3Ref.TryParseObjectUrl(uri, out var s3Arn);

        // Assert
        result.ShouldBeTrue();
        s3Arn.ShouldNotBeNull();
        s3Arn!.Bucket.ShouldBe(expectedBucket);
        s3Arn.Key.ShouldBe(expectedKey);
        s3Arn.Region.ShouldBe(expectedRegion);
        s3Arn.FileName.ShouldBe(expectedFilename);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid-uri")]
    [InlineData("https://invalid-uri")]
    [InlineData("https://my-bucket.s3.us-east-1.amazonaws.com/")]
    public void TryParseUri_InvalidUri_ReturnsFalseAndNull(string uri)
    {
        // Act
        var result = S3Ref.TryParseObjectUrl(uri, out var s3Arn);

        // Assert
        result.ShouldBeFalse();
        s3Arn.ShouldBeNull();
    }

    [Theory]
    [InlineData("arn:aws:s3:::my-bucket/path/to/file.txt", true, "file.txt")]
    [InlineData("arn:aws:s3:::test-bucket/folder/file.json", true, "file.json")]
    [InlineData(null, false, null)]
    [InlineData("", false, null)]
    [InlineData("invalid-arn", false, null)]
    public void TryParseArn_ValidAndInvalidArns_ReturnsExpectedResult(string arn,
        bool expectedResult,
        string? expectedFilename)
    {
        // Act
        var result = S3Ref.TryParseArn(arn, out var s3Arn);

        // Assert
        result.ShouldBe(expectedResult);
        if (expectedResult)
        {
            s3Arn.ShouldNotBeNull();
            s3Arn!.ToString().ShouldBe(arn);
            s3Arn.FileName.ShouldBe(expectedFilename);
        }
        else
        {
            s3Arn.ShouldBeNull();
        }
    }
}