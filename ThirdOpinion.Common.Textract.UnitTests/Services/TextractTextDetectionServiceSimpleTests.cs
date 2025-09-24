using FluentAssertions;
using Moq;
using ThirdOpinion.Common.Textract.Services;
using Amazon.Textract;
using Amazon.Textract.Model;

namespace TextractLib.Tests.Services;

public class TextractTextDetectionServiceSimpleTests
{
    private readonly Mock<IAmazonTextract> _mockTextractClient;
    private readonly TextractTextDetectionService _service;

    public TextractTextDetectionServiceSimpleTests()
    {
        _mockTextractClient = new Mock<IAmazonTextract>();
        _service = new TextractTextDetectionService(_mockTextractClient.Object);
    }

    [Fact]
    public void Constructor_WithNullClient_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Action act = () => new TextractTextDetectionService(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithValidClient_ShouldInitializeCorrectly()
    {
        // Act
        var service = new TextractTextDetectionService(_mockTextractClient.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public async Task StartDocumentTextDetectionPollingAsync_WithValidParameters_ShouldReturnJobId()
    {
        // Arrange
        var bucketName = "test-bucket";
        var documentName = "test-document.pdf";
        var expectedJobId = "test-job-id-123";

        var startResponse = new StartDocumentTextDetectionResponse
        {
            JobId = expectedJobId
        };

        _mockTextractClient
            .Setup(x => x.StartDocumentTextDetectionAsync(It.IsAny<StartDocumentTextDetectionRequest>(), default))
            .ReturnsAsync(startResponse);

        // Act
        var result = await _service.StartDocumentTextDetectionPollingAsync(bucketName, documentName);

        // Assert
        result.Should().Be(expectedJobId);
    }

    [Fact]
    public async Task StartDocumentTextDetectionPollingAsync_WithEmptyBucketName_ShouldThrowArgumentException()
    {
        // Arrange
        var bucketName = "";
        var documentName = "test-document.pdf";

        // Act & Assert
        var act = () => _service.StartDocumentTextDetectionPollingAsync(bucketName, documentName);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task StartDocumentTextDetectionPollingAsync_WithEmptyDocumentName_ShouldThrowArgumentException()
    {
        // Arrange
        var bucketName = "test-bucket";
        var documentName = "";

        // Act & Assert
        var act = () => _service.StartDocumentTextDetectionPollingAsync(bucketName, documentName);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DetectTextS3Async_WithValidParameters_ShouldCallTextractClient()
    {
        // Arrange
        var bucketName = "test-bucket";
        var documentName = "test-document.jpg";
        var expectedBlocks = new List<Block>
        {
            new Block { Id = "block1", BlockType = BlockType.WORD, Text = "Hello" }
        };

        var detectResponse = new DetectDocumentTextResponse
        {
            Blocks = expectedBlocks
        };

        _mockTextractClient
            .Setup(x => x.DetectDocumentTextAsync(It.IsAny<DetectDocumentTextRequest>(), default))
            .ReturnsAsync(detectResponse);

        // Act
        var result = await _service.DetectTextS3Async(bucketName, documentName);

        // Assert
        result.Should().NotBeNull();
        result.Blocks.Should().HaveCount(1);
        result.Blocks[0].Text.Should().Be("Hello");
    }

    [Fact]
    public async Task DetectTextS3Async_WithEmptyBucketName_ShouldThrowArgumentException()
    {
        // Arrange
        var bucketName = "";
        var documentName = "test-document.jpg";

        // Act & Assert
        var act = () => _service.DetectTextS3Async(bucketName, documentName);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void GetJobResults_WithEmptyJobId_ShouldThrowArgumentException()
    {
        // Arrange
        var jobId = "";

        // Act & Assert
        var act = () => _service.GetJobResults(jobId);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetJobResults_WithValidJobId_ShouldReturnResults()
    {
        // Arrange
        var jobId = "test-job-id";
        var expectedBlocks = new List<Block>
        {
            new Block { Id = "result1", BlockType = BlockType.LINE, Text = "Result text" }
        };

        var getResponse = new GetDocumentTextDetectionResponse
        {
            JobStatus = JobStatus.SUCCEEDED,
            Blocks = expectedBlocks,
            NextToken = null
        };

        _mockTextractClient
            .Setup(x => x.GetDocumentTextDetectionAsync(It.IsAny<GetDocumentTextDetectionRequest>(), default))
            .ReturnsAsync(getResponse);

        // Act
        var results = _service.GetJobResults(jobId);

        // Assert
        results.Should().HaveCount(1);
        results[0].JobStatus.Should().Be(JobStatus.SUCCEEDED);
        results[0].Blocks.Should().HaveCount(1);
        results[0].Blocks[0].Text.Should().Be("Result text");
    }
}