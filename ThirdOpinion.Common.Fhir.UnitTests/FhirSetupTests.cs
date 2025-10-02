using System.Text.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace ThirdOpinion.Common.Fhir.UnitTests;

public class FhirSetupTests
{
    [Fact]
    public void CanCreatePatientResource()
    {
        // Arrange & Act
        var patient = new Patient
        {
            Id = "test-patient-001",
            Active = true
        };

        // Add name
        var name = new HumanName
        {
            Use = HumanName.NameUse.Official,
            Family = "Smith",
            Given = new[] { "John", "Michael" }
        };
        patient.Name.Add(name);

        // Add birth date
        patient.BirthDate = "1970-01-15";

        // Add identifier
        var identifier = new Identifier
        {
            System = "http://example.org/patient-ids",
            Value = "12345"
        };
        patient.Identifier.Add(identifier);

        // Assert
        patient.ShouldNotBeNull();
        patient.Id.ShouldBe("test-patient-001");
        patient.Active.ShouldBe(true);
        patient.Name.ShouldHaveSingleItem();
        patient.Name[0].Family.ShouldBe("Smith");
        patient.Name[0].Given.ShouldContain("John");
        patient.BirthDate.ShouldBe("1970-01-15");
        patient.Identifier.ShouldHaveSingleItem();
        patient.Identifier[0].Value.ShouldBe("12345");
    }

    [Fact]
    public void CanSerializeAndDeserializePatientResource()
    {
        // Arrange
        var patient = new Patient
        {
            Id = "test-patient-002",
            Active = true
        };

        var name = new HumanName
        {
            Use = HumanName.NameUse.Official,
            Family = "Doe",
            Given = new[] { "Jane" }
        };
        patient.Name.Add(name);
        patient.BirthDate = "1985-05-20";
        patient.Gender = AdministrativeGender.Female;

        // Act - Serialize to JSON
        var serializer = new FhirJsonSerializer(new SerializerSettings { Pretty = true });
        var json = serializer.SerializeToString(patient);

        // Deserialize back to Patient object
        var parser = new FhirJsonParser();
        var deserializedPatient = parser.Parse<Patient>(json);

        // Assert
        json.ShouldNotBeNullOrWhiteSpace();
        json.ShouldContain("\"resourceType\": \"Patient\"");
        json.ShouldContain("\"id\": \"test-patient-002\"");
        json.ShouldContain("\"family\": \"Doe\"");

        deserializedPatient.ShouldNotBeNull();
        deserializedPatient.Id.ShouldBe(patient.Id);
        deserializedPatient.Active.ShouldBe(patient.Active);
        deserializedPatient.Name[0].Family.ShouldBe("Doe");
        deserializedPatient.Name[0].Given.ShouldContain("Jane");
        deserializedPatient.BirthDate.ShouldBe(patient.BirthDate);
        deserializedPatient.Gender.ShouldBe(AdministrativeGender.Female);
    }

    [Fact]
    public void CanCreateObservationResource()
    {
        // Arrange & Act
        var observation = new Observation
        {
            Id = "test-obs-001",
            Status = ObservationStatus.Final,
            Code = new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new Coding
                    {
                        System = "http://loinc.org",
                        Code = "8867-4",
                        Display = "Heart rate"
                    }
                },
                Text = "Heart rate"
            }
        };

        // Add value
        observation.Value = new Quantity
        {
            Value = 72,
            Unit = "beats/minute",
            System = "http://unitsofmeasure.org",
            Code = "/min"
        };

        // Add effective date
        observation.Effective = new FhirDateTime("2024-01-15T10:30:00Z");

        // Assert
        observation.ShouldNotBeNull();
        observation.Id.ShouldBe("test-obs-001");
        observation.Status.ShouldBe(ObservationStatus.Final);
        observation.Code.ShouldNotBeNull();
        observation.Code.Coding[0].Code.ShouldBe("8867-4");

        var quantityValue = observation.Value as Quantity;
        quantityValue.ShouldNotBeNull();
        quantityValue.Value.ShouldBe(72);
        quantityValue.Unit.ShouldBe("beats/minute");

        observation.Effective.ShouldNotBeNull();
    }

    [Fact]
    public void CanCreateDocumentReferenceResource()
    {
        // Arrange & Act
        var docReference = new DocumentReference
        {
            Id = "test-doc-001",
            Status = DocumentReferenceStatus.Current,
            Type = new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new Coding
                    {
                        System = "http://loinc.org",
                        Code = "34108-1",
                        Display = "Outpatient Note"
                    }
                }
            }
        };

        // Add content
        var content = new DocumentReference.ContentComponent
        {
            Attachment = new Attachment
            {
                ContentType = "application/pdf",
                Title = "Clinical Note",
                Creation = "2024-01-15"
            }
        };
        docReference.Content.Add(content);

        // Add date
        docReference.Date = new FhirDateTime("2024-01-15T14:00:00Z").ToDateTimeOffset(TimeSpan.Zero);

        // Assert
        docReference.ShouldNotBeNull();
        docReference.Id.ShouldBe("test-doc-001");
        docReference.Status.ShouldBe(DocumentReferenceStatus.Current);
        docReference.Type.ShouldNotBeNull();
        docReference.Type.Coding[0].Code.ShouldBe("34108-1");
        docReference.Content.ShouldHaveSingleItem();
        docReference.Content[0].Attachment.ContentType.ShouldBe("application/pdf");
        docReference.Content[0].Attachment.Title.ShouldBe("Clinical Note");
        docReference.Date.ShouldNotBeNull();
    }

    [Fact]
    public void FhirLibraryAndNuGetPackagesConfiguredCorrectly()
    {
        // This test verifies all the necessary types are available from the packages
        // Arrange & Act & Assert

        // Verify Hl7.Fhir.R4 types
        typeof(Patient).ShouldNotBeNull();
        typeof(Observation).ShouldNotBeNull();
        typeof(DocumentReference).ShouldNotBeNull();
        typeof(CodeableConcept).ShouldNotBeNull();
        typeof(Identifier).ShouldNotBeNull();

        // Verify Hl7.Fhir.Serialization types
        typeof(FhirJsonSerializer).ShouldNotBeNull();
        typeof(FhirJsonParser).ShouldNotBeNull();
        typeof(SerializerSettings).ShouldNotBeNull();

        // Verify common FHIR operations work
        var bundle = new Bundle { Type = Bundle.BundleType.Searchset };
        bundle.Type.ShouldBe(Bundle.BundleType.Searchset);
    }
}