using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using ThirdOpinion.Common.Fhir.Builders.Observations;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;
using ThirdOpinion.Common.Fhir.Models;

namespace ThirdOpinion.Common.Fhir.UnitTests.Builders.Observations;

public class RecistProgressionObservationBuilderTests
{
    private readonly AiInferenceConfiguration _configuration;
    private readonly ResourceReference _patientReference;
    private readonly ResourceReference _deviceReference;
    private readonly ResourceReference _tumorReference;
    private readonly ResourceReference _imagingStudyReference;
    private readonly ResourceReference _radiologyReportReference;
    private readonly Fact[] _sampleFacts;

    public RecistProgressionObservationBuilderTests()
    {
        _configuration = AiInferenceConfiguration.CreateDefault();
        _patientReference = new ResourceReference("Patient/test-patient", "Test Patient");
        _deviceReference = new ResourceReference("Device/ai-device", "RECIST Analysis Device");
        _tumorReference = new ResourceReference("Condition/tumor-123", "Primary Tumor");
        _imagingStudyReference = new ResourceReference("ImagingStudy/ct-123", "CT Chest/Abdomen");
        _radiologyReportReference = new ResourceReference("DiagnosticReport/rad-456", "Radiology Report");

        _sampleFacts = new[]
        {
            new Fact
            {
                factGuid = "aa10e02a-f391-4614-96e4-35cbe47d2a85",
                factDocumentReference = "DocumentReference/ct-report-001",
                type = "measurement",
                fact = "Target lesion 1 measures 45.2 mm (previously 38.5 mm)",
                @ref = new[] { "1.123" },
                timeRef = "2025-01-15",
                relevance = "Demonstrates progression in target lesion"
            },
            new Fact
            {
                factGuid = "bb10e02a-f391-4614-96e4-35cbe47d2a86",
                factDocumentReference = "DocumentReference/ct-report-002",
                type = "finding",
                fact = "New liver metastasis identified measuring 12 mm",
                @ref = new[] { "2.456" },
                timeRef = "2025-01-15",
                relevance = "New lesion indicating disease progression"
            }
        };
    }

    [Fact]
    public void Build_WithProgressiveDisease_CreatesCorrectObservation()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithCriteria("1.1")
            .WithRecistResponse(FhirCodingHelper.NciCodes.PROGRESSIVE_DISEASE, "Progressive Disease")
            .AddComponent("33728-8", new Quantity { Value = 45.2m, Unit = "mm", System = "http://unitsofmeasure.org", Code = "mm" })
            .AddComponent("nadir-sld", new Quantity { Value = 38.5m, Unit = "mm", System = "http://unitsofmeasure.org", Code = "mm" })
            .AddComponent("percent-change", new Quantity { Value = 17.4m, Unit = "%", System = "http://unitsofmeasure.org", Code = "%" })
            .AddComponent("new-lesions", true)
            .AddImagingStudy(_imagingStudyReference)
            .AddRadiologyReport(_radiologyReportReference)
            .WithBodySite("39607008", "Lung structure")
            .Build();

        // Assert
        observation.ShouldNotBeNull();
        observation.Status.ShouldBe(ObservationStatus.Final);

        // Check category
        observation.Category.ShouldHaveSingleItem();
        observation.Category[0].Coding[0].Code.ShouldBe("imaging");

        // Check codes - should have both LOINC 21976-6 and NCI C111544
        observation.Code.Coding.Count.ShouldBe(2);
        var loincCoding = observation.Code.Coding.First(c => c.System == FhirCodingHelper.Systems.LOINC_SYSTEM);
        loincCoding.Code.ShouldBe("21976-6");
        loincCoding.Display.ShouldBe("Cancer disease status");

        var nciCoding = observation.Code.Coding.First(c => c.System == FhirCodingHelper.Systems.NCI_SYSTEM);
        nciCoding.Code.ShouldBe("C111544");
        nciCoding.Display.ShouldBe("RECIST 1.1");

        // Check focus
        observation.Focus.ShouldNotBeNull();
        observation.Focus.Count.ShouldBe(1);
        observation.Focus[0].ShouldBe(_tumorReference);

        // Check patient and device
        observation.Subject.ShouldBe(_patientReference);
        observation.Device.ShouldBe(_deviceReference);

        // Check RECIST response value
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding[0].Code.ShouldBe(FhirCodingHelper.NciCodes.PROGRESSIVE_DISEASE);

        // Check body site
        observation.BodySite.ShouldNotBeNull();
        observation.BodySite.Coding[0].Code.ShouldBe("39607008");

        // Check derivedFrom includes imaging studies and reports
        observation.DerivedFrom.Count.ShouldBe(2);
        observation.DerivedFrom.ShouldContain(_imagingStudyReference);
        observation.DerivedFrom.ShouldContain(_radiologyReportReference);

        // Check components
        observation.Component.ShouldNotBeNull();
        observation.Component.Count.ShouldBe(4);

        // Check SLD component
        var sldComponent = observation.Component.FirstOrDefault(c =>
            c.Code.Coding.Any(cd => cd.Code == "33728-8"));
        sldComponent.ShouldNotBeNull();
        var sldValue = sldComponent.Value as Quantity;
        sldValue.Value.ShouldBe(45.2m);

        // Check new lesions component
        var newLesionsComponent = observation.Component.FirstOrDefault(c =>
            c.Code.Coding.Any(cd => cd.Code == "new-lesions"));
        newLesionsComponent.ShouldNotBeNull();
        var newLesionsValue = newLesionsComponent.Value as FhirBoolean;
        newLesionsValue.Value.ShouldBe(true);
    }

    [Fact]
    public void Build_WithStableDisease_CreatesCorrectObservation()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act
        var observation = builder
            .WithPatient("patient-456", "Jane Doe")
            .WithDevice("device-789", "AI Imaging Analyzer")
            .WithCriteria("1.1")
            .WithRecistResponse(FhirCodingHelper.NciCodes.STABLE_DISEASE, "Stable Disease")
            .AddComponent("33728-8", new Quantity { Value = 42.0m, Unit = "mm", System = "http://unitsofmeasure.org", Code = "mm" })
            .AddComponent("percent-change", new Quantity { Value = -5.2m, Unit = "%", System = "http://unitsofmeasure.org", Code = "%" })
            .AddComponent("new-lesions", false)
            .Build();

        // Assert
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding[0].Code.ShouldBe(FhirCodingHelper.NciCodes.STABLE_DISEASE);

        var percentComponent = observation.Component.FirstOrDefault(c =>
            c.Code.Coding.Any(cd => cd.Code == "percent-change"));
        var percentValue = percentComponent?.Value as Quantity;
        percentValue.Value.ShouldBe(-5.2m);
    }

    [Fact]
    public void AddComponent_WithQuantity_AddsCorrectComponent()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);
        var quantity = new Quantity { Value = 25.5m, Unit = "mm", System = "http://unitsofmeasure.org", Code = "mm" };

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddComponent("33728-8", quantity)
            .Build();

        // Assert
        var component = observation.Component.FirstOrDefault(c =>
            c.Code.Coding.Any(cd => cd.Code == "33728-8"));
        component.ShouldNotBeNull();
        var componentValue = component.Value as Quantity;
        componentValue.ShouldBe(quantity);
    }

    [Fact]
    public void AddComponent_WithBoolean_AddsCorrectComponent()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddComponent("44666-9", true)
            .Build();

        // Assert
        var component = observation.Component.FirstOrDefault(c =>
            c.Code.Coding.Any(cd => cd.Code == "44666-9"));
        component.ShouldNotBeNull();
        var componentValue = component.Value as FhirBoolean;
        componentValue.Value.ShouldBe(true);
    }

    [Fact]
    public void AddComponent_WithCodeableConcept_AddsCorrectComponent()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);
        var concept = FhirCodingHelper.CreateSnomedConcept("123456789", "Test Concept");

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddComponent("test-code", concept)
            .Build();

        // Assert
        var component = observation.Component.FirstOrDefault(c =>
            c.Code.Coding.Any(cd => cd.Code == "test-code"));
        component.ShouldNotBeNull();
        var componentValue = component.Value as CodeableConcept;
        componentValue.ShouldBe(concept);
    }

    [Fact]
    public void WithBodySite_SetsBodySite()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithBodySite("39607008", "Lung structure")
            .Build();

        // Assert
        observation.BodySite.ShouldNotBeNull();
        observation.BodySite.Coding[0].Code.ShouldBe("39607008");
        observation.BodySite.Coding[0].Display.ShouldBe("Lung structure");
    }

    [Fact]
    public void WithFocus_MultipleFoci_SetsFocusArray()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);
        var tumor1 = new ResourceReference("Condition/tumor-1", "Primary Tumor");
        var tumor2 = new ResourceReference("Condition/tumor-2", "Metastatic Lesion");

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(tumor1, tumor2)
            .Build();

        // Assert
        observation.Focus.Count.ShouldBe(2);
        observation.Focus[0].ShouldBe(tumor1);
        observation.Focus[1].ShouldBe(tumor2);
    }

    [Fact]
    public void AddImagingStudy_MultipleStudies_AddsToDeriviedFrom()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);
        var study1 = new ResourceReference("ImagingStudy/ct-1", "Baseline CT");
        var study2 = new ResourceReference("ImagingStudy/ct-2", "Follow-up CT");

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddImagingStudy(study1)
            .AddImagingStudy(study2)
            .Build();

        // Assert
        observation.DerivedFrom.Count.ShouldBe(2);
        observation.DerivedFrom.ShouldContain(study1);
        observation.DerivedFrom.ShouldContain(study2);
    }

    [Fact]
    public void AddRadiologyReport_AddsToDeriviedFrom()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);
        var report = new ResourceReference("DiagnosticReport/rad-report", "Imaging Report");

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddRadiologyReport(report)
            .Build();

        // Assert
        observation.DerivedFrom.ShouldContain(report);
    }

    [Fact]
    public void Build_WithoutPatient_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithDevice(_deviceReference)
                .Build());

        exception.Message.ShouldContain("Patient reference is required");
    }

    [Fact]
    public void Build_WithoutDevice_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithPatient(_patientReference)
                .Build());

        exception.Message.ShouldContain("Device reference is required");
    }

    [Fact]
    public void WithPatient_NullPatient_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.WithPatient((ResourceReference)null!));
    }

    [Fact]
    public void WithPatient_EmptyPatientId_ThrowsArgumentException()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.WithPatient("", "Display"));
    }

    [Fact]
    public void WithDevice_NullDevice_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.WithDevice((ResourceReference)null!));
    }

    [Fact]
    public void WithDevice_EmptyDeviceId_ThrowsArgumentException()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.WithDevice("", "Display"));
    }

    [Fact]
    public void WithFocus_NoReferences_ThrowsArgumentException()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.WithFocus());
    }

    [Fact]
    public void WithCriteria_EmptyCriteria_ThrowsArgumentException()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.WithCriteria(""));
    }

    [Fact]
    public void AddComponent_EmptyCode_ThrowsArgumentException()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);
        var quantity = new Quantity { Value = 10m, Unit = "mm" };

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.AddComponent("", quantity));
    }

    [Fact]
    public void AddComponent_NullQuantity_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.AddComponent("test-code", (Quantity)null!));
    }

    [Fact]
    public void AddComponent_NullCodeableConcept_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.AddComponent("test-code", (CodeableConcept)null!));
    }

    [Fact]
    public void WithRecistResponse_EmptyNciCode_ThrowsArgumentException()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.WithRecistResponse("", "Display"));
    }

    [Fact]
    public void WithRecistResponse_EmptyDisplay_ThrowsArgumentException()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.WithRecistResponse("C35571", ""));
    }

    [Fact]
    public void WithBodySite_EmptyCode_ThrowsArgumentException()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.WithBodySite("", "Display"));
    }

    [Fact]
    public void WithBodySite_EmptyDisplay_ThrowsArgumentException()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.WithBodySite("39607008", ""));
    }

    [Fact]
    public void AddImagingStudy_NullReference_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.AddImagingStudy(null!));
    }

    [Fact]
    public void AddRadiologyReport_NullReference_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.AddRadiologyReport(null!));
    }

    [Fact]
    public void FluentInterface_SupportsCompleteChaining()
    {
        // Arrange & Act
        var observation = new RecistProgressionObservationBuilder(_configuration)
            .WithInferenceId("recist-001")
            .WithPatient("Patient/p789", "John Smith")
            .WithDevice("Device/d123", "RECIST AI Device")
            .WithFocus(_tumorReference)
            .WithCriteria("1.1")
            .WithRecistResponse(FhirCodingHelper.NciCodes.COMPLETE_RESPONSE, "Complete Response")
            .AddComponent("33728-8", new Quantity { Value = 0m, Unit = "mm", System = "http://unitsofmeasure.org", Code = "mm" })
            .AddComponent("new-lesions", false)
            .WithBodySite("39607008", "Lung structure")
            .AddImagingStudy(_imagingStudyReference)
            .AddRadiologyReport(_radiologyReportReference)
            .AddDerivedFrom("Procedure/biopsy-123", "Tissue Biopsy")
            .Build();

        // Assert
        observation.Id.ShouldBe("recist-001");
        observation.Subject.Reference.ShouldBe("Patient/p789");
        observation.Device.Reference.ShouldBe("Device/d123");
        observation.Focus[0].ShouldBe(_tumorReference);
                observation.DerivedFrom.Count.ShouldBe(3); // imaging + report + derived
        observation.Component.Count.ShouldBe(2);

        var responseValue = observation.Value as CodeableConcept;
        responseValue.Coding[0].Code.ShouldBe(FhirCodingHelper.NciCodes.COMPLETE_RESPONSE);
    }

    [Fact]
    public void Build_GeneratesValidFhirJson()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithCriteria("1.1")
            .WithRecistResponse(FhirCodingHelper.NciCodes.PROGRESSIVE_DISEASE, "Progressive Disease")
            .AddComponent("33728-8", new Quantity { Value = 55.7m, Unit = "mm", System = "http://unitsofmeasure.org", Code = "mm" })
            .AddComponent("percent-change", new Quantity { Value = 23.5m, Unit = "%", System = "http://unitsofmeasure.org", Code = "%" })
            .AddComponent("new-lesions", true)
            .WithBodySite("39607008", "Lung structure")
            .AddImagingStudy(_imagingStudyReference)
            .AddRadiologyReport(_radiologyReportReference)
            .Build();

        // Act
        var serializer = new FhirJsonSerializer(new SerializerSettings { Pretty = true });
        var json = serializer.SerializeToString(observation);

        // Assert
        json.ShouldNotBeNullOrEmpty();
        json.ShouldContain("\"resourceType\": \"Observation\"");
        json.ShouldContain("\"status\": \"final\"");
        json.ShouldContain("\"code\": \"imaging\""); // Category
        json.ShouldContain("21976-6"); // LOINC code
        json.ShouldContain("C111544"); // NCI RECIST code
        json.ShouldContain("C35571"); // Progressive disease NCI code
        json.ShouldContain("component"); // Components array
        json.ShouldContain("33728-8"); // SLD LOINC code
        json.ShouldContain("39607008"); // Body site SNOMED code
        json.ShouldContain("\"code\": \"AIAST\""); // AIAST security label

        // Verify it can be deserialized
        var parser = new FhirJsonParser();
        var deserializedObs = parser.Parse<Observation>(json);
        deserializedObs.ShouldNotBeNull();
        deserializedObs.Status.ShouldBe(ObservationStatus.Final);
        deserializedObs.Category[0].Coding[0].Code.ShouldBe("imaging");
        deserializedObs.Component.Count.ShouldBe(3);
        deserializedObs.BodySite.ShouldNotBeNull();
    }

    [Fact]
    public void CreateComponentCode_LoincCode_CreatesLoincConcept()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddComponent("33359-2", new Quantity { Value = 15.5m, Unit = "%", System = "http://unitsofmeasure.org", Code = "%" })
            .Build();

        // Assert
        var component = observation.Component.FirstOrDefault(c =>
            c.Code.Coding.Any(cd => cd.Code == "33359-2"));
        component.ShouldNotBeNull();
        component.Code.Coding[0].System.ShouldBe(FhirCodingHelper.Systems.LOINC_SYSTEM);
        component.Code.Coding[0].Display.ShouldBe("Percent change");
    }

    [Fact]
    public void CreateComponentCode_SnomedCode_CreatesSnomedConcept()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddComponent("371508000", new Quantity { Value = 45.2m, Unit = "mm", System = "http://unitsofmeasure.org", Code = "mm" })
            .Build();

        // Assert
        var component = observation.Component.FirstOrDefault(c =>
            c.Code.Coding.Any(cd => cd.Code == "371508000"));
        component.ShouldNotBeNull();
        component.Code.Coding[0].System.ShouldBe(FhirCodingHelper.Systems.SNOMED_SYSTEM);
        component.Code.Coding[0].Display.ShouldBe("Sum of longest diameters");
    }

    [Fact]
    public void CreateComponentCode_CustomCode_CreatesCustomConcept()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .AddComponent("nadir-sld", new Quantity { Value = 38.5m, Unit = "mm", System = "http://unitsofmeasure.org", Code = "mm" })
            .Build();

        // Assert
        var component = observation.Component.FirstOrDefault(c =>
            c.Code.Coding.Any(cd => cd.Code == "nadir-sld"));
        component.ShouldNotBeNull();
        component.Code.Coding[0].System.ShouldBe("http://thirdopinion.ai/fhir/CodeSystem/recist-components");
        component.Code.Coding[0].Display.ShouldBe("Nadir sum of longest diameters");
    }

    [Fact]
    public void Build_WithAllRecistResponseTypes_SupportsAllNciCodes()
    {
        // Test Complete Response
        var crObservation = new RecistProgressionObservationBuilder(_configuration)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithRecistResponse("C18213", "Complete Response")
            .Build();

        var crValue = crObservation.Value as CodeableConcept;
        crValue.Coding[0].Code.ShouldBe("C18213");

        // Test Partial Response
        var prObservation = new RecistProgressionObservationBuilder(_configuration)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithRecistResponse("C18058", "Partial Response")
            .Build();

        var prValue = prObservation.Value as CodeableConcept;
        prValue.Coding[0].Code.ShouldBe("C18058");

        // Test Stable Disease
        var sdObservation = new RecistProgressionObservationBuilder(_configuration)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithRecistResponse("C18052", "Stable Disease")
            .Build();

        var sdValue = sdObservation.Value as CodeableConcept;
        sdValue.Coding[0].Code.ShouldBe("C18052");
    }

    [Fact]
    public void WithDetermination_True_CreatesComponentCorrectly()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithDetermination("True")
            .WithConfidence(0.92f)
            .Build();

        // Assert
        observation.ShouldNotBeNull();

        // Check determination component
        var determinationComponent = observation.Component
            .FirstOrDefault(c => c.Code.Coding.Any(cd => cd.Code == "determination"));
        determinationComponent.ShouldNotBeNull();
        ((FhirString)determinationComponent.Value).Value.ShouldBe("True");

        // Check confidence component
        var confidenceComponent = observation.Component
            .FirstOrDefault(c => c.Code.Coding.Any(cd => cd.Code == "LA11892-6"));
        confidenceComponent.ShouldNotBeNull();
        ((Quantity)confidenceComponent.Value).Value.ShouldBe(0.92m);
    }

    [Fact]
    public void WithDetermination_False_CreatesComponentCorrectly()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithDetermination("False")
            .WithConfidence(0.88f)
            .Build();

        // Assert
        observation.ShouldNotBeNull();

        // Check determination component
        var determinationComponent = observation.Component
            .FirstOrDefault(c => c.Code.Coding.Any(cd => cd.Code == "determination"));
        determinationComponent.ShouldNotBeNull();
        ((FhirString)determinationComponent.Value).Value.ShouldBe("False");
    }

    [Fact]
    public void WithDetermination_Inconclusive_CreatesComponentCorrectly()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithDetermination("Inconclusive")
            .WithConfidence(0.5f)
            .Build();

        // Assert
        observation.ShouldNotBeNull();

        // Check determination component
        var determinationComponent = observation.Component
            .FirstOrDefault(c => c.Code.Coding.Any(cd => cd.Code == "determination"));
        determinationComponent.ShouldNotBeNull();
        ((FhirString)determinationComponent.Value).Value.ShouldBe("Inconclusive");
    }

    [Fact]
    public void WithMeasurementChange_CreatesComponentCorrectly()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithDetermination("True")
            .WithMeasurementChange("Target lesions increased from 38.5mm to 45.2mm")
            .Build();

        // Assert
        var measurementComponent = observation.Component
            .FirstOrDefault(c => c.Code.Coding.Any(cd => cd.Code == "measurement-change"));
        measurementComponent.ShouldNotBeNull();
        ((FhirString)measurementComponent.Value).Value.ShouldBe("Target lesions increased from 38.5mm to 45.2mm");
    }

    [Fact]
    public void WithImagingType_CreatesComponentCorrectly()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithDetermination("True")
            .WithImagingType("CT Chest/Abdomen/Pelvis with contrast")
            .Build();

        // Assert
        var imagingTypeComponent = observation.Component
            .FirstOrDefault(c => c.Code.Coding.Any(cd => cd.Code == "imaging-type"));
        imagingTypeComponent.ShouldNotBeNull();
        ((FhirString)imagingTypeComponent.Value).Value.ShouldBe("CT Chest/Abdomen/Pelvis with contrast");
    }

    [Fact]
    public void WithConfirmationDate_CreatesComponentCorrectly()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);
        var confirmationDate = new DateTime(2025, 2, 15);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithDetermination("True")
            .WithConfirmationDate(confirmationDate)
            .Build();

        // Assert
        var confirmationDateComponent = observation.Component
            .FirstOrDefault(c => c.Code.Coding.Any(cd => cd.Code == "confirmation-date"));
        confirmationDateComponent.ShouldNotBeNull();
        var dateValue = confirmationDateComponent.Value as FhirDateTime;
        dateValue.ShouldNotBeNull();
    }

    [Fact]
    public void WithSupportingFacts_AddsClinicalFactExtensions()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithDetermination("True")
            .WithSupportingFacts(_sampleFacts)
            .Build();

        // Assert
        observation.Extension.ShouldNotBeEmpty();

        // Check that clinical fact extensions were added
        var clinicalFactExtensions = observation.Extension
            .Where(e => e.Url == "https://thirdopinion.io/clinical-fact")
            .ToList();

        clinicalFactExtensions.Count.ShouldBe(2);

        // Verify first fact extension has nested extensions
        var firstFactExtension = clinicalFactExtensions[0];
        firstFactExtension.Extension.ShouldNotBeEmpty();

        // Check for factGuid in nested extensions
        var factGuidExtension = firstFactExtension.Extension
            .FirstOrDefault(e => e.Url == "factGuid");
        factGuidExtension.ShouldNotBeNull();
        ((FhirString)factGuidExtension.Value).Value.ShouldBe("aa10e02a-f391-4614-96e4-35cbe47d2a85");

        // Verify second fact extension has nested extensions
        var secondFactExtension = clinicalFactExtensions[1];
        secondFactExtension.Extension.ShouldNotBeEmpty();
    }

    [Fact]
    public void WithConfidence_WithInvalidRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => builder.WithConfidence(-0.1f));
        Should.Throw<ArgumentOutOfRangeException>(() => builder.WithConfidence(1.1f));
    }

    [Fact]
    public void Build_WithAllEnhancedComponents_CreatesCompleteObservation()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);
        var confirmationDate = new DateTime(2025, 3, 20);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithDetermination("True")
            .WithMeasurementChange("Target lesions increased by 17.4%")
            .WithImagingType("CT Chest/Abdomen/Pelvis")
            .WithConfirmationDate(confirmationDate)
            .WithSupportingFacts(_sampleFacts)
            .WithConfidence(0.89f)
            .WithRecistResponse(FhirCodingHelper.NciCodes.PROGRESSIVE_DISEASE, "Progressive Disease")
            .Build();

        // Assert
        observation.ShouldNotBeNull();

        // Check value shows progression from WithRecistResponse
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding.First().Code.ShouldBe(FhirCodingHelper.NciCodes.PROGRESSIVE_DISEASE);

        // Check all enhanced components are present
        var measurementComponent = observation.Component
            .FirstOrDefault(c => c.Code.Coding.Any(cd => cd.Code == "measurement-change"));
        measurementComponent.ShouldNotBeNull();

        var imagingComponent = observation.Component
            .FirstOrDefault(c => c.Code.Coding.Any(cd => cd.Code == "imaging-type"));
        imagingComponent.ShouldNotBeNull();

        var confirmationComponent = observation.Component
            .FirstOrDefault(c => c.Code.Coding.Any(cd => cd.Code == "confirmation-date"));
        confirmationComponent.ShouldNotBeNull();

        var confidenceComponent = observation.Component
            .FirstOrDefault(c => c.Code.Coding.Any(cd => cd.Code == "LA11892-6"));
        confidenceComponent.ShouldNotBeNull();

        // Check supporting facts extensions
        observation.Extension.Any(e => e.Url == "https://thirdopinion.io/clinical-fact").ShouldBeTrue();
    }

    [Fact]
    public void Build_WithoutIdentified_SucceedsWithoutException()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .Build();

        // Assert
        observation.ShouldNotBeNull();
        // No identified component should be present
        var identifiedComponent = observation.Component
            ?.FirstOrDefault(c => c.Code.Coding.Any(cd => cd.Code == "identified"));
        identifiedComponent.ShouldBeNull();
    }

    [Fact]
    public void Build_HasAiastSecurityLabel()
    {
        // Arrange
        var builder = new RecistProgressionObservationBuilder(_configuration);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithFocus(_tumorReference)
            .WithDetermination("True")
            .Build();

        // Assert
        observation.Meta.Security.ShouldNotBeNull();
        observation.Meta.Security.Any(s => s.Code == "AIAST").ShouldBeTrue();
    }
}