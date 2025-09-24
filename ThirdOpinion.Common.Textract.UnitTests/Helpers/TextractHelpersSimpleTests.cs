using FluentAssertions;
using Moq;
using ThirdOpinion.Common.Textract.Helpers;
using ThirdOpinion.Common.Textract.Services;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Textract.Model;
using System.Text;

namespace TextractLib.Tests.Helpers;

public class TextractHelpersSimpleTests
{
    [Fact]
    public void ResultObject_DefaultConstructor_ShouldInitializeCorrectly()
    {
        // Act
        var resultObject = new ResultObject();

        // Assert
        resultObject.FileInformation.Should().BeNull();
        resultObject.JobId.Should().BeNull();
        resultObject.JobStatus.Should().BeNull();
        resultObject.CreatedAt.Should().Be(default);
        resultObject.SourceObjectKey.Should().BeNull();
        resultObject.TextractOutputObjectKey.Should().BeNull();
        resultObject.TextractOutputFilteredObjectKey.Should().BeNull();
    }

    [Fact]
    public void FileInformation_DefaultConstructor_ShouldInitializeCorrectly()
    {
        // Act
        var fileInfo = new FileInformation();

        // Assert
        fileInfo.Name.Should().BeNull();
        fileInfo.DOB.Should().BeNull();
        fileInfo.Id.Should().BeNull();
        fileInfo.Guid.Should().BeNull();
        fileInfo.PageStart.Should().BeNull();
        fileInfo.PageEnd.Should().BeNull();
        fileInfo.LlmType.Should().BeNull();
        fileInfo.KeyFull.Should().BeNull();
        fileInfo.FullSize.Should().Be(0);
        fileInfo.KeyNoGeo.Should().BeNull();
        fileInfo.NoGeoSize.Should().Be(0);
        fileInfo.KeyNoGeoNoRelationships.Should().BeNull();
        fileInfo.NoGeoNoRelationshipsSize.Should().Be(0);
    }

    [Fact]
    public void FileInformation_WithProperties_ShouldStoreCorrectly()
    {
        // Act
        var fileInfo = new FileInformation
        {
            Name = "John Doe",
            DOB = "1990-01-01",
            Id = "123456",
            Guid = "test-guid-123",
            PageStart = "1",
            PageEnd = "5",
            LlmType = "GPT-4",
            KeyFull = "full-key",
            FullSize = 1024,
            KeyNoGeo = "no-geo-key",
            NoGeoSize = 512,
            KeyNoGeoNoRelationships = "no-rel-key",
            NoGeoNoRelationshipsSize = 256
        };

        // Assert
        fileInfo.Name.Should().Be("John Doe");
        fileInfo.DOB.Should().Be("1990-01-01");
        fileInfo.Id.Should().Be("123456");
        fileInfo.Guid.Should().Be("test-guid-123");
        fileInfo.PageStart.Should().Be("1");
        fileInfo.PageEnd.Should().Be("5");
        fileInfo.LlmType.Should().Be("GPT-4");
        fileInfo.KeyFull.Should().Be("full-key");
        fileInfo.FullSize.Should().Be(1024);
        fileInfo.KeyNoGeo.Should().Be("no-geo-key");
        fileInfo.NoGeoSize.Should().Be(512);
        fileInfo.KeyNoGeoNoRelationships.Should().Be("no-rel-key");
        fileInfo.NoGeoNoRelationshipsSize.Should().Be(256);
    }

    [Fact]
    public async Task GetAllTextTractedFiles_WithEmptyBucket_ShouldReturnEmptyList()
    {
        // Arrange
        var bucketName = "test-bucket";
        var mockS3Client = new Mock<IAmazonS3>();

        var listResponse = new ListObjectsV2Response
        {
            S3Objects = new List<Amazon.S3.Model.S3Object>(),
            IsTruncated = false
        };

        mockS3Client
            .Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), default))
            .ReturnsAsync(listResponse);

        // Act
        var result = await TextractHelpers.GetAllTextTractedFiles(bucketName, mockS3Client.Object);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllReportFiles_WithEmptyBucket_ShouldReturnEmptyList()
    {
        // Arrange
        var bucketName = "test-bucket";
        var mockS3Client = new Mock<IAmazonS3>();

        var listResponse = new ListObjectsV2Response
        {
            S3Objects = new List<Amazon.S3.Model.S3Object>(),
            IsTruncated = false
        };

        mockS3Client
            .Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), default))
            .ReturnsAsync(listResponse);

        // Act
        var result = await TextractHelpers.GetAllReportFiles(bucketName, mockS3Client.Object);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllTextTractedFiles_WithValidFiles_ShouldFilterCorrectly()
    {
        // Arrange
        var bucketName = "test-bucket";
        var mockS3Client = new Mock<IAmazonS3>();

        var mockS3Objects = new List<Amazon.S3.Model.S3Object>
        {
            new Amazon.S3.Model.S3Object { Key = "document1-textract-240101120000.json" }, // Should be included
            new Amazon.S3.Model.S3Object { Key = "document2-textract-240101130000.json" }, // Should be included
            new Amazon.S3.Model.S3Object { Key = "document3-textract-filtered-240101140000.json" }, // Should be excluded
            new Amazon.S3.Model.S3Object { Key = "document4-textract-merged-240101150000.json" }, // Should be excluded
            new Amazon.S3.Model.S3Object { Key = "document5.pdf" }, // Should be excluded
            new Amazon.S3.Model.S3Object { Key = "other-file.json" } // Should be excluded
        };

        var listResponse = new ListObjectsV2Response
        {
            S3Objects = mockS3Objects,
            IsTruncated = false
        };

        mockS3Client
            .Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), default))
            .ReturnsAsync(listResponse);

        // Act
        var result = await TextractHelpers.GetAllTextTractedFiles(bucketName, mockS3Client.Object);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain("document1-textract-240101120000.json");
        result.Should().Contain("document2-textract-240101130000.json");
        result.Should().NotContain("document3-textract-filtered-240101140000.json");
        result.Should().NotContain("document4-textract-merged-240101150000.json");
    }

    [Fact]
    public async Task GetAllReportFiles_WithValidFiles_ShouldFilterCorrectly()
    {
        // Arrange
        var bucketName = "test-bucket";
        var mockS3Client = new Mock<IAmazonS3>();

        var mockS3Objects = new List<Amazon.S3.Model.S3Object>
        {
            new Amazon.S3.Model.S3Object { Key = "document1.pdf" }, // Should be included
            new Amazon.S3.Model.S3Object { Key = "image1.tiff" }, // Should be included
            new Amazon.S3.Model.S3Object { Key = "image2.tif" }, // Should be included
            new Amazon.S3.Model.S3Object { Key = "document2.docx" }, // Should be excluded
            new Amazon.S3.Model.S3Object { Key = "image3.jpg" }, // Should be excluded
            new Amazon.S3.Model.S3Object { Key = "textract-result.json" } // Should be excluded
        };

        var listResponse = new ListObjectsV2Response
        {
            S3Objects = mockS3Objects,
            IsTruncated = false
        };

        mockS3Client
            .Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), default))
            .ReturnsAsync(listResponse);

        // Act
        var result = await TextractHelpers.GetAllReportFiles(bucketName, mockS3Client.Object);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain("document1.pdf");
        result.Should().Contain("image1.tiff");
        result.Should().Contain("image2.tif");
        result.Should().NotContain("document2.docx");
        result.Should().NotContain("image3.jpg");
        result.Should().NotContain("textract-result.json");
    }
}