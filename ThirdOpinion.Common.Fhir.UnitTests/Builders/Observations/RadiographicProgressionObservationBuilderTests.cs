using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using ThirdOpinion.Common.Fhir.Builders.Observations;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;
using ThirdOpinion.Common.Fhir.Models;

namespace ThirdOpinion.Common.Fhir.UnitTests.Builders.Observations;

public class RadiographicProgressionObservationBuilderTests
{
    private readonly AiInferenceConfiguration _configuration;
    private readonly ResourceReference _deviceReference;
    private readonly ResourceReference _imagingStudyReference;
    private readonly ResourceReference _patientReference;
    private readonly ResourceReference _radiologyReportReference;
    private readonly ResourceReference _conditionReference;
    private readonly Fact[] _sampleFacts;

    public RadiographicProgressionObservationBuilderTests()
    {
        _configuration = AiInferenceConfiguration.CreateDefault();
        _patientReference = new ResourceReference("Patient/test-patient", "Test Patient");
        _deviceReference = new ResourceReference("Device/radiographic-ai", "Radiographic AI v2.0");
        _conditionReference = new ResourceReference("Condition/prostate-cancer", "Metastatic Prostate Cancer");
        _imagingStudyReference = new ResourceReference("ImagingStudy/ct-123", "CT Abdomen/Pelvis");
        _radiologyReportReference = new ResourceReference("DocumentReference/rad-report-456", "Radiology Report");

        _sampleFacts = new[]
        {
            new Fact
            {
                factGuid = "aa10e02a-f391-4614-96e4-35cbe47d2a85",
                factDocumentReference = "DocumentReference/ct-report-001",
                type = "finding",
                fact = "New 2.5cm lesion in liver segment 6",
                @ref = new[] { "1.123" },
                timeRef = "2024-11-02",
                relevance = "New metastatic lesion indicating progression"
            },
            new Fact
            {
                factGuid = "bb10e02a-f391-4614-96e4-35cbe47d2a86",
                factDocumentReference = "DocumentReference/ct-report-001",
                type = "finding",
                fact = "Qualitative increase in disease burden",
                @ref = new[] { "2.456" },
                timeRef = "2024-11-02",
                relevance = "Overall assessment of progression"
            }
        };
    }

    [Fact]
    public void Build_WithProgressionDetected_CreatesCorrectObservation()
    {
        // Arrange
        var builder = new RadiographicProgressionObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithInferenceId("radiographic-001")
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_conditionReference)
            .WithProgressionDetected()
            .AddImagingStudy(_imagingStudyReference)
            .AddRadiologyReport(_radiologyReportReference)
            .AddBodySite("10200004", "Liver structure")
            .WithImagingType("CT")
            .WithImagingDate(DateTime.Parse("2024-11-02"))
            .WithConfidence(0.92f)
            .AddNote("New hepatic lesions consistent with metastatic progression")
            .Build();

        // Assert
        observation.ShouldNotBeNull();
        observation.Id.ShouldBe("radiographic-001");
        observation.Status.ShouldBe(ObservationStatus.Final);

        // Check category
        observation.Category.ShouldHaveSingleItem();
        observation.Category[0].Coding[0].Code.ShouldBe("imaging");
        observation.Category[0].Coding[0].System.ShouldBe("http://terminology.hl7.org/CodeSystem/observation-category");

        // Check code
        observation.Code.Coding.Count.ShouldBe(1);
        Coding? snomedCoding = observation.Code.Coding.First(c => c.System == FhirCodingHelper.Systems.SNOMED_SYSTEM);
        snomedCoding.Code.ShouldBe("246455001");
        snomedCoding.Display.ShouldBe("Recurrence status");
        observation.Code.Text.ShouldBe("Radiographic Disease Progression Assessment");

        // Check value - should be progression detected
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding[0].Code.ShouldBe("162573006");
        valueCodeableConcept.Coding[0].Display.ShouldBe("Progression of disease");

        // Check subject
        observation.Subject.Reference.ShouldBe("Patient/test-patient");

        // Check device
        observation.Device.Reference.ShouldBe("Device/radiographic-ai");

        // Check focus
        observation.Focus.ShouldHaveSingleItem();
        observation.Focus[0].Reference.ShouldBe("Condition/prostate-cancer");

        // Check notes
        observation.Note.Count.ShouldBe(1);
        observation.Note[0].Text.ShouldBe("New hepatic lesions consistent with metastatic progression");

        // Check confidence component
        var confidenceComponent = observation.Component.FirstOrDefault(c => c.Code.Text == "AI Confidence Score");
        confidenceComponent.ShouldNotBeNull();
        var confidenceQuantity = confidenceComponent.Value as Quantity;
        confidenceQuantity.ShouldNotBeNull();
        confidenceQuantity.Value.ShouldBe(0.92m);
    }

    [Fact]
    public void Build_WithNoProgressionDetected_CreatesCorrectObservation()
    {
        // Arrange
        var builder = new RadiographicProgressionObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithInferenceId("radiographic-002")
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithNoProgressionDetected()
            .AddImagingStudy(_imagingStudyReference)
            .WithConfidence(0.88f)
            .AddNote("Stable appearance of known lesions")
            .Build();

        // Assert
        observation.ShouldNotBeNull();

        // Check value - should be not detected
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding[0].Code.ShouldBe("260415000");
        valueCodeableConcept.Coding[0].Display.ShouldBe("Not detected");
    }

    [Fact]
    public void Build_WithCustomProgressionStatus_CreatesCorrectObservation()
    {
        // Arrange
        var builder = new RadiographicProgressionObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithProgressionStatus("162573006", "Progression of disease")
            .Build();

        // Assert
        observation.ShouldNotBeNull();
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding[0].Code.ShouldBe("162573006");
        valueCodeableConcept.Coding[0].Display.ShouldBe("Progression of disease");
    }

    [Fact]
    public void Build_WithMultipleBodySites_CreatesCorrectComponents()
    {
        // Arrange
        var builder = new RadiographicProgressionObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithProgressionDetected()
            .AddBodySite("10200004", "Liver structure")
            .AddBodySite("39607008", "Lung structure")
            .AddBodySite("181422007", "Entire prostate")
            .Build();

        // Assert
        observation.ShouldNotBeNull();

        // First body site goes to BodySite property
        observation.BodySite.ShouldNotBeNull();
        observation.BodySite.Coding.Count.ShouldBe(1);
        observation.BodySite.Coding[0].Code.ShouldBe("10200004");
        observation.BodySite.Coding[0].Display.ShouldBe("Liver structure");

        // All body sites also stored as components
        var bodySiteComponents = observation.Component
            .Where(c => c.Code.Coding.Any(coding => coding.Code == "363698007")).ToList();
        bodySiteComponents.Count.ShouldBe(3);

        var firstBodySite = bodySiteComponents[0].Value as CodeableConcept;
        firstBodySite.ShouldNotBeNull();
        firstBodySite.Coding[0].Code.ShouldBe("10200004");

        var secondBodySite = bodySiteComponents[1].Value as CodeableConcept;
        secondBodySite.ShouldNotBeNull();
        secondBodySite.Coding[0].Code.ShouldBe("39607008");

        var thirdBodySite = bodySiteComponents[2].Value as CodeableConcept;
        thirdBodySite.ShouldNotBeNull();
        thirdBodySite.Coding[0].Code.ShouldBe("181422007");
    }

    [Fact]
    public void Build_WithMultipleNotes_CreatesCorrectAnnotations()
    {
        // Arrange
        var builder = new RadiographicProgressionObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithProgressionDetected()
            .AddNote("New 2.5cm liver lesion identified")
            .AddNote("Qualitative increase in disease burden")
            .AddNote("RECIST 1.1 not applicable - no baseline measurements")
            .Build();

        // Assert
        observation.ShouldNotBeNull();
        observation.Note.Count.ShouldBe(3);
        observation.Note[0].Text.ShouldBe("New 2.5cm liver lesion identified");
        observation.Note[1].Text.ShouldBe("Qualitative increase in disease burden");
        observation.Note[2].Text.ShouldBe("RECIST 1.1 not applicable - no baseline measurements");
    }

    [Fact]
    public void Build_WithQualitativeAssessment_AddsCorrectComponent()
    {
        // Arrange
        var builder = new RadiographicProgressionObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithProgressionDetected()
            .WithQualitativeAssessment("Multiple new hepatic lesions with increased tumor burden")
            .Build();

        // Assert
        observation.ShouldNotBeNull();
        var assessmentComponent = observation.Component.FirstOrDefault(c =>
            c.Code.Coding.Any(coding => coding.Code == "qualitative-assessment"));
        assessmentComponent.ShouldNotBeNull();
        var assessmentString = assessmentComponent.Value as FhirString;
        assessmentString.ShouldNotBeNull();
        assessmentString.Value.ShouldBe("Multiple new hepatic lesions with increased tumor burden");
    }

    [Fact]
    public void Build_WithConfidenceRationale_AddsCorrectComponent()
    {
        // Arrange
        var builder = new RadiographicProgressionObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithProgressionDetected()
            .WithConfidence(0.95f)
            .WithConfidenceRationale("High confidence based on clear new lesion detection")
            .Build();

        // Assert
        observation.ShouldNotBeNull();
        var rationaleComponent = observation.Component.FirstOrDefault(c =>
            c.Code.Coding.Any(coding => coding.Code == "confidence-rationale"));
        rationaleComponent.ShouldNotBeNull();
        var rationaleString = rationaleComponent.Value as FhirString;
        rationaleString.ShouldNotBeNull();
        rationaleString.Value.ShouldBe("High confidence based on clear new lesion detection");
    }

    [Fact]
    public void Build_WithImagingMetadata_AddsCorrectComponents()
    {
        // Arrange
        var builder = new RadiographicProgressionObservationBuilder(_configuration);
        var imagingDate = DateTime.Parse("2024-11-02");

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithProgressionDetected()
            .WithImagingType("CT Abdomen/Pelvis with IV contrast")
            .WithImagingDate(imagingDate)
            .Build();

        // Assert
        observation.ShouldNotBeNull();

        var imagingTypeComponent = observation.Component.FirstOrDefault(c =>
            c.Code.Coding.Any(coding => coding.Code == "imaging-type"));
        imagingTypeComponent.ShouldNotBeNull();
        var typeString = imagingTypeComponent.Value as FhirString;
        typeString.ShouldNotBeNull();
        typeString.Value.ShouldBe("CT Abdomen/Pelvis with IV contrast");

        // Imaging date is set in Effective, not as a component
        observation.Effective.ShouldNotBeNull();
        var effectiveDateTime = observation.Effective as FhirDateTime;
        effectiveDateTime.ShouldNotBeNull();
        effectiveDateTime.ToDateTimeOffset(TimeSpan.Zero).Date.ShouldBe(imagingDate.Date);
    }

    [Fact]
    public void Build_WithSupportingFacts_AddsCorrectExtension()
    {
        // Arrange
        var builder = new RadiographicProgressionObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithProgressionDetected()
            .WithSupportingFacts(_sampleFacts)
            .Build();

        // Assert
        observation.ShouldNotBeNull();
        var factsExtensions = observation.Extension.Where(e => e.Url == "https://thirdopinion.io/clinical-fact").ToList();
        factsExtensions.Count.ShouldBe(2);

        // Also check that radiology reports were added
        observation.DerivedFrom.ShouldNotBeNull();
        observation.DerivedFrom.Any(r => r.Reference == "DocumentReference/ct-report-001").ShouldBeTrue();
    }

    [Fact]
    public void Build_WithConflictingFacts_AddsCorrectExtension()
    {
        // Arrange
        var builder = new RadiographicProgressionObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithProgressionDetected()
            .WithConflictingFacts(_sampleFacts)
            .Build();

        // Assert
        observation.ShouldNotBeNull();
        var factsExtensions = observation.Extension.Where(e => e.Url == "https://thirdopinion.io/conflicting-fact").ToList();
        factsExtensions.Count.ShouldBe(2);
    }

    [Fact]
    public void Build_WithCustomComponents_AddsCorrectly()
    {
        // Arrange
        var builder = new RadiographicProgressionObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithProgressionDetected()
            .AddComponent("New lesion detected", true)
            .AddComponent("Largest new lesion", new Quantity(2.5m, "cm"))
            .AddComponent("PSA trend", FhirCodingHelper.CreateSnomedConcept("281300000", "Increasing"))
            .Build();

        // Assert
        observation.ShouldNotBeNull();
        observation.Component.Count.ShouldBeGreaterThan(0);

        var boolComponent = observation.Component.FirstOrDefault(c => c.Code.Text == "New lesion detected");
        boolComponent.ShouldNotBeNull();
        var boolValue = boolComponent.Value as FhirBoolean;
        boolValue.ShouldNotBeNull();
        boolValue.Value.ShouldBe(true);

        var quantityComponent = observation.Component.FirstOrDefault(c => c.Code.Text == "Largest new lesion");
        quantityComponent.ShouldNotBeNull();
        var quantityValue = quantityComponent.Value as Quantity;
        quantityValue.ShouldNotBeNull();
        quantityValue.Value.ShouldBe(2.5m);

        var codeComponent = observation.Component.FirstOrDefault(c => c.Code.Text == "PSA trend");
        codeComponent.ShouldNotBeNull();
        var codeValue = codeComponent.Value as CodeableConcept;
        codeValue.ShouldNotBeNull();
        codeValue.Coding[0].Code.ShouldBe("281300000");
    }

    [Fact]
    public void Build_WithoutPatient_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new RadiographicProgressionObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
        {
            builder
                .WithDevice(_deviceReference)
                .WithProgressionDetected()
                .Build();
        }).Message.ShouldContain("Patient");
    }

    [Fact]
    public void Build_WithoutDevice_DoesNotThrow()
    {
        // Arrange
        var builder = new RadiographicProgressionObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithProgressionDetected()
            .Build();

        // Assert
        observation.ShouldNotBeNull();
        observation.Device.ShouldBeNull(); // Device is optional
    }

    [Fact]
    public void Build_WithoutProgressionStatus_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new RadiographicProgressionObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
        {
            builder
                .WithPatient(_patientReference)
                .WithDevice(_deviceReference)
                .Build();
        }).Message.ShouldContain("progression status");
    }

    [Fact]
    public void Build_WithAllFeatures_ProducesValidFhirJson()
    {
        // Arrange
        var builder = new RadiographicProgressionObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithInferenceId("radiographic-comprehensive-001")
            .WithPatient("Patient/patient-456", "Jane Smith")
            .WithDevice("Device/radiographic-ai-v2", "Radiographic AI Analyzer v2.0")
            .WithFocus("Condition/prostate-cancer", "Metastatic Prostate Cancer")
            .WithProgressionDetected()
            .AddImagingStudy("ImagingStudy/ct-scan-002", "CT Abdomen/Pelvis")
            .AddRadiologyReport("DocumentReference/report-002", "Radiology interpretation")
            .AddBodySite("10200004", "Liver structure")
            .AddBodySite("181422007", "Entire prostate")
            .WithImagingType("CT")
            .WithImagingDate(DateTime.Parse("2024-11-02"))
            .WithConfidence(0.85f)
            .WithQualitativeAssessment("Multiple new hepatic lesions with increased tumor burden")
            .WithConfidenceRationale("High confidence based on clear new lesion detection")
            .AddNote("New 2.5cm liver lesion in segment 6")
            .AddNote("RECIST 1.1 not applicable - no baseline measurements")
            .AddComponent("New lesion detected", true)
            .AddComponent("Largest new lesion", new Quantity(2.5m, "cm"))
            .WithSupportingFacts(_sampleFacts)
            .Build();

        // Assert
        observation.ShouldNotBeNull();

        // Serialize to JSON
        var serializer = new FhirJsonSerializer(new SerializerSettings { Pretty = true });
        string json = serializer.SerializeToString(observation);

        json.ShouldNotBeNullOrEmpty();
        json.ShouldContain("\"resourceType\": \"Observation\"");
        json.ShouldContain("\"162573006\""); // Progression of disease code
        json.ShouldContain("\"246455001\""); // Recurrence status code
        json.ShouldContain("Jane Smith");
        json.ShouldContain("Liver structure");
    }

    [Fact]
    public void Build_WithMultipleImagingStudies_AddsToDerivedFrom()
    {
        // Arrange
        var builder = new RadiographicProgressionObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithProgressionDetected()
            .AddImagingStudy("ImagingStudy/ct-001", "CT Chest")
            .AddImagingStudy("ImagingStudy/ct-002", "CT Abdomen")
            .AddImagingStudy("ImagingStudy/ct-003", "CT Pelvis")
            .Build();

        // Assert
        observation.ShouldNotBeNull();
        observation.DerivedFrom.ShouldNotBeNull();
        observation.DerivedFrom.Count.ShouldBe(3);
        observation.DerivedFrom[0].Reference.ShouldBe("ImagingStudy/ct-001");
        observation.DerivedFrom[1].Reference.ShouldBe("ImagingStudy/ct-002");
        observation.DerivedFrom[2].Reference.ShouldBe("ImagingStudy/ct-003");
    }

    [Fact]
    public void Build_WithMultipleRadiologyReports_AddsToDerivedFrom()
    {
        // Arrange
        var builder = new RadiographicProgressionObservationBuilder(_configuration);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithProgressionDetected()
            .AddRadiologyReport("DocumentReference/report-001", "Baseline report")
            .AddRadiologyReport("DocumentReference/report-002", "Follow-up report")
            .Build();

        // Assert
        observation.ShouldNotBeNull();
        observation.DerivedFrom.Count.ShouldBe(2);
        observation.DerivedFrom[0].Reference.ShouldBe("DocumentReference/report-001");
        observation.DerivedFrom[1].Reference.ShouldBe("DocumentReference/report-002");
    }

    [Fact]
    public void Build_ProgressionDetectedScenario_MatchesExpectedOutput()
    {
        // Arrange
        var builder = new RadiographicProgressionObservationBuilder(_configuration);

        // Act - Real-world scenario: new liver metastases detected
        Observation observation = builder
            .WithInferenceId("radiographic-progression-real-001")
            .WithPatient("Patient/patient-789", "John Doe")
            .WithDevice("Device/radiographic-ai-v2", "Radiographic Assessment AI v2.0")
            .WithFocus("Condition/metastatic-prostate-cancer", "Metastatic Prostate Cancer")
            .WithProgressionDetected()
            .AddImagingStudy("ImagingStudy/ct-2024-11-02", "CT Abdomen/Pelvis with IV contrast")
            .AddRadiologyReport("DocumentReference/rad-report-789", "Radiology interpretation")
            .AddBodySite("10200004", "Liver structure")
            .WithImagingType("CT")
            .WithImagingDate(DateTime.Parse("2024-11-02"))
            .WithConfidence(0.92f)
            .WithQualitativeAssessment("New hepatic lesions consistent with metastatic progression")
            .WithConfidenceRationale("Clear visualization of new lesions with characteristic metastatic appearance")
            .AddNote("New 2.5cm hypodense lesion in liver segment 6")
            .AddNote("Additional 1.8cm lesion in segment 8")
            .AddNote("RECIST 1.1 not applicable - no baseline target lesion measurements available")
            .AddComponent("New lesion detected", true)
            .AddComponent("Number of new lesions", new Quantity(2, "lesions"))
            .AddComponent("Largest new lesion", new Quantity(2.5m, "cm"))
            .Build();

        // Assert
        observation.ShouldNotBeNull();
        observation.Id.ShouldBe("radiographic-progression-real-001");
        observation.Status.ShouldBe(ObservationStatus.Final);
        observation.Code.Coding[0].Code.ShouldBe("246455001");

        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding[0].Code.ShouldBe("162573006");

        observation.Note.Count.ShouldBe(3);
        observation.Component.Count.ShouldBeGreaterThan(4);
    }
}
