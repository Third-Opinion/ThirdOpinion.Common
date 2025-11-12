using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Builders.Observations;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;
using ThirdOpinion.Common.Fhir.Models;
using Xunit.Abstractions;

namespace ThirdOpinion.Common.Fhir.UnitTests.Builders.Observations;

public class RadiographicObservationBuilderTests
{
    private readonly AiInferenceConfiguration _configuration;
    private readonly ResourceReference _deviceReference;
    private readonly ResourceReference _patientReference;
    private readonly ResourceReference _tumorReference;
    private readonly Fact[] _sampleFacts;
    private readonly ITestOutputHelper _output;

    public RadiographicObservationBuilderTests(ITestOutputHelper output)
    {
        _output = output;
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
            .WithDetermination("PD")
            .WithInitialLesions("2 new bone lesions identified")
            .WithConfirmationDate(new DateTime(2025, 1, 15))
            .WithAdditionalLesions("3 additional lesions on confirmation scan")
            .WithTimeBetweenScans("12 weeks")
            .WithConfidence(0.92f)
            .AddNote("PCWG3 criteria met for bone scan progression")
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
            .WithDetermination("PD")
            .WithInitialLesions("2 new bone lesions")
            .WithInitialScanDate(new DateTime(2024, 10, 1))
            .WithConfirmationDate(new DateTime(2025, 1, 15))
            .WithConfirmationLesions("5 lesions total")
            .WithAdditionalLesions("3 additional lesions")
            .WithTimeBetweenScans("12 weeks")
            .WithConfidence(0.95f)
            .WithConfidenceRationale("High confidence based on clear lesion progression")
            .AddNote("Progressive disease per PCWG3 criteria")
            .WithSupportingFacts(_sampleFacts)
            .AddNote("Confirmed by nuclear medicine specialist")
            .Build();

        // Assert
        observation.ShouldNotBeNull();
        observation.Component.Count.ShouldBe(9); // All PCWG3 components (summary moved to Note)

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

        // Check notes (should have 2 notes from two AddNote calls)
        observation.Note.Count.ShouldBe(2);
        observation.Note.Any(n => n.Text.ToString().Contains("Progressive disease per PCWG3 criteria")).ShouldBeTrue();
        observation.Note.Any(n => n.Text.ToString().Contains("nuclear medicine specialist")).ShouldBeTrue();
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

        // Check derivedFrom includes radiology report
        observation.DerivedFrom.Count.ShouldBe(1);
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
            .WithDetermination("PD")
            .AddNote("Observed radiographic progression without formal criteria")
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
    public void Build_WithDuplicateDerivedFromReferences_DeduplicatesCorrectly()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.Observed);
        var documentRef = "DocumentReference/doc-123";

        // Create multiple facts that reference the same document
        var facts = new[]
        {
            new Fact
            {
                factGuid = "fact-001",
                factDocumentReference = documentRef,
                fact = "First finding from the document"
            },
            new Fact
            {
                factGuid = "fact-002",
                factDocumentReference = documentRef,
                fact = "Second finding from the same document"
            },
            new Fact
            {
                factGuid = "fact-003",
                factDocumentReference = documentRef,
                fact = "Third finding from the same document"
            }
        };

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithSupportingFacts(facts)
            .Build();

        // Assert - Should only have ONE derivedFrom entry even though 3 facts reference the same document
        observation.DerivedFrom.Count.ShouldBe(1);
        observation.DerivedFrom[0].Reference.ShouldBe(documentRef);
    }

    [Fact]
    public void Build_ObservedWithObservedChanges_AddsObservedChangesComponent()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.Observed);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithDetermination("Inconclusive")
            .WithObservedChanges("Progression")
            .AddNote("RECIST criteria inconclusive, but clear progression observed")
            .Build();

        // Assert
        observation.ShouldNotBeNull();

        // Check that observedChanges component was added
        Observation.ComponentComponent? observedChangesComponent = observation.Component
            .FirstOrDefault(c => c.Code.Coding.Any(coding => coding.Code == "observed-changes"));

        observedChangesComponent.ShouldNotBeNull();
        observedChangesComponent.Code.Coding[0].System.ShouldBe(
            "http://thirdopinion.ai/fhir/CodeSystem/radiographic-components");
        observedChangesComponent.Code.Coding[0].Code.ShouldBe("observed-changes");
        observedChangesComponent.Code.Coding[0].Display.ShouldBe("Observed Changes");

        // Verify the value is a CodeableConcept with SNOMED code
        var valueCodeableConcept = observedChangesComponent.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding.ShouldNotBeEmpty();
        valueCodeableConcept.Coding[0].System.ShouldBe("http://snomed.info/sct");
        valueCodeableConcept.Coding[0].Code.ShouldBe("444391001");
        valueCodeableConcept.Coding[0].Display.ShouldBe("Malignant tumor progression (finding)");

        // Check that determination is Inconclusive
        Observation.ComponentComponent? determinationComponent = observation.Component
            .FirstOrDefault(c => c.Code.Coding.Any(coding => coding.Code == "determination"));
        determinationComponent.ShouldNotBeNull();
        var determinationValue = determinationComponent.Value as FhirString;
        determinationValue?.Value.ShouldBe("Inconclusive");
    }

    [Fact]
    public void Build_ObservedWithoutObservedChanges_DoesNotAddComponent()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.Observed);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithDetermination("PD")
            .Build();

        // Assert
        observation.ShouldNotBeNull();

        // Check that observedChanges component was NOT added
        Observation.ComponentComponent? observedChangesComponent = observation.Component
            .FirstOrDefault(c => c.Code.Coding.Any(coding => coding.Code == "observed-changes"));

        observedChangesComponent.ShouldBeNull();
    }

    [Fact]
    public void Build_ObservedWithStableChanges_AddsSnomedCode()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.Observed);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithDetermination("Inconclusive")
            .WithObservedChanges("Stable")
            .AddNote("RECIST criteria inconclusive, but lesions appear stable")
            .Build();

        // Assert
        observation.ShouldNotBeNull();

        // Check that observedChanges component has correct SNOMED code for Stable
        Observation.ComponentComponent? observedChangesComponent = observation.Component
            .FirstOrDefault(c => c.Code.Coding.Any(coding => coding.Code == "observed-changes"));

        observedChangesComponent.ShouldNotBeNull();

        var valueCodeableConcept = observedChangesComponent.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding.ShouldNotBeEmpty();
        valueCodeableConcept.Coding[0].System.ShouldBe("http://snomed.info/sct");
        valueCodeableConcept.Coding[0].Code.ShouldBe("713837000");
        valueCodeableConcept.Coding[0].Display.ShouldBe("Neoplasm stable (finding)");
    }

    [Fact]
    public void Build_ObservedWithRegressionChanges_AddsSnomedCode()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.Observed);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithDetermination("Inconclusive")
            .WithObservedChanges("Regression")
            .AddNote("RECIST criteria inconclusive, but tumor regression observed")
            .Build();

        // Assert
        observation.ShouldNotBeNull();

        // Check that observedChanges component has correct SNOMED code for Regression
        Observation.ComponentComponent? observedChangesComponent = observation.Component
            .FirstOrDefault(c => c.Code.Coding.Any(coding => coding.Code == "observed-changes"));

        observedChangesComponent.ShouldNotBeNull();

        var valueCodeableConcept = observedChangesComponent.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding.ShouldNotBeEmpty();
        valueCodeableConcept.Coding[0].System.ShouldBe("http://snomed.info/sct");
        valueCodeableConcept.Coding[0].Code.ShouldBe("265743007");
        valueCodeableConcept.Coding[0].Display.ShouldBe("Regression of neoplasm (finding)");
    }

    [Fact]
    public void Build_ObservedWithUnmappedChanges_UsesFallbackText()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.Observed);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithDetermination("Inconclusive")
            .WithObservedChanges("Mixed response with some progression and some regression")
            .AddNote("Complex response pattern")
            .Build();

        // Assert
        observation.ShouldNotBeNull();

        // Check that observedChanges component uses text fallback for unmapped values
        Observation.ComponentComponent? observedChangesComponent = observation.Component
            .FirstOrDefault(c => c.Code.Coding.Any(coding => coding.Code == "observed-changes"));

        observedChangesComponent.ShouldNotBeNull();

        var valueCodeableConcept = observedChangesComponent.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Text.ShouldBe("Mixed response with some progression and some regression");
        // Should not have SNOMED coding for unmapped values
        valueCodeableConcept.Coding.ShouldBeEmpty();
    }

    #endregion

    #region Common Functionality Tests

    [Fact]
    public void Build_WithDeterminationPD_CreatesProgressiveDiseaseValue()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.PCWG3);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithDetermination("PD")
            .Build();

        // Assert
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding[0].Code.ShouldBe("277022003"); // Progressive disease
        valueCodeableConcept.Coding[0].Display.ShouldBe("Progressive disease");
    }

    [Fact]
    public void Build_WithDeterminationSD_CreatesStableDiseaseValue()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.PCWG3);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithDetermination("SD")
            .Build();

        // Assert
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding[0].Code.ShouldBe("359746009"); // Stable disease
        valueCodeableConcept.Coding[0].Display.ShouldBe("Stable disease");
    }

    [Fact]
    public void Build_WithDeterminationCR_CreatesCompleteResponseValue()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.PCWG3);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithDetermination("CR")
            .Build();

        // Assert
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding[0].Code.ShouldBe("268910001"); // Complete response
        valueCodeableConcept.Coding[0].Display.ShouldBe("Complete response");
    }

    [Fact]
    public void Build_WithDeterminationPR_CreatesPartialResponseValue()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.PCWG3);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithDetermination("PR")
            .Build();

        // Assert
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding[0].Code.ShouldBe("268905007"); // Partial response
        valueCodeableConcept.Coding[0].Display.ShouldBe("Partial response");
    }

    [Fact]
    public void Build_WithDeterminationBaseline_CreatesBaselineValue()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.PCWG3);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithDetermination("Baseline")
            .Build();

        // Assert
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding[0].Code.ShouldBe("261935009"); // Baseline
        valueCodeableConcept.Coding[0].Display.ShouldBe("Baseline (qualifier value)");
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
        valueCodeableConcept.Coding[0].Code.ShouldBe("419984006"); // Inconclusive
        valueCodeableConcept.Coding[0].Display.ShouldStartWith("Inconclusive");
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
            .WithDetermination("PD")
            .WithConfidence(0.9f)
            .WithConfidenceRationale("High confidence")
            .AddNote("Summary text")
            .AddNote("Note text");

        // Assert - if fluent interface works, builder should not be null and Build() should succeed
        builder.ShouldNotBeNull();
        Observation observation = builder.Build();
        observation.ShouldNotBeNull();
    }

    [Fact]
    public void Build_WithFhirResourceId_IncludesInferenceIdInMethod()
    {
        // Arrange
        const string inferenceId = "pcwg3-bone-progression";
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.PCWG3);

        // Act
        Observation observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithFhirResourceId(inferenceId)
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

    #region JSON Serialization Tests

    [Fact]
    public void Build_PCWG3WithProgressionExample_SerializesToJson()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.PCWG3);

        var supportingFacts = new[]
        {
            new Fact
            {
                factGuid = "fact-001",
                factDocumentReference = "DocumentReference/psma-2024-07-03",
                type = "imaging",
                fact = "PSMA imaging from 2024-07-03 showed 'progression of metastatic disease'",
                @ref = new[] { "1.100" },
                timeRef = "2024-07-03",
                relevance = "Initial progression documentation"
            },
            new Fact
            {
                factGuid = "fact-002",
                factDocumentReference = "DocumentReference/psma-2025-03-10",
                type = "imaging",
                fact = "PSMA from 2025-03-10 demonstrating 'widespread progression of bone metastases'",
                @ref = new[] { "1.101" },
                timeRef = "2025-03-10",
                relevance = "Confirmation of widespread progression"
            },
            new Fact
            {
                factGuid = "fact-003",
                factDocumentReference = "DocumentReference/radiation-c-spine",
                type = "treatment",
                fact = "Patient developed new symptomatic sites requiring palliative radiation (C-spine pain documented 2025-03-17, treated with RT completed 2025-04-25)",
                @ref = new[] { "1.102" },
                timeRef = "2025-03-17",
                relevance = "New symptomatic bone lesions requiring treatment"
            }
        };

        // Act
        Observation observation = builder
            .WithPatient("Patient/example-patient", "Example Patient")
            .WithDevice("Device/ai-pcwg3-analyzer", "AI PCWG3 Assessment Device")
            .WithFocus(new ResourceReference("Condition/prostate-cancer", "Metastatic Prostate Cancer"))
            .WithDetermination("PD")
            .WithConfidence(0.9f)
            .AddNote("<div xmlns='http://www.w3.org/1999/xhtml'><p><strong>PCWG3 Bone Progression: True</strong></p><p><strong>Rationale:</strong> The patient demonstrates clear bone disease progression meeting PCWG3 criteria based on serial PSMA PET imaging:</p><p><strong>Evidence of Progression:</strong></p><ul><li><strong>Initial Disease:</strong> Patient had documented bone metastases requiring palliative radiation to T10-T11 vertebrae (completed April 2023) and later to C-spine (completed 2025-04-25).</li><li><strong>Progression Documentation:</strong> PSMA imaging from 2024-07-03 showed 'progression of metastatic disease', followed by PSMA from 2025-03-10 demonstrating 'widespread progression of bone metastases'.</li><li><strong>Clinical Correlation:</strong> Patient developed new symptomatic sites requiring palliative radiation (C-spine pain documented 2025-03-17, treated with RT completed 2025-04-25), indicating new or progressive bone lesions.</li><li><strong>Treatment Escalation:</strong> Progression through multiple lines of therapy (ADT + Xtandi, ADT + Zytiga, chemotherapy, now Pluvicto) with continued bone disease progression supports PCWG3 progression.</li></ul><p><strong>PCWG3 2+2 Rule Consideration:</strong> While the documentation does not explicitly state '2 or more new lesions confirmed on subsequent scan', the clinical narrative of 'widespread progression' on serial PSMA scans, new symptomatic sites requiring radiation, and progression through multiple therapies strongly indicates bone progression meeting PCWG3 criteria. PSMA PET is more sensitive than traditional bone scan and the terminology 'widespread progression' implies multiple new or enlarging lesions.</p></div>")
            .WithInitialLesions("Multiple (T10, T11 vertebrae documented, widespread disease)")
            .WithConfirmationDate(new DateTime(2025, 3, 10))
            .WithSupportingFacts(supportingFacts)
            .WithEffectiveDate(new DateTime(2025, 3, 10))
            .Build();

        // Serialize to JSON (formatted)
        var serializer = new Hl7.Fhir.Serialization.FhirJsonSerializer(new Hl7.Fhir.Serialization.SerializerSettings { Pretty = true });
        var json = serializer.SerializeToString(observation);

        // Output to test output
        _output.WriteLine("=== PCWG3 Progression Example ===");
        _output.WriteLine(json);
        _output.WriteLine("");

        // Assert
        observation.ShouldNotBeNull();
        observation.Value.ShouldNotBeNull();
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding.First().Code.ShouldBe("277022003"); // Progressive disease
    }

    [Fact]
    public void Build_RECISTWithProgressionExample_SerializesToJson()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.RECIST_1_1);

        var supportingFacts = new[]
        {
            new Fact
            {
                factGuid = "fact-101",
                factDocumentReference = "DocumentReference/ct-2024-12-20",
                type = "imaging",
                fact = "CT scan on 12/20/2024 revealed a new polypoid mass contiguous with the bladder and prostate, representing new soft tissue disease",
                @ref = new[] { "2.200" },
                timeRef = "2024-12-20",
                relevance = "New lesion constitutes RECIST progression"
            },
            new Fact
            {
                factGuid = "fact-102",
                factDocumentReference = "DocumentReference/ct-lymph-nodes",
                type = "imaging",
                fact = "Persistent bilateral pelvic and paraaortic lymphadenopathy documented on imaging",
                @ref = new[] { "2.201" },
                timeRef = "2024-12-20",
                relevance = "Persistent disease burden"
            }
        };

        // Act
        Observation observation = builder
            .WithPatient("Patient/example-patient-2", "Example Patient 2")
            .WithDevice("Device/ai-recist-analyzer", "AI RECIST 1.1 Assessment Device")
            .WithFocus(new ResourceReference("Condition/prostate-cancer-2", "Metastatic Prostate Cancer"))
            .WithDetermination("PD")
            .WithConfidence(0.75f)
            .AddNote("<div xmlns='http://www.w3.org/1999/xhtml'><p><strong>RECIST 1.1 Progression: TRUE</strong></p><p><strong>Evidence:</strong></p><ul><li>CT scan on 12/20/2024 revealed a new polypoid mass contiguous with the bladder and prostate, representing new soft tissue disease.</li><li>Persistent bilateral pelvic and paraaortic lymphadenopathy documented on imaging.</li><li>The appearance of a new polypoid mass meets RECIST 1.1 criteria for progressive disease (new lesions constitute progression regardless of target lesion measurements).</li></ul><p><strong>Assessment:</strong> The identification of a new soft tissue mass (polypoid lesion) on CT imaging constitutes radiographic progression by RECIST 1.1 criteria. While specific measurements and comparison to prior imaging are not provided in the documentation, the presence of new lesions is sufficient for a determination of progressive disease. Confidence is moderate-high (0.75) due to clear documentation of new soft tissue findings, though formal radiology reports with measurements would increase certainty.</p></div>")
            .WithSupportingFacts(supportingFacts)
            .WithImagingType("CT")
            .WithImagingDate(new DateTime(2024, 12, 20))
            .WithEffectiveDate(new DateTime(2024, 12, 20))
            .AddRadiologyReport(new ResourceReference("DocumentReference/ct-2024-12-20", "CT Abdomen/Pelvis"))
            .Build();

        // Serialize to JSON (formatted)
        var serializer = new Hl7.Fhir.Serialization.FhirJsonSerializer(new Hl7.Fhir.Serialization.SerializerSettings { Pretty = true });
        var json = serializer.SerializeToString(observation);

        // Output to test output
        _output.WriteLine("=== RECIST 1.1 Progression Example ===");
        _output.WriteLine(json);
        _output.WriteLine("");

        // Assert
        observation.ShouldNotBeNull();
        observation.Value.ShouldNotBeNull();
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding.First().Code.ShouldBe("277022003"); // Progressive disease
    }

    [Fact]
    public void Build_PCWG3CompleteResponseExample_SerializesToJson()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.PCWG3);

        // Act
        Observation observation = builder
            .WithPatient("Patient/cr-patient", "CR Patient")
            .WithDevice("Device/ai-pcwg3-analyzer", "AI PCWG3 Assessment Device")
            .WithFocus(new ResourceReference("Condition/prostate-cancer-cr", "Prostate Cancer"))
            .WithDetermination("CR")
            .WithConfidence(0.95f)
            .AddNote("<div xmlns='http://www.w3.org/1999/xhtml'><p>Complete resolution of all bone lesions on follow-up imaging. No new lesions identified.</p></div>")
            .WithInitialLesions("Multiple bone metastases at T10, T11, L3")
            .WithConfirmationDate(new DateTime(2025, 6, 15))
            .WithEffectiveDate(new DateTime(2025, 6, 15))
            .Build();

        // Serialize to JSON (formatted)
        var serializer = new Hl7.Fhir.Serialization.FhirJsonSerializer(new Hl7.Fhir.Serialization.SerializerSettings { Pretty = true });
        var json = serializer.SerializeToString(observation);

        // Output to test output
        _output.WriteLine("=== PCWG3 Complete Response Example ===");
        _output.WriteLine(json);
        _output.WriteLine("");

        // Assert
        observation.ShouldNotBeNull();
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding.First().Code.ShouldBe("268910001"); // Complete response
    }

    [Fact]
    public void Build_RECISTStableDiseaseExample_SerializesToJson()
    {
        // Arrange
        var builder = new RadiographicObservationBuilder(_configuration, RadiographicStandard.RECIST_1_1);

        // Act
        Observation observation = builder
            .WithPatient("Patient/sd-patient", "SD Patient")
            .WithDevice("Device/ai-recist-analyzer", "AI RECIST 1.1 Assessment Device")
            .WithFocus(new ResourceReference("Condition/lung-cancer", "Non-Small Cell Lung Cancer"))
            .WithDetermination("SD")
            .WithConfidence(0.88f)
            .AddNote("<div xmlns='http://www.w3.org/1999/xhtml'><p>Target lesions remain stable with <10% change in sum of longest diameters. No new lesions.</p></div>")
            .WithMeasurementChange("+5% change in SLD from baseline")
            .WithImagingType("CT Chest")
            .WithImagingDate(new DateTime(2025, 4, 1))
            .WithEffectiveDate(new DateTime(2025, 4, 1))
            .Build();

        // Serialize to JSON (formatted)
        var serializer = new Hl7.Fhir.Serialization.FhirJsonSerializer(new Hl7.Fhir.Serialization.SerializerSettings { Pretty = true });
        var json = serializer.SerializeToString(observation);

        // Output to test output
        _output.WriteLine("=== RECIST 1.1 Stable Disease Example ===");
        _output.WriteLine(json);
        _output.WriteLine("");

        // Assert
        observation.ShouldNotBeNull();
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding.First().Code.ShouldBe("359746009"); // Stable disease
    }

    #endregion
}