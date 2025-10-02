using System.Text.RegularExpressions;
using Misc.patients.PatientHuid;

namespace ThirdOpinion.Common.UnitTests.Helpers.PatientHuid;

public class HuidGeneratorTests
{
    private const string HuidPattern
        = @"^P[2346789ADEHJKLMNPQRUVWXYZBCFGT]{3}-[2346789ADEHJKLMNPQRUVWXYZBCFGT]{3}-[2346789ADEHJKLMNPQRUVWXYZBCFGT]{4}$";

    private const int ExpectedHuidLength = 13; // Pxxx-xxx-xxxx

    [Theory]
    [InlineData(1L)]
    [InlineData(12345L)]
    [InlineData(9876543210L)]
    [InlineData(0L)] // Test edge case
    [InlineData(long.MaxValue / 2)] // Test large value
    public void GeneratePatientHuid_WithValidPatientId_ShouldReturnCorrectFormatAndLength(
        long patientId)
    {
        // Act
        string huid = HuidGenerator.GeneratePatientHuid(patientId);

        // Assert
        huid.ShouldNotBeNullOrEmpty();
        huid.Length.ShouldBe(ExpectedHuidLength);
        Regex.IsMatch(huid, HuidPattern).ShouldBeTrue(
            $"HUID '{huid}' did not match pattern '{HuidPattern}' for patientId {patientId}");
    }

    [Theory]
    [InlineData(54321L)]
    [InlineData(99999L)]
    public void GeneratePatientHuid_WithSamePatientId_ShouldBeIdempotent(long patientId)
    {
        // Act
        string huid1 = HuidGenerator.GeneratePatientHuid(patientId);
        string huid2 = HuidGenerator.GeneratePatientHuid(patientId);

        // Assert
        huid1.ShouldBe(huid2);
    }

    // Using known Guids for predictable tests
    [Theory]
    [InlineData("123e4567-e89b-12d3-a456-426614174000")]
    [InlineData("00000000-0000-0000-0000-000000000000")] // Guid.Empty
    [InlineData("f47ac10b-58cc-4372-a567-0e02b2c3d479")]
    public void GeneratePatientHuidFromGuid_WithValidGuid_ShouldReturnCorrectFormatAndLength(
        string guidString)
    {
        // Arrange
        var patientGuid = new Guid(guidString);

        // Act
        string huid = HuidGenerator.GeneratePatientHuidFromGuid(patientGuid);

        // Assert
        huid.ShouldNotBeNullOrEmpty();
        huid.Length.ShouldBe(ExpectedHuidLength);
        Regex.IsMatch(huid, HuidPattern)
            .ShouldBeTrue(
                $"HUID '{huid}' did not match pattern '{HuidPattern}' for Guid {guidString}");
    }

    [Theory]
    [InlineData("87f5a3b1-9c0e-4a8d-bd3a-5e7b9f0d1c2e")]
    [InlineData("c5b9e8a1-7d6f-4c3b-af29-1a0d9e8c7b6a")]
    public void GeneratePatientHuidFromGuid_WithSameGuid_ShouldBeIdempotent(string guidString)
    {
        // Arrange
        var patientGuid = new Guid(guidString);

        // Act
        string huid1 = HuidGenerator.GeneratePatientHuidFromGuid(patientGuid);
        string huid2 = HuidGenerator.GeneratePatientHuidFromGuid(patientGuid);

        // Assert
        huid1.ShouldBe(huid2);
    }

    [Fact]
    public void GeneratePatientHuidFromGuid_WithDifferentGuids_ShouldProduceDifferentHuids()
    {
        // Arrange
        var guid1 = new Guid("11111111-1111-1111-1111-111111111111");
        var guid2 = new Guid("22222222-2222-2222-2222-222222222222");

        // Act
        string huid1 = HuidGenerator.GeneratePatientHuidFromGuid(guid1);
        string huid2 = HuidGenerator.GeneratePatientHuidFromGuid(guid2);

        // Assert
        huid1.ShouldNotBe(huid2);
    }
}