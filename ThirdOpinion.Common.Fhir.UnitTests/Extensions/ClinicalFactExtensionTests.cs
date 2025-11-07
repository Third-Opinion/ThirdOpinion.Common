using Hl7.Fhir.Model;
using Newtonsoft.Json;
using Shouldly;
using ThirdOpinion.Common.Fhir.Extensions;
using ThirdOpinion.Common.Fhir.Models;
using Xunit;

namespace ThirdOpinion.Common.Fhir.UnitTests.Extensions;

/// <summary>
///     Unit tests for ClinicalFactExtension
/// </summary>
public class ClinicalFactExtensionTests
{
    [Fact]
    public void CreateNdjsonExtension_WithMultipleFacts_CreatesCorrectNdjsonFormat()
    {
        // Arrange
        var facts = new[]
        {
            new Fact
            {
                factGuid = "fact-001",
                factDocumentReference = "DocumentReference/doc-001",
                type = "diagnosis",
                fact = "advanced prostate cancer, T3bN1",
                @ref = Array.Empty<string>(),
                timeRef = "2025-09-08",
                relevance = "Confirms advanced prostate cancer with lymph node metastases"
            },
            new Fact
            {
                factGuid = "fact-002",
                factDocumentReference = "DocumentReference/doc-001",
                type = "finding",
                fact = "Involvement of bilateral pelvic lymph nodes and retroperitoneal lymph nodes are noted",
                @ref = Array.Empty<string>(),
                timeRef = "2025-09-08",
                relevance = "PSMA scan confirms metastatic lymph node involvement"
            },
            new Fact
            {
                factGuid = "fact-003",
                factDocumentReference = "DocumentReference/doc-002",
                type = "diagnosis",
                fact = "Metastatic adenocarcinoma",
                @ref = Array.Empty<string>(),
                timeRef = "2025-05-08",
                relevance = "Explicit diagnosis of metastatic disease in problem list"
            }
        };

        // Act
        Extension extension = ClinicalFactExtension.CreateNdjsonExtension(facts);

        // Assert
        extension.ShouldNotBeNull();
        extension.Url.ShouldBe("https://thirdopinion.io/clinical-fact");
        extension.Extension.Count.ShouldBe(1);

        var subExtension = extension.Extension[0];
        subExtension.Url.ShouldBe("factsArrayJson");
        subExtension.Value.ShouldBeOfType<FhirString>();

        var ndjsonValue = ((FhirString)subExtension.Value).Value;
        ndjsonValue.ShouldNotBeNull();

        // Verify NDJSON format - each line should be valid JSON
        string[] lines = ndjsonValue.Split('\n');
        lines.Length.ShouldBe(3);

        // Verify each line is valid JSON and contains expected data
        for (int i = 0; i < lines.Length; i++)
        {
            var deserializedFact = JsonConvert.DeserializeObject<Fact>(lines[i]);
            deserializedFact.ShouldNotBeNull();
            deserializedFact.factGuid.ShouldBe(facts[i].factGuid);
            deserializedFact.fact.ShouldBe(facts[i].fact);
            deserializedFact.timeRef.ShouldBe(facts[i].timeRef);
            deserializedFact.relevance.ShouldBe(facts[i].relevance);
        }
    }

    [Fact]
    public void CreateNdjsonExtension_WithSingleFact_CreatesCorrectFormat()
    {
        // Arrange
        var fact = new Fact
        {
            factGuid = "fact-001",
            type = "diagnosis",
            fact = "Prostate cancer",
            @ref = new[] { "1.1", "1.2" },
            timeRef = "2025-01-01",
            relevance = "Primary diagnosis"
        };

        // Act
        Extension extension = ClinicalFactExtension.CreateNdjsonExtension(fact);

        // Assert
        extension.ShouldNotBeNull();
        extension.Url.ShouldBe("https://thirdopinion.io/clinical-fact");

        var subExtension = extension.Extension[0];
        var ndjsonValue = ((FhirString)subExtension.Value).Value;

        // Should be single line JSON (no newlines within the fact)
        ndjsonValue.ShouldNotContain('\n');

        var deserializedFact = JsonConvert.DeserializeObject<Fact>(ndjsonValue);
        deserializedFact.ShouldNotBeNull();
        deserializedFact.factGuid.ShouldBe(fact.factGuid);
        deserializedFact.@ref.Length.ShouldBe(2);
    }

    [Fact]
    public void CreateNdjsonExtension_WithNullFacts_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            ClinicalFactExtension.CreateNdjsonExtension((IEnumerable<Fact>)null));
    }

    [Fact]
    public void CreateNdjsonExtension_WithEmptyFacts_ThrowsArgumentException()
    {
        // Arrange
        var emptyFacts = Array.Empty<Fact>();

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            ClinicalFactExtension.CreateNdjsonExtension(emptyFacts));
    }

    [Fact]
    public void CreateNdjsonExtension_FiltersOutNullFacts()
    {
        // Arrange
        var facts = new[]
        {
            new Fact { factGuid = "fact-001", fact = "Fact 1" },
            null,
            new Fact { factGuid = "fact-002", fact = "Fact 2" }
        };

        // Act & Assert
        // Should throw because after filtering nulls, we still have valid facts
        Extension extension = ClinicalFactExtension.CreateNdjsonExtension(facts);

        var subExtension = extension.Extension[0];
        var ndjsonValue = ((FhirString)subExtension.Value).Value;
        string[] lines = ndjsonValue.Split('\n');

        lines.Length.ShouldBe(2); // Only non-null facts
    }

    [Fact]
    public void CreateExtension_SingleFact_CreatesStructuredExtension()
    {
        // Arrange
        var fact = new Fact
        {
            factGuid = "fact-001",
            factDocumentReference = "DocumentReference/doc-001",
            type = "diagnosis",
            fact = "Prostate cancer",
            @ref = new[] { "1.1" },
            timeRef = "2025-01-01",
            relevance = "Primary diagnosis"
        };

        // Act
        Extension extension = ClinicalFactExtension.CreateExtension(fact);

        // Assert
        extension.ShouldNotBeNull();
        extension.Url.ShouldBe("https://thirdopinion.io/clinical-fact");
        extension.Extension.Count.ShouldBe(7); // factGuid, factDocumentReference, type, fact, ref, timeRef, relevance
    }

    [Fact]
    public void CreateExtensions_MultipleFacts_CreatesMultipleExtensions()
    {
        // Arrange
        var facts = new[]
        {
            new Fact { factGuid = "fact-001", fact = "Fact 1" },
            new Fact { factGuid = "fact-002", fact = "Fact 2" }
        };

        // Act
        List<Extension> extensions = ClinicalFactExtension.CreateExtensions(facts);

        // Assert
        extensions.ShouldNotBeNull();
        extensions.Count.ShouldBe(1);
        extensions[0].Url.ShouldBe("https://thirdopinion.io/clinical-fact");
    }
}