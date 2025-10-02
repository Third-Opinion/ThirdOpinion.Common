using ThirdOpinion.Common.Aws.S3;

namespace ThirdOpinion.Common.UnitTests.Helpers;

public class HelpersTest
{
    [Fact]
    public void IsValidS3Arn_ReturnsFalse_WhenArnIsInvalid()
    {
        // Arrange
        var arn = "arn:aws:s2:::bucket/file.txt";
        arn.IsValidS3Arn().ShouldBeFalse();
    }

    [Theory]
    [InlineData("arn:aws:s3:::bucket/file.txt", true)]
    [InlineData("arn:aws:s2:::bucket/file.txt", false)]
    [InlineData("arn:aws:s3:::anotherbucket/anotherfile.txt", true)]
    [InlineData("arn:aws:s3:::bucket/", true)]
    [InlineData("arn:aws:s3:::/", false)]
    [InlineData("arn:aws:s3:::bucket-name", true)]
    [InlineData("", false)]
    public void IsValidS3Arn_ReturnsExpectedResult_ForVariousInputs(string arn, bool expected)
    {
        arn.IsValidS3Arn().ShouldBe(expected);
    }
}