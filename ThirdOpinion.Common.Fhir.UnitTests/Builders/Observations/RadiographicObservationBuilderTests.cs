using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Builders.Observations;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;
using ThirdOpinion.Common.Fhir.Models;

namespace ThirdOpinion.Common.Fhir.UnitTests.Builders.Observations;

public class RadiographicObservationBuilderTests
{
    private readonly AiInferenceConfiguration _configuration;
    private readonly ResourceReference _deviceReference;
    private readonly ResourceReference _patientReference;
    private readonly ResourceReference _tumorReference;
    private readonly Fact[] _sampleFacts;

    public RadiographicObservationBuilderTests()
    {
        _configuration = AiInferenceConfiguration.CreateDefault();
        _patientReference = new ResourceReference("Patient/test-patient", "Test Patient");
        _deviceReference = new ResourceReference("Device/ai-device", "Radiographic Analysis Device");
        _tumorReference = new ResourceReference("Condition/tumor-123", "Primary Tumor");

        _sampleFacts = new[]
        {
            new Fact
            {
                factGuid = "aa10e02a-f391-4614-96e4-35cbe47d2a85",
                factDocumentReference = "DocumentReference/report-001",
                type = "measurement",
                fact = "Target lesion measures 45.2 mm (previously 38.5 mm)",
                @ref = new[] { "1.123" },
                timeRef = "2025-01-15",
                relevance = "Demonstrates progression"
            }
        };
    }

    #region PCWG3 Standard Tests

    [Fact]
    public void Build_PCWG3Standard_CreatesCorrectObservation()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.PCWG3);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithDetermination("True")
            .WithInitialLesions("2 new bone lesions identified")
            .WithConfirmationDate(new DateTime(2025, 1, 15))
            .WithAdditionalLesions("3 additional lesions on confirmation scan")
            .WithTimeBetweenScans("12 weeks")
            .WithConfidence(0.92f)
            .WithSummary("PCWG3 criteria met for bone scan progression")
            .Build();

        // Assert
        observation.ShouldNotBeNull();
        observation.Status.ShouldBe(ObservationStatus.Final);

        // Check category
        observation.Category.ShouldHaveSingleItem();
        observation.Category[0].Coding[0].Code.ShouldBe("imaging");

        // Check code - PCWG3 uses LOINC 44667-7 for bone scan findings
        Coding? loincCoding
            = observation.Code.Coding.First(c => c.System == FhirCodingHelper.Systems.LOINC_SYSTEM);
        loincCoding.Code.ShouldBe("44667-7");
        loincCoding.Display.ShouldBe("Bone scan findings");
        observation.Code.Text.ShouldBe("PCWG3 bone scan progression assessment");

        // Check method
        observation.Method.ShouldNotBeNull();
        observation.Method.Coding[0].Display.ShouldBe("PCWG3 Bone Scan Progression Criteria");

        // Check value - should be Progressive disease for True determination
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding[0].Code.ShouldBe("277022003"); // Progressive disease

        // Check components
        observation.Component.ShouldNotBeNull();
        observation.Component.Count.ShouldBeGreaterThan(0);

        // Check initial lesions component
        Observation.ComponentComponent? initialLesionsComponent
            = observation.Component.FirstOrDefault(c =>
                c.Code.Coding.Any(cd => cd.Code == "initial-lesions"));
        initialLesionsComponent.ShouldNotBeNull();
        var initialLesionsValue = initialLesionsComponent.Value as FhirString;
        initialLesionsValue.Value.ShouldBe("2 new bone lesions identified");

        // Check confidence component
        Observation.ComponentComponent? confidenceComponent
            = observation.Component.FirstOrDefault(c =>
                c.Code.Coding.Any(cd => cd.Code == "LA11892-6"));
        confidenceComponent.ShouldNotBeNull();
        var confidenceValue = confidenceComponent.Value as Quantity;
        confidenceValue.Value.ShouldBe(0.92m);
    }

    [Fact]
    public void Build_PCWG3WithAllFields_CreatesCompleteObservation()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.PCWG3);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithDetermination("True")
            .WithInitialLesions("2 new bone lesions")
            .WithInitialScanDate(new DateTime(2024, 10, 1))
            .WithConfirmationDate(new DateTime(2025, 1, 15))
            .WithConfirmationLesions("5 lesions total")
            .WithAdditionalLesions("3 additional lesions")
            .WithTimeBetweenScans("12 weeks")
            .WithConfidence(0.95f)
            .WithConfidenceRationale("High confidence based on clear lesion progression")
            .WithSummary("Progressive disease per PCWG3 criteria")
            .WithSupportingFacts(_sampleFacts)
            .AddNote("Confirmed by nuclear medicine specialist")
            .Build();

        // Assert
        observation.ShouldNotBeNull();
        observation.Component.Count.ShouldBe(10); // All PCWG3 components

        // Verify all PCWG3-specific components are present
        observation.Component.Any(c => c.Code.Coding.Any(cd => cd.Code == "initial-lesions"))
            .ShouldBeTrue();
        observation.Component.Any(c => c.Code.Coding.Any(cd => cd.Code == "confirmation-date"))
            .ShouldBeTrue();
        observation.Component.Any(c => c.Code.Coding.Any(cd => cd.Code == "additional-lesions"))
            .ShouldBeTrue();
        observation.Component.Any(c => c.Code.Coding.Any(cd => cd.Code == "time-between-scans"))
            .ShouldBeTrue();
        observation.Component.Any(c => c.Code.Coding.Any(cd => cd.Code == "initial-scan-date"))
            .ShouldBeTrue();
        observation.Component.Any(c => c.Code.Coding.Any(cd => cd.Code == "confirmation-lesions"))
            .ShouldBeTrue();

        // Check supporting facts extension
        observation.Extension.ShouldContain(e => e.Url == "https://thirdopinion.io/clinical-fact");

        // Check notes
        observation.Note.ShouldHaveSingleItem();
        observation.Note[0].Text.ShouldContain("nuclear medicine specialist");
    }

    [Fact]
    public void Build_PCWG3WithImagingReferences_AddsReferencesToDerivedFrom()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.PCWG3);
        var imagingStudyRef = new ResourceReference("ImagingStudy/bone-scan-123", "Bone Scan");
        var radiologyReportRef = new ResourceReference("DiagnosticReport/rad-456", "Bone Scan Report");

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .AddImagingStudy(imagingStudyRef)
            .AddRadiologyReport(radiologyReportRef)
            .Build();

        // Assert - Imaging studies and radiology reports should work with PCWG3 too
        observation.DerivedFrom.Count.ShouldBe(2);
        observation.DerivedFrom.ShouldContain(imagingStudyRef);
        observation.DerivedFrom.ShouldContain(radiologyReportRef);
    }

    #endregion

    #region RECIST Standard Tests

    [Fact]
    public void Build_RECISTStandard_CreatesCorrectObservation()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.RECIST_1_1);
        var imagingStudyRef = new ResourceReference("ImagingStudy/ct-123", "CT Chest/Abdomen");
        var radiologyReportRef = new ResourceReference("DiagnosticReport/rad-456", "Radiology Report");

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithRecistResponse(FhirCodingHelper.NciCodes.PROGRESSIVE_DISEASE, "Progressive Disease")
            .WithBodySite("39607008", "Lung structure")
            .AddComponent("33728-8", new Quantity
            {
                Value = 45.2m,
                Unit = "mm",
                System = "http://unitsofmeasure.org",
                Code = "mm"
            })
            .AddComponent("new-lesions", true)
            .AddImagingStudy(imagingStudyRef)
            .AddRadiologyReport(radiologyReportRef)
            .WithMeasurementChange("20% increase in sum of longest diameters")
            .WithImagingType("CT")
            .WithImagingDate(new DateTime(2025, 1, 15))
            .WithConfidence(0.88f)
            .Build();

        // Assert
        observation.ShouldNotBeNull();
        observation.Status.ShouldBe(ObservationStatus.Final);

        // Check code - RECIST uses LOINC 21976-6 and NCI C111544
        observation.Code.Coding.Count.ShouldBe(2);
        Coding? loincCoding
            = observation.Code.Coding.First(c => c.System == FhirCodingHelper.Systems.LOINC_SYSTEM);
        loincCoding.Code.ShouldBe("21976-6");
        loincCoding.Display.ShouldBe("Cancer disease status");

        Coding? nciCoding
            = observation.Code.Coding.First(c => c.System == FhirCodingHelper.Systems.NCI_SYSTEM);
        nciCoding.Code.ShouldBe("C111544");
        nciCoding.Display.ShouldBe("RECIST 1.1");

        // Check method
        observation.Method.ShouldNotBeNull();
        observation.Method.Coding[0].Display.ShouldBe("RECIST 1.1");

        // Check body site
        observation.BodySite.ShouldNotBeNull();
        observation.BodySite.Coding[0].Code.ShouldBe("39607008");

        // Check derivedFrom includes imaging studies and reports
        observation.DerivedFrom.Count.ShouldBe(2);
        observation.DerivedFrom.ShouldContain(imagingStudyRef);
        observation.DerivedFrom.ShouldContain(radiologyReportRef);

        // Check RECIST response value
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding[0].Code.ShouldBe(FhirCodingHelper.NciCodes.PROGRESSIVE_DISEASE);

        // Check components
        observation.Component.ShouldNotBeNull();
        observation.Component.Count.ShouldBeGreaterThan(0);

        // Check SLD component
        Observation.ComponentComponent? sldComponent
            = observation.Component.FirstOrDefault(c =>
                c.Code.Coding.Any(cd => cd.Code == "33728-8"));
        sldComponent.ShouldNotBeNull();
        var sldValue = sldComponent.Value as Quantity;
        sldValue.Value.ShouldBe(45.2m);

        // Check new lesions component
        Observation.ComponentComponent? newLesionsComponent
            = observation.Component.FirstOrDefault(c =>
                c.Code.Coding.Any(cd => cd.Code == "new-lesions"));
        newLesionsComponent.ShouldNotBeNull();
        var newLesionsValue = newLesionsComponent.Value as FhirBoolean;
        newLesionsValue.Value.ShouldBe(true);
    }

    [Fact]
    public void Build_RECISTWithTimepointsJson_IncludesExtension()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.RECIST_1_1);
        const string timepointsJson = "{\"baseline\":{\"date\":\"2024-01-01\",\"sld\":100}}";

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithRecistTimepointsJson(timepointsJson)
            .Build();

        // Assert
        observation.Extension.ShouldContain(e =>
            e.Url == "https://thirdopinion.io/recist-timepoints");
    }

    [Fact]
    public void Build_RECISTWithAllComponentTypes_CreatesAllComponents()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.RECIST_1_1);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .AddComponent("33728-8", new Quantity { Value = 45.2m, Unit = "mm" }) // Quantity
            .AddComponent("new-lesions", true) // Boolean
            .AddComponent("custom-response",
                FhirCodingHelper.CreateSnomedConcept("277022003", "Progressive disease")) // CodeableConcept
            .Build();

        // Assert
        observation.Component.Count.ShouldBe(3);
        observation.Component.Any(c => c.Value is Quantity).ShouldBeTrue();
        observation.Component.Any(c => c.Value is FhirBoolean).ShouldBeTrue();
        observation.Component.Any(c => c.Value is CodeableConcept).ShouldBeTrue();
    }

    #endregion

    #region Observed Standard Tests

    [Fact]
    public void Build_ObservedStandard_CreatesCorrectObservation()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.Observed);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithDetermination("True")
            .WithSummary("Observed radiographic progression without formal criteria")
            .WithConfidence(0.75f)
            .Build();

        // Assert
        observation.ShouldNotBeNull();
        observation.Status.ShouldBe(ObservationStatus.Final);

        // Check code - Observed uses LOINC 59462-2
        Coding? loincCoding
            = observation.Code.Coding.First(c => c.System == FhirCodingHelper.Systems.LOINC_SYSTEM);
        loincCoding.Code.ShouldBe("59462-2");
        loincCoding.Display.ShouldBe("Imaging study Observations");
        observation.Code.Text.ShouldBe("Observed radiographic progression");

        // Check method
        observation.Method.ShouldNotBeNull();
        observation.Method.Coding[0].Display.ShouldBe("Observed Radiographic Assessment");

        // Check value
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding[0].Code.ShouldBe("277022003"); // Progressive disease
    }

    [Fact]
    public void Build_ObservedWithImagingReferences_AddsReferencesToDerivedFrom()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.Observed);
        var imagingStudyRef = new ResourceReference("ImagingStudy/ct-123", "CT Scan");
        var radiologyReportRef = new ResourceReference("DiagnosticReport/rad-456", "CT Report");

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .AddImagingStudy(imagingStudyRef)
            .AddRadiologyReport(radiologyReportRef)
            .Build();

        // Assert - Imaging studies and radiology reports should work with Observed standard too
        observation.DerivedFrom.Count.ShouldBe(2);
        observation.DerivedFrom.ShouldContain(imagingStudyRef);
        observation.DerivedFrom.ShouldContain(radiologyReportRef);
    }

    #endregion

    #region Common Functionality Tests

    [Fact]
    public void Build_WithDeterminationTrue_CreatesProgressiveDiseaseValue()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.PCWG3);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithDetermination("True")
            .Build();

        // Assert
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding[0].Code.ShouldBe("277022003"); // Progressive disease
        valueCodeableConcept.Coding[0].Display.ShouldBe("Progressive disease");
    }

    [Fact]
    public void Build_WithDeterminationFalse_CreatesStableDiseaseValue()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.PCWG3);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithDetermination("False")
            .Build();

        // Assert
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding[0].Code.ShouldBe("359746009"); // Stable disease
        valueCodeableConcept.Coding[0].Display.ShouldBe("Stable disease");
    }

    [Fact]
    public void Build_WithDeterminationInconclusive_CreatesInconclusiveValue()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.PCWG3);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithDetermination("Inconclusive")
            .Build();

        // Assert
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding[0].Code.ShouldBe("373067005"); // Inconclusive
        valueCodeableConcept.Coding[0].Display.ShouldBe("Inconclusive");
    }

    [Fact]
    public void WithDetermination_InvalidValue_ThrowsException()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.PCWG3)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference);

        // Act & Assert
        Should.Throw<ArgumentException>(() => builder.WithDetermination("Invalid"));
    }

    [Fact]
    public void Build_WithSupportingFacts_AddsFactExtensions()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.PCWG3);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithSupportingFacts(_sampleFacts)
            .Build();

        // Assert
        observation.Extension.ShouldContain(e => e.Url == "https://thirdopinion.io/clinical-fact");
        observation.Extension.Count(e => e.Url == "https://thirdopinion.io/clinical-fact")
            .ShouldBe(_sampleFacts.Length);
    }

    [Fact]
    public void Build_WithConflictingFacts_AddsConflictingFactExtensions()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.PCWG3);
        var conflictingFact = new Fact
        {
            factGuid = "cc10e02a-f391-4614-96e4-35cbe47d2a87",
            type = "conflicting",
            fact = "Prior scan showed stable disease",
            relevance = "Conflicts with current progression finding"
        };

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithConflictingFacts(conflictingFact)
            .Build();

        // Assert
        observation.Extension.ShouldContain(e => e.Url == "https://thirdopinion.io/conflicting-fact");
    }

    [Fact]
    public void Build_WithoutPatient_ThrowsException()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.PCWG3)
            .WithDevice(_deviceReference);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_WithoutDevice_ThrowsException()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.PCWG3)
            .WithPatient(_patientReference);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_WithEffectiveDate_SetsCorrectDate()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.PCWG3);
        var effectiveDate = new DateTime(2025, 1, 15, 10, 30, 0);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithEffectiveDate(effectiveDate)
            .Build();

        // Assert
        var fhirDate = observation.Effective as FhirDateTime;
        fhirDate.ShouldNotBeNull();
        fhirDate.ToDateTimeOffset(TimeSpan.Zero).Date.ShouldBe(effectiveDate.Date);
    }

    [Fact]
    public void FluentInterface_AllMethods_ReturnBuilderInstance()
    {
        // Arrange & Act
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.PCWG3)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithDetermination("True")
            .WithConfidence(0.9f)
            .WithConfidenceRationale("High confidence")
            .WithSummary("Summary text")
            .AddNote("Note text");

        // Assert - if fluent interface works, builder should not be null and Build() should succeed
        builder.ShouldNotBeNull();
        Observation observation = builder.Build();
        observation.ShouldNotBeNull();
    }

    [Fact]
    public void Build_WithInferenceId_IncludesInferenceIdInMethod()
    {
        // Arrange
        const string inferenceId = "test-inference-123";
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.PCWG3);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithInferenceId(inferenceId)
            .Build();

        // Assert
        observation.Method.ShouldNotBeNull();
        observation.Method.Coding[0].Code.ShouldContain(inferenceId);
    }

    [Fact]
    public void Build_WithConfirmationDate_AddsConfirmationDateComponent()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.PCWG3);
        var confirmationDate = new DateTime(2025, 1, 15);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithConfirmationDate(confirmationDate)
            .Build();

        // Assert
        Observation.ComponentComponent? confirmationDateComponent
            = observation.Component.FirstOrDefault(c =>
                c.Code.Coding.Any(cd => cd.Code == "confirmation-date"));
        confirmationDateComponent.ShouldNotBeNull();
        var dateValue = confirmationDateComponent.Value as FhirDateTime;
        dateValue.ShouldNotBeNull();
    }

    #endregion
}
