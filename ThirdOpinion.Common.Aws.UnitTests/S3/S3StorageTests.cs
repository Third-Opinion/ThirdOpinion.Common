using System.Net;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Moq;
using ThirdOpinion.Common.Aws.S3;

namespace ThirdOpinion.Common.Aws.Tests.S3;

public class S3StorageTests
{
    private readonly Mock<ILogger<S3Storage>> _loggerMock;
    private readonly Mock<IAmazonS3> _s3ClientMock;
    private readonly S3Storage _s3Storage;

    public S3StorageTests()
    {
        _s3ClientMock = new Mock<IAmazonS3>();
        _loggerMock = new Mock<ILogger<S3Storage>>();
        _s3Storage = new S3Storage(_s3ClientMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task PutObjectAsync_WithStream_SuccessfulUpload()
    {
        // Arrange
        var bucketName = "test-bucket";
        var key = "test/file.txt";
        var content = "test content";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var contentType = "text/plain";
        var metadata = new Dictionary<string, string> { { "author", "test-user" } };

        var expectedResponse = new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK };
        _s3ClientMock.Setup(x =>
                x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        PutObjectResponse result
            = await _s3Storage.PutObjectAsync(bucketName, key, stream, contentType, metadata);

        // Assert
        result.ShouldBe(expectedResponse);
        _s3ClientMock.Verify(x => x.PutObjectAsync(
            It.Is<PutObjectRequest>(r =>
                r.BucketName == bucketName &&
                r.Key == key &&
                r.ContentType == contentType &&
                r.Metadata.Count > 0),
            It.IsAny<CancellationToken>()), Times.Once);

        VerifyLoggerDebugWasCalled($"Uploaded object to S3: {bucketName}/{key}");
    }

    [Fact]
    public async Task PutObjectAsync_WithString_SuccessfulUpload()
    {
        // Arrange
        var bucketName = "test-bucket";
        var key = "test/file.txt";
        var content = "test content";
        var contentType = "text/plain";

        var expectedResponse = new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK };
        _s3ClientMock.Setup(x =>
                x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        PutObjectResponse result
            = await _s3Storage.PutObjectAsync(bucketName, key, content, contentType);

        // Assert
        result.ShouldBe(expectedResponse);
        _s3ClientMock.Verify(x => x.PutObjectAsync(
            It.Is<PutObjectRequest>(r =>
                r.BucketName == bucketName &&
                r.Key == key &&
                r.ContentType == contentType),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PutObjectAsync_WithDefaultContentType_UsesApplicationOctetStream()
    {
        // Arrange
        var bucketName = "test-bucket";
        var key = "test/file.bin";
        var content = "binary content";

        var expectedResponse = new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK };
        _s3ClientMock.Setup(x =>
                x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        await _s3Storage.PutObjectAsync(bucketName, key, content);

        // Assert
        _s3ClientMock.Verify(x => x.PutObjectAsync(
            It.Is<PutObjectRequest>(r => r.ContentType == "application/octet-stream"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PutObjectAsync_Exception_LogsErrorAndRethrows()
    {
        // Arrange
        var bucketName = "test-bucket";
        var key = "test/file.txt";
        var content = "test content";
        var expectedException = new AmazonS3Exception("S3 error");

        _s3ClientMock.Setup(x =>
                x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Should.ThrowAsync<AmazonS3Exception>(() =>
            _s3Storage.PutObjectAsync(bucketName, key, content));

        exception.ShouldBe(expectedException);
        VerifyLoggerErrorWasCalled($"Error uploading object to S3: {bucketName}/{key}");
    }

    [Fact]
    public async Task GetObjectAsync_SuccessfulDownload_ReturnsStream()
    {
        // Arrange
        var bucketName = "test-bucket";
        var key = "test/file.txt";
        var expectedContent = "test content";
        var responseStream = new MemoryStream(Encoding.UTF8.GetBytes(expectedContent));

        var mockResponse = new GetObjectResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            ResponseStream = responseStream
        };

        _s3ClientMock.Setup(x =>
                x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        Stream result = await _s3Storage.GetObjectAsync(bucketName, key);

        // Assert
        result.ShouldBe(responseStream);
        _s3ClientMock.Verify(x => x.GetObjectAsync(
            It.Is<GetObjectRequest>(r => r.BucketName == bucketName && r.Key == key),
            It.IsAny<CancellationToken>()), Times.Once);

        VerifyLoggerDebugWasCalled($"Downloaded object from S3: {bucketName}/{key}");
    }

    [Fact]
    public async Task GetObjectAsStringAsync_SuccessfulDownload_ReturnsString()
    {
        // Arrange
        var bucketName = "test-bucket";
        var key = "test/file.txt";
        var expectedContent = "test content";
        var responseStream = new MemoryStream(Encoding.UTF8.GetBytes(expectedContent));

        var mockResponse = new GetObjectResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            ResponseStream = responseStream
        };

        _s3ClientMock.Setup(x =>
                x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        string result = await _s3Storage.GetObjectAsStringAsync(bucketName, key);

        // Assert
        result.ShouldBe(expectedContent);
    }

    [Fact]
    public async Task GetObjectMetadataAsync_SuccessfulRequest_ReturnsMetadata()
    {
        // Arrange
        var bucketName = "test-bucket";
        var key = "test/file.txt";

        var expectedResponse = new GetObjectMetadataResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            ContentLength = 1024
        };

        _s3ClientMock.Setup(x =>
                x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        GetObjectMetadataResponse result = await _s3Storage.GetObjectMetadataAsync(bucketName, key);

        // Assert
        result.ShouldBe(expectedResponse);
        _s3ClientMock.Verify(x => x.GetObjectMetadataAsync(
            It.Is<GetObjectMetadataRequest>(r => r.BucketName == bucketName && r.Key == key),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteObjectAsync_SuccessfulDeletion_ReturnsResponse()
    {
        // Arrange
        var bucketName = "test-bucket";
        var key = "test/file.txt";

        var expectedResponse = new DeleteObjectResponse
            { HttpStatusCode = HttpStatusCode.NoContent };
        _s3ClientMock.Setup(x =>
                x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        DeleteObjectResponse result = await _s3Storage.DeleteObjectAsync(bucketName, key);

        // Assert
        result.ShouldBe(expectedResponse);
        _s3ClientMock.Verify(x => x.DeleteObjectAsync(
            It.Is<DeleteObjectRequest>(r => r.BucketName == bucketName && r.Key == key),
            It.IsAny<CancellationToken>()), Times.Once);

        VerifyLoggerDebugWasCalled($"Deleted object from S3: {bucketName}/{key}");
    }

    [Fact]
    public async Task DeleteObjectsAsync_MultipleObjects_SuccessfulDeletion()
    {
        // Arrange
        var bucketName = "test-bucket";
        var keys = new List<string> { "file1.txt", "file2.txt", "file3.txt" };

        var expectedResponse = new DeleteObjectsResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            DeletedObjects = keys.Select(k => new DeletedObject { Key = k }).ToList()
        };

        _s3ClientMock.Setup(x =>
                x.DeleteObjectsAsync(It.IsAny<DeleteObjectsRequest>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        DeleteObjectsResponse result = await _s3Storage.DeleteObjectsAsync(bucketName, keys);

        // Assert
        result.ShouldBe(expectedResponse);
        _s3ClientMock.Verify(x => x.DeleteObjectsAsync(
            It.Is<DeleteObjectsRequest>(r =>
                r.BucketName == bucketName &&
                r.Objects.Count == 3),
            It.IsAny<CancellationToken>()), Times.Once);

        VerifyLoggerDebugWasCalled($"Deleted 3 objects from S3 bucket {bucketName}");
    }

    [Fact]
    public async Task ObjectExistsAsync_ObjectExists_ReturnsTrue()
    {
        // Arrange
        var bucketName = "test-bucket";
        var key = "test/file.txt";

        _s3ClientMock.Setup(x =>
                x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectMetadataResponse { HttpStatusCode = HttpStatusCode.OK });

        // Act
        bool result = await _s3Storage.ObjectExistsAsync(bucketName, key);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ObjectExistsAsync_ObjectNotFound_ReturnsFalse()
    {
        // Arrange
        var bucketName = "test-bucket";
        var key = "test/nonexistent.txt";

        _s3ClientMock.Setup(x =>
                x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(),
                    It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound });

        // Act
        bool result = await _s3Storage.ObjectExistsAsync(bucketName, key);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ListObjectsAsync_SuccessfulListing_ReturnsObjects()
    {
        // Arrange
        var bucketName = "test-bucket";
        var prefix = "test/";
        var maxKeys = 100;

        var expectedObjects = new List<S3Object>
        {
            new() { Key = "test/file1.txt", Size = 1024 },
            new() { Key = "test/file2.txt", Size = 2048 }
        };

        var mockResponse = new ListObjectsV2Response
        {
            HttpStatusCode = HttpStatusCode.OK,
            S3Objects = expectedObjects
        };

        _s3ClientMock.Setup(x =>
                x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        IEnumerable<S3Object> result
            = await _s3Storage.ListObjectsAsync(bucketName, prefix, maxKeys);

        // Assert
        result.ShouldBe(expectedObjects);
        _s3ClientMock.Verify(x => x.ListObjectsV2Async(
            It.Is<ListObjectsV2Request>(r =>
                r.BucketName == bucketName &&
                r.Prefix == prefix &&
                r.MaxKeys == maxKeys),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CopyObjectAsync_SuccessfulCopy_ReturnsResponse()
    {
        // Arrange
        var sourceBucket = "source-bucket";
        var sourceKey = "source/file.txt";
        var destBucket = "dest-bucket";
        var destKey = "dest/file.txt";

        var expectedResponse = new CopyObjectResponse { HttpStatusCode = HttpStatusCode.OK };
        _s3ClientMock.Setup(x =>
                x.CopyObjectAsync(It.IsAny<CopyObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        CopyObjectResponse result
            = await _s3Storage.CopyObjectAsync(sourceBucket, sourceKey, destBucket, destKey);

        // Assert
        result.ShouldBe(expectedResponse);
        _s3ClientMock.Verify(x => x.CopyObjectAsync(
            It.Is<CopyObjectRequest>(r =>
                r.SourceBucket == sourceBucket &&
                r.SourceKey == sourceKey &&
                r.DestinationBucket == destBucket &&
                r.DestinationKey == destKey),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GeneratePresignedUrlAsync_ValidRequest_ReturnsUrl()
    {
        // Arrange
        var bucketName = "test-bucket";
        var key = "test/file.txt";
        TimeSpan expiration = TimeSpan.FromHours(1);
        var expectedUrl = "https://test-bucket.s3.amazonaws.com/test/file.txt?AWSAccessKeyId=...";

        _s3ClientMock.Setup(x => x.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
            .ReturnsAsync(expectedUrl);

        // Act
        string result = await _s3Storage.GeneratePresignedUrlAsync(bucketName, key, expiration);

        // Assert
        result.ShouldBe(expectedUrl);
        _s3ClientMock.Verify(x => x.GetPreSignedURLAsync(
            It.Is<GetPreSignedUrlRequest>(r =>
                r.BucketName == bucketName &&
                r.Key == key &&
                r.Verb == HttpVerb.GET &&
                r.Protocol == Protocol.HTTPS)), Times.Once);
    }

    [Fact]
    public async Task GeneratePresignedPutUrlAsync_ValidRequest_ReturnsUrl()
    {
        // Arrange
        var bucketName = "test-bucket";
        var key = "test/file.txt";
        TimeSpan expiration = TimeSpan.FromHours(1);
        var contentType = "text/plain";
        var metadata = new Dictionary<string, string> { { "author", "test-user" } };
        var expectedUrl = "https://test-bucket.s3.amazonaws.com/test/file.txt?AWSAccessKeyId=...";

        _s3ClientMock.Setup(x => x.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
            .ReturnsAsync(expectedUrl);

        // Act
        string result
            = await _s3Storage.GeneratePresignedPutUrlAsync(bucketName, key, expiration,
                contentType, metadata);

        // Assert
        result.ShouldBe(expectedUrl);
        _s3ClientMock.Verify(x => x.GetPreSignedURLAsync(
            It.Is<GetPreSignedUrlRequest>(r =>
                r.BucketName == bucketName &&
                r.Key == key &&
                r.Verb == HttpVerb.PUT &&
                r.ContentType == contentType &&
                r.Metadata.Keys.Contains("x-amz-meta-author"))), Times.Once);
    }

    private void VerifyLoggerDebugWasCalled(string expectedMessage)
    {
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private void VerifyLoggerErrorWasCalled(string expectedMessage)
    {
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}