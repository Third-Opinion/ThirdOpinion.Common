using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Builders.Conditions;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Extensions;
using ThirdOpinion.Common.Fhir.Helpers;
using ThirdOpinion.Common.Fhir.Models;
using Xunit.Abstractions;

namespace ThirdOpinion.Common.Fhir.UnitTests.Builders.Conditions;

public class HsdmAssessmentConditionBuilderTests
{
    private readonly ResourceReference _conditionReference;
    private readonly AiInferenceConfiguration _configuration;
    private readonly ResourceReference _deviceReference;
    private readonly ResourceReference _patientReference;
    private readonly Fact[] _sampleFacts;
    private readonly ITestOutputHelper _output;

    public HsdmAssessmentConditionBuilderTests(ITestOutputHelper output)
    {
        _output = output;
        _configuration = AiInferenceConfiguration.CreateDefault();
        _conditionReference
            = new ResourceReference("Condition/prostate-cancer-001", "Prostate Cancer");
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
        Condition condition = builder
            .WithFocus(_conditionReference)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithHSDMResult(HsdmAssessmentConditionBuilder.HsdmResults
                .NonMetastaticBiochemicalRelapse)
            .AddFactEvidence(_sampleFacts)
            .AddNote("Patient shows biochemical relapse with rising PSA following treatment")
            .WithEffectiveDate(new DateTime(2024, 1, 15, 10, 30, 0))
            .Build();

        // Assert
        condition.ShouldNotBeNull();
        condition.ClinicalStatus.Coding[0].Code.ShouldBe("active");
        condition.VerificationStatus.Coding[0].Code.ShouldBe("confirmed");

        // Check category
        condition.Category.ShouldHaveSingleItem();
        condition.Category[0].Coding[0].Code.ShouldBe("encounter-diagnosis");
        condition.Category[0].Coding[0].System
            .ShouldBe("http://terminology.hl7.org/CodeSystem/condition-category");

        // Check code - should have 3 coding entries for nmCSPC_biochemical_relapse
        condition.Code.Coding.Count.ShouldBe(3);

        // Check SNOMED code
        Coding? snomedCoding
            = condition.Code.Coding.FirstOrDefault(c =>
                c.System == FhirCodingHelper.Systems.SNOMED_SYSTEM);
        snomedCoding.ShouldNotBeNull();
        snomedCoding.Code.ShouldBe("1197209002");
        snomedCoding.Display.ShouldBe("Castration-sensitive prostate cancer");

        // Check ICD-10 Z19.1 code
        Coding? icd10Z191Coding = condition.Code.Coding.FirstOrDefault(c =>
            c.System == "http://hl7.org/fhir/sid/icd-10-cm" && c.Code == "Z19.1");
        icd10Z191Coding.ShouldNotBeNull();
        icd10Z191Coding.Display.ShouldBe("Hormone sensitive malignancy status");

        // Check ICD-10 R97.21 code
        Coding? icd10R9721Coding = condition.Code.Coding.FirstOrDefault(c =>
            c.System == "http://hl7.org/fhir/sid/icd-10-cm" && c.Code == "R97.21");
        icd10R9721Coding.ShouldNotBeNull();
        icd10R9721Coding.Display.ShouldBe(
            "Rising PSA following treatment for malignant neoplasm of prostate");

        // Check text
        condition.Code.Text.ShouldBe(
            "Castration-Sensitive Prostate Cancer with Biochemical Relapse");

        // Check subject and recorder
        condition.Subject.ShouldBe(_patientReference);
        condition.Recorder.ShouldBe(_deviceReference);

        // Check evidence
        // condition.Evidence.ShouldNotBeNull();
        // condition.Evidence.Count.ShouldBe(1);

        // Check notes
        condition.Note.ShouldNotBeNull();
        condition.Note.Count.ShouldBe(1);
        condition.Note[0].Text
            .ShouldBe("Patient shows biochemical relapse with rising PSA following treatment");

        // Check fact extensions
        condition.Extension.ShouldNotBeNull();
        IEnumerable<Extension> factExtensions
            = condition.Extension.Where(e => e.Url == ClinicalFactExtension.ExtensionUrl);
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
        Condition condition = builder
            .WithFocus("condition-456", "Advanced Prostate Cancer")
            .WithPatient("patient-123", "John Doe")
            .WithDevice("device-789", "Assessment AI")
            .WithHSDMResult(
                HsdmAssessmentConditionBuilder.HsdmResults.MetastaticCastrationSensitive)
            .AddFactEvidence(_sampleFacts)
            .AddNote("Patient has metastatic castration-sensitive prostate cancer")
            .Build();

        // Assert
        condition.Code.Coding.Count.ShouldBe(2); // Only SNOMED + Z19.1, no R97.21

        // Check SNOMED code
        Coding? snomedCoding
            = condition.Code.Coding.FirstOrDefault(c =>
                c.System == FhirCodingHelper.Systems.SNOMED_SYSTEM);
        snomedCoding.ShouldNotBeNull();
        snomedCoding.Code.ShouldBe("1197209002");

        // Check ICD-10 Z19.1 code
        Coding? icd10Coding = condition.Code.Coding.FirstOrDefault(c =>
            c.System == "http://hl7.org/fhir/sid/icd-10-cm");
        icd10Coding.ShouldNotBeNull();
        icd10Coding.Code.ShouldBe("Z19.1");

        // Should NOT have R97.21 code
        Coding? risingPsaCoding = condition.Code.Coding.FirstOrDefault(c => c.Code == "R97.21");
        risingPsaCoding.ShouldBeNull();

        condition.Code.Text.ShouldBe("Castration-Sensitive Prostate Cancer (mCSPC)");
    }

    [Fact]
    public void Build_WithMCRPC_CreatesCorrectCondition()
    {
        // Arrange
        var builder = new HsdmAssessmentConditionBuilder(_configuration);

        // Act
        Condition condition = builder
            .WithFocus(_conditionReference)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithHSDMResult(
                HsdmAssessmentConditionBuilder.HsdmResults.MetastaticCastrationResistant)
            .AddFactEvidence(_sampleFacts)
            .AddNote("Patient has progressed to castration-resistant disease")
            .Build();

        // Assert
        condition.Code.Coding.Count.ShouldBe(2); // SNOMED + Z19.2

        // Check SNOMED code for castration-resistant
        Coding? snomedCoding
            = condition.Code.Coding.FirstOrDefault(c =>
                c.System == FhirCodingHelper.Systems.SNOMED_SYSTEM);
        snomedCoding.ShouldNotBeNull();
        snomedCoding.Code.ShouldBe("445848006");
        snomedCoding.Display.ShouldBe("Castration resistant prostate cancer");

        // Check ICD-10 Z19.2 code
        Coding? icd10Coding = condition.Code.Coding.FirstOrDefault(c =>
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
        Condition condition = builder
            .WithFocus(_conditionReference)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithHSDMResult(
                HsdmAssessmentConditionBuilder.HsdmResults.MetastaticCastrationSensitive)
            .AddFactEvidence(_sampleFacts)
            .AddNote("Assessment with high confidence")
            .WithConfidence(0.95f)
            .Build();

        // Assert
        condition.Extension.ShouldNotBeNull();
        Extension? confidenceExtension = condition.Extension.FirstOrDefault(e =>
            e.Url == "http://thirdopinion.ai/fhir/StructureDefinition/confidence");
        confidenceExtension.ShouldNotBeNull();
        var confidenceValue = (FhirDecimal)confidenceExtension.Value;
        confidenceValue.Value.ShouldBe(0.95m);
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
        Condition condition = builder
            .WithFocus(_conditionReference)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithHSDMResult(
                HsdmAssessmentConditionBuilder.HsdmResults.MetastaticCastrationSensitive)
            .AddFactEvidence(multipleFacts)
            .AddNote("Assessment based on multiple facts")
            .Build();

        // Assert
        condition.Extension.ShouldNotBeNull();
        IEnumerable<Extension> factExtensions
            = condition.Extension.Where(e => e.Url == ClinicalFactExtension.ExtensionUrl);
        factExtensions.Count().ShouldBe(1);

        // Check evidence count (should include document references from facts)
        condition.Evidence.Count.ShouldBe(0); //TODO removed
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
                .WithHSDMResult(HsdmAssessmentConditionBuilder.HsdmResults
                    .MetastaticCastrationSensitive)
                .AddFactEvidence(_sampleFacts)
                .AddNote("Test summary")
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
                .WithHSDMResult(HsdmAssessmentConditionBuilder.HsdmResults
                    .MetastaticCastrationSensitive)
                .AddFactEvidence(_sampleFacts)
                .AddNote("Test summary")
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
                .WithHSDMResult(HsdmAssessmentConditionBuilder.HsdmResults
                    .MetastaticCastrationSensitive)
                .AddFactEvidence(_sampleFacts)
                .AddNote("Test summary")
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
                .AddNote("Test summary")
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
                .WithHSDMResult(HsdmAssessmentConditionBuilder.HsdmResults
                    .MetastaticCastrationSensitive)
                .AddNote("Test summary")
                .Build());

        exception.Message.ShouldContain("Fact evidence is required");
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
    public void Build_WithComplexMCSPCDiagnosisModifier_CreatesConditionWithAllMetadata()
    {
        // Arrange - Based on real-world mCSPC diagnosis data
        var builder = new HsdmAssessmentConditionBuilder(_configuration);

        // Source metadata from the JSON
        const string factGuid = "fc6bb4ff-c438-4a13-9942-e97660339718";
        const string documentId = "unknown";
        const string practiceId = "a-15454";
        const float confidence = 0.85f;

        // Supporting facts from the diagnosisModifier
        var supportingFacts = new[]
        {
            new Fact
            {
                factGuid = factGuid,
                factDocumentReference = $"DocumentReference/{documentId}",
                type = "problem",
                fact = "Carcinoma of prostate",
                @ref = Array.Empty<string>(),
                timeRef = "2018-12-24",
                relevance = "Active problem confirming prostate cancer diagnosis"
            },
            new Fact
            {
                factGuid = factGuid,
                factDocumentReference = $"DocumentReference/{documentId}",
                type = "problem",
                fact = "Malignant neoplasm of prostate",
                @ref = Array.Empty<string>(),
                timeRef = "2011-10-27",
                relevance = "Active problem confirming long-standing prostate cancer diagnosis"
            },
            new Fact
            {
                factGuid = factGuid,
                factDocumentReference = $"DocumentReference/{documentId}",
                type = "clinical_note",
                fact = "Pt is here for his 6month lupron inj",
                @ref = Array.Empty<string>(),
                timeRef = "2025-05-12",
                relevance = "Current visit for ADT administration, confirms ongoing hormone therapy"
            },
            new Fact
            {
                factGuid = factGuid,
                factDocumentReference = $"DocumentReference/{documentId}",
                type = "medication_administration",
                fact = "Lupron 45Mg was injected IM in right hip",
                @ref = Array.Empty<string>(),
                timeRef = "2025-05-12",
                relevance = "Administration of ADT confirming castration-sensitive status"
            },
            new Fact
            {
                factGuid = factGuid,
                factDocumentReference = $"DocumentReference/{documentId}",
                type = "medication",
                fact = "Lupron Depot 45 mg (6 Month) intramuscular syringe kit Inject 1 kit(s) by intramuscular route",
                @ref = Array.Empty<string>(),
                timeRef = "2022-01-14",
                relevance = "Historical evidence of ongoing ADT since at least 2022"
            },
            new Fact
            {
                factGuid = factGuid,
                factDocumentReference = $"DocumentReference/{documentId}",
                type = "medication",
                fact = "bicalutamide 50 mg tablet TAKE 1 TABLET BY MOUTH ONCE DAILY",
                @ref = Array.Empty<string>(),
                timeRef = "2024-12-06",
                relevance = "Antiandrogen therapy indicating combined androgen blockade for mCSPC"
            },
            new Fact
            {
                factGuid = factGuid,
                factDocumentReference = $"DocumentReference/{documentId}",
                type = "medication",
                fact = "Prolia 60 mg/mL subcutaneous syringe Inject 1 mL by subcutaneous route as directed",
                @ref = Array.Empty<string>(),
                timeRef = "2024-07-17",
                relevance = "Denosumab for bone protection strongly suggests metastatic bone disease in prostate cancer patient"
            },
            new Fact
            {
                factGuid = factGuid,
                factDocumentReference = $"DocumentReference/{documentId}",
                type = "medication",
                fact = "Prolia 60 mg/mL subcutaneous syringe",
                @ref = Array.Empty<string>(),
                timeRef = "2025-05-12",
                relevance = "Current administration of Prolia for bone metastases management"
            },
            new Fact
            {
                factGuid = factGuid,
                factDocumentReference = $"DocumentReference/{documentId}",
                type = "problem",
                fact = "Osteopenia",
                @ref = Array.Empty<string>(),
                timeRef = "2024-03-11",
                relevance = "Bone health concern in context of prostate cancer, consistent with metastatic bone involvement"
            },
            new Fact
            {
                factGuid = factGuid,
                factDocumentReference = $"DocumentReference/{documentId}",
                type = "clinical_note",
                fact = "comes in for lupron and prolia injections",
                @ref = Array.Empty<string>(),
                timeRef = "2025-05-12",
                relevance = "Routine visit for both ADT and bone-protective therapy"
            }
        };

        const string summary = "<div xmlns='http://www.w3.org/1999/xhtml'><p><strong>Diagnosis: Metastatic Castration-Sensitive Prostate Cancer (mCSPC)</strong></p><p><strong>Evidence Summary:</strong></p><ul><li><strong>Confirmed Prostate Cancer:</strong> Multiple documentation of 'Carcinoma of prostate' (onset 2018-12-24) and 'Malignant neoplasm of prostate' (onset 2011-10-27) in active problem list and past medical history</li><li><strong>Hormone-Sensitive/Castration-Sensitive Status:</strong> Patient receiving ongoing androgen deprivation therapy (ADT) with Lupron Depot 45mg 6-month injections. Most recent injection administered 05/12/2025 IM in right hip. Patient has been on continuous ADT since at least 2022 (medication record from 01/14/2022)</li><li><strong>Metastatic Disease Indicator:</strong> Patient prescribed Prolia (denosumab) 60mg subcutaneously every 6 months for bone health, administered 07/17/2024 and 05/12/2025. Prolia is commonly used in metastatic prostate cancer patients to prevent skeletal-related events from bone metastases. Patient also has documented 'Osteopenia' (onset 2024-03-11), which in the context of prostate cancer and Prolia use suggests bone involvement</li><li><strong>Adjunctive Antiandrogen Therapy:</strong> Bicalutamide 50mg daily (filled 12/06/2024) indicates combined androgen blockade, consistent with mCSPC treatment approach</li><li><strong>No Evidence of Castration Resistance:</strong> No documentation of rising PSA on ADT, no progression on hormonal therapy, no mCRPC-specific medications (abiraterone, enzalutamide, docetaxel, cabazitaxel). Patient continues on standard ADT with good tolerance</li></ul><p><strong>Clinical Context:</strong> 93-year-old male with long-standing prostate cancer (diagnosed 2011) on continuous ADT with Lupron 6-month depot formulation plus bicalutamide. The use of Prolia for bone protection in this clinical context strongly suggests metastatic bone disease. Patient remains hormone-sensitive as evidenced by continuation of standard ADT without escalation to CRPC-specific therapies.</p></div>";

        // Act
        Condition condition = builder
            .WithFhirResourceId(factGuid)
            .WithFocus("Condition/prostate-cancer-primary", "Carcinoma of prostate")
            .WithPatient("Patient/patient-93yo-male", "93-year-old male patient")
            .WithDevice("Device/ai-hsdm-classifier", "HSDM AI Classifier")
            .WithHSDMResult(HsdmAssessmentConditionBuilder.HsdmResults.MetastaticCastrationSensitive)
            .WithConfidence(confidence)
            .AddFactEvidence(supportingFacts)
            .AddNote(summary)
            .WithEffectiveDate(new DateTime(2025, 5, 12, 10, 0, 0))
            .AddDerivedFrom($"DocumentReference/{documentId}", "Source clinical documentation")
            .Build();

        // Assert - Basic structure
        condition.ShouldNotBeNull();
        condition.ClinicalStatus.Coding[0].Code.ShouldBe("active");
        condition.VerificationStatus.Coding[0].Code.ShouldBe("confirmed");
        condition.Category.ShouldHaveSingleItem();
        condition.Category[0].Coding[0].Code.ShouldBe("encounter-diagnosis");

        // Assert - HSDM Result Code (mCSPC)
        condition.Code.Coding.Count.ShouldBe(2);
        condition.Code.Text.ShouldBe("Castration-Sensitive Prostate Cancer (mCSPC)");

        // Check SNOMED code for castration-sensitive
        Coding? snomedCoding = condition.Code.Coding.FirstOrDefault(c =>
            c.System == FhirCodingHelper.Systems.SNOMED_SYSTEM);
        snomedCoding.ShouldNotBeNull();
        snomedCoding.Code.ShouldBe("1197209002");
        snomedCoding.Display.ShouldBe("Castration-sensitive prostate cancer");

        // Check ICD-10 Z19.1 code matches the diagnosisModifier.icd10Codes from JSON
        Coding? icd10Coding = condition.Code.Coding.FirstOrDefault(c =>
            c.System == "http://hl7.org/fhir/sid/icd-10-cm" && c.Code == "Z19.1");
        icd10Coding.ShouldNotBeNull();
        icd10Coding.Display.ShouldBe("Hormone sensitive malignancy status");

        // Assert - Patient and Device references
        condition.Subject.Reference.ShouldBe("Patient/patient-93yo-male");
        condition.Recorder.Reference.ShouldBe("Device/ai-hsdm-classifier");

        // Assert - Focus reference
        condition.Extension.ShouldNotBeNull();

        // Assert - Confidence extension (from diagnosisModifier.confidence)
        Extension? confidenceExtension = condition.Extension.FirstOrDefault(e =>
            e.Url == "http://thirdopinion.ai/fhir/StructureDefinition/confidence");
        confidenceExtension.ShouldNotBeNull();
        var confidenceValue = (FhirDecimal)confidenceExtension.Value;
        confidenceValue.Value.ShouldBe(0.85m);

        // Assert - Summary note
        condition.Note.ShouldNotBeNull();
        condition.Note.ShouldHaveSingleItem();
        string noteText = condition.Note[0].Text;
        noteText.ShouldContain("Metastatic Castration-Sensitive Prostate Cancer (mCSPC)");
        noteText.ShouldContain("93-year-old male");
        noteText.ShouldContain("Lupron Depot 45mg");
        noteText.ShouldContain("Prolia");
        noteText.ShouldContain("bone metastases");

        // Assert - Fact extensions (all 10 supporting facts)
        IEnumerable<Extension> factExtensions = condition.Extension
            .Where(e => e.Url == ClinicalFactExtension.ExtensionUrl);
        factExtensions.Count().ShouldBe(1);

        // Verify specific facts are present
        // var factList = factExtensions.ToList();
        //
        // // Fact 1: Carcinoma of prostate
        // Extension? fact1 = factList.FirstOrDefault(e =>
        //     e.Extension.Any(innerExt =>
        //         innerExt.Url == "fact" &&
        //         ((FhirString)innerExt.Value).Value == "Carcinoma of prostate"));
        // fact1.ShouldNotBeNull();
        //
        // // Fact 4: Lupron administration
        // Extension? fact4 = factList.FirstOrDefault(e =>
        //     e.Extension.Any(innerExt =>
        //         innerExt.Url == "fact" &&
        //         ((FhirString)innerExt.Value).Value == "Lupron 45Mg was injected IM in right hip"));
        // fact4.ShouldNotBeNull();
        //
        // // Fact 7: Prolia for bone metastases
        // Extension? fact7 = factList.FirstOrDefault(e =>
        //     e.Extension.Any(innerExt =>
        //         innerExt.Url == "fact" &&
        //         ((FhirString)innerExt.Value).Value.Contains("Prolia 60 mg/mL subcutaneous syringe Inject")));
        // fact7.ShouldNotBeNull();

        // Assert - Evidence references (should have 10 evidence entries from 10 facts)
        // condition.Evidence.ShouldNotBeNull();
        // condition.Evidence.Count.ShouldBe(10); //TODO removedß

        // Assert - AIAST security label
        condition.Meta.ShouldNotBeNull();
        condition.Meta.Security.Any(s => s.Code == "AIAST").ShouldBeTrue();

        // Assert - Effective date
        condition.RecordedDate.ShouldContain("2025-05-12");
    }

    #region JSON Serialization Tests

    [Fact]
    public void Build_mCSPCExample_SerializesToJson()
    {
        // Arrange
        var builder = new HsdmAssessmentConditionBuilder(_configuration);

        var supportingFacts = new[]
        {
            new Fact
            {
                factGuid = "fact-mcspc-001",
                factDocumentReference = "DocumentReference/clinical-note-2025-05-12",
                type = "problem",
                fact = "Carcinoma of prostate (onset 2018-12-24)",
                @ref = Array.Empty<string>(),
                timeRef = "2018-12-24",
                relevance = "Confirmed prostate cancer diagnosis"
            },
            new Fact
            {
                factGuid = "fact-mcspc-002",
                factDocumentReference = "DocumentReference/clinical-note-2025-05-12",
                type = "medication",
                fact = "Lupron Depot 45mg 6-month injections, most recent administered 05/12/2025 IM in right hip",
                @ref = Array.Empty<string>(),
                timeRef = "2025-05-12",
                relevance = "Ongoing ADT confirming castration-sensitive status"
            },
            new Fact
            {
                factGuid = "fact-mcspc-003",
                factDocumentReference = "DocumentReference/clinical-note-2025-05-12",
                type = "medication",
                fact = "Prolia (denosumab) 60mg subcutaneously every 6 months for bone health",
                @ref = Array.Empty<string>(),
                timeRef = "2024-07-17",
                relevance = "Bone protection medication strongly suggests metastatic bone disease"
            },
            new Fact
            {
                factGuid = "fact-mcspc-004",
                factDocumentReference = "DocumentReference/medications-2024-12-06",
                type = "medication",
                fact = "Bicalutamide 50mg daily",
                @ref = Array.Empty<string>(),
                timeRef = "2024-12-06",
                relevance = "Combined androgen blockade consistent with mCSPC treatment"
            }
        };

        const string summary = "<div xmlns='http://www.w3.org/1999/xhtml'><p><strong>Diagnosis: Metastatic Castration-Sensitive Prostate Cancer (mCSPC)</strong></p><p><strong>Evidence Summary:</strong></p><ul><li><strong>Confirmed Prostate Cancer:</strong> Multiple documentation of 'Carcinoma of prostate' (onset 2018-12-24) and 'Malignant neoplasm of prostate' (onset 2011-10-27) in active problem list and past medical history</li><li><strong>Hormone-Sensitive/Castration-Sensitive Status:</strong> Patient receiving ongoing androgen deprivation therapy (ADT) with Lupron Depot 45mg 6-month injections. Most recent injection administered 05/12/2025 IM in right hip. Patient has been on continuous ADT since at least 2022 (medication record from 01/14/2022)</li><li><strong>Metastatic Disease Indicator:</strong> Patient prescribed Prolia (denosumab) 60mg subcutaneously every 6 months for bone health, administered 07/17/2024 and 05/12/2025. Prolia is commonly used in metastatic prostate cancer patients to prevent skeletal-related events from bone metastases. Patient also has documented 'Osteopenia' (onset 2024-03-11), which in the context of prostate cancer and Prolia use suggests bone involvement</li><li><strong>Adjunctive Antiandrogen Therapy:</strong> Bicalutamide 50mg daily (filled 12/06/2024) indicates combined androgen blockade, consistent with mCSPC treatment approach</li><li><strong>No Evidence of Castration Resistance:</strong> No documentation of rising PSA on ADT, no progression on hormonal therapy, no mCRPC-specific medications (abiraterone, enzalutamide, docetaxel, cabazitaxel). Patient continues on standard ADT with good tolerance</li></ul><p><strong>Clinical Context:</strong> 93-year-old male with long-standing prostate cancer (diagnosed 2011) on continuous ADT with Lupron 6-month depot formulation plus bicalutamide. The use of Prolia for bone protection in this clinical context strongly suggests metastatic bone disease. Patient remains hormone-sensitive as evidenced by continuation of standard ADT without escalation to CRPC-specific therapies.</p></div>";

        // Act
        Condition condition = builder
            .WithFhirResourceId("inference-mcspc-001")
            .WithFocus("Condition/prostate-cancer-primary", "Prostate Cancer")
            .WithPatient("Patient/example-patient-1", "93-year-old male")
            .WithDevice("Device/ai-hsdm-classifier", "HSDM AI Classifier")
            .WithHSDMResult(HsdmAssessmentConditionBuilder.HsdmResults.MetastaticCastrationSensitive)
            .WithConfidence(0.85f)
            .AddFactEvidence(supportingFacts)
            .AddNote(summary)
            .WithEffectiveDate(new DateTime(2025, 5, 12))
            .Build();

        // Serialize to JSON (formatted)
        var serializer = new Hl7.Fhir.Serialization.FhirJsonSerializer(new Hl7.Fhir.Serialization.SerializerSettings { Pretty = true });
        var json = serializer.SerializeToString(condition);

        // Output to test output
        _output.WriteLine("=== mCSPC (Metastatic Castration-Sensitive Prostate Cancer) Example ===");
        _output.WriteLine(json);
        _output.WriteLine("");

        // Assert
        condition.ShouldNotBeNull();
        condition.Code.Text.ShouldBe("Castration-Sensitive Prostate Cancer (mCSPC)");
        condition.Code.Coding.Any(c => c.Code == "Z19.1").ShouldBeTrue(); // ICD-10
    }

    [Fact]
    public void Build_mCRPCExample_SerializesToJson()
    {
        // Arrange
        var builder = new HsdmAssessmentConditionBuilder(_configuration);

        var supportingFacts = new[]
        {
            new Fact
            {
                factGuid = "fact-mcrpc-001",
                factDocumentReference = "DocumentReference/psma-scan-2025-07-03",
                type = "imaging",
                fact = "PSMA scan demonstrates 'innumerable bone metastases'",
                @ref = Array.Empty<string>(),
                timeRef = "2025-07-03",
                relevance = "Confirms widespread metastatic disease"
            },
            new Fact
            {
                factGuid = "fact-mcrpc-002",
                factDocumentReference = "DocumentReference/medication-2025-05-06",
                type = "medication",
                fact = "Xtandi (enzalutamide) 160 mg daily started 2025-05-06",
                @ref = Array.Empty<string>(),
                timeRef = "2025-05-06",
                relevance = "mCRPC-specific therapy indicates castration-resistant status"
            },
            new Fact
            {
                factGuid = "fact-mcrpc-003",
                factDocumentReference = "DocumentReference/lab-2025-05-05",
                type = "lab",
                fact = "PSA from 2025-05-05 was >153",
                @ref = Array.Empty<string>(),
                timeRef = "2025-05-05",
                relevance = "Markedly elevated PSA indicates biochemical progression despite ADT"
            },
            new Fact
            {
                factGuid = "fact-mcrpc-004",
                factDocumentReference = "DocumentReference/radiation-summary",
                type = "treatment",
                fact = "Palliative radiation therapy (IG-IMRT, 3000 cGy over 10 fractions to L-spine) for 2-3 months of progressing lumbar spine pain",
                @ref = Array.Empty<string>(),
                timeRef = "2025-04-01",
                relevance = "Symptomatic progression requiring palliative intervention"
            }
        };

        var conflictingFacts = new[]
        {
            new Fact
            {
                factGuid = "fact-mcrpc-conflict-001",
                factDocumentReference = "DocumentReference/problem-list-2021",
                type = "problem",
                fact = "Hormone sensitive prostate cancer documented in 2021",
                @ref = Array.Empty<string>(),
                timeRef = "2021-03-25",
                relevance = "Historical hormone-sensitive status, but patient has since progressed to castration-resistant"
            }
        };

        const string summary = "<div xmlns='http://www.w3.org/1999/xhtml'><p><strong>Diagnosis: Metastatic Castration-Resistant Prostate Cancer (mCRPC)</strong></p><p><strong>Evidence Summary:</strong></p><ul><li><strong>Metastatic Disease:</strong> PSMA scan demonstrates 'innumerable bone metastases' (2025-07-03). Patient has 'widely metastatic prostate cancer' with secondary malignant neoplasm of bone (ICD-10: C79.51).</li><li><strong>Castration-Resistant Status:</strong> Patient is currently on Xtandi (enzalutamide) 160 mg daily (started 2025-05-06), a medication specifically indicated for mCRPC. Patient has been 'on and off of ARI' (androgen receptor inhibitor), indicating prior treatment attempts.</li><li><strong>Disease Progression on ADT:</strong> Patient 'is on ADT' but has progressed to widely metastatic disease. PSA from 2025-05-05 was >153, indicating biochemical progression despite androgen deprivation therapy.</li><li><strong>Clinical Context:</strong> Patient has symptomatic disease with '2-3 months of progressing and aching pain of the lumbar spine' requiring palliative radiation therapy (IG-IMRT, 3000 cGy over 10 fractions to L-spine). Historical problem list notes 'Hormone sensitive prostate cancer' (2021-03-25), but current clinical picture demonstrates progression to castration-resistant state.</li></ul><p><strong>Conclusion:</strong> The combination of metastatic bone disease, treatment with enzalutamide (mCRPC-specific therapy), disease progression on ADT with markedly elevated PSA, and need for palliative interventions clearly establishes mCRPC diagnosis. The patient has transitioned from hormone-sensitive disease (documented in 2021) to castration-resistant disease by 2025.</p></div>";

        // Act
        Condition condition = builder
            .WithFhirResourceId("inference-mcrpc-001")
            .WithFocus("Condition/prostate-cancer-metastatic", "Widely Metastatic Prostate Cancer")
            .WithPatient("Patient/example-patient-2", "Patient with progressive disease")
            .WithDevice("Device/ai-hsdm-classifier", "HSDM AI Classifier")
            .WithHSDMResult(HsdmAssessmentConditionBuilder.HsdmResults.MetastaticCastrationResistant)
            .WithConfidence(0.95f)
            .AddFactEvidence(supportingFacts.Concat(conflictingFacts).ToArray())
            .AddNote(summary)
            .WithEffectiveDate(new DateTime(2025, 7, 3))
            .Build();

        // Serialize to JSON (formatted)
        var serializer = new Hl7.Fhir.Serialization.FhirJsonSerializer(new Hl7.Fhir.Serialization.SerializerSettings { Pretty = true });
        var json = serializer.SerializeToString(condition);

        // Output to test output
        _output.WriteLine("=== mCRPC (Metastatic Castration-Resistant Prostate Cancer) Example ===");
        _output.WriteLine(json);
        _output.WriteLine("");

        // Assert
        condition.ShouldNotBeNull();
        condition.Code.Text.ShouldBe("Castration-Resistant Prostate Cancer (mCRPC)");
        condition.Code.Coding.Any(c => c.Code == "Z19.2").ShouldBeTrue(); // ICD-10
    }

    [Fact]
    public void Build_nmCSPCBiochemicalRelapseExample_SerializesToJson()
    {
        // Arrange
        var builder = new HsdmAssessmentConditionBuilder(_configuration);

        var supportingFacts = new[]
        {
            new Fact
            {
                factGuid = "fact-nmcspc-001",
                factDocumentReference = "DocumentReference/pathology-2024-07-18",
                type = "pathology",
                fact = "Adenocarcinoma of prostate with Gleason score 8 (4+4) diagnosed via needle biopsy",
                @ref = Array.Empty<string>(),
                timeRef = "2024-07-18",
                relevance = "High-grade prostate cancer diagnosis"
            },
            new Fact
            {
                factGuid = "fact-nmcspc-002",
                factDocumentReference = "DocumentReference/lab-2024-08-29",
                type = "lab",
                fact = "PSA of 14.6",
                @ref = Array.Empty<string>(),
                timeRef = "2024-08-29",
                relevance = "Elevated PSA indicating biochemical abnormality"
            },
            new Fact
            {
                factGuid = "fact-nmcspc-003",
                factDocumentReference = "DocumentReference/bone-scan-2024-12-16",
                type = "imaging",
                fact = "Bone scan showed 'no conclusive evidence of metastatic disease'",
                @ref = Array.Empty<string>(),
                timeRef = "2024-12-16",
                relevance = "Confirms non-metastatic status (M0)"
            },
            new Fact
            {
                factGuid = "fact-nmcspc-004",
                factDocumentReference = "DocumentReference/ct-2024-12-16",
                type = "imaging",
                fact = "CT abdomen/pelvis showed 'no conclusive evidence of metastatic disease'",
                @ref = Array.Empty<string>(),
                timeRef = "2024-12-16",
                relevance = "Confirms non-metastatic status (M0)"
            },
            new Fact
            {
                factGuid = "fact-nmcspc-005",
                factDocumentReference = "DocumentReference/medication-2024-12-26",
                type = "medication",
                fact = "Initiated ADT with Orgovyx (oral) and injection-based ADT starting 12/26/2024",
                @ref = Array.Empty<string>(),
                timeRef = "2024-12-26",
                relevance = "Response to ADT confirms castration-sensitive status"
            },
            new Fact
            {
                factGuid = "fact-nmcspc-006",
                factDocumentReference = "DocumentReference/followup-2025-06-30",
                type = "clinical_note",
                fact = "Patient 'tolerating ADT well' as of 06/30/2025",
                @ref = Array.Empty<string>(),
                timeRef = "2025-06-30",
                relevance = "Ongoing response to hormone therapy without resistance"
            }
        };

        const string summary = "<div xmlns='http://www.w3.org/1999/xhtml'><p><strong>Diagnosis: Non-Metastatic Castration-Sensitive Prostate Cancer with Biochemical Relapse</strong></p><p><strong>Evidence Summary:</strong></p><ul><li><strong>Prostate Cancer Confirmed:</strong> Adenocarcinoma of prostate with Gleason score 8 (4+4) diagnosed via needle biopsy on 07/18/2024. High-grade/high-risk prostate cancer, clinical stage T1c.</li><li><strong>Elevated PSA (Biochemical Finding):</strong> PSA of 14.6 documented on 08/29/2024, indicating biochemical abnormality consistent with active disease or relapse.</li><li><strong>No Metastatic Disease (M0):</strong> Bone scan on 12/16/2024 showed 'no conclusive evidence of metastatic disease.' CT abdomen/pelvis on 12/16/2024 also showed 'no conclusive evidence of metastatic disease.' Patient is non-metastatic (M0).</li><li><strong>Castration-Sensitive (Hormone-Sensitive):</strong> Patient initiated androgen deprivation therapy (ADT) with Orgovyx (oral) and injection-based ADT starting 12/26/2024. Patient is 'tolerating ADT well' as of 03/17/2025 and 06/30/2025. Planned duration of ADT is two years. No evidence of castration resistance; patient is responding to hormone therapy.</li><li><strong>Treatment Plan:</strong> Patient proceeding with IG-IMRT radiation therapy (8100 cGy to prostate gland and pelvic lymph nodes). Patient underwent PVP procedure on 04/17/2025 for BPH and was cleared for radiation therapy by 06/30/2025.</li></ul><p><strong>Rationale:</strong> The combination of confirmed prostate cancer, elevated PSA (biochemical marker), absence of metastases on imaging (M0 status), and ongoing response to ADT (castration-sensitive) supports the diagnosis of <strong>nmCSPC with biochemical relapse</strong>. The elevated PSA of 14.6 represents the biochemical component, while imaging confirms non-metastatic status. The patient remains hormone-sensitive as evidenced by tolerating ADT well without progression to castration resistance.</p></div>";

        // Act
        Condition condition = builder
            .WithFhirResourceId("inference-nmcspc-001")
            .WithFocus("Condition/prostate-cancer-localized", "Localized Prostate Cancer")
            .WithPatient("Patient/example-patient-3", "Patient with biochemical relapse")
            .WithDevice("Device/ai-hsdm-classifier", "HSDM AI Classifier")
            .WithHSDMResult(HsdmAssessmentConditionBuilder.HsdmResults.NonMetastaticBiochemicalRelapse)
            .WithConfidence(0.85f)
            .AddFactEvidence(supportingFacts)
            .AddNote(summary)
            .WithEffectiveDate(new DateTime(2024, 12, 16))
            .Build();

        // Serialize to JSON (formatted)
        var serializer = new Hl7.Fhir.Serialization.FhirJsonSerializer(new Hl7.Fhir.Serialization.SerializerSettings { Pretty = true });
        var json = serializer.SerializeToString(condition);

        // Output to test output
        _output.WriteLine("=== nmCSPC with Biochemical Relapse (Non-Metastatic Castration-Sensitive) Example ===");
        _output.WriteLine(json);
        _output.WriteLine("");

        // Assert
        condition.ShouldNotBeNull();
        condition.Code.Text.ShouldBe("Castration-Sensitive Prostate Cancer with Biochemical Relapse");
        condition.Code.Coding.Any(c => c.Code == "Z19.1").ShouldBeTrue(); // ICD-10 Z19.1
        condition.Code.Coding.Any(c => c.Code == "R97.21").ShouldBeTrue(); // ICD-10 R97.21
    }

    #endregion
}