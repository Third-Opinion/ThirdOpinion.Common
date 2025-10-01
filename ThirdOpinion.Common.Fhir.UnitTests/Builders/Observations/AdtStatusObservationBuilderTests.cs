using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using ThirdOpinion.Common.Fhir.Builders.Observations;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;

namespace ThirdOpinion.Common.Fhir.UnitTests.Builders.Observations;

public class AdtStatusObservationBuilderTests
{
    private readonly AiInferenceConfiguration _configuration;
    private readonly ResourceReference _patientReference;
    private readonly ResourceReference _deviceReference;

    public AdtStatusObservationBuilderTests()
    {
        _configuration = AiInferenceConfiguration.CreateDefault();
        _patientReference = new ResourceReference("Patient/test-patient", "Test Patient");
        _deviceReference = new ResourceReference("Device/ai-device", "AI Detection Device");
    }

    [Fact]
    public void Build_WithActiveAdtStatus_CreatesCorrectObservation()
    {
        // Arrange
        var builder = new AdtStatusObservationBuilder(_configuration);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithStatus(true) // Active ADT
            .WithEffectiveDate(new DateTime(2024, 1, 15, 10, 30, 0))
            .Build();

        // Assert
        observation.ShouldNotBeNull();
        observation.Status.ShouldBe(ObservationStatus.Final);

        // Check category
        observation.Category.ShouldHaveSingleItem();
        observation.Category[0].Coding[0].Code.ShouldBe("therapy");
        observation.Category[0].Coding[0].System.ShouldBe("http://terminology.hl7.org/CodeSystem/observation-category");

        // Check code (ADT therapy)
        observation.Code.Coding[0].System.ShouldBe(FhirCodingHelper.Systems.SNOMED_SYSTEM);
        observation.Code.Coding[0].Code.ShouldBe(FhirCodingHelper.SnomedCodes.ADT_THERAPY);
        observation.Code.Coding[0].Display.ShouldBe("Androgen deprivation therapy");

        // Check subject and device
        observation.Subject.ShouldBe(_patientReference);
        observation.Device.ShouldBe(_deviceReference);

        // Check value (Active status)
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding[0].Code.ShouldBe(FhirCodingHelper.SnomedCodes.ACTIVE_STATUS);
        valueCodeableConcept.Coding[0].Display.ShouldBe("Active");

        // Check AIAST label from base class
        observation.Meta.ShouldNotBeNull();
        observation.Meta.Security.Any(s => s.Code == "AIAST").ShouldBeTrue();
    }

    [Fact]
    public void Build_WithInactiveAdtStatus_CreatesCorrectObservation()
    {
        // Arrange
        var builder = new AdtStatusObservationBuilder(_configuration);

        // Act
        var observation = builder
            .WithPatient("patient-123", "John Doe")
            .WithDevice("device-456", "Detection AI")
            .WithStatus(false) // Inactive ADT
            .Build();

        // Assert
        var valueCodeableConcept = observation.Value as CodeableConcept;
        valueCodeableConcept.ShouldNotBeNull();
        valueCodeableConcept.Coding[0].Code.ShouldBe("385655000"); // Inactive status
        valueCodeableConcept.Coding[0].Display.ShouldBe("Inactive");

        // Check patient and device references were formatted correctly
        observation.Subject.Reference.ShouldBe("Patient/patient-123");
        observation.Device.Reference.ShouldBe("Device/device-456");
    }

    [Fact]
    public void Build_WithMultipleEvidence_AddsAllToDerivedFrom()
    {
        // Arrange
        var builder = new AdtStatusObservationBuilder(_configuration);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithStatus(true)
            .AddEvidence("DocumentReference/doc1", "Clinical Note 1")
            .AddEvidence("Observation/obs2", "Lab Result")
            .AddEvidence(new ResourceReference("DiagnosticReport/report3", "Pathology Report"))
            .Build();

        // Assert
        observation.DerivedFrom.ShouldNotBeNull();
        observation.DerivedFrom.Count.ShouldBe(3);
        observation.DerivedFrom[0].Reference.ShouldBe("DocumentReference/doc1");
        observation.DerivedFrom[0].Display.ShouldBe("Clinical Note 1");
        observation.DerivedFrom[1].Reference.ShouldBe("Observation/obs2");
        observation.DerivedFrom[1].Display.ShouldBe("Lab Result");
        observation.DerivedFrom[2].Reference.ShouldBe("DiagnosticReport/report3");
    }

    [Fact]
    public void Build_WithNotes_AddsAnnotations()
    {
        // Arrange
        var builder = new AdtStatusObservationBuilder(_configuration);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithStatus(true)
            .AddNote("Patient reported starting ADT therapy 3 months ago")
            .AddNote("PSA levels declining as expected")
            .Build();

        // Assert
        observation.Note.ShouldNotBeNull();
        observation.Note.Count.ShouldBe(2);
        observation.Note[0].Text.ShouldBe("Patient reported starting ADT therapy 3 months ago");
        observation.Note[1].Text.ShouldBe("PSA levels declining as expected");
        observation.Note[0].Time.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Build_WithExplicitInferenceId_UsesProvidedId()
    {
        // Arrange
        var builder = new AdtStatusObservationBuilder(_configuration);
        var customId = "custom-inference-12345";

        // Act
        var observation = builder
            .WithInferenceId(customId)
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithStatus(true)
            .Build();

        // Assert
        observation.Id.ShouldBe(customId);
    }

    [Fact]
    public void Build_WithoutExplicitInferenceId_AutoGeneratesId()
    {
        // Arrange
        var builder = new AdtStatusObservationBuilder(_configuration);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithStatus(false)
            .Build();

        // Assert
        observation.Id.ShouldNotBeNullOrEmpty();
        observation.Id.ShouldStartWith("to.ai-inference-");
    }

    [Fact]
    public void Build_WithCriteria_SetsMethodCodeableConcept()
    {
        // Arrange
        var builder = new AdtStatusObservationBuilder(_configuration);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithStatus(true)
            .WithCriteria("adt-detect-v1", "ADT Detection Algorithm v1.0", "http://example.org/criteria")
            .Build();

        // Assert
        observation.Method.ShouldNotBeNull();
        observation.Method.Coding[0].System.ShouldBe("http://example.org/criteria");
        observation.Method.Coding[0].Code.ShouldBe("adt-detect-v1");
        observation.Method.Coding[0].Display.ShouldBe("ADT Detection Algorithm v1.0");
        observation.Method.Text.ShouldBe("ADT Detection Algorithm v1.0");
    }

    [Fact]
    public void Build_MissingPatient_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new AdtStatusObservationBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithDevice(_deviceReference)
                .WithStatus(true)
                .Build());

        exception.Message.ShouldContain("Patient reference is required");
    }

    [Fact]
    public void Build_MissingDevice_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new AdtStatusObservationBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithPatient(_patientReference)
                .WithStatus(true)
                .Build());

        exception.Message.ShouldContain("Device reference is required");
    }

    [Fact]
    public void Build_MissingStatus_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new AdtStatusObservationBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithPatient(_patientReference)
                .WithDevice(_deviceReference)
                .Build());

        exception.Message.ShouldContain("ADT status is required");
    }

    [Fact]
    public void FluentInterface_SupportsCompleteChaining()
    {
        // Arrange & Act
        var observation = new AdtStatusObservationBuilder(_configuration)
            .WithInferenceId("test-inference-001")
            .WithPatient("Patient/p123", "Jane Doe")
            .WithDevice("Device/d456", "AI System")
            .WithStatus(true)
            .WithCriteria("criteria-001", "Test Criteria")
            .AddEvidence("DocumentReference/doc1", "Note 1")
            .AddEvidence("Observation/obs1", "Lab 1")
            .WithEffectiveDate(new DateTime(2024, 2, 1))
            .AddNote("Clinical observation note")
            .AddDerivedFrom("Procedure/proc1", "Related Procedure")
            .Build();

        // Assert
        observation.Id.ShouldBe("test-inference-001");
        observation.Subject.Reference.ShouldBe("Patient/p123");
        observation.Device.Reference.ShouldBe("Device/d456");
        observation.DerivedFrom.Count.ShouldBe(3); // 2 evidence + 1 derivedFrom
        observation.Note.Count.ShouldBe(1);
        observation.Method.ShouldNotBeNull();
    }

    [Fact]
    public void WithPatient_NullReference_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new AdtStatusObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => builder.WithPatient((ResourceReference)null!));
        Should.Throw<ArgumentException>(() => builder.WithPatient(""));
        Should.Throw<ArgumentException>(() => builder.WithPatient("   "));
    }

    [Fact]
    public void WithDevice_NullReference_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new AdtStatusObservationBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => builder.WithDevice((ResourceReference)null!));
        Should.Throw<ArgumentException>(() => builder.WithDevice(""));
        Should.Throw<ArgumentException>(() => builder.WithDevice("   "));
    }

    [Fact]
    public void WithEffectiveDate_MultipleForms_SetsCorrectly()
    {
        // Arrange
        var builder1 = new AdtStatusObservationBuilder(_configuration);
        var builder2 = new AdtStatusObservationBuilder(_configuration);
        var dateTime = new DateTime(2024, 3, 15, 14, 30, 0);
        var dateTimeOffset = new DateTimeOffset(2024, 3, 15, 14, 30, 0, TimeSpan.FromHours(-5));

        // Act
        var observation1 = builder1
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithStatus(true)
            .WithEffectiveDate(dateTime)
            .Build();

        var observation2 = builder2
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithStatus(true)
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
        var builder = new AdtStatusObservationBuilder(_configuration);
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithStatus(true)
            .WithEffectiveDate(new DateTime(2024, 1, 15))
            .AddEvidence("DocumentReference/evidence1", "Supporting Document")
            .AddNote("ADT therapy confirmed")
            .Build();

        // Act
        var serializer = new FhirJsonSerializer(new SerializerSettings { Pretty = true });
        var json = serializer.SerializeToString(observation);

        // Assert
        json.ShouldNotBeNullOrEmpty();
        json.ShouldContain("\"resourceType\": \"Observation\"");
        json.ShouldContain("\"status\": \"final\"");
        json.ShouldContain("\"code\": \"therapy\"");
        json.ShouldContain(FhirCodingHelper.SnomedCodes.ADT_THERAPY);
        json.ShouldContain("\"code\": \"AIAST\""); // AIAST security label
        json.ShouldContain("derivedFrom");

        // Verify it can be deserialized
        var parser = new FhirJsonParser();
        var deserializedObs = parser.Parse<Observation>(json);
        deserializedObs.ShouldNotBeNull();
        deserializedObs.Status.ShouldBe(ObservationStatus.Final);
    }

    [Fact]
    public void AddEvidence_EmptyString_IsIgnored()
    {
        // Arrange
        var builder = new AdtStatusObservationBuilder(_configuration);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithStatus(true)
            .AddEvidence("", "Display")
            .AddEvidence("   ", "Display2")
            .Build();

        // Assert
        // FHIR Observation initializes collections to empty lists, not null
        observation.DerivedFrom.ShouldNotBeNull();
        observation.DerivedFrom.Count.ShouldBe(0);
    }

    [Fact]
    public void AddNote_EmptyString_IsIgnored()
    {
        // Arrange
        var builder = new AdtStatusObservationBuilder(_configuration);

        // Act
        var observation = builder
            .WithPatient(_patientReference)
            .WithDevice(_deviceReference)
            .WithStatus(false)
            .AddNote("")
            .AddNote("   ")
            .Build();

        // Assert
        // FHIR Observation initializes collections to empty lists, not null
        observation.Note.ShouldNotBeNull();
        observation.Note.Count.ShouldBe(0);
    }
}