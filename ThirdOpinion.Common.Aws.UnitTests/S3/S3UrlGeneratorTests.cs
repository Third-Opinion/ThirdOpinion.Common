using Amazon.S3;
using Amazon.S3.Model;
using Moq;
using ThirdOpinion.Common.Aws.S3;

namespace ThirdOpinion.Common.Aws.Tests.S3;

public class S3UrlGeneratorTests
{
    private readonly Mock<IAmazonS3> _s3ClientMock;
    private readonly S3UrlGenerator _urlGenerator;

    public S3UrlGeneratorTests()
    {
        _s3ClientMock = new Mock<IAmazonS3>();
        _urlGenerator = new S3UrlGenerator(_s3ClientMock.Object);
    }

    [Fact]
    public async Task GeneratePreSignedUrl_ValidArn_ReturnsPresignedUrl()
    {
        // Arrange
        var s3Arn = "arn:aws:s3:us-east-1:123456789012:test-bucket/test-file.txt";
        var expiration = TimeSpan.FromHours(1);
        var expectedUrl = "https://test-bucket.s3.amazonaws.com/test-file.txt?AWSAccessKeyId=AKIAIOSFODNN7EXAMPLE&Expires=1234567890&Signature=abcdef123456";

        _s3ClientMock.Setup(x => x.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
                    .ReturnsAsync(expectedUrl);

        // Act
        var result = await _urlGenerator.GeneratePreSignedUrl(s3Arn, expiration);

        // Assert
        result.ShouldBe(expectedUrl);
        _s3ClientMock.Verify(x => x.GetPreSignedURLAsync(
            It.Is<GetPreSignedUrlRequest>(r => 
                r.BucketName == "test-bucket" &&
                r.Key == "test-file.txt")), Times.Once);
    }

    [Fact]
    public async Task GeneratePreSignedUrl_ValidArnWithPath_ReturnsPresignedUrl()
    {
        // Arrange
        var s3Arn = "arn:aws:s3:us-west-2:123456789012:my-bucket/folder/subfolder/document.pdf";
        var expiration = TimeSpan.FromMinutes(30);
        var expectedUrl = "https://my-bucket.s3.amazonaws.com/folder/subfolder/document.pdf?AWSAccessKeyId=EXAMPLE&Expires=1234567890&Signature=signature";

        _s3ClientMock.Setup(x => x.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
                    .ReturnsAsync(expectedUrl);

        // Act
        var result = await _urlGenerator.GeneratePreSignedUrl(s3Arn, expiration);

        // Assert
        result.ShouldBe(expectedUrl);
        _s3ClientMock.Verify(x => x.GetPreSignedURLAsync(
            It.Is<GetPreSignedUrlRequest>(r => 
                r.BucketName == "my-bucket" &&
                r.Key == "folder/subfolder/document.pdf")), Times.Once);
    }

    [Fact]
    public async Task GeneratePreSignedUrl_SetsCorrectExpiration_UsesProvidedTimeSpan()
    {
        // Arrange
        var s3Arn = "arn:aws:s3:eu-west-1:123456789012:test-bucket/test-file.txt";
        var expiration = TimeSpan.FromHours(24);
        var expectedUrl = "https://test-bucket.s3.amazonaws.com/test-file.txt?expires=tomorrow";
        var utcNow = DateTime.UtcNow;

        _s3ClientMock.Setup(x => x.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
                    .ReturnsAsync(expectedUrl);

        // Act
        var result = await _urlGenerator.GeneratePreSignedUrl(s3Arn, expiration);

        // Assert
        result.ShouldBe(expectedUrl);
        _s3ClientMock.Verify(x => x.GetPreSignedURLAsync(
            It.Is<GetPreSignedUrlRequest>(r => 
                r.Expires > utcNow.Add(TimeSpan.FromHours(23)) &&
                r.Expires < utcNow.Add(TimeSpan.FromHours(25)))), Times.Once);
    }

    [Fact]
    public async Task GeneratePreSignedUrl_InvalidArn_ThrowsArgumentException()
    {
        // Arrange
        var invalidArn = "not-a-valid-arn";
        var expiration = TimeSpan.FromHours(1);

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(() => 
            _urlGenerator.GeneratePreSignedUrl(invalidArn, expiration));
    }

    [Fact]
    public async Task GeneratePreSignedUrl_EmptyArn_ThrowsArgumentException()
    {
        // Arrange
        var emptyArn = "";
        var expiration = TimeSpan.FromHours(1);

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(() => 
            _urlGenerator.GeneratePreSignedUrl(emptyArn, expiration));
    }

    [Fact]
    public async Task GeneratePreSignedUrl_NullArn_ThrowsArgumentException()
    {
        // Arrange
        string nullArn = null!;
        var expiration = TimeSpan.FromHours(1);

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(() => 
            _urlGenerator.GeneratePreSignedUrl(nullArn, expiration));
    }

    [Fact]
    public async Task GeneratePreSignedUrl_ArnWithoutObjectKey_ThrowsArgumentException()
    {
        // Arrange
        var arnWithoutKey = "arn:aws:s3:us-east-1:123456789012:test-bucket";
        var expiration = TimeSpan.FromHours(1);

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(() => 
            _urlGenerator.GeneratePreSignedUrl(arnWithoutKey, expiration));
    }

    [Fact]
    public async Task GeneratePreSignedUrl_ArnWithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var s3Arn = "arn:aws:s3:ap-southeast-1:987654321098:test-bucket/folder/file%20with%20spaces.txt";
        var expiration = TimeSpan.FromHours(2);
        var expectedUrl = "https://test-bucket.s3.amazonaws.com/folder/file%20with%20spaces.txt?encoded=url";

        _s3ClientMock.Setup(x => x.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
                    .ReturnsAsync(expectedUrl);

        // Act
        var result = await _urlGenerator.GeneratePreSignedUrl(s3Arn, expiration);

        // Assert
        result.ShouldBe(expectedUrl);
        _s3ClientMock.Verify(x => x.GetPreSignedURLAsync(
            It.Is<GetPreSignedUrlRequest>(r => 
                r.BucketName == "test-bucket" &&
                r.Key == "folder/file%20with%20spaces.txt")), Times.Once);
    }

    [Fact]
    public async Task GeneratePreSignedUrl_S3ClientThrowsException_RethrowsException()
    {
        // Arrange
        var s3Arn = "arn:aws:s3:us-east-1:123456789012:test-bucket/test-file.txt";
        var expiration = TimeSpan.FromHours(1);
        var expectedException = new AmazonS3Exception("S3 service error");

        _s3ClientMock.Setup(x => x.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
                    .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Should.ThrowAsync<AmazonS3Exception>(() => 
            _urlGenerator.GeneratePreSignedUrl(s3Arn, expiration));
        
        exception.ShouldBe(expectedException);
    }

    [Fact]
    public async Task GeneratePreSignedUrl_ZeroExpiration_HandlesCorrectly()
    {
        // Arrange
        var s3Arn = "arn:aws:s3:us-east-1:123456789012:test-bucket/test-file.txt";
        var expiration = TimeSpan.Zero;
        var expectedUrl = "https://test-bucket.s3.amazonaws.com/test-file.txt?expires=now";

        _s3ClientMock.Setup(x => x.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
                    .ReturnsAsync(expectedUrl);

        // Act
        var result = await _urlGenerator.GeneratePreSignedUrl(s3Arn, expiration);

        // Assert
        result.ShouldBe(expectedUrl);
    }

    [Fact]
    public async Task GeneratePreSignedUrl_NegativeExpiration_HandlesCorrectly()
    {
        // Arrange
        var s3Arn = "arn:aws:s3:us-east-1:123456789012:test-bucket/test-file.txt";
        var expiration = TimeSpan.FromHours(-1);
        var expectedUrl = "https://test-bucket.s3.amazonaws.com/test-file.txt?expires=past";
        var utcNow = DateTime.UtcNow;

        _s3ClientMock.Setup(x => x.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
                    .ReturnsAsync(expectedUrl);

        // Act
        var result = await _urlGenerator.GeneratePreSignedUrl(s3Arn, expiration);

        // Assert
        result.ShouldBe(expectedUrl);
        _s3ClientMock.Verify(x => x.GetPreSignedURLAsync(
            It.Is<GetPreSignedUrlRequest>(r => r.Expires < utcNow)), Times.Once);
    }

    [Fact]
    public async Task GeneratePreSignedUrl_CrossRegionArn_ParsesBucketAndKeyCorrectly()
    {
        // Arrange
        var s3Arn = "arn:aws:s3:eu-central-1:555666777888:cross-region-bucket/data/analytics/report.json";
        var expiration = TimeSpan.FromMinutes(15);
        var expectedUrl = "https://cross-region-bucket.s3.amazonaws.com/data/analytics/report.json";

        _s3ClientMock.Setup(x => x.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
                    .ReturnsAsync(expectedUrl);

        // Act
        var result = await _urlGenerator.GeneratePreSignedUrl(s3Arn, expiration);

        // Assert
        result.ShouldBe(expectedUrl);
        _s3ClientMock.Verify(x => x.GetPreSignedURLAsync(
            It.Is<GetPreSignedUrlRequest>(r => 
                r.BucketName == "cross-region-bucket" &&
                r.Key == "data/analytics/report.json")), Times.Once);
    }

    [Fact]
    public async Task GeneratePreSignedUrl_DeepNestedPath_ParsesCorrectly()
    {
        // Arrange
        var s3Arn = "arn:aws:s3:us-west-1:111222333444:deep-bucket/level1/level2/level3/level4/level5/deep-file.txt";
        var expiration = TimeSpan.FromHours(1);
        var expectedUrl = "https://deep-bucket.s3.amazonaws.com/level1/level2/level3/level4/level5/deep-file.txt";

        _s3ClientMock.Setup(x => x.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
                    .ReturnsAsync(expectedUrl);

        // Act
        var result = await _urlGenerator.GeneratePreSignedUrl(s3Arn, expiration);

        // Assert
        result.ShouldBe(expectedUrl);
        _s3ClientMock.Verify(x => x.GetPreSignedURLAsync(
            It.Is<GetPreSignedUrlRequest>(r => 
                r.BucketName == "deep-bucket" &&
                r.Key == "level1/level2/level3/level4/level5/deep-file.txt")), Times.Once);
    }
}