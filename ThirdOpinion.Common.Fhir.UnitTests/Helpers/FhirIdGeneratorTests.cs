using System.Text.RegularExpressions;
using ThirdOpinion.Common.Fhir.Helpers;

namespace ThirdOpinion.Common.Fhir.UnitTests.Helpers;

public class FhirIdGeneratorTests
{
    [Fact]
    public void GenerateInferenceId_WithoutParameter_ReturnsCorrectFormat()
    {
        // Act
        var id = FhirIdGenerator.GenerateInferenceId();

        // Assert
        id.ShouldNotBeNullOrEmpty();
        id.ShouldStartWith("to.ai-inference-");

        // Check GUID format (lowercase)
        var guidPart = id.Substring("to.ai-inference-".Length);
        Regex.IsMatch(guidPart, @"^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$").ShouldBeTrue();
    }

    [Fact]
    public void GenerateInferenceId_WithSequenceNumber_ReturnsCorrectFormat()
    {
        // Arrange & Act
        var id1 = FhirIdGenerator.GenerateInferenceId(1);
        var id999 = FhirIdGenerator.GenerateInferenceId(999);
        var id100000 = FhirIdGenerator.GenerateInferenceId(100000);

        // Assert
        id1.ShouldBe("to.ai-inference-000001");
        id999.ShouldBe("to.ai-inference-000999");
        id100000.ShouldBe("to.ai-inference-100000");
    }

    [Fact]
    public void GenerateProvenanceId_ReturnsCorrectFormat()
    {
        // Act
        var id = FhirIdGenerator.GenerateProvenanceId();

        // Assert
        id.ShouldNotBeNullOrEmpty();
        id.ShouldStartWith("to.ai-provenance-");

        var guidPart = id.Substring("to.ai-provenance-".Length);
        Regex.IsMatch(guidPart, @"^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$").ShouldBeTrue();
    }

    [Fact]
    public void GenerateDocumentId_ReturnsCorrectFormat()
    {
        // Arrange & Act
        var id1 = FhirIdGenerator.GenerateDocumentId("report");
        var id2 = FhirIdGenerator.GenerateDocumentId("Clinical Note");
        var id3 = FhirIdGenerator.GenerateDocumentId("X-Ray");

        // Assert
        id1.ShouldStartWith("to.ai-document-report-");
        id2.ShouldStartWith("to.ai-document-clinical-note-");
        id3.ShouldStartWith("to.ai-document-x-ray-");

        // Check GUID format in each
        var guidPart1 = id1.Substring("to.ai-document-report-".Length);
        Regex.IsMatch(guidPart1, @"^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$").ShouldBeTrue();
    }

    [Fact]
    public void GenerateDocumentId_WithNullOrEmpty_ThrowsException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => FhirIdGenerator.GenerateDocumentId(null!));
        Should.Throw<ArgumentException>(() => FhirIdGenerator.GenerateDocumentId(""));
    }

    [Fact]
    public void GenerateResourceId_WithPrefix_ReturnsCorrectFormat()
    {
        // Act
        var id = FhirIdGenerator.GenerateResourceId("custom-prefix");

        // Assert
        id.ShouldStartWith("custom-prefix-");
        var guidPart = id.Substring("custom-prefix-".Length);
        Regex.IsMatch(guidPart, @"^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$").ShouldBeTrue();
    }

    [Fact]
    public void GenerateResourceId_WithProvidedGuid_UsesProvidedGuid()
    {
        // Arrange
        var providedGuid = "550e8400-e29b-41d4-a716-446655440000";

        // Act
        var id = FhirIdGenerator.GenerateResourceId("test", providedGuid);

        // Assert
        id.ShouldBe("test-550e8400-e29b-41d4-a716-446655440000");
    }

    [Fact]
    public void GenerateSequentialId_AutoIncrements()
    {
        // Arrange
        FhirIdGenerator.ResetSequenceCounter();

        // Act
        var id1 = FhirIdGenerator.GenerateSequentialId("test");
        var id2 = FhirIdGenerator.GenerateSequentialId("test");
        var id3 = FhirIdGenerator.GenerateSequentialId("other");

        // Assert
        id1.ShouldBe("test-000001");
        id2.ShouldBe("test-000002");
        id3.ShouldBe("other-000003");
    }

    [Fact]
    public void GenerateSequentialId_WithSpecifiedSequence_UsesProvidedNumber()
    {
        // Act
        var id1 = FhirIdGenerator.GenerateSequentialId("test", 42);
        var id2 = FhirIdGenerator.GenerateSequentialId("test", 999999);
        var id3 = FhirIdGenerator.GenerateSequentialId("test", null);

        // Assert
        id1.ShouldBe("test-000042");
        id2.ShouldBe("test-999999");
        id3.ShouldNotBe("test-000042"); // Should use auto-increment
    }

    [Fact]
    public void AllGenerators_ProduceUniqueIds()
    {
        // Arrange
        var ids = new HashSet<string>();
        const int count = 1000;

        // Act
        for (int i = 0; i < count; i++)
        {
            ids.Add(FhirIdGenerator.GenerateInferenceId());
            ids.Add(FhirIdGenerator.GenerateProvenanceId());
            ids.Add(FhirIdGenerator.GenerateDocumentId("test"));
            ids.Add(FhirIdGenerator.GenerateResourceId("resource"));
        }

        // Assert
        ids.Count.ShouldBe(count * 4); // All IDs should be unique
    }

    [Fact]
    public async Task GenerateSequentialId_ThreadSafe()
    {
        // Arrange
        FhirIdGenerator.ResetSequenceCounter();
        var ids = new System.Collections.Concurrent.ConcurrentBag<string>();
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    ids.Add(FhirIdGenerator.GenerateSequentialId("concurrent"));
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        ids.Count.ShouldBe(1000);
        ids.Distinct().Count().ShouldBe(1000); // All IDs should be unique
    }
}