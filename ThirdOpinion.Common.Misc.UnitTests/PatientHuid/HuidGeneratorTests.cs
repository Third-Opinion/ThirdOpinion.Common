using System.Diagnostics;
using System.Text.RegularExpressions;
using Misc.patients.PatientHuid;

namespace ThirdOpinion.Common.UnitTests.PatientHuid;

public class HuidGeneratorTests
{
    [Fact]
    public void IsValidHuid_ValidHuid_ReturnsTrue()
    {
        // Arrange
        var validHuid = "P234-ADH-KLMN";

        // Act
        bool result = HuidGenerator.IsValidHuid(validHuid);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsValidHuid_InvalidFormat_ReturnsFalse()
    {
        // Test various invalid formats
        var invalidHuids = new[]
        {
            "P23-ADH-KLMN", // Too short
            "P234-ADH-KLMNP", // Too long
            "234-ADH-KLMN", // Missing prefix
            "Q234-ADH-KLMN", // Wrong prefix
            "P234-ADH_KLMN", // Wrong separator
            "P234ADH-KLMN", // Missing dash
            "P234-ADHKLMN", // Missing dash
            "P234-ADH-KLM1", // Invalid character (1 not in alphabet)
            "P234-ADH-KLMI", // Invalid character (I not in alphabet)
            "", // Empty string
            "P234-ADH-KLM" // Too short
        };

        foreach (string invalidHuid in invalidHuids)
        {
            // Act
            bool result = HuidGenerator.IsValidHuid(invalidHuid);

            // Assert
            result.ShouldBeFalse($"HUID '{invalidHuid}' should be invalid");
        }
    }

    [Fact]
    public void IsValidHuid_ValidCharactersOnly_ReturnsTrue()
    {
        // Arrange - using only valid characters from the alphabet
        string validHuid
            = "P234-679A-DEHJKLMNPQRUVWXYZBCFGT"[..13]; // Take first 13 chars in correct format
        validHuid = "P234-679-ADEH"; // Properly formatted

        // Act
        bool result = HuidGenerator.IsValidHuid(validHuid);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void GeneratePatientHuid_ValidPatientId_ReturnsValidFormat()
    {
        // Arrange
        var patientId = 12345L;

        // Act
        string huid = HuidGenerator.GeneratePatientHuid(patientId);

        // Assert
        huid.ShouldNotBeNull();
        huid.Length.ShouldBe(HuidGenerator.HuidLength);
        HuidGenerator.IsValidHuid(huid).ShouldBeTrue();
        huid.ShouldStartWith("P");
        huid.ShouldMatch(HuidGenerator.HuidPattern);
    }

    [Fact]
    public void GeneratePatientHuid_DifferentPatientIds_GeneratesDifferentHuids()
    {
        // Arrange
        var patientIds = new long[] { 1, 2, 100, 1000, 999999 };

        // Act
        List<string> huids = patientIds.Select(HuidGenerator.GeneratePatientHuid).ToList();

        // Assert
        huids.Distinct().Count().ShouldBe(patientIds.Length); // All HUIDs should be unique

        foreach (string huid in huids) HuidGenerator.IsValidHuid(huid).ShouldBeTrue();
    }

    [Fact]
    public void GeneratePatientHuid_SamePatientId_GeneratesSameHuid()
    {
        // Arrange
        var patientId = 54321L;

        // Act
        string huid1 = HuidGenerator.GeneratePatientHuid(patientId);
        string huid2 = HuidGenerator.GeneratePatientHuid(patientId);

        // Assert
        huid1.ShouldBe(huid2);
    }

    [Fact]
    public void GeneratePatientHuid_ZeroPatientId_ReturnsValidHuid()
    {
        // Arrange
        var patientId = 0L;

        // Act
        string huid = HuidGenerator.GeneratePatientHuid(patientId);

        // Assert
        HuidGenerator.IsValidHuid(huid).ShouldBeTrue();
    }

    [Fact]
    public void GeneratePatientHuid_MaxLongValue_ReturnsValidHuid()
    {
        // Arrange
        var patientId = long.MaxValue;

        // Act
        string huid = HuidGenerator.GeneratePatientHuid(patientId);

        // Assert
        HuidGenerator.IsValidHuid(huid).ShouldBeTrue();
    }

    [Fact]
    public void GeneratePatientHuidFromGuid_ValidGuid_ReturnsValidHuid()
    {
        // Arrange
        var patientGuid = Guid.NewGuid();

        // Act
        string huid = HuidGenerator.GeneratePatientHuidFromGuid(patientGuid);

        // Assert
        HuidGenerator.IsValidHuid(huid).ShouldBeTrue();
        huid.Length.ShouldBe(HuidGenerator.HuidLength);
        huid.ShouldStartWith("P");
    }

    [Fact]
    public void GeneratePatientHuidFromGuid_SameGuid_GeneratesSameHuid()
    {
        // Arrange
        var patientGuid = Guid.NewGuid();

        // Act
        string huid1 = HuidGenerator.GeneratePatientHuidFromGuid(patientGuid);
        string huid2 = HuidGenerator.GeneratePatientHuidFromGuid(patientGuid);

        // Assert
        huid2.ShouldBe(huid1);
    }

    [Fact]
    public void GeneratePatientHuidFromGuid_DifferentGuids_GeneratesDifferentHuids()
    {
        // Arrange
        var guids = new[]
            { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        // Act
        List<string> huids = guids.Select(HuidGenerator.GeneratePatientHuidFromGuid).ToList();

        // Assert
        huids.Distinct().Count().ShouldBe(guids.Length); // All HUIDs should be unique

        foreach (string huid in huids) HuidGenerator.IsValidHuid(huid).ShouldBeTrue();
    }

    [Fact]
    public void GeneratePatientHuidFromGuid_EmptyGuid_ReturnsValidHuid()
    {
        // Arrange
        var emptyGuid = Guid.Empty;

        // Act
        string huid = HuidGenerator.GeneratePatientHuidFromGuid(emptyGuid);

        // Assert
        HuidGenerator.IsValidHuid(huid).ShouldBeTrue();
    }

    [Fact]
    public void HuidPattern_MatchesGeneratedHuids()
    {
        // Arrange
        var regex = new Regex(HuidGenerator.HuidPattern);
        long[] testIds = new[] { 1, 100, 12345, 999999, long.MaxValue / 2 };

        foreach (long id in testIds)
        {
            // Act
            string huid = HuidGenerator.GeneratePatientHuid(id);

            // Assert
            regex.IsMatch(huid).ShouldBeTrue($"Generated HUID '{huid}' doesn't match pattern");
        }
    }

    [Fact]
    public void HuidGenerator_Constants_AreCorrect()
    {
        // Assert
        HuidGenerator.HuidLength.ShouldBe(13);
        HuidGenerator.HuidPattern.ShouldNotBeNull();
        HuidGenerator.HuidRegex.ShouldNotBeNull();

        // Test that the regex compiles correctly
        HuidGenerator.HuidRegex.IsMatch("P234-ADH-KLMN").ShouldBeTrue();
    }

    [Fact]
    public void GeneratePatientHuid_LargeVolume_AllUnique()
    {
        // Arrange - Generate a large number of HUIDs to test for collisions
        var count = 10000;
        var huids = new HashSet<string>();

        // Act
        for (long i = 0; i < count; i++)
        {
            string huid = HuidGenerator.GeneratePatientHuid(i);
            huids.Add(huid);
        }

        // Assert - All generated HUIDs should be unique
        huids.Count.ShouldBe(count);
    }

    [Fact]
    public void GeneratePatientHuid_Performance_CompletesWithinReasonableTime()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();
        var iterations = 1000;

        // Act
        for (var i = 0; i < iterations; i++) HuidGenerator.GeneratePatientHuid(i);

        stopwatch.Stop();

        // Assert - Should complete within 1 second for 1000 iterations
        (stopwatch.ElapsedMilliseconds < 1000).ShouldBeTrue(
            $"HUID generation took {stopwatch.ElapsedMilliseconds}ms for {iterations} iterations");
    }

    [Fact]
    public void GeneratePatientHuid_ContainsOnlyValidCharacters()
    {
        // Arrange
        const string
            validChars
                = "2346789ADEHJKLMNPQRUVWXYZBCFGTP-"; // Including P for prefix and - for separator
        var testIds = new long[] { 1, 100, 12345, 999999 };

        foreach (long id in testIds)
        {
            // Act
            string huid = HuidGenerator.GeneratePatientHuid(id);

            // Assert
            foreach (char c in huid)
                validChars.Contains(c)
                    .ShouldBeTrue($"HUID '{huid}' contains invalid character '{c}'");
        }
    }

    [Fact]
    public void GeneratePatientHuid_FormatStructure_IsCorrect()
    {
        // Arrange
        var testIds = new long[] { 1, 100, 12345 };

        foreach (long id in testIds)
        {
            // Act
            string huid = HuidGenerator.GeneratePatientHuid(id);

            // Assert
            huid[0].ShouldBe('P'); // Starts with P
            huid[4].ShouldBe('-'); // First dash at position 4
            huid[8].ShouldBe('-'); // Second dash at position 8
            huid.Length.ShouldBe(13); // Total length is 13

            // Check segment lengths
            string[] parts = huid.Split('-');
            parts.Length.ShouldBe(3);
            parts[0].Length.ShouldBe(4); // P + 3 chars
            parts[1].Length.ShouldBe(3); // 3 chars
            parts[2].Length.ShouldBe(4); // 4 chars
        }
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(42L)]
    [InlineData(12345L)]
    [InlineData(999999L)]
    [InlineData(long.MaxValue)]
    public void GeneratePatientHuid_Deterministic_SameInputProducesSameOutput(long patientId)
    {
        // Act
        string huid1 = HuidGenerator.GeneratePatientHuid(patientId);
        string huid2 = HuidGenerator.GeneratePatientHuid(patientId);
        string huid3 = HuidGenerator.GeneratePatientHuid(patientId);

        // Assert
        huid2.ShouldBe(huid1);
        huid3.ShouldBe(huid2);
        Assert.True(HuidGenerator.IsValidHuid(huid1));
    }

    [Fact]
    public void GeneratePatientHuidFromGuid_KnownGuid_ProducesExpectedFormat()
    {
        // Arrange
        var knownGuid = new Guid("12345678-1234-5678-9ABC-123456789012");

        // Act
        string huid1 = HuidGenerator.GeneratePatientHuidFromGuid(knownGuid);
        string huid2 = HuidGenerator.GeneratePatientHuidFromGuid(knownGuid);

        // Assert
        huid2.ShouldBe(huid1); // Should be deterministic
        Assert.True(HuidGenerator.IsValidHuid(huid1));
        huid1.ShouldStartWith("P");
    }
}