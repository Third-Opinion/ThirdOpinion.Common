using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using ThirdOpinion.Common.Fhir.Builders.Observations;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;
using static ThirdOpinion.Common.Fhir.Builders.Observations.PsaProgressionObservationBuilder;

namespace ThirdOpinion.Common.Fhir.UnitTests.Builders.Observations;

public class PsaProgressionObservationBuilderTests
{
    private readonly AiInferenceConfiguration _configuration;
    private readonly ResourceReference _patientReference;
    private readonly ResourceReference _deviceReference;
    private readonly ResourceReference _conditionReference;

    public PsaProgressionObservationBuilderTests()
    {
        _configuration = AiInferenceConfiguration.CreateDefault();
        _patientReference = new ResourceReference("Patient/test-patient", "Test Patient");
        _deviceReference = new ResourceReference("Device/ai-device", "PSA Analysis Device");
        _conditionReference = new ResourceReference("Condition/prostate-cancer", "Prostate Cancer");
    }

    [Fact]
    public void Build_WithPCWG3Criteria_CreatesCorrectObservation()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);
        var psaNadir = new ResourceReference("Observation/psa-nadir", "PSA Nadir");
        var psaCurrent = new ResourceReference("Observation/psa-current", "Current PSA");

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_conditionReference)
            .WithCriteria(CriteriaType.PCWG3, "3.0")
            .AddPsaEvidence(psaNadir, "nadir", 2.0m)
            .AddPsaEvidence(psaCurrent, "current", 3.5m)  // 75% increase from nadir
            .WithProgression(true)
            .WithEffectiveDate(new DateTime(2024, 1, 15))
            .Build();

        // Assert
        observation.ShouldNotBeNull();
        observation.Status.ShouldBe(ObservationStatus.Final);

        // Check category
        observation.Category.ShouldHaveSingleItem();
        observation.Category[0].Coding[0].Code.ShouldBe("laboratory");

        // Check LOINC code
        observation.Code.Coding[0].System.ShouldBe(FhirCodingHelper.Systems.LOINC_SYSTEM);
        observation.Code.Coding[0].Code.ShouldBe("97509-4");
        observation.Code.Coding[0].Display.ShouldBe("PSA progression");

        // Check focus
        observation.Focus.ShouldNotBeNull();
        observation.Focus.Count.ShouldBe(1);
        observation.Focus[0].ShouldBe(_conditionReference);

        // Check method contains PCWG3
        observation.Method.ShouldNotBeNull();
        observation.Method.Coding[0].Code.ShouldContain("pcwg3");
        observation.Method.Coding[0].Display.ShouldContain("PCWG3");

        // Check progression value
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding[0].Code.ShouldBe("277022003"); // Progressive disease

        // Check PSA evidence in derivedFrom
        observation.DerivedFrom.Count.ShouldBe(2);
        observation.DerivedFrom[0].ShouldBe(psaNadir);
        observation.DerivedFrom[1].ShouldBe(psaCurrent);

        // Check calculated components
        observation.Component.ShouldNotBeNull();

        // Should have percentage change component
        var percentageComponent = observation.Component.FirstOrDefault(c =>
            c.Code.Coding.Any(cd => cd.Code == "percentage-change"));
        percentageComponent.ShouldNotBeNull();
        var percentageValue = percentageComponent.Value as Quantity;
        percentageValue.ShouldNotBeNull();
        percentageValue.Value.ShouldBe(75m); // (3.5 - 2.0) / 2.0 * 100 = 75%

        // Should have absolute change component
        var absoluteComponent = observation.Component.FirstOrDefault(c =>
            c.Code.Coding.Any(cd => cd.Code == "absolute-change"));
        absoluteComponent.ShouldNotBeNull();
        var absoluteValue = absoluteComponent.Value as Quantity;
        absoluteValue.ShouldNotBeNull();
        absoluteValue.Value.ShouldBe(1.5m); // 3.5 - 2.0 = 1.5

        // Should have threshold met component (75% > 25% threshold)
        var thresholdComponent = observation.Component.FirstOrDefault(c =>
            c.Code.Coding.Any(cd => cd.Code == "threshold-met"));
        thresholdComponent.ShouldNotBeNull();
        var thresholdValue = thresholdComponent.Value as FhirBoolean;
        thresholdValue.ShouldNotBeNull();
        thresholdValue.Value.ShouldBe(true);
    }

    [Fact]
    public void Build_WithThirdOpinionIOCriteria_CreatesCorrectObservation()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);
        var psaBaseline = new ResourceReference("Observation/psa-baseline", "Baseline PSA");
        var psaCurrent = new ResourceReference("Observation/psa-current", "Current PSA");

        // Act
        var observation = builder
            .WithPatient("patient-123", "John Doe")
            .WithDevice("device-456", "Analysis AI")
            .WithCriteria(CriteriaType.ThirdOpinionIO, "2.0")
            .AddPsaEvidence(psaBaseline, "baseline", 5.0m)
            .AddPsaEvidence(psaCurrent, "current", 6.0m)  // 20% increase
            .WithProgression(false) // Not considered progression
            .Build();

        // Assert
        observation.Method.ShouldNotBeNull();
        observation.Method.Coding[0].Code.ShouldNotContain("pcwg3");
        observation.Method.Coding[0].Display.ShouldContain("ThirdOpinion.io");

        // Check stable disease value
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding[0].Code.ShouldBe("359746009"); // Stable disease

        // Check percentage calculation
        var percentageComponent = observation.Component.FirstOrDefault(c =>
            c.Code.Coding.Any(cd => cd.Code == "percentage-change"));
        var percentageValue = percentageComponent?.Value as Quantity;
        percentageValue.ShouldNotBeNull();
        percentageValue.Value.ShouldBe(20m); // (6.0 - 5.0) / 5.0 * 100 = 20%
    }

    [Fact]
    public void AddPsaEvidence_StoresValuesForCalculation()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddPsaEvidence(new ResourceReference("Observation/1"), "baseline", 4.5m)
            .AddPsaEvidence(new ResourceReference("Observation/2"), "nadir", 1.2m)
            .AddPsaEvidence(new ResourceReference("Observation/3"), "current", 2.4m)
            .WithProgression(true)
            .WithCriteria(CriteriaType.PCWG3, "1.0")
            .Build();

        // Assert - PCWG3 calculates from nadir
        var percentageComponent = observation.Component.FirstOrDefault(c =>
            c.Code.Coding.Any(cd => cd.Code == "percentage-change"));
        var percentageValue = percentageComponent?.Value as Quantity;
        percentageValue.ShouldNotBeNull();
        percentageValue.Value.ShouldBe(100m); // (2.4 - 1.2) / 1.2 * 100 = 100%

        var absoluteComponent = observation.Component.FirstOrDefault(c =>
            c.Code.Coding.Any(cd => cd.Code == "absolute-change"));
        var absoluteValue = absoluteComponent?.Value as Quantity;
        absoluteValue.ShouldNotBeNull();
        absoluteValue.Value.ShouldBe(1.2m); // 2.4 - 1.2 = 1.2
    }

    [Fact]
    public void WithFocus_AcceptsMultipleReferences()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);
        var condition1 = new ResourceReference("Condition/1", "Primary Cancer");
        var condition2 = new ResourceReference("Condition/2", "Metastatic Disease");

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(condition1, condition2)
            .AddPsaEvidence(new ResourceReference("Observation/psa"), "current", 5.0m)
            .WithProgression(false)
            .Build();

        // Assert
        observation.Focus.ShouldNotBeNull();
        observation.Focus.Count.ShouldBe(2);
        observation.Focus[0].ShouldBe(condition1);
        observation.Focus[1].ShouldBe(condition2);
    }

    [Fact]
    public void AddValidUntilComponent_AddsCorrectComponent()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);
        var validUntil = new DateTime(2024, 6, 30);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddPsaEvidence(new ResourceReference("Observation/psa"), "current", 5.0m)
            .WithProgression(false)
            .AddValidUntilComponent(validUntil)
            .Build();

        // Assert
        var validUntilComponent = observation.Component.FirstOrDefault(c =>
            c.Code.Coding.Any(cd => cd.Code == "valid-until"));
        validUntilComponent.ShouldNotBeNull();
        var period = validUntilComponent.Value as Period;
        period.ShouldNotBeNull();
        period.End.ShouldContain("2024-06-30");
    }

    [Fact]
    public void AddThresholdMetComponent_AddsCorrectComponent()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddPsaEvidence(new ResourceReference("Observation/psa"), "current", 5.0m)
            .WithProgression(true)
            .AddThresholdMetComponent(true)
            .Build();

        // Assert
        var thresholdComponent = observation.Component.FirstOrDefault(c =>
            c.Code.Coding.Any(cd => cd.Code == "threshold-met"));
        thresholdComponent.ShouldNotBeNull();
        var boolValue = thresholdComponent.Value as FhirBoolean;
        boolValue.ShouldNotBeNull();
        boolValue.Value.ShouldBe(true);
    }

    [Fact]
    public void AddDetailedAnalysisNote_AddsStringComponent()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);
        var analysisNote = "PSA velocity exceeds expected range based on treatment response";

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddPsaEvidence(new ResourceReference("Observation/psa"), "current", 5.0m)
            .WithProgression(true)
            .AddDetailedAnalysisNote(analysisNote)
            .Build();

        // Assert
        var analysisComponent = observation.Component.FirstOrDefault(c =>
            c.Code.Coding.Any(cd => cd.Code == "analysis-note"));
        analysisComponent.ShouldNotBeNull();
        var stringValue = analysisComponent.Value as FhirString;
        stringValue.ShouldNotBeNull();
        stringValue.Value.ShouldBe(analysisNote);
    }

    [Fact]
    public void Build_WithoutPatient_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithDevice(_deviceReference)
                .AddPsaEvidence(new ResourceReference("Observation/psa"), "current", 5.0m)
                .WithProgression(true)
                .Build());

        exception.Message.ShouldContain("Patient reference is required");
    }

    [Fact]
    public void Build_WithoutDevice_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithPatient(_patientReference)
                .AddPsaEvidence(new ResourceReference("Observation/psa"), "current", 5.0m)
                .WithProgression(true)
                .Build());

        exception.Message.ShouldContain("Device reference is required");
    }

    [Fact]
    public void Build_WithoutProgression_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithPatient(_patientReference)
                .WithDevice(_deviceReference)
                .AddPsaEvidence(new ResourceReference("Observation/psa"), "current", 5.0m)
                .Build());

        exception.Message.ShouldContain("Progression status is required");
    }

    [Fact]
    public void Build_WithoutPsaEvidence_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithPatient(_patientReference)
                .WithDevice(_deviceReference)
                .WithProgression(true)
                .Build());

        exception.Message.ShouldContain("At least one PSA evidence reference is required");
    }

    [Fact]
    public void AddPsaEvidence_NullObservation_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.AddPsaEvidence(null!, "baseline", 5.0m));
    }

    [Fact]
    public void AddPsaEvidence_EmptyRole_ThrowsArgumentException()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.AddPsaEvidence(new ResourceReference("Observation/psa"), "", 5.0m));
    }

    [Fact]
    public void WithCriteria_EmptyVersion_ThrowsArgumentException()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.WithCriteria(CriteriaType.PCWG3, ""));
    }

    [Fact]
    public void WithFocus_NoReferences_ThrowsArgumentException()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.WithFocus());
    }

    [Fact]
    public void PCWG3_AutomaticallyAddsThresholdComponent()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);

        // Act - 30% increase, above PCWG3 25% threshold
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithCriteria(CriteriaType.PCWG3, "1.0")
            .AddPsaEvidence(new ResourceReference("Observation/nadir"), "nadir", 10.0m)
            .AddPsaEvidence(new ResourceReference("Observation/current"), "current", 13.0m)
            .WithProgression(true)
            .Build();

        // Assert
        var thresholdComponent = observation.Component.FirstOrDefault(c =>
            c.Code.Coding.Any(cd => cd.Code == "threshold-met"));
        thresholdComponent.ShouldNotBeNull();
        var boolValue = thresholdComponent.Value as FhirBoolean;
        boolValue.Value.ShouldBe(true); // 30% > 25% threshold
    }

    [Fact]
    public void PCWG3_BelowThreshold_SetsThresholdMetToFalse()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);

        // Act - 20% increase, below PCWG3 25% threshold
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithCriteria(CriteriaType.PCWG3, "1.0")
            .AddPsaEvidence(new ResourceReference("Observation/nadir"), "nadir", 10.0m)
            .AddPsaEvidence(new ResourceReference("Observation/current"), "current", 12.0m)
            .WithProgression(false)
            .Build();

        // Assert
        var thresholdComponent = observation.Component.FirstOrDefault(c =>
            c.Code.Coding.Any(cd => cd.Code == "threshold-met"));
        thresholdComponent.ShouldNotBeNull();
        var boolValue = thresholdComponent.Value as FhirBoolean;
        boolValue.Value.ShouldBe(false); // 20% < 25% threshold
    }

    [Fact]
    public void FluentInterface_SupportsCompleteChaining()
    {
        // Arrange & Act
        var observation = new PsaProgressionObservationBuilder(_configuration)
            .WithInferenceId("psa-prog-001")
            .WithPatient("Patient/p123", "Jane Smith")
            .WithDevice("Device/d456", "PSA Analyzer")
            .WithFocus(_conditionReference)
            .WithCriteria(CriteriaType.ThirdOpinionIO, "3.0")
            .AddPsaEvidence(new ResourceReference("Observation/psa1"), "baseline", 4.0m)
            .AddPsaEvidence(new ResourceReference("Observation/psa2"), "current", 5.5m)
            .WithProgression(true)
            .AddValidUntilComponent(DateTime.Now.AddMonths(3))
            .AddThresholdMetComponent(true)
            .AddDetailedAnalysisNote("Significant PSA rise detected")
            .WithEffectiveDate(new DateTime(2024, 2, 1))
            .AddNote("Clinical review recommended")
            .AddDerivedFrom("Procedure/biopsy1", "Recent Biopsy")
            .Build();

        // Assert
        observation.Id.ShouldBe("psa-prog-001");
        observation.Subject.Reference.ShouldBe("Patient/p123");
        observation.Device.Reference.ShouldBe("Device/d456");
        observation.Focus[0].ShouldBe(_conditionReference);
        observation.DerivedFrom.Count.ShouldBeGreaterThan(2); // PSA evidence + derivedFrom
        observation.Note.Count.ShouldBe(1);
        observation.Component.Count.ShouldBeGreaterThanOrEqualTo(5); // Multiple components
    }

    [Fact]
    public void Build_GeneratesValidFhirJson()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithCriteria(CriteriaType.PCWG3, "1.0")
            .AddPsaEvidence(new ResourceReference("Observation/psa-nadir"), "nadir", 2.5m)
            .AddPsaEvidence(new ResourceReference("Observation/psa-current"), "current", 3.5m)
            .WithProgression(true)
            .AddDetailedAnalysisNote("PSA progression confirmed per PCWG3 criteria")
            .WithEffectiveDate(new DateTime(2024, 1, 15))
            .Build();

        // Act
        var serializer = new FhirJsonSerializer(new SerializerSettings { Pretty = true });
        var json = serializer.SerializeToString(observation);

        // Assert
        json.ShouldNotBeNullOrEmpty();
        json.ShouldContain("\"resourceType\": \"Observation\"");
        json.ShouldContain("\"status\": \"final\"");
        json.ShouldContain("\"code\": \"laboratory\""); // Category
        json.ShouldContain("97509-4"); // LOINC code
        json.ShouldContain("277022003"); // Progressive disease SNOMED
        json.ShouldContain("pcwg3"); // Method
        json.ShouldContain("component"); // Components array
        json.ShouldContain("percentage-change");
        json.ShouldContain("\"code\": \"AIAST\""); // AIAST security label

        // Verify it can be deserialized
        var parser = new FhirJsonParser();
        var deserializedObs = parser.Parse<Observation>(json);
        deserializedObs.ShouldNotBeNull();
        deserializedObs.Status.ShouldBe(ObservationStatus.Final);
        deserializedObs.Component.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void AddNote_MultipleNotes_AddsAnnotations()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddPsaEvidence(new ResourceReference("Observation/psa"), "current", 5.0m)
            .WithProgression(true)
            .AddNote("First clinical note")
            .AddNote("Second observation")
            .AddNote("Third remark")
            .Build();

        // Assert
        observation.Note.ShouldNotBeNull();
        observation.Note.Count.ShouldBe(3);
        observation.Note[0].Text.ShouldBe("First clinical note");
        observation.Note.All(n => !string.IsNullOrEmpty(n.Time)).ShouldBeTrue();
    }

    [Fact]
    public void CalculatesCorrectly_WithZeroBaseline()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);

        // Act - baseline is 0, should handle division by zero
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddPsaEvidence(new ResourceReference("Observation/baseline"), "baseline", 0m)
            .AddPsaEvidence(new ResourceReference("Observation/current"), "current", 2.0m)
            .WithProgression(true)
            .Build();

        // Assert - Should have absolute change but no percentage change
        var absoluteComponent = observation.Component.FirstOrDefault(c =>
            c.Code.Coding.Any(cd => cd.Code == "absolute-change"));
        absoluteComponent.ShouldNotBeNull();
        var absoluteValue = absoluteComponent.Value as Quantity;
        absoluteValue.Value.ShouldBe(2.0m);

        // Percentage component should not be added when baseline is 0
        var percentageComponent = observation.Component.FirstOrDefault(c =>
            c.Code.Coding.Any(cd => cd.Code == "percentage-change"));
        percentageComponent.ShouldBeNull();
    }
}