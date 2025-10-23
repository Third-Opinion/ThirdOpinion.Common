using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Builders.Conditions;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Extensions;
using ThirdOpinion.Common.Fhir.Helpers;
using ThirdOpinion.Common.Fhir.Models;

namespace ThirdOpinion.Common.Fhir.UnitTests.Builders.Conditions;

public class HsdmAssessmentConditionBuilderTests
{
    private readonly AiInferenceConfiguration _configuration;
    private readonly ResourceReference _conditionReference;
    private readonly ResourceReference _patientReference;
    private readonly ResourceReference _deviceReference;
    private readonly Fact[] _sampleFacts;

    public HsdmAssessmentConditionBuilderTests()
    {
        _configuration = AiInferenceConfiguration.CreateDefault();
        _conditionReference = new ResourceReference("Condition/prostate-cancer-001", "Prostate Cancer");
        _patientReference = new ResourceReference("Patient/test-patient", "Test Patient");
        _deviceReference = new ResourceReference("Device/ai-device", "AI Assessment Device");

        _sampleFacts = new[]
        {
            new Fact
            {
                factGuid = "cc58eb7a-2417-4dab-8782-ec1c99315fd2",
                factDocumentReference = "DocumentReference/123345",
                type = "diagnosis",
                fact = "Metastatic malignant neoplasm to bone",
                @ref = new[] { "1.44" },
                timeRef = "2025-03-20",
                relevance = "Confirms metastatic disease to bone (M1b status)"
            }
        };
    }

    [Fact]
    public void Build_WithNmCSPCBiochemicalRelapse_CreatesCorrectCondition()
    {
        // Arrange
        var builder = new HsdmAssessmentConditionBuilder(_configuration);

        // Act
        var condition = builder
            .WithFocus(_conditionReference)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithHSDMResult(HsdmAssessmentConditionBuilder.HsdmResults.NonMetastaticBiochemicalRelapse)
            .AddFactEvidence(_sampleFacts)
            .WithSummary("Patient shows biochemical relapse with rising PSA following treatment")
            .WithEffectiveDate(new DateTime(2024, 1, 15, 10, 30, 0))
            .Build();

        // Assert
        condition.ShouldNotBeNull();
        condition.ClinicalStatus.Coding[0].Code.ShouldBe("active");
        condition.VerificationStatus.Coding[0].Code.ShouldBe("confirmed");

        // Check category
        condition.Category.ShouldHaveSingleItem();
        condition.Category[0].Coding[0].Code.ShouldBe("encounter-diagnosis");
        condition.Category[0].Coding[0].System.ShouldBe("http://terminology.hl7.org/CodeSystem/condition-category");

        // Check code - should have 3 coding entries for nmCSPC_biochemical_relapse
        condition.Code.Coding.Count.ShouldBe(3);

        // Check SNOMED code
        var snomedCoding = condition.Code.Coding.FirstOrDefault(c => c.System == FhirCodingHelper.Systems.SNOMED_SYSTEM);
        snomedCoding.ShouldNotBeNull();
        snomedCoding.Code.ShouldBe("1197209002");
        snomedCoding.Display.ShouldBe("Castration-sensitive prostate cancer");

        // Check ICD-10 Z19.1 code
        var icd10Z191Coding = condition.Code.Coding.FirstOrDefault(c =>
            c.System == "http://hl7.org/fhir/sid/icd-10-cm" && c.Code == "Z19.1");
        icd10Z191Coding.ShouldNotBeNull();
        icd10Z191Coding.Display.ShouldBe("Hormone sensitive malignancy status");

        // Check ICD-10 R97.21 code
        var icd10R9721Coding = condition.Code.Coding.FirstOrDefault(c =>
            c.System == "http://hl7.org/fhir/sid/icd-10-cm" && c.Code == "R97.21");
        icd10R9721Coding.ShouldNotBeNull();
        icd10R9721Coding.Display.ShouldBe("Rising PSA following treatment for malignant neoplasm of prostate");

        // Check text
        condition.Code.Text.ShouldBe("Castration-Sensitive Prostate Cancer with Biochemical Relapse");

        // Check subject and recorder
        condition.Subject.ShouldBe(_patientReference);
        condition.Recorder.ShouldBe(_deviceReference);

        // Check evidence
        condition.Evidence.ShouldNotBeNull();
        condition.Evidence.Count.ShouldBe(1);

        // Check notes
        condition.Note.ShouldNotBeNull();
        condition.Note.Count.ShouldBe(1);
        condition.Note[0].Text.ToString().ShouldBe("Patient shows biochemical relapse with rising PSA following treatment");

        // Check fact extensions
        condition.Extension.ShouldNotBeNull();
        var factExtensions = condition.Extension.Where(e => e.Url == ClinicalFactExtension.ExtensionUrl);
        factExtensions.ShouldHaveSingleItem();

        // Check AIAST label from base class
        condition.Meta.ShouldNotBeNull();
        condition.Meta.Security.Any(s => s.Code == "AIAST").ShouldBeTrue();
    }

    [Fact]
    public void Build_WithMCSPC_CreatesCorrectCondition()
    {
        // Arrange
        var builder = new HsdmAssessmentConditionBuilder(_configuration);

        // Act
        var condition = builder
            .WithFocus("condition-456", "Advanced Prostate Cancer")
            .WithPatient("patient-123", "John Doe")
            .WithDevice("device-789", "Assessment AI")
            .WithHSDMResult(HsdmAssessmentConditionBuilder.HsdmResults.MetastaticCastrationSensitive)
            .AddFactEvidence(_sampleFacts)
            .WithSummary("Patient has metastatic castration-sensitive prostate cancer")
            .Build();

        // Assert
        condition.Code.Coding.Count.ShouldBe(2); // Only SNOMED + Z19.1, no R97.21

        // Check SNOMED code
        var snomedCoding = condition.Code.Coding.FirstOrDefault(c => c.System == FhirCodingHelper.Systems.SNOMED_SYSTEM);
        snomedCoding.ShouldNotBeNull();
        snomedCoding.Code.ShouldBe("1197209002");

        // Check ICD-10 Z19.1 code
        var icd10Coding = condition.Code.Coding.FirstOrDefault(c =>
            c.System == "http://hl7.org/fhir/sid/icd-10-cm");
        icd10Coding.ShouldNotBeNull();
        icd10Coding.Code.ShouldBe("Z19.1");

        // Should NOT have R97.21 code
        var risingPsaCoding = condition.Code.Coding.FirstOrDefault(c => c.Code == "R97.21");
        risingPsaCoding.ShouldBeNull();

        condition.Code.Text.ShouldBe("Castration-Sensitive Prostate Cancer (mCSPC)");
    }

    [Fact]
    public void Build_WithMCRPC_CreatesCorrectCondition()
    {
        // Arrange
        var builder = new HsdmAssessmentConditionBuilder(_configuration);

        // Act
        var condition = builder
            .WithFocus(_conditionReference)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithHSDMResult(HsdmAssessmentConditionBuilder.HsdmResults.MetastaticCastrationResistant)
            .AddFactEvidence(_sampleFacts)
            .WithSummary("Patient has progressed to castration-resistant disease")
            .Build();

        // Assert
        condition.Code.Coding.Count.ShouldBe(2); // SNOMED + Z19.2

        // Check SNOMED code for castration-resistant
        var snomedCoding = condition.Code.Coding.FirstOrDefault(c => c.System == FhirCodingHelper.Systems.SNOMED_SYSTEM);
        snomedCoding.ShouldNotBeNull();
        snomedCoding.Code.ShouldBe("445848006");
        snomedCoding.Display.ShouldBe("Castration resistant prostate cancer");

        // Check ICD-10 Z19.2 code
        var icd10Coding = condition.Code.Coding.FirstOrDefault(c =>
            c.System == "http://hl7.org/fhir/sid/icd-10-cm");
        icd10Coding.ShouldNotBeNull();
        icd10Coding.Code.ShouldBe("Z19.2");
        icd10Coding.Display.ShouldBe("Hormone resistant malignancy status");

        condition.Code.Text.ShouldBe("Castration-Resistant Prostate Cancer (mCRPC)");
    }

    [Fact]
    public void Build_WithConfidence_AddsConfidenceExtension()
    {
        // Arrange
        var builder = new HsdmAssessmentConditionBuilder(_configuration);

        // Act
        var condition = builder
            .WithFocus(_conditionReference)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithHSDMResult(HsdmAssessmentConditionBuilder.HsdmResults.MetastaticCastrationSensitive)
            .AddFactEvidence(_sampleFacts)
            .WithSummary("Assessment with high confidence")
            .WithConfidence(0.95f)
            .Build();

        // Assert
        condition.Extension.ShouldNotBeNull();
        var confidenceExtension = condition.Extension.FirstOrDefault(e =>
            e.Url == "http://thirdopinion.ai/fhir/StructureDefinition/confidence");
        confidenceExtension.ShouldNotBeNull();
        var confidenceValue = (FhirDecimal)confidenceExtension.Value;
        confidenceValue.Value.ShouldBe(0.95m);
    }

    [Fact]
    public void Build_WithCriteria_AddsCriteriaExtension()
    {
        // Arrange
        var builder = new HsdmAssessmentConditionBuilder(_configuration);

        // Act
        var condition = builder
            .WithFocus(_conditionReference)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithCriteria("HSDM-001", "HSDM Assessment Criteria", "Detailed criteria for HSDM classification")
            .WithHSDMResult(HsdmAssessmentConditionBuilder.HsdmResults.MetastaticCastrationSensitive)
            .AddFactEvidence(_sampleFacts)
            .WithSummary("Assessment using specific criteria")
            .Build();

        // Assert
        condition.Extension.ShouldNotBeNull();
        var criteriaExtension = condition.Extension.FirstOrDefault(e =>
            e.Url == "http://thirdopinion.ai/fhir/StructureDefinition/assessment-criteria");
        criteriaExtension.ShouldNotBeNull();

        var idExtension = criteriaExtension.Extension.FirstOrDefault(e => e.Url == "id");
        idExtension.ShouldNotBeNull();
        ((FhirString)idExtension.Value).Value.ShouldBe("HSDM-001");

        var displayExtension = criteriaExtension.Extension.FirstOrDefault(e => e.Url == "display");
        displayExtension.ShouldNotBeNull();
        ((FhirString)displayExtension.Value).Value.ShouldBe("HSDM Assessment Criteria");

        var descriptionExtension = criteriaExtension.Extension.FirstOrDefault(e => e.Url == "description");
        descriptionExtension.ShouldNotBeNull();
        ((FhirString)descriptionExtension.Value).Value.ShouldBe("Detailed criteria for HSDM classification");
    }

    [Fact]
    public void Build_WithMultipleFacts_CreatesMultipleFactExtensions()
    {
        // Arrange
        var builder = new HsdmAssessmentConditionBuilder(_configuration);
        var multipleFacts = new[]
        {
            new Fact
            {
                factGuid = "fact-1",
                factDocumentReference = "DocumentReference/doc1",
                type = "diagnosis",
                fact = "Primary tumor diagnosis",
                relevance = "Establishes primary diagnosis"
            },
            new Fact
            {
                factGuid = "fact-2",
                factDocumentReference = "DocumentReference/doc2",
                type = "treatment",
                fact = "Previous treatment history",
                relevance = "Shows treatment response pattern"
            }
        };

        // Act
        var condition = builder
            .WithFocus(_conditionReference)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithHSDMResult(HsdmAssessmentConditionBuilder.HsdmResults.MetastaticCastrationSensitive)
            .AddFactEvidence(multipleFacts)
            .WithSummary("Assessment based on multiple facts")
            .Build();

        // Assert
        condition.Extension.ShouldNotBeNull();
        var factExtensions = condition.Extension.Where(e => e.Url == ClinicalFactExtension.ExtensionUrl);
        factExtensions.Count().ShouldBe(2);

        // Check evidence count (should include document references from facts)
        condition.Evidence.Count.ShouldBe(2);
    }

    [Fact]
    public void WithHSDMResult_InvalidResult_ThrowsArgumentException()
    {
        // Arrange
        var builder = new HsdmAssessmentConditionBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentException>(() => builder.WithHSDMResult("invalid-result"));
    }

    [Fact]
    public void WithConfidence_InvalidRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var builder = new HsdmAssessmentConditionBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => builder.WithConfidence(1.5f));
        Should.Throw<ArgumentOutOfRangeException>(() => builder.WithConfidence(-0.1f));
    }

    [Fact]
    public void Build_MissingPatient_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new HsdmAssessmentConditionBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithFocus(_conditionReference)
                .WithDevice(_deviceReference)
                .WithHSDMResult(HsdmAssessmentConditionBuilder.HsdmResults.MetastaticCastrationSensitive)
                .AddFactEvidence(_sampleFacts)
                .WithSummary("Test summary")
                .Build());

        exception.Message.ShouldContain("Patient reference is required");
    }

    [Fact]
    public void Build_MissingDevice_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new HsdmAssessmentConditionBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithFocus(_conditionReference)
                .WithPatient(_patientReference)
                .WithHSDMResult(HsdmAssessmentConditionBuilder.HsdmResults.MetastaticCastrationSensitive)
                .AddFactEvidence(_sampleFacts)
                .WithSummary("Test summary")
                .Build());

        exception.Message.ShouldContain("Device reference is required");
    }

    [Fact]
    public void Build_MissingFocus_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new HsdmAssessmentConditionBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithPatient(_patientReference)
                .WithDevice(_deviceReference)
                .WithHSDMResult(HsdmAssessmentConditionBuilder.HsdmResults.MetastaticCastrationSensitive)
                .AddFactEvidence(_sampleFacts)
                .WithSummary("Test summary")
                .Build());

        exception.Message.ShouldContain("Focus reference is required");
    }

    [Fact]
    public void Build_MissingHSDMResult_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new HsdmAssessmentConditionBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithFocus(_conditionReference)
                .WithPatient(_patientReference)
                .WithDevice(_deviceReference)
                .AddFactEvidence(_sampleFacts)
                .WithSummary("Test summary")
                .Build());

        exception.Message.ShouldContain("HSDM result is required");
    }

    [Fact]
    public void Build_MissingFactEvidence_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new HsdmAssessmentConditionBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithFocus(_conditionReference)
                .WithPatient(_patientReference)
                .WithDevice(_deviceReference)
                .WithHSDMResult(HsdmAssessmentConditionBuilder.HsdmResults.MetastaticCastrationSensitive)
                .WithSummary("Test summary")
                .Build());

        exception.Message.ShouldContain("Fact evidence is required");
    }

    [Fact]
    public void Build_MissingSummary_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new HsdmAssessmentConditionBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithFocus(_conditionReference)
                .WithPatient(_patientReference)
                .WithDevice(_deviceReference)
                .WithHSDMResult(HsdmAssessmentConditionBuilder.HsdmResults.MetastaticCastrationSensitive)
                .AddFactEvidence(_sampleFacts)
                .Build());

        exception.Message.ShouldContain("Summary note is required");
    }

    [Fact]
    public void WithFocus_InvalidConditionReference_ThrowsArgumentException()
    {
        // Arrange
        var builder = new HsdmAssessmentConditionBuilder(_configuration);
        var invalidReference = new ResourceReference("Patient/123", "Not a condition");

        // Act & Assert
        Should.Throw<ArgumentException>(() => builder.WithFocus(invalidReference));
    }

    [Fact]
    public void AddFactEvidence_EmptyArray_ThrowsArgumentException()
    {
        // Arrange
        var builder = new HsdmAssessmentConditionBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentException>(() => builder.AddFactEvidence());
    }

    [Fact]
    public void WithSummary_EmptyString_ThrowsArgumentException()
    {
        // Arrange
        var builder = new HsdmAssessmentConditionBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentException>(() => builder.WithSummary(""));
        Should.Throw<ArgumentException>(() => builder.WithSummary("   "));
    }
}