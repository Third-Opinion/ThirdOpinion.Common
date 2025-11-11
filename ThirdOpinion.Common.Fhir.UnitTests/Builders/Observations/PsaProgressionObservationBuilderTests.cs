using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using ThirdOpinion.Common.Fhir.Builders.Observations;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;
using static ThirdOpinion.Common.Fhir.Builders.Observations.PsaProgressionObservationBuilder;

namespace ThirdOpinion.Common.Fhir.UnitTests.Builders.Observations;

public class PsaProgressionObservationBuilderTests
{
    private readonly ResourceReference _conditionReference;
    private readonly AiInferenceConfiguration _configuration;
    private readonly ResourceReference _deviceReference;
    private readonly ResourceReference _patientReference;

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
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_conditionReference)
            .WithCriteria(CriteriaType.PCWG3, "1.0")
            .AddPsaEvidence(psaNadir, "nadir", 2.0m)
            .AddPsaEvidence(psaCurrent, "current", 3.5m) // 75% increase from nadir
            .WithProgression("true")
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

        // Check nadir reference
        observation.DerivedFrom[0].Reference.ShouldBe(psaNadir.Reference);
        observation.DerivedFrom[0].Display.ShouldBe(psaNadir.Display);
        observation.DerivedFrom[0].Extension.ShouldNotBeNull();
        observation.DerivedFrom[0].Extension.Count.ShouldBe(2); // role + value

        Extension? nadirRoleExt = observation.DerivedFrom[0].Extension
            .FirstOrDefault(e => e.Url.Contains("psa-evidence-role"));
        nadirRoleExt.ShouldNotBeNull();
        ((FhirString)nadirRoleExt.Value).Value.ShouldBe("nadir");

        Extension? nadirValueExt = observation.DerivedFrom[0].Extension
            .FirstOrDefault(e => e.Url.Contains("psa-evidence-value"));
        nadirValueExt.ShouldNotBeNull();
        ((Quantity)nadirValueExt.Value).Value.ShouldBe(2.0m);

        // Check current reference
        observation.DerivedFrom[1].Reference.ShouldBe(psaCurrent.Reference);
        observation.DerivedFrom[1].Display.ShouldBe(psaCurrent.Display);
        observation.DerivedFrom[1].Extension.ShouldNotBeNull();
        observation.DerivedFrom[1].Extension.Count.ShouldBe(2); // role + value

        Extension? currentRoleExt = observation.DerivedFrom[1].Extension
            .FirstOrDefault(e => e.Url.Contains("psa-evidence-role"));
        currentRoleExt.ShouldNotBeNull();
        ((FhirString)currentRoleExt.Value).Value.ShouldBe("current");

        Extension? currentValueExt = observation.DerivedFrom[1].Extension
            .FirstOrDefault(e => e.Url.Contains("psa-evidence-value"));
        currentValueExt.ShouldNotBeNull();
        ((Quantity)currentValueExt.Value).Value.ShouldBe(3.5m);

        // Check calculated components
        observation.Component.ShouldNotBeNull();

        // Should have percentage change component
        Observation.ComponentComponent? percentageComponent
            = observation.Component.FirstOrDefault(c =>
                c.Code.Coding.Any(cd => cd.Code == "percentage-change"));
        percentageComponent.ShouldNotBeNull();
        var percentageValue = percentageComponent.Value as Quantity;
        percentageValue.ShouldNotBeNull();
        percentageValue.Value.ShouldBe(75m); // (3.5 - 2.0) / 2.0 * 100 = 75%

        // Should have absolute change component
        Observation.ComponentComponent? absoluteComponent
            = observation.Component.FirstOrDefault(c =>
                c.Code.Coding.Any(cd => cd.Code == "absolute-change"));
        absoluteComponent.ShouldNotBeNull();
        var absoluteValue = absoluteComponent.Value as Quantity;
        absoluteValue.ShouldNotBeNull();
        absoluteValue.Value.ShouldBe(1.5m); // 3.5 - 2.0 = 1.5

        // Should have threshold met component (75% > 25% threshold)
        Observation.ComponentComponent? thresholdComponent
            = observation.Component.FirstOrDefault(c =>
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
        Observation observation = builder
            .WithPatient("patient-123", "John Doe")
            .WithDevice("device-456", "Analysis AI")
            .WithCriteria(CriteriaType.ThirdOpinionIO, "2.0")
            .AddPsaEvidence(psaBaseline, "baseline", 5.0m)
            .AddPsaEvidence(psaCurrent, "current", 6.0m) // 20% increase
            .WithProgression("false") // Not considered progression
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
        Observation.ComponentComponent? percentageComponent
            = observation.Component.FirstOrDefault(c =>
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
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddPsaEvidence(new ResourceReference("Observation/1"), "baseline", 4.5m)
            .AddPsaEvidence(new ResourceReference("Observation/2"), "nadir", 1.2m)
            .AddPsaEvidence(new ResourceReference("Observation/3"), "current", 2.4m)
            .WithProgression("true")
            .WithCriteria(CriteriaType.PCWG3, "1.0")
            .Build();

        // Assert - PCWG3 calculates from nadir
        Observation.ComponentComponent? percentageComponent
            = observation.Component.FirstOrDefault(c =>
                c.Code.Coding.Any(cd => cd.Code == "percentage-change"));
        var percentageValue = percentageComponent?.Value as Quantity;
        percentageValue.ShouldNotBeNull();
        percentageValue.Value.ShouldBe(100m); // (2.4 - 1.2) / 1.2 * 100 = 100%

        Observation.ComponentComponent? absoluteComponent
            = observation.Component.FirstOrDefault(c =>
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
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(condition1, condition2)
            .AddPsaEvidence(new ResourceReference("Observation/psa"), "current", 5.0m)
            .WithProgression("false")
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
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddPsaEvidence(new ResourceReference("Observation/psa"), "current", 5.0m)
            .WithProgression("false")
            .AddValidUntilComponent(validUntil)
            .Build();

        // Assert
        Observation.ComponentComponent? validUntilComponent
            = observation.Component.FirstOrDefault(c =>
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
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddPsaEvidence(new ResourceReference("Observation/psa"), "current", 5.0m)
            .WithProgression("true")
            .AddThresholdMetComponent(true)
            .Build();

        // Assert
        Observation.ComponentComponent? thresholdComponent
            = observation.Component.FirstOrDefault(c =>
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
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddPsaEvidence(new ResourceReference("Observation/psa"), "current", 5.0m)
            .WithProgression("true")
            .AddDetailedAnalysisNote(analysisNote)
            .Build();

        // Assert
        Observation.ComponentComponent? analysisComponent
            = observation.Component.FirstOrDefault(c =>
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
                .WithProgression("true")
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
                .WithProgression("true")
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
                .WithProgression("true")
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
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithCriteria(CriteriaType.PCWG3, "1.0")
            .AddPsaEvidence(new ResourceReference("Observation/nadir"), "nadir", 10.0m)
            .AddPsaEvidence(new ResourceReference("Observation/current"), "current", 13.0m)
            .WithProgression("true")
            .Build();

        // Assert
        Observation.ComponentComponent? thresholdComponent
            = observation.Component.FirstOrDefault(c =>
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
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithCriteria(CriteriaType.PCWG3, "1.0")
            .AddPsaEvidence(new ResourceReference("Observation/nadir"), "nadir", 10.0m)
            .AddPsaEvidence(new ResourceReference("Observation/current"), "current", 12.0m)
            .WithProgression("false")
            .Build();

        // Assert
        Observation.ComponentComponent? thresholdComponent
            = observation.Component.FirstOrDefault(c =>
                c.Code.Coding.Any(cd => cd.Code == "threshold-met"));
        thresholdComponent.ShouldNotBeNull();
        var boolValue = thresholdComponent.Value as FhirBoolean;
        boolValue.Value.ShouldBe(false); // 20% < 25% threshold
    }

    [Fact]
    public void FluentInterface_SupportsCompleteChaining()
    {
        // Arrange & Act
        Observation observation = new PsaProgressionObservationBuilder(_configuration)
            .WithFhirResourceId("psa-prog-001")
            .WithPatient("Patient/p123", "Jane Smith")
            .WithDevice("Device/d456", "PSA Analyzer")
            .WithFocus(_conditionReference)
            .WithCriteria(CriteriaType.ThirdOpinionIO, "3.0")
            .AddPsaEvidence(new ResourceReference("Observation/psa1"), "baseline", 4.0m)
            .AddPsaEvidence(new ResourceReference("Observation/psa2"), "current", 5.5m)
            .WithProgression("true")
            .AddValidUntilComponent(DateTime.Now.AddMonths(3))
            .AddThresholdMetComponent(true)
            .AddDetailedAnalysisNote("Significant PSA rise detected")
            .WithEffectiveDate(new DateTime(2024, 2, 1))
            .AddNote("Clinical review recommended")
            .AddDerivedFrom("Procedure/biopsy1", "Recent Biopsy")
            .Build();

        // Assert
        observation.Id.ShouldBe("to.ai-psa-prog-001");
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
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithCriteria(CriteriaType.PCWG3, "1.0")
            .AddPsaEvidence(new ResourceReference("Observation/psa-nadir"), "nadir", 2.5m)
            .AddPsaEvidence(new ResourceReference("Observation/psa-current"), "current", 3.5m)
            .WithProgression("true")
            .AddDetailedAnalysisNote("PSA progression confirmed per PCWG3 criteria")
            .WithEffectiveDate(new DateTime(2024, 1, 15))
            .Build();

        // Act
        var serializer = new FhirJsonSerializer(new SerializerSettings { Pretty = true });
        string json = serializer.SerializeToString(observation);

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
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddPsaEvidence(new ResourceReference("Observation/psa"), "current", 5.0m)
            .WithProgression("true")
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
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddPsaEvidence(new ResourceReference("Observation/baseline"), "baseline", 0m)
            .AddPsaEvidence(new ResourceReference("Observation/current"), "current", 2.0m)
            .WithProgression("true")
            .Build();

        // Assert - Should have absolute change but no percentage change
        Observation.ComponentComponent? absoluteComponent
            = observation.Component.FirstOrDefault(c =>
                c.Code.Coding.Any(cd => cd.Code == "absolute-change"));
        absoluteComponent.ShouldNotBeNull();
        var absoluteValue = absoluteComponent.Value as Quantity;
        absoluteValue.Value.ShouldBe(2.0m);

        // Percentage component should not be added when baseline is 0
        Observation.ComponentComponent? percentageComponent
            = observation.Component.FirstOrDefault(c =>
                c.Code.Coding.Any(cd => cd.Code == "percentage-change"));
        percentageComponent.ShouldBeNull();
    }

    [Fact]
    public void BuildCondition_WithProgression_CreatesCorrectCondition()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_conditionReference)
            .WithCriteria(CriteriaType.PCWG3, "3.0")
            .AddPsaEvidence(new ResourceReference("Observation/psa-nadir"), "nadir", 2.0m)
            .AddPsaEvidence(new ResourceReference("Observation/psa-current"), "current", 3.5m)
            .WithProgression("true")
            .WithEffectiveDate(new DateTime(2024, 1, 15))
            .WithConfidence(0.85f)
            .AddNote("PSA progression detected")
            .Build();

        // Act
        Condition? condition = builder.BuildCondition(observation);

        // Assert
        condition.ShouldNotBeNull();
        condition.ClinicalStatus.Coding[0].Code.ShouldBe("active");
        condition.VerificationStatus.Coding[0].Code.ShouldBe("confirmed");

        // Check category
        condition.Category.ShouldHaveSingleItem();
        condition.Category[0].Coding[0].Code.ShouldBe("encounter-diagnosis");

        // Check SNOMED code for PSA progression
        condition.Code.Coding.ShouldContain(c =>
            c.System == FhirCodingHelper.Systems.SNOMED_SYSTEM);
        Coding? snomedCode
            = condition.Code.Coding.First(c => c.System == FhirCodingHelper.Systems.SNOMED_SYSTEM);
        snomedCode.Code.ShouldBe("428119001");
        snomedCode.Display.ShouldBe("Procedure to assess prostate specific antigen progression");

        // Check ICD-10 code
        condition.Code.Coding.ShouldContain(c => c.System == "http://hl7.org/fhir/sid/icd-10-cm");
        Coding? icd10Code
            = condition.Code.Coding.First(c => c.System == "http://hl7.org/fhir/sid/icd-10-cm");
        icd10Code.Code.ShouldBe("R97.21");
        icd10Code.Display.ShouldBe(
            "Rising PSA following treatment for malignant neoplasm of prostate");

        // Check text
        condition.Code.Text.ShouldBe("PSA Progression");

        // Check subject and recorder
        condition.Subject.ShouldBe(_patientReference);
        condition.Recorder.ShouldBe(_deviceReference);

        // Check evidence references the observation
        condition.Evidence.ShouldNotBeNull();
        condition.Evidence.Count.ShouldBe(1);
        condition.Evidence[0].Detail.Count.ShouldBe(1);
        condition.Evidence[0].Detail[0].Reference.ShouldBe($"Observation/{observation.Id}");
        condition.Evidence[0].Detail[0].Display.ShouldBe("PSA Progression Assessment");

        // Check AI inference extension
        condition.Extension.ShouldNotBeNull();
        Extension? aiInferredExtension = condition.Extension.FirstOrDefault(e =>
            e.Url == "http://thirdopinion.ai/fhir/StructureDefinition/ai-inferred");
        aiInferredExtension.ShouldNotBeNull();
        var aiInferredValue = aiInferredExtension.Value as FhirBoolean;
        aiInferredValue.ShouldNotBeNull();
        aiInferredValue.Value.ShouldBe(true);

        // Check confidence extension
        Extension? confidenceExtension = condition.Extension.FirstOrDefault(e =>
            e.Url == "http://thirdopinion.ai/fhir/StructureDefinition/confidence");
        confidenceExtension.ShouldNotBeNull();
        var confidenceValue = confidenceExtension.Value as FhirDecimal;
        confidenceValue.ShouldNotBeNull();
        confidenceValue.Value.ShouldBe(0.85m);

        // Check notes
        condition.Note.ShouldNotBeNull();
        condition.Note.Count.ShouldBe(1);
        condition.Note[0].Text.ShouldBe("PSA progression detected");
    }

    [Fact]
    public void BuildCondition_WithoutProgression_ReturnsNull()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_conditionReference)
            .AddPsaEvidence(new ResourceReference("Observation/psa"), "current", 2.0m)
            .WithProgression("false") // No progression
            .Build();

        // Act
        Condition? condition = builder.BuildCondition(observation);

        // Assert
        condition.ShouldBeNull();
    }

    [Fact]
    public void BuildWithCondition_WithProgression_ReturnsBothResources()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);

        // Act
        (Observation observation, Condition? condition) = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_conditionReference)
            .WithCriteria(CriteriaType.ThirdOpinionIO, "2.0")
            .AddPsaEvidence(new ResourceReference("Observation/psa"), "current", 5.0m)
            .WithProgression("true")
            .WithConfidence(0.92f)
            .BuildWithCondition();

        // Assert
        observation.ShouldNotBeNull();
        condition.ShouldNotBeNull();

        // Verify observation
        observation.Status.ShouldBe(ObservationStatus.Final);
        var progressionValue = observation.Value as CodeableConcept;
        progressionValue.Coding[0].Code.ShouldBe("277022003"); // Progressive disease

        // Verify condition
        condition.ClinicalStatus.Coding[0].Code.ShouldBe("active");
        condition.Evidence[0].Detail[0].Reference.ShouldBe($"Observation/{observation.Id}");

        // Both should have same confidence
        var obsConfidence = observation.Component.FirstOrDefault(c =>
            c.Code.Text == "AI Confidence Score")?.Value as Quantity;
        var condConfidence = condition.Extension.FirstOrDefault(e =>
                e.Url == "http://thirdopinion.ai/fhir/StructureDefinition/confidence")
            ?.Value as FhirDecimal;

        obsConfidence.ShouldNotBeNull();
        condConfidence.ShouldNotBeNull();
        condConfidence.Value.ShouldBe(0.92m);
    }

    [Fact]
    public void BuildWithCondition_WithoutProgression_ReturnsObservationOnly()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);

        // Act
        (Observation observation, Condition? condition) = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddPsaEvidence(new ResourceReference("Observation/psa"), "current", 2.0m)
            .WithProgression("false") // No progression
            .BuildWithCondition();

        // Assert
        observation.ShouldNotBeNull();
        condition.ShouldBeNull();

        // Verify observation shows stable disease
        var stableValue = observation.Value as CodeableConcept;
        stableValue.Coding[0].Code.ShouldBe("359746009"); // Stable disease
    }

    [Fact]
    public void BuildCondition_WithCriteriaExtension_IncludesCriteriaInCondition()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithCriteria("test-criteria-id", "Test Criteria Display")
            .AddPsaEvidence(new ResourceReference("Observation/psa"), "current", 5.0m)
            .WithProgression("true")
            .Build();

        // Act
        Condition? condition = builder.BuildCondition(observation);

        // Assert
        condition.ShouldNotBeNull();
        condition.Extension.ShouldNotBeNull();

        Extension? criteriaExtension = condition.Extension.FirstOrDefault(e =>
            e.Url == "http://thirdopinion.ai/fhir/StructureDefinition/assessment-criteria");
        criteriaExtension.ShouldNotBeNull();

        Extension? idExtension = criteriaExtension.Extension.FirstOrDefault(e => e.Url == "id");
        idExtension.ShouldNotBeNull();
        var idValue = idExtension.Value as FhirString;
        idValue.Value.ShouldBe("test-criteria-id");

        Extension? displayExtension
            = criteriaExtension.Extension.FirstOrDefault(e => e.Url == "display");
        displayExtension.ShouldNotBeNull();
        var displayValue = displayExtension.Value as FhirString;
        displayValue.Value.ShouldBe("Test Criteria Display");
    }

    [Fact]
    public void WithProgression_Unknown_CreatesCorrectObservation()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddPsaEvidence(new ResourceReference("Observation/psa"), "current", 5.0m)
            .WithProgression("unknown")
            .Build();

        // Assert
        observation.ShouldNotBeNull();
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding[0].Code.ShouldBe("261665006"); // Unknown SNOMED code
        valueCodeableConcept.Coding[0].Display.ShouldBe("Unknown");
    }

    [Fact]
    public void WithProgression_InvalidValue_ThrowsArgumentException()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() =>
            builder.WithProgression("maybe"));

        exception.Message.ShouldContain("must be 'true', 'false', or 'unknown'");
    }

    [Fact]
    public void WithProgression_NullValue_ThrowsArgumentException()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.WithProgression(null!));
    }

    [Fact]
    public void WithProgression_EmptyString_ThrowsArgumentException()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.WithProgression(""));
    }

    [Fact]
    public void WithProgression_CaseInsensitive_WorksCorrectly()
    {
        // Arrange
        var builder1 = new PsaProgressionObservationBuilder(_configuration);
        var builder2 = new PsaProgressionObservationBuilder(_configuration);
        var builder3 = new PsaProgressionObservationBuilder(_configuration);

        // Act & Assert - Should accept various cases
        Observation obs1 = builder1
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddPsaEvidence(new ResourceReference("Observation/psa"), "current", 5.0m)
            .WithProgression("TRUE")
            .Build();
        var value1 = obs1.Value as CodeableConcept;
        value1?.Coding[0].Code.ShouldBe("277022003"); // Progressive disease

        Observation obs2 = builder2
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddPsaEvidence(new ResourceReference("Observation/psa"), "current", 5.0m)
            .WithProgression("False")
            .Build();
        var value2 = obs2.Value as CodeableConcept;
        value2?.Coding[0].Code.ShouldBe("359746009"); // Stable disease

        Observation obs3 = builder3
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddPsaEvidence(new ResourceReference("Observation/psa"), "current", 5.0m)
            .WithProgression("UnKnOwN")
            .Build();
        var value3 = obs3.Value as CodeableConcept;
        value3?.Coding[0].Code.ShouldBe("261665006"); // Unknown
    }

    [Fact]
    public void AddPsaEvidence_CustomUnit_StoresCorrectUnit()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);

        // Act - Use custom unit
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddPsaEvidence(new ResourceReference("Observation/psa1"), "baseline", 4.5m, "ug/L")
            .AddPsaEvidence(new ResourceReference("Observation/psa2"), "current", 6.0m, "ug/L")
            .WithProgression("true")
            .Build();

        // Assert - Check custom unit is preserved
        observation.DerivedFrom[0].Extension.ShouldNotBeNull();
        Extension? valueExt1 = observation.DerivedFrom[0].Extension
            .FirstOrDefault(e => e.Url.Contains("psa-evidence-value"));
        valueExt1.ShouldNotBeNull();
        var quantity1 = valueExt1.Value as Quantity;
        quantity1.ShouldNotBeNull();
        quantity1.Value.ShouldBe(4.5m);
        quantity1.Unit.ShouldBe("ug/L");
        quantity1.Code.ShouldBe("ug/L");

        observation.DerivedFrom[1].Extension.ShouldNotBeNull();
        Extension? valueExt2 = observation.DerivedFrom[1].Extension
            .FirstOrDefault(e => e.Url.Contains("psa-evidence-value"));
        valueExt2.ShouldNotBeNull();
        var quantity2 = valueExt2.Value as Quantity;
        quantity2.ShouldNotBeNull();
        quantity2.Value.ShouldBe(6.0m);
        quantity2.Unit.ShouldBe("ug/L");
        quantity2.Code.ShouldBe("ug/L");
    }

    [Fact]
    public void AddPsaEvidence_DefaultUnit_UsesNgPerMl()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);

        // Act - Don't specify unit, should default to ng/mL
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddPsaEvidence(new ResourceReference("Observation/psa"), "current", 5.0m)
            .WithProgression("true")
            .Build();

        // Assert - Check default unit is ng/mL
        Extension? valueExt = observation.DerivedFrom[0].Extension
            .FirstOrDefault(e => e.Url.Contains("psa-evidence-value"));
        valueExt.ShouldNotBeNull();
        var quantity = valueExt.Value as Quantity;
        quantity.ShouldNotBeNull();
        quantity.Value.ShouldBe(5.0m);
        quantity.Unit.ShouldBe("ng/mL");
        quantity.Code.ShouldBe("ng/mL");
    }

    [Fact]
    public void AddPsaEvidence_NullUnit_OmitsUnitInformation()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);

        // Act - Explicitly pass null for unit
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddPsaEvidence(new ResourceReference("Observation/psa"), "current", 5.0m, null)
            .WithProgression("true")
            .Build();

        // Assert - Check no unit information is present
        Extension? valueExt = observation.DerivedFrom[0].Extension
            .FirstOrDefault(e => e.Url.Contains("psa-evidence-value"));
        valueExt.ShouldNotBeNull();
        var quantity = valueExt.Value as Quantity;
        quantity.ShouldNotBeNull();
        quantity.Value.ShouldBe(5.0m);
        quantity.Unit.ShouldBeNull();
        quantity.Code.ShouldBeNull();
        quantity.System.ShouldBeNull();
    }

    [Fact]
    public void WithMostRecentPsaValue_AddsCorrectComponent()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);
        var mostRecentDate = new DateTime(2025, 1, 1);
        var mostRecentText
            = "The most recent result used in the analysis is 2025-01-01 with a value of 7 ng/mL";
        var mostRecentObservation
            = new ResourceReference("Observation/some-measurement-3", "Most Recent PSA");

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddPsaEvidence(new ResourceReference("Observation/psa"), "current", 5.0m)
            .WithProgression("true")
            .WithMostRecentPsaValue(mostRecentDate, mostRecentText, mostRecentObservation)
            .Build();

        // Assert
        observation.Component.ShouldNotBeNull();
        Observation.ComponentComponent? mostRecentComponent
            = observation.Component.FirstOrDefault(c =>
                c.Code.Coding.Any(cd => cd.Code == "mostRecentMeasurement_v1"));

        mostRecentComponent.ShouldNotBeNull();

        // Verify code
        mostRecentComponent.Code.Coding[0].System.ShouldBe("https://thirdopinion.io/result-code");
        mostRecentComponent.Code.Coding[0].Code.ShouldBe("mostRecentMeasurement_v1");
        mostRecentComponent.Code.Coding[0].Display
            .ShouldBe("The most recent measurement used in the analysis");
        mostRecentComponent.Code.Text.ShouldBe(mostRecentText);

        // Verify value
        var dateTimeValue = mostRecentComponent.Value as FhirDateTime;
        dateTimeValue.ShouldNotBeNull();
        dateTimeValue.Value.ShouldStartWith("2025-01-01");

        // Verify extension
        mostRecentComponent.Extension.ShouldNotBeNull();
        mostRecentComponent.Extension.Count.ShouldBe(1);
        mostRecentComponent.Extension[0].Url
            .ShouldBe("https://thirdopinion.io/fhir/StructureDefinition/source-observation");

        var extensionReference = mostRecentComponent.Extension[0].Value as ResourceReference;
        extensionReference.ShouldNotBeNull();
        extensionReference.Reference.ShouldBe("Observation/some-measurement-3");
        extensionReference.Display.ShouldBe("The most recent result used in the analysis");
    }

    [Fact]
    public void WithMostRecentPsaValue_NullText_ThrowsArgumentException()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);
        var mostRecentDate = new DateTime(2025, 1, 1);
        var mostRecentObservation = new ResourceReference("Observation/some-measurement-3");

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() =>
            builder.WithMostRecentPsaValue(mostRecentDate, null!, mostRecentObservation));

        exception.Message.ShouldContain("Most recent PSA value text cannot be null or empty");
    }

    [Fact]
    public void WithMostRecentPsaValue_EmptyText_ThrowsArgumentException()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);
        var mostRecentDate = new DateTime(2025, 1, 1);
        var mostRecentObservation = new ResourceReference("Observation/some-measurement-3");

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() =>
            builder.WithMostRecentPsaValue(mostRecentDate, "", mostRecentObservation));

        exception.Message.ShouldContain("Most recent PSA value text cannot be null or empty");
    }

    [Fact]
    public void WithMostRecentPsaValue_NullObservation_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);
        var mostRecentDate = new DateTime(2025, 1, 1);
        var mostRecentText
            = "The most recent result used in the analysis is 2025-01-01 with a value of 7 ng/mL";

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.WithMostRecentPsaValue(mostRecentDate, mostRecentText, null!));
    }

    [Fact]
    public void WithMostRecentPsaValue_IntegratesWithCompleteBuilder()
    {
        // Arrange & Act
        Observation observation = new PsaProgressionObservationBuilder(_configuration)
            .WithPatient("Patient/p123", "Jane Smith")
            .WithDevice("Device/d456", "PSA Analyzer")
            .WithFocus(_conditionReference)
            .WithCriteria(CriteriaType.PCWG3, "1.0")
            .AddPsaEvidence(new ResourceReference("Observation/psa-nadir"), "nadir", 2.0m)
            .AddPsaEvidence(new ResourceReference("Observation/psa-current"), "current", 7.0m)
            .WithProgression("true")
            .WithMostRecentPsaValue(
                new DateTime(2025, 1, 1),
                "The most recent result used in the analysis is 2025-01-01 with a value of 7 ng/mL",
                new ResourceReference("Observation/psa-current", "Current PSA Measurement"))
            .AddValidUntilComponent(DateTime.Now.AddMonths(3))
            .AddDetailedAnalysisNote("Significant PSA progression detected")
            .Build();

        // Assert
        observation.ShouldNotBeNull();
        observation.Component.ShouldNotBeNull();

        // Verify most recent component exists alongside other components
        Observation.ComponentComponent? mostRecentComponent
            = observation.Component.FirstOrDefault(c =>
                c.Code.Coding.Any(cd => cd.Code == "mostRecentMeasurement_v1"));
        mostRecentComponent.ShouldNotBeNull();

        // Verify other components still exist
        Observation.ComponentComponent? percentageComponent
            = observation.Component.FirstOrDefault(c =>
                c.Code.Coding.Any(cd => cd.Code == "percentage-change"));
        percentageComponent.ShouldNotBeNull();

        Observation.ComponentComponent? validUntilComponent
            = observation.Component.FirstOrDefault(c =>
                c.Code.Coding.Any(cd => cd.Code == "valid-until"));
        validUntilComponent.ShouldNotBeNull();

        Observation.ComponentComponent? analysisComponent
            = observation.Component.FirstOrDefault(c =>
                c.Code.Coding.Any(cd => cd.Code == "analysis-note"));
        analysisComponent.ShouldNotBeNull();
    }

    [Fact]
    public void WithMostRecentPsaValue_GeneratesValidFhirJson()
    {
        // Arrange
        var builder = new PsaProgressionObservationBuilder(_configuration);
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddPsaEvidence(new ResourceReference("Observation/psa"), "current", 7.0m)
            .WithProgression("true")
            .WithMostRecentPsaValue(
                new DateTime(2025, 1, 1),
                "The most recent result used in the analysis is 2025-01-01 with a value of 7 ng/mL",
                new ResourceReference("Observation/some-measurement-3", "Most Recent Measurement"))
            .Build();

        // Act
        var serializer = new FhirJsonSerializer(new SerializerSettings { Pretty = true });
        string json = serializer.SerializeToString(observation);

        // Assert
        json.ShouldNotBeNullOrEmpty();
        json.ShouldContain("\"component\"");
        json.ShouldContain("mostRecentMeasurement_v1");
        json.ShouldContain("https://thirdopinion.io/result-code");
        json.ShouldContain("The most recent measurement used in the analysis");
        json.ShouldContain("2025-01-01");
        json.ShouldContain("https://thirdopinion.io/fhir/StructureDefinition/source-observation");
        json.ShouldContain("Observation/some-measurement-3");

        // Verify it can be deserialized
        var parser = new FhirJsonParser();
        var deserializedObs = parser.Parse<Observation>(json);
        deserializedObs.ShouldNotBeNull();

        Observation.ComponentComponent? mostRecentComponent
            = deserializedObs.Component.FirstOrDefault(c =>
                c.Code.Coding.Any(cd => cd.Code == "mostRecentMeasurement_v1"));
        mostRecentComponent.ShouldNotBeNull();
        mostRecentComponent.Extension.Count.ShouldBe(1);
    }
}