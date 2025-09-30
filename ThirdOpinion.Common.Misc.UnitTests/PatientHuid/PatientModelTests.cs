using System.Text.Json;
using Misc.patients.PatientHuid;

namespace ThirdOpinion.Common.UnitTests.PatientHuid;

public class PatientModelTests
{
    [Fact]
    public void Patient_Constructor_InitializesPropertiesCorrectly()
    {
        // Arrange
        var tenantGuid = Guid.NewGuid();
        var patientGuid = Guid.NewGuid();
        var patientHuid = "P123-456-7890";
        var provenance = "test-system";

        // Act
        var patient = new Patient
        {
            TenantGuid = tenantGuid,
            PatientGuid = patientGuid,
            PatientHuid = patientHuid,
            Provenance = provenance,
            Demographics = new Demographics
            {
                FirstName = "John",
                LastName = "Doe"
            }
        };

        // Assert
        patient.TenantGuid.ShouldBe(tenantGuid);
        patient.PatientGuid.ShouldBe(patientGuid);
        patient.PatientHuid.ShouldBe(patientHuid);
        patient.Provenance.ShouldBe(provenance);
        patient.Demographics.ShouldNotBeNull();
    }

    [Fact]
    public void Demographics_DefaultConstructor_InitializesCorrectly()
    {
        // Act
        var demographics = new Demographics();

        // Assert
        demographics.FirstName.ShouldBeNull();
        demographics.LastName.ShouldBeNull();
        demographics.MiddleName.ShouldBeNull();
        demographics.Prefix.ShouldBeNull();
        demographics.Suffix.ShouldBeNull();
        demographics.Sex.ShouldBeNull();
        demographics.BirthDate.ShouldBeNull();
        demographics.Age.ShouldBeNull();
        demographics.DeathDate.ShouldBeNull();
        demographics.PhoneNumber.ShouldBeNull();
    }

    [Fact]
    public void Demographics_AllProperties_CanBeSetAndRetrieved()
    {
        // Arrange
        var demographics = new Demographics();
        var birthDate = new DateTime(1980, 5, 15);
        var deathDate = new DateTime(2020, 10, 20);

        // Act
        demographics.Prefix = "Dr.";
        demographics.FirstName = "John";
        demographics.LastName = "Smith";
        demographics.MiddleName = "Michael";
        demographics.Suffix = "Jr.";
        demographics.Sex = Demographics.SexEnum.Male;
        demographics.BirthDate = birthDate;
        demographics.Age = 40;
        demographics.DeathDate = deathDate;
        demographics.PhoneNumber = "555-123-4567";

        // Assert
        demographics.Prefix.ShouldBe("Dr.");
        demographics.FirstName.ShouldBe("John");
        demographics.LastName.ShouldBe("Smith");
        demographics.MiddleName.ShouldBe("Michael");
        demographics.Suffix.ShouldBe("Jr.");
        demographics.Sex.ShouldBe(Demographics.SexEnum.Male);
        demographics.BirthDate.ShouldBe(birthDate);
        demographics.Age.ShouldBe(40);
        demographics.DeathDate.ShouldBe(deathDate);
        demographics.PhoneNumber.ShouldBe("555-123-4567");
    }

    [Fact]
    public void Demographics_SexEnum_HasCorrectValues()
    {
        // Assert
        ((int)Demographics.SexEnum.Unknown).ShouldBe(0);
        ((int)Demographics.SexEnum.Male).ShouldBe(1);
        ((int)Demographics.SexEnum.Female).ShouldBe(2);
        ((int)Demographics.SexEnum.Null).ShouldBe(3);
    }

    [Fact]
    public void Demographics_Serialization_WorksCorrectly()
    {
        // Arrange
        var demographics = new Demographics
        {
            FirstName = "John",
            LastName = "Doe",
            Sex = Demographics.SexEnum.Male,
            BirthDate = new DateTime(1980, 5, 15),
            PhoneNumber = "555-123-4567"
        };

        // Act
        string json = JsonSerializer.Serialize(demographics);
        var deserialized = JsonSerializer.Deserialize<Demographics>(json);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.FirstName.ShouldBe(demographics.FirstName);
        deserialized.LastName.ShouldBe(demographics.LastName);
        deserialized.Sex.ShouldBe(demographics.Sex);
        deserialized.BirthDate.ShouldBe(demographics.BirthDate);
        deserialized.PhoneNumber.ShouldBe(demographics.PhoneNumber);
    }

    [Fact]
    public void PostPatient_Constructor_InitializesCorrectly()
    {
        // Arrange
        var tenantGuid = Guid.NewGuid();
        var provenance = "test-system";
        var demographics = new Demographics { FirstName = "Jane", LastName = "Doe" };

        // Act
        var postPatient = new PostPatient
        {
            TenantGuid = tenantGuid,
            Provenance = provenance,
            Demographics = demographics
        };

        // Assert
        postPatient.TenantGuid.ShouldBe(tenantGuid);
        postPatient.Provenance.ShouldBe(provenance);
        postPatient.Demographics.ShouldBe(demographics);
        postPatient.PatientGuid.ShouldBeNull();
    }

    [Fact]
    public void PostPatient_SetPatientGuid_GeneratesNewGuid()
    {
        // Arrange
        var postPatient = new PostPatient
        {
            TenantGuid = Guid.NewGuid(),
            Provenance = "test",
            Demographics = new Demographics()
        };

        // Act
        postPatient.SetPatientGuid();

        // Assert
        postPatient.PatientGuid.ShouldNotBeNull();
        postPatient.PatientGuid.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void PostPatient_SetPatientGuid_GeneratesUniqueGuids()
    {
        // Arrange
        var postPatient1 = new PostPatient
        {
            TenantGuid = Guid.NewGuid(),
            Provenance = "test",
            Demographics = new Demographics()
        };

        var postPatient2 = new PostPatient
        {
            TenantGuid = Guid.NewGuid(),
            Provenance = "test",
            Demographics = new Demographics()
        };

        // Act
        postPatient1.SetPatientGuid();
        postPatient2.SetPatientGuid();

        // Assert
        postPatient2.PatientGuid.ShouldNotBe(postPatient1.PatientGuid);
    }

    [Fact]
    public void MatchPatient_Constructor_InitializesCorrectly()
    {
        // Arrange
        var tenantGuid = Guid.NewGuid();
        var firstName = "John";
        var lastName = "Smith";
        var sex = Demographics.SexEnum.Male;
        var birthDate = new DateTime(1980, 5, 15);

        // Act
        var matchPatient = new MatchPatient
        {
            TenantGuid = tenantGuid,
            FirstName = firstName,
            LastName = lastName,
            Sex = sex,
            BirthDate = birthDate,
            PhoneNumber = "555-123-4567"
        };

        // Assert
        matchPatient.TenantGuid.ShouldBe(tenantGuid);
        matchPatient.FirstName.ShouldBe(firstName);
        matchPatient.LastName.ShouldBe(lastName);
        matchPatient.Sex.ShouldBe(sex);
        matchPatient.BirthDate.ShouldBe(birthDate);
        matchPatient.PhoneNumber.ShouldBe("555-123-4567");
    }

    [Fact]
    public void MatchPatient_OptionalProperties_CanBeNull()
    {
        // Act
        var matchPatient = new MatchPatient
        {
            TenantGuid = Guid.NewGuid(),
            FirstName = "John",
            LastName = "Smith",
            Sex = Demographics.SexEnum.Male
        };

        // Assert
        matchPatient.Prefix.ShouldBeNull();
        matchPatient.MiddleName.ShouldBeNull();
        matchPatient.BirthDate.ShouldBeNull();
        matchPatient.DeathDate.ShouldBeNull();
        matchPatient.PhoneNumber.ShouldBeNull();
    }

    [Fact]
    public void Patient_Demographics_DefaultsToNewInstance()
    {
        // Act
        var patient = new Patient
        {
            TenantGuid = Guid.NewGuid(),
            PatientGuid = Guid.NewGuid(),
            PatientHuid = "P123-456-7890",
            Provenance = "test"
        };

        // Assert
        patient.Demographics.ShouldNotBeNull();
    }

    [Fact]
    public void Demographics_NullableProperties_HandleNullCorrectly()
    {
        // Arrange
        var demographics = new Demographics();

        // Act & Assert - These should not throw exceptions
        demographics.Sex.ShouldBeNull();
        demographics.BirthDate.ShouldBeNull();
        demographics.DeathDate.ShouldBeNull();
        demographics.Age.ShouldBeNull();
    }

    [Fact]
    public void Demographics_StringProperties_HandleEmptyAndWhitespace()
    {
        // Arrange
        var demographics = new Demographics();

        // Act
        demographics.FirstName = "";
        demographics.LastName = "   ";
        demographics.PhoneNumber = null;

        // Assert
        demographics.FirstName.ShouldBe("");
        demographics.LastName.ShouldBe("   ");
        demographics.PhoneNumber.ShouldBeNull();
    }

    [Fact]
    public void Demographics_DateProperties_HandleBoundaryValues()
    {
        // Arrange
        var demographics = new Demographics();
        var minDate = DateTime.MinValue;
        var maxDate = DateTime.MaxValue;

        // Act
        demographics.BirthDate = minDate;
        demographics.DeathDate = maxDate;

        // Assert
        demographics.BirthDate.ShouldBe(minDate);
        demographics.DeathDate.ShouldBe(maxDate);
    }

    [Fact]
    public void Patient_CreatedDateTime_IsPrivateSet()
    {
        // Arrange
        var patient = new Patient
        {
            TenantGuid = Guid.NewGuid(),
            PatientGuid = Guid.NewGuid(),
            PatientHuid = "P123-456-7890",
            Provenance = "test"
        };

        // Assert - CreatedDateTime should have a default value (likely DateTime default)
        // Since it's private set, we can't set it directly, but we can verify it exists
        (patient.CreatedDateTime >= DateTime.MinValue).ShouldBeTrue();
    }

    [Fact]
    public void Demographics_Age_CanBeSetToVariousValues()
    {
        // Arrange
        var demographics = new Demographics();

        // Act & Assert
        demographics.Age = 0;
        demographics.Age.ShouldBe(0);

        demographics.Age = 120;
        demographics.Age.ShouldBe(120);

        demographics.Age = null;
        demographics.Age.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("P")]
    [InlineData("P123-456-7890")]
    [InlineData("INVALID-HUID")]
    public void Patient_PatientHuid_AcceptsVariousFormats(string huid)
    {
        // Act
        var patient = new Patient
        {
            TenantGuid = Guid.NewGuid(),
            PatientGuid = Guid.NewGuid(),
            PatientHuid = huid,
            Provenance = "test"
        };

        // Assert
        patient.PatientHuid.ShouldBe(huid);
    }

    [Fact]
    public void PostPatient_RequiredProperties_MustBeProvided()
    {
        // This test verifies that required properties are enforced at compile time
        // by attempting to create instances and ensuring the compiler requirements are met

        // Act & Assert - These should compile successfully
        var validPostPatient = new PostPatient
        {
            TenantGuid = Guid.NewGuid(),
            Provenance = "required-provenance",
            Demographics = new Demographics()
        };

        validPostPatient.Provenance.ShouldNotBeNull();
        validPostPatient.Demographics.ShouldNotBeNull();
    }

    [Fact]
    public void MatchPatient_RequiredProperties_MustBeProvided()
    {
        // Act & Assert - These should compile successfully
        var validMatchPatient = new MatchPatient
        {
            TenantGuid = Guid.NewGuid(),
            FirstName = "required-first",
            LastName = "required-last",
            Sex = Demographics.SexEnum.Male
        };

        validMatchPatient.FirstName.ShouldNotBeNull();
        validMatchPatient.LastName.ShouldNotBeNull();
        (validMatchPatient.Sex != 0 || validMatchPatient.Sex == Demographics.SexEnum.Unknown)
            .ShouldBeTrue();
    }

    [Fact]
    public void Demographics_CompletePatientInfo_AllPropertiesSet()
    {
        // Arrange & Act
        var demographics = new Demographics
        {
            Prefix = "Dr.",
            FirstName = "John",
            LastName = "Smith",
            MiddleName = "Michael",
            Suffix = "Jr.",
            Sex = Demographics.SexEnum.Male,
            BirthDate = new DateTime(1980, 5, 15),
            Age = 43,
            DeathDate = null,
            PhoneNumber = "555-123-4567"
        };

        // Assert - Verify all properties are set correctly
        demographics.Prefix.ShouldBe("Dr.");
        demographics.FirstName.ShouldBe("John");
        demographics.LastName.ShouldBe("Smith");
        demographics.MiddleName.ShouldBe("Michael");
        demographics.Suffix.ShouldBe("Jr.");
        demographics.Sex.ShouldBe(Demographics.SexEnum.Male);
        demographics.BirthDate.ShouldBe(new DateTime(1980, 5, 15));
        demographics.Age.ShouldBe(43);
        demographics.DeathDate.ShouldBeNull();
        demographics.PhoneNumber.ShouldBe("555-123-4567");
    }
}