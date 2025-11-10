using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Builders.Observations;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Models;

namespace ThirdOpinion.Common.Fhir.UnitTests.Builders.Observations;

public class Pcwg3ProgressionObservationBuilderTests
{
    private readonly AiInferenceConfiguration _configuration;
    private readonly ResourceReference _deviceReference;
    private readonly ResourceReference _focusReference;
    private readonly ResourceReference _patientReference;
    private readonly Fact[] _sampleFacts;

    public Pcwg3ProgressionObservationBuilderTests()
    {
        _configuration = AiInferenceConfiguration.CreateDefault();
        _patientReference = new ResourceReference("Patient/test-patient", "Test Patient");
        _deviceReference = new ResourceReference("Device/ai-device", "AI PCWG3 Assessment Device");
        _focusReference = new ResourceReference("Condition/prostate-cancer-001", "Prostate Cancer");

        _sampleFacts = new[]
        {
            new Fact
            {
                factGuid = "ca20e02a-f391-4614-96e4-35cbe47d2a83",
                factDocumentReference = "DocumentReference/bone-scan-001",
                type = "finding",
                fact = "Bone scan from 11/24 reported poss new lesion at L5 similar to CT report",
                @ref = new[] { "1.264" },
                timeRef = "2024-11-01",
                relevance = "Initial new bone lesion identified"
            },
            new Fact
            {
                factGuid = "bb20e02a-f391-4614-96e4-35cbe47d2a84",
                factDocumentReference = "DocumentReference/bone-scan-002",
                type = "finding",
                fact = "bone scan reported new lesions",
                @ref = new[] { "1.265" },
                timeRef = "2025-01-29",
                relevance = "Confirmation of new bone lesions"
            }
        };
    }

    [Fact]
    public void Build_WithProgression_CreatesCorrectObservation()
    {
        // Arrange
        var builder = new Pcwg3ProgressionObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithInferenceId("pcwg3-test-001")
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_focusReference)
            .WithDetermination("PD")
            .WithInitialLesions("new lesions")
            .WithConfirmationDate(new DateTime(2025, 4, 23))
            .WithTimeBetweenScans("12 weeks")
            .WithSupportingFacts(_sampleFacts)
            .WithConfidence(0.85f)
            .Build();

        // Assert
        observation.ShouldNotBeNull();
        observation.Status.ShouldBe(ObservationStatus.Final);
        observation.Subject.Reference.ShouldBe("Patient/test-patient");
        observation.Device.Reference.ShouldBe("Device/ai-device");
        observation.Focus.ShouldNotBeNull();
        observation.Focus.Count.ShouldBe(1);
        observation.Focus[0].Reference.ShouldBe("Condition/prostate-cancer-001");

        // Check code
        observation.Code.Coding.ShouldNotBeEmpty();
        observation.Code.Coding.First().Code.ShouldBe("44667-7");
        observation.Code.Coding.First().Display.ShouldBe("Bone scan findings");

        // Check value (progression identified)
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding.First().Code.ShouldBe("277022003");
        valueCodeableConcept.Coding.First().Display.ShouldBe("Progressive disease");

        // Check components
        observation.Component.ShouldNotBeNull();
        observation.Component.Count.ShouldBeGreaterThan(0);

        // Check for initial lesions component
        Observation.ComponentComponent? initialLesionsComponent = observation.Component
            .FirstOrDefault(c => c.Code.Coding.Any(cd => cd.Code == "initial-lesions"));
        initialLesionsComponent.ShouldNotBeNull();
        ((FhirString)initialLesionsComponent.Value).Value.ShouldBe("new lesions");

        // Check for confirmation date component
        Observation.ComponentComponent? confirmationDateComponent = observation.Component
            .FirstOrDefault(c => c.Code.Coding.Any(cd => cd.Code == "confirmation-date"));
        confirmationDateComponent.ShouldNotBeNull();

        // Check for time between scans component
        Observation.ComponentComponent? timeBetweenScansComponent = observation.Component
            .FirstOrDefault(c => c.Code.Coding.Any(cd => cd.Code == "time-between-scans"));
        timeBetweenScansComponent.ShouldNotBeNull();
        ((FhirString)timeBetweenScansComponent.Value).Value.ShouldBe("12 weeks");

        // Check for confidence component
        Observation.ComponentComponent? confidenceComponent = observation.Component
            .FirstOrDefault(c => c.Code.Coding.Any(cd => cd.Code == "LA11892-6"));
        confidenceComponent.ShouldNotBeNull();
        ((Quantity)confidenceComponent.Value).Value.ShouldBe(0.85m);

        // Check supporting facts extensions
        observation.Extension.ShouldNotBeEmpty();
        observation.Extension.Any(e => e.Url == "https://thirdopinion.io/clinical-fact")
            .ShouldBeTrue();

        // Check method
        observation.Method.ShouldNotBeNull();
        observation.Method.Coding.First().Display.ShouldBe("PCWG3 Bone Scan Progression Criteria");
    }

    [Fact]
    public void Build_WithNoProgression_CreatesStableDiseaseObservation()
    {
        // Arrange
        var builder = new Pcwg3ProgressionObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_focusReference)
            .WithDetermination("SD")
            .WithConfidence(0.95f)
            .Build();

        // Assert
        observation.ShouldNotBeNull();

        // Check value (no progression - stable disease)
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding.First().Code.ShouldBe("359746009");
        valueCodeableConcept.Coding.First().Display.ShouldBe("Stable disease");
    }

    [Fact]
    public void Build_WithInconclusiveDetermination_CreatesInconclusiveObservation()
    {
        // Arrange
        var builder = new Pcwg3ProgressionObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_focusReference)
            .WithDetermination("Inconclusive")
            .WithConfidence(0.5f)
            .Build();

        // Assert
        observation.ShouldNotBeNull();

        // Check value (inconclusive)
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding.First().Code.ShouldBe("419984006");
        valueCodeableConcept.Coding.First().Display.ShouldStartWith("Inconclusive");
    }

    [Fact]
    public void Build_WithoutPatient_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new Pcwg3ProgressionObservationBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithDevice(_deviceReference)
                .WithFocus(_focusReference)
                .WithDetermination("PD")
                .Build());

        exception.Message.ShouldContain("Patient reference is required");
    }

    [Fact]
    public void Build_WithoutDevice_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new Pcwg3ProgressionObservationBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithPatient(_patientReference)
                .WithFocus(_focusReference)
                .WithDetermination("PD")
                .Build());

        exception.Message.ShouldContain("Device reference is required");
    }

    [Fact]
    public void Build_WithoutDetermination_CreatesObservationWithoutValue()
    {
        // Arrange
        var builder = new Pcwg3ProgressionObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_focusReference)
            .Build();

        // Assert
        observation.ShouldNotBeNull();
        observation.Value.ShouldBeNull(); // No value when determination not set
    }

    [Fact]
    public void WithConfidence_WithInvalidRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var builder = new Pcwg3ProgressionObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => builder.WithConfidence(-0.1f));
        Should.Throw<ArgumentOutOfRangeException>(() => builder.WithConfidence(1.1f));
    }

    [Fact]
    public void Build_WithAdditionalLesions_CreatesComponentCorrectly()
    {
        // Arrange
        var builder = new Pcwg3ProgressionObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_focusReference)
            .WithDetermination("PD")
            .WithAdditionalLesions("Multiple new bone lesions")
            .Build();

        // Assert
        Observation.ComponentComponent? additionalLesionsComponent = observation.Component
            .FirstOrDefault(c => c.Code.Coding.Any(cd => cd.Code == "additional-lesions"));
        additionalLesionsComponent.ShouldNotBeNull();
        ((FhirString)additionalLesionsComponent.Value).Value.ShouldBe("Multiple new bone lesions");
    }

    [Fact]
    public void Build_WithEffectiveDate_SetsDateCorrectly()
    {
        // Arrange
        var builder = new Pcwg3ProgressionObservationBuilder(_configuration);
        var testDate = new DateTime(2025, 4, 23, 14, 30, 0);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_focusReference)
            .WithDetermination("PD")
            .WithEffectiveDate(testDate)
            .Build();

        // Assert
        observation.Effective.ShouldNotBeNull();
        var effectiveDateTime = observation.Effective as FhirDateTime;
        effectiveDateTime.ShouldNotBeNull();
    }

    [Fact]
    public void Build_WithNotes_AddsNotesCorrectly()
    {
        // Arrange
        var builder = new Pcwg3ProgressionObservationBuilder(_configuration);
        var note1 = "Initial bone scan showing possible progression";
        var note2 = "Confirmation scan required per PCWG3 criteria";

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_focusReference)
            .WithDetermination("PD")
            .AddNote(note1)
            .AddNote(note2)
            .Build();

        // Assert
        observation.Note.ShouldNotBeNull();
        observation.Note.Count.ShouldBe(2);
        observation.Note[0].Text.ShouldBe(note1);
        observation.Note[1].Text.ShouldBe(note2);
    }

    [Fact]
    public void Build_WithFocusMultiple_SetsFocusCorrectly()
    {
        // Arrange
        var builder = new Pcwg3ProgressionObservationBuilder(_configuration);
        var focus1 = new ResourceReference("Condition/cancer-1", "Primary Cancer");
        var focus2 = new ResourceReference("Condition/cancer-2", "Metastatic Disease");

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(focus1, focus2)
            .WithDetermination("PD")
            .Build();

        // Assert
        observation.Focus.ShouldNotBeNull();
        observation.Focus.Count.ShouldBe(2);
        observation.Focus[0].Reference.ShouldBe("Condition/cancer-1");
        observation.Focus[1].Reference.ShouldBe("Condition/cancer-2");
    }

    [Fact]
    public void Build_WithEmptyFocus_ThrowsArgumentException()
    {
        // Arrange
        var builder = new Pcwg3ProgressionObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentException>(() => builder.WithFocus());
    }

    [Fact]
    public void Build_HasAiastSecurityLabel()
    {
        // Arrange
        var builder = new Pcwg3ProgressionObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_focusReference)
            .WithDetermination("PD")
            .Build();

        // Assert
        observation.Meta.Security.ShouldNotBeNull();
        observation.Meta.Security.Any(s => s.Code == "AIAST").ShouldBeTrue();
    }
}