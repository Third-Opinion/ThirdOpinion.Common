using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using ThirdOpinion.Common.Fhir.Builders.Observations;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;

namespace ThirdOpinion.Common.Fhir.UnitTests.Builders.Observations;

public class CspcAssessmentObservationBuilderTests
{
    private readonly ResourceReference _conditionReference;
    private readonly AiInferenceConfiguration _configuration;
    private readonly ResourceReference _deviceReference;
    private readonly ResourceReference _patientReference;

    public CspcAssessmentObservationBuilderTests()
    {
        _configuration = AiInferenceConfiguration.CreateDefault();
        _conditionReference
            = new ResourceReference("Condition/prostate-cancer-001", "Prostate Cancer");
        _patientReference = new ResourceReference("Patient/test-patient", "Test Patient");
        _deviceReference = new ResourceReference("Device/ai-device", "AI Assessment Device");
    }

    [Fact]
    public void Build_WithCastrationSensitive_CreatesCorrectObservation()
    {
        // Arrange
        var builder = new CspcAssessmentObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithFocus(_conditionReference)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithCastrationSensitive(true)
            .WithEffectiveDate(new DateTime(2024, 1, 15, 10, 30, 0))
            .Build();

        // Assert
        observation.ShouldNotBeNull();
        observation.Status.ShouldBe(ObservationStatus.Final);

        // Check category
        observation.Category.ShouldHaveSingleItem();
        observation.Category[0].Coding[0].Code.ShouldBe("exam");
        observation.Category[0].Coding[0].System
            .ShouldBe("http://terminology.hl7.org/CodeSystem/observation-category");

        // Check LOINC code
        observation.Code.Coding[0].System.ShouldBe(FhirCodingHelper.Systems.LOINC_SYSTEM);
        observation.Code.Coding[0].Code.ShouldBe("21889-1");
        observation.Code.Coding[0].Display.ShouldBe("Cancer disease status");

        // Check focus
        observation.Focus.ShouldNotBeNull();
        observation.Focus.Count.ShouldBe(1);
        observation.Focus[0].ShouldBe(_conditionReference);

        // Check subject and device
        observation.Subject.ShouldBe(_patientReference);
        observation.Device.ShouldBe(_deviceReference);

        // Check value has BOTH SNOMED and ICD-10 codes
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding.Count.ShouldBe(2);

        // Check SNOMED code
        Coding? snomedCoding
            = valueCodeableConcept.Coding.FirstOrDefault(c =>
                c.System == FhirCodingHelper.Systems.SNOMED_SYSTEM);
        snomedCoding.ShouldNotBeNull();
        snomedCoding.Code.ShouldBe("1197209002");
        snomedCoding.Display.ShouldBe("Castration sensitive prostate cancer");

        // Check ICD-10 code
        Coding? icd10Coding
            = valueCodeableConcept.Coding.FirstOrDefault(c =>
                c.System == "http://hl7.org/fhir/sid/icd-10-cm");
        icd10Coding.ShouldNotBeNull();
        icd10Coding.Code.ShouldBe("Z19.1");
        icd10Coding.Display.ShouldBe("Hormone sensitive status");

        // Check AIAST label from base class
        observation.Meta.ShouldNotBeNull();
        observation.Meta.Security.Any(s => s.Code == "AIAST").ShouldBeTrue();
    }

    [Fact]
    public void Build_WithCastrationResistant_CreatesCorrectObservation()
    {
        // Arrange
        var builder = new CspcAssessmentObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithFocus("condition-456", "Advanced Prostate Cancer")
            .WithPatient("patient-123", "John Doe")
            .WithDevice("device-789", "Assessment AI")
            .WithCastrationSensitive(false) // Castration-resistant
            .Build();

        // Assert
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding.Count.ShouldBe(2);

        // Check SNOMED code for resistant
        Coding? snomedCoding
            = valueCodeableConcept.Coding.FirstOrDefault(c =>
                c.System == FhirCodingHelper.Systems.SNOMED_SYSTEM);
        snomedCoding.ShouldNotBeNull();
        snomedCoding.Code.ShouldBe("445848006");
        snomedCoding.Display.ShouldBe("Castration resistant prostate cancer");

        // Check ICD-10 code for resistant
        Coding? icd10Coding
            = valueCodeableConcept.Coding.FirstOrDefault(c =>
                c.System == "http://hl7.org/fhir/sid/icd-10-cm");
        icd10Coding.ShouldNotBeNull();
        icd10Coding.Code.ShouldBe("Z19.2");
        icd10Coding.Display.ShouldBe("Hormone resistant status");

        // Check references were formatted correctly
        observation.Focus[0].Reference.ShouldBe("Condition/condition-456");
        observation.Subject.Reference.ShouldBe("Patient/patient-123");
        observation.Device.Reference.ShouldBe("Device/device-789");
    }

    [Fact]
    public void WithFocus_RequiresConditionReference()
    {
        // Arrange
        var builder = new CspcAssessmentObservationBuilder(_configuration);
        var patientRef = new ResourceReference("Patient/123", "Wrong Type");
        var observationRef = new ResourceReference("Observation/456", "Also Wrong");

        // Act & Assert - Patient reference should throw
        var ex1 = Should.Throw<ArgumentException>(() => builder.WithFocus(patientRef));
        ex1.Message.ShouldContain("Focus must reference a Condition resource");

        // Observation reference should also throw
        var ex2 = Should.Throw<ArgumentException>(() => builder.WithFocus(observationRef));
        ex2.Message.ShouldContain("Focus must reference a Condition resource");

        // Condition reference should work
        var conditionRef = new ResourceReference("Condition/789", "Correct Type");
        Should.NotThrow(() => builder.WithFocus(conditionRef));
    }

    [Fact]
    public void WithFocus_NullReference_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new CspcAssessmentObservationBuilder(_configuration);

        // Act & Assert
        var ex = Should.Throw<ArgumentNullException>(() => builder.WithFocus(null!));
        ex.ParamName.ShouldBe("focus");
    }

    [Fact]
    public void Build_WithoutFocus_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new CspcAssessmentObservationBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithPatient(_patientReference)
                .WithDevice(_deviceReference)
                .WithCastrationSensitive(true)
                .Build());

        exception.Message.ShouldBe(
            "CSPC assessment requires focus reference to existing Condition. Call WithFocus() before Build().");
    }

    [Fact]
    public void Build_WithoutPatient_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new CspcAssessmentObservationBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithFocus(_conditionReference)
                .WithDevice(_deviceReference)
                .WithCastrationSensitive(true)
                .Build());

        exception.Message.ShouldContain("Patient reference is required");
    }

    [Fact]
    public void Build_WithoutDevice_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new CspcAssessmentObservationBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithFocus(_conditionReference)
                .WithPatient(_patientReference)
                .WithCastrationSensitive(true)
                .Build());

        exception.Message.ShouldContain("Device reference is required");
    }

    [Fact]
    public void Build_WithoutCastrationSensitivity_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new CspcAssessmentObservationBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithFocus(_conditionReference)
                .WithPatient(_patientReference)
                .WithDevice(_deviceReference)
                .Build());

        exception.Message.ShouldContain("Castration sensitivity status is required");
    }

    [Fact]
    public void WithInterpretation_SetsInterpretationText()
    {
        // Arrange
        var builder = new CspcAssessmentObservationBuilder(_configuration);
        var interpretation = "High confidence assessment based on PSA levels and imaging";

        // Act
        Observation observation = builder
            .WithFocus(_conditionReference)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithCastrationSensitive(true)
            .WithInterpretation(interpretation)
            .Build();

        // Assert
        observation.Interpretation.ShouldNotBeNull();
        observation.Interpretation.Count.ShouldBe(1);
        observation.Interpretation[0].Text.ShouldBe(interpretation);
    }

    [Fact]
    public void AddEvidence_MultipleReferences_AddsAllToDerivedFrom()
    {
        // Arrange
        var builder = new CspcAssessmentObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithFocus(_conditionReference)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithCastrationSensitive(true)
            .AddEvidence("DocumentReference/psa-test", "PSA Test Results")
            .AddEvidence("ImagingStudy/ct-scan", "CT Scan")
            .AddEvidence(new ResourceReference("DiagnosticReport/pathology", "Pathology Report"))
            .Build();

        // Assert
        observation.DerivedFrom.ShouldNotBeNull();
        observation.DerivedFrom.Count.ShouldBe(3);
        observation.DerivedFrom[0].Reference.ShouldBe("DocumentReference/psa-test");
        observation.DerivedFrom[0].Display.ShouldBe("PSA Test Results");
        observation.DerivedFrom[1].Reference.ShouldBe("ImagingStudy/ct-scan");
        observation.DerivedFrom[2].Reference.ShouldBe("DiagnosticReport/pathology");
    }

    [Fact]
    public void AddNote_MultipleNotes_AddsAnnotations()
    {
        // Arrange
        var builder = new CspcAssessmentObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithFocus(_conditionReference)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithCastrationSensitive(false)
            .AddNote("Patient has been on ADT for 18 months")
            .AddNote("Rising PSA despite treatment indicates resistance")
            .AddNote("Consider alternative therapies")
            .Build();

        // Assert
        observation.Note.ShouldNotBeNull();
        observation.Note.Count.ShouldBe(3);
        observation.Note[0].Text.ShouldBe("Patient has been on ADT for 18 months");
        observation.Note[1].Text.ShouldBe("Rising PSA despite treatment indicates resistance");
        observation.Note[2].Text.ShouldBe("Consider alternative therapies");
        observation.Note.All(n => !string.IsNullOrEmpty(n.Time)).ShouldBeTrue();
    }

    [Fact]
    public void WithCriteria_SetsCriteriaInMethod()
    {
        // Arrange
        var builder = new CspcAssessmentObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithFocus(_conditionReference)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithCastrationSensitive(true)
            .WithCriteria("cspc-criteria-v2", "CSPC Assessment Criteria Version 2.0",
                "http://example.org/criteria")
            .Build();

        // Assert
        observation.Method.ShouldNotBeNull();
        observation.Method.Coding[0].System.ShouldBe("http://example.org/criteria");
        observation.Method.Coding[0].Code.ShouldBe("cspc-criteria-v2");
        observation.Method.Coding[0].Display.ShouldBe("CSPC Assessment Criteria Version 2.0");
        observation.Method.Text.ShouldBe("CSPC Assessment Criteria Version 2.0");
    }

    [Fact]
    public void WithInferenceId_SetsCustomId()
    {
        // Arrange
        var builder = new CspcAssessmentObservationBuilder(_configuration);
        var customId = "cspc-assessment-12345";

        // Act
        Observation observation = builder
            .WithInferenceId(customId)
            .WithFocus(_conditionReference)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithCastrationSensitive(true)
            .Build();

        // Assert
        observation.Id.ShouldBe(customId);
    }

    [Fact]
    public void Build_WithoutExplicitInferenceId_AutoGeneratesId()
    {
        // Arrange
        var builder = new CspcAssessmentObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithFocus(_conditionReference)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithCastrationSensitive(false)
            .Build();

        // Assert
        observation.Id.ShouldNotBeNullOrEmpty();
        observation.Id.ShouldStartWith("to.ai-inference-");
    }

    [Fact]
    public void FluentInterface_SupportsCompleteChaining()
    {
        // Arrange & Act
        Observation observation = new CspcAssessmentObservationBuilder(_configuration)
            .WithInferenceId("test-cspc-001")
            .WithFocus("Condition/cancer-123", "Prostate Cancer Diagnosis")
            .WithPatient("Patient/p456", "Jane Smith")
            .WithDevice("Device/d789", "AI Assessment System")
            .WithCastrationSensitive(true)
            .WithInterpretation("Assessment indicates castration-sensitive disease")
            .WithCriteria("criteria-001", "Test Criteria")
            .AddEvidence("DocumentReference/doc1", "Clinical Note")
            .AddEvidence("Observation/psa1", "PSA Level")
            .WithEffectiveDate(new DateTime(2024, 2, 1))
            .AddNote("Initial assessment")
            .AddDerivedFrom("Procedure/biopsy1", "Biopsy Procedure")
            .Build();

        // Assert
        observation.Id.ShouldBe("test-cspc-001");
        observation.Focus[0].Reference.ShouldBe("Condition/cancer-123");
        observation.Subject.Reference.ShouldBe("Patient/p456");
        observation.Device.Reference.ShouldBe("Device/d789");
        observation.DerivedFrom.Count.ShouldBe(3); // 2 evidence + 1 derivedFrom
        observation.Note.Count.ShouldBe(1);
        observation.Method.ShouldNotBeNull();
        observation.Interpretation[0].Text
            .ShouldBe("Assessment indicates castration-sensitive disease");
    }

    [Fact]
    public void WithPatient_NullReference_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new CspcAssessmentObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => builder.WithPatient(null!));
        Should.Throw<ArgumentException>(() => builder.WithPatient(""));
        Should.Throw<ArgumentException>(() => builder.WithPatient("   "));
    }

    [Fact]
    public void WithDevice_NullReference_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new CspcAssessmentObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => builder.WithDevice(null!));
        Should.Throw<ArgumentException>(() => builder.WithDevice(""));
        Should.Throw<ArgumentException>(() => builder.WithDevice("   "));
    }

    [Fact]
    public void WithFocus_StringOverload_HandlesConditionPrefix()
    {
        // Arrange
        var builder = new CspcAssessmentObservationBuilder(_configuration);

        // Act - with prefix
        Observation obs1 = builder
            .WithFocus("Condition/123", "Test Condition")
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithCastrationSensitive(true)
            .Build();

        // Act - without prefix
        var builder2 = new CspcAssessmentObservationBuilder(_configuration);
        Observation obs2 = builder2
            .WithFocus("456", "Another Condition")
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithCastrationSensitive(true)
            .Build();

        // Assert
        obs1.Focus[0].Reference.ShouldBe("Condition/123");
        obs2.Focus[0].Reference.ShouldBe("Condition/456");
    }

    [Fact]
    public void WithEffectiveDate_MultipleForms_SetsCorrectly()
    {
        // Arrange
        var builder1 = new CspcAssessmentObservationBuilder(_configuration);
        var builder2 = new CspcAssessmentObservationBuilder(_configuration);
        var dateTime = new DateTime(2024, 3, 15, 14, 30, 0);
        var dateTimeOffset = new DateTimeOffset(2024, 3, 15, 14, 30, 0, TimeSpan.FromHours(-5));

        // Act
        Observation observation1 = builder1
            .WithFocus(_conditionReference)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithCastrationSensitive(true)
            .WithEffectiveDate(dateTime)
            .Build();

        Observation observation2 = builder2
            .WithFocus(_conditionReference)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithCastrationSensitive(false)
            .WithEffectiveDate(dateTimeOffset)
            .Build();

        // Assert
        observation1.Effective.ShouldBeOfType<FhirDateTime>();
        observation2.Effective.ShouldBeOfType<FhirDateTime>();
    }

    [Fact]
    public void Build_GeneratesValidFhirJson()
    {
        // Arrange
        var builder = new CspcAssessmentObservationBuilder(_configuration);
        Observation observation = builder
            .WithFocus(_conditionReference)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithCastrationSensitive(true)
            .WithInterpretation("High confidence assessment")
            .WithEffectiveDate(new DateTime(2024, 1, 15))
            .AddEvidence("DocumentReference/evidence1", "Supporting Document")
            .AddNote("CSPC confirmed based on biomarkers")
            .Build();

        // Act
        var serializer = new FhirJsonSerializer(new SerializerSettings { Pretty = true });
        string json = serializer.SerializeToString(observation);

        // Assert
        json.ShouldNotBeNullOrEmpty();
        json.ShouldContain("\"resourceType\": \"Observation\"");
        json.ShouldContain("\"status\": \"final\"");
        json.ShouldContain("\"code\": \"exam\""); // Category
        json.ShouldContain("21889-1"); // LOINC code
        json.ShouldContain("1197209002"); // SNOMED code for CSPC
        json.ShouldContain("Z19.1"); // ICD-10 code
        json.ShouldContain("focus");
        json.ShouldContain("interpretation");
        json.ShouldContain("\"code\": \"AIAST\""); // AIAST security label

        // Verify it can be deserialized
        var parser = new FhirJsonParser();
        var deserializedObs = parser.Parse<Observation>(json);
        deserializedObs.ShouldNotBeNull();
        deserializedObs.Status.ShouldBe(ObservationStatus.Final);
        deserializedObs.Focus.Count.ShouldBe(1);
    }

    [Fact]
    public void AddEvidence_EmptyString_IsIgnored()
    {
        // Arrange
        var builder = new CspcAssessmentObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithFocus(_conditionReference)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithCastrationSensitive(true)
            .AddEvidence("", "Display")
            .AddEvidence("   ", "Display2")
            .Build();

        // Assert
        observation.DerivedFrom.ShouldNotBeNull();
        observation.DerivedFrom.Count.ShouldBe(0);
    }

    [Fact]
    public void AddNote_EmptyString_IsIgnored()
    {
        // Arrange
        var builder = new CspcAssessmentObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithFocus(_conditionReference)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithCastrationSensitive(false)
            .AddNote("")
            .AddNote("   ")
            .Build();

        // Assert
        observation.Note.ShouldNotBeNull();
        observation.Note.Count.ShouldBe(0);
    }

    [Fact]
    public void WithInterpretation_EmptyString_IsIgnored()
    {
        // Arrange
        var builder = new CspcAssessmentObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithFocus(_conditionReference)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithCastrationSensitive(true)
            .WithInterpretation("")
            .WithInterpretation("   ")
            .Build();

        // Assert
        observation.Interpretation.ShouldBeEmpty();
    }
}