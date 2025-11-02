using Misc.patients.PatientHuid;

namespace ThirdOpinion.Common.UnitTests.Modals;

public class PatientMatcherTests
{
    private string FormatPatientData(Patient patient)
    {
        return
            $"FirstName: {patient.Demographics.FirstName}, LastName: {patient.Demographics.LastName}, MiddleName: {patient.Demographics.MiddleName}, Prefix: {patient.Demographics.Prefix}, Suffix: {patient.Demographics.Suffix}, BirthDate: {patient.Demographics.BirthDate}, DeathDate: {patient.Demographics.DeathDate}, Sex: {patient.Demographics.Sex}, PhoneNumber: {patient.Demographics.PhoneNumber}";
    }

    [Fact]
    public void CalculateSamePerson_ExactMatch_ReturnsOne()
    {
        // Arrange
        var patient1 = new Patient
        {
            Demographics = new Demographics
            {
                FirstName = "John",
                LastName = "Doe",
                MiddleName = "A",
                Prefix = "Mr",
                Suffix = "Jr",
                BirthDate = new DateTime(1980, 1, 1),
                Sex = Demographics.SexEnum.Male,
                PhoneNumber = "123-456-7890"
            }
        };

        var patient2 = new Patient
        {
            Demographics = new Demographics
            {
                FirstName = "John",
                LastName = "Doe",
                MiddleName = "A",
                Prefix = "Mr",
                Suffix = "Jr",
                BirthDate = new DateTime(1980, 1, 1),
                Sex = Demographics.SexEnum.Male,
                PhoneNumber = "123-456-7890"
            }
        };

        // Act
        float similarity = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Output patient data
        Console.WriteLine("Patient 1:");
        Console.WriteLine(FormatPatientData(patient1));
        Console.WriteLine("Patient 2:");
        Console.WriteLine(FormatPatientData(patient2));
        Console.WriteLine($"Match Score: {similarity}");

        // Assert
        similarity.ShouldBe(1.0f);
    }

    [Fact]
    public void CalculateSamePerson_SimilarNamesWithMatchingSex_ReturnsHighScore()
    {
        // Arrange
        var patient1 = new Patient
        {
            Demographics = new Demographics
            {
                FirstName = "Ken",
                LastName = "Sheppard",
                Sex = Demographics.SexEnum.Male,
                BirthDate = new DateTime(1980, 1, 1)
            }
        };

        var patient2 = new Patient
        {
            Demographics = new Demographics
            {
                FirstName = "Kenneth",
                LastName = "Shephard",
                Sex = Demographics.SexEnum.Male,
                BirthDate = new DateTime(1980, 1, 1)
            }
        };

        // Act
        float similarity = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Output patient data
        Console.WriteLine("Patient 1:");
        Console.WriteLine(FormatPatientData(patient1));
        Console.WriteLine("Patient 2:");
        Console.WriteLine(FormatPatientData(patient2));
        Console.WriteLine($"Match Score: {similarity}");

        // Assert
        similarity.ShouldBeGreaterThan(0.7f);
    }

    [Fact]
    public void CalculateSamePerson_DifferentSex_ReturnsLowScore()
    {
        // Arrange
        var patient1 = new Patient
        {
            Demographics = new Demographics
            {
                FirstName = "John",
                LastName = "Doe",
                Sex = Demographics.SexEnum.Male,
                BirthDate = new DateTime(1980, 1, 1)
            }
        };

        var patient2 = new Patient
        {
            Demographics = new Demographics
            {
                FirstName = "John",
                LastName = "Doe",
                Sex = Demographics.SexEnum.Female,
                BirthDate = new DateTime(1980, 1, 1)
            }
        };

        // Act
        float similarity = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Output patient data
        Console.WriteLine("Patient 1:");
        Console.WriteLine(FormatPatientData(patient1));
        Console.WriteLine("Patient 2:");
        Console.WriteLine(FormatPatientData(patient2));
        Console.WriteLine($"Match Score: {similarity}");

        // Assert
        similarity.ShouldBeLessThan(0.81f);
    }

    [Fact]
    public void CalculateSamePerson_SimilarPhoneNumbers_ReturnsHighScore()
    {
        // Arrange
        var patient1 = new Patient
        {
            Demographics = new Demographics
            {
                FirstName = "John",
                LastName = "Doe",
                PhoneNumber = "123-456-7890",
                Sex = Demographics.SexEnum.Male
            }
        };

        var patient2 = new Patient
        {
            Demographics = new Demographics
            {
                FirstName = "John",
                LastName = "Doe",
                PhoneNumber = "555-456-7890",
                Sex = Demographics.SexEnum.Male
            }
        };

        // Act
        float similarity = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Output patient data
        Console.WriteLine("Patient 1:");
        Console.WriteLine(FormatPatientData(patient1));
        Console.WriteLine("Patient 2:");
        Console.WriteLine(FormatPatientData(patient2));
        Console.WriteLine($"Match Score: {similarity}");

        // Assert
        similarity.ShouldBeGreaterThan(0.78f);
    }

    [Fact]
    public void CalculateSamePerson_DeathDateMatch_ReturnsHighScore()
    {
        // Arrange
        var patient1 = new Patient
        {
            Demographics = new Demographics
            {
                FirstName = "John",
                LastName = "Doe",
                DeathDate = new DateTime(2020, 1, 1),
                Sex = Demographics.SexEnum.Male
            }
        };

        var patient2 = new Patient
        {
            Demographics = new Demographics
            {
                FirstName = "John",
                LastName = "Doe",
                DeathDate = new DateTime(2020, 1, 1),
                Sex = Demographics.SexEnum.Male
            }
        };

        // Act
        float similarity = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Output patient data
        Console.WriteLine("Patient 1:");
        Console.WriteLine(FormatPatientData(patient1));
        Console.WriteLine("Patient 2:");
        Console.WriteLine(FormatPatientData(patient2));
        Console.WriteLine($"Match Score: {similarity}");

        // Assert
        similarity.ShouldBeGreaterThan(0.78f);
    }

    [Fact]
    public void CalculateSamePerson_NameAndSex_ReturnsHighScore()
    {
        // Arrange
        var patient1 = new Patient
        {
            Demographics = new Demographics
            {
                FirstName = "John",
                LastName = "Doe",
                Sex = Demographics.SexEnum.Male
            }
        };

        var patient2 = new Patient
        {
            Demographics = new Demographics
            {
                FirstName = "John",
                LastName = "Doe",
                Sex = Demographics.SexEnum.Male
            }
        };

        // Act
        float similarity = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Output patient data
        Console.WriteLine("Patient 1:");
        Console.WriteLine(FormatPatientData(patient1));
        Console.WriteLine("Patient 2:");
        Console.WriteLine(FormatPatientData(patient2));
        Console.WriteLine($"Match Score: {similarity}");

        // Assert
        similarity.ShouldBeGreaterThan(0.7f);
    }


    [Fact]
    public void CalculateSamePerson_OneHasDeathDate_ReturnsLowerScore()
    {
        // Arrange
        var patient1 = new Patient
        {
            Demographics = new Demographics
            {
                FirstName = "John",
                LastName = "Doe",
                DeathDate = new DateTime(2020, 1, 1),
                Sex = Demographics.SexEnum.Male
            }
        };

        var patient2 = new Patient
        {
            Demographics = new Demographics
            {
                FirstName = "John",
                LastName = "Doe",
                DeathDate = null,
                Sex = Demographics.SexEnum.Male
            }
        };

        // Act
        float similarity = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Output patient data
        Console.WriteLine("Patient 1:");
        Console.WriteLine(FormatPatientData(patient1));
        Console.WriteLine("Patient 2:");
        Console.WriteLine(FormatPatientData(patient2));
        Console.WriteLine($"Match Score: {similarity}");

        // Assert
        similarity.ShouldBeLessThan(1.0f);
    }

    [Theory]
    [InlineData("Ken", "Kenneth", 0.8f)]
    [InlineData("Kenny", "Kenneth", 0.8f)]
    [InlineData("Kene", "Kenneth", 0.7f)]
    [InlineData("Ken", "Kenny", 0.8f)]
    public void CalculateSamePerson_FirstNameVariations_ReturnsHighScore(string firstName1,
        string firstName2,
        float expectedMinScore)
    {
        // Arrange
        var patient1 = new Patient
        {
            Demographics = new Demographics
            {
                FirstName = firstName1,
                LastName = "Smith",
                Sex = Demographics.SexEnum.Male,
                BirthDate = new DateTime(1980, 1, 1)
            }
        };

        var patient2 = new Patient
        {
            Demographics = new Demographics
            {
                FirstName = firstName2,
                LastName = "Smith",
                Sex = Demographics.SexEnum.Male,
                BirthDate = new DateTime(1980, 1, 1)
            }
        };

        // Act
        float similarity = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Output patient data
        Console.WriteLine("Patient 1:");
        Console.WriteLine(FormatPatientData(patient1));
        Console.WriteLine("Patient 2:");
        Console.WriteLine(FormatPatientData(patient2));
        Console.WriteLine($"Match Score: {similarity}");

        // Assert
        similarity.ShouldBeGreaterThan(expectedMinScore);
    }

    [Theory]
    [InlineData("John", "Jon", 0.8f)]
    [InlineData("John", "Johnny", 0.8f)]
    [InlineData("Jon", "Johnny", 0.8f)]
    [InlineData("John", "Jack", 0.7f)]
    public void CalculateSamePerson_JohnVariations_ReturnsHighScore(string firstName1,
        string firstName2,
        float expectedMinScore)
    {
        // Arrange
        var patient1 = new Patient
        {
            Demographics = new Demographics
            {
                FirstName = firstName1,
                LastName = "Doe",
                Sex = Demographics.SexEnum.Male,
                BirthDate = new DateTime(1980, 1, 1)
            }
        };

        var patient2 = new Patient
        {
            Demographics = new Demographics
            {
                FirstName = firstName2,
                LastName = "Doe",
                Sex = Demographics.SexEnum.Male,
                BirthDate = new DateTime(1980, 1, 1)
            }
        };

        // Act
        float similarity = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Output patient data
        Console.WriteLine("Patient 1:");
        Console.WriteLine(FormatPatientData(patient1));
        Console.WriteLine("Patient 2:");
        Console.WriteLine(FormatPatientData(patient2));
        Console.WriteLine($"Match Score: {similarity}");

        // Assert
        similarity.ShouldBeGreaterThan(expectedMinScore);
    }

    [Theory]
    [InlineData("Robert", "Bob", 0.8f)]
    [InlineData("Robert", "Rob", 0.8f)]
    [InlineData("Robert", "Bobby", 0.8f)]
    [InlineData("Bob", "Bobby", 0.8f)]
    public void CalculateSamePerson_RobertVariations_ReturnsHighScore(string firstName1,
        string firstName2,
        float expectedMinScore)
    {
        // Arrange
        var patient1 = new Patient
        {
            Demographics = new Demographics
            {
                FirstName = firstName1,
                LastName = "Johnson",
                Sex = Demographics.SexEnum.Male,
                BirthDate = new DateTime(1980, 1, 1)
            }
        };

        var patient2 = new Patient
        {
            Demographics = new Demographics
            {
                FirstName = firstName2,
                LastName = "Johnson",
                Sex = Demographics.SexEnum.Male,
                BirthDate = new DateTime(1980, 1, 1)
            }
        };

        // Act
        float similarity = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Output patient data
        Console.WriteLine("Patient 1:");
        Console.WriteLine(FormatPatientData(patient1));
        Console.WriteLine("Patient 2:");
        Console.WriteLine(FormatPatientData(patient2));
        Console.WriteLine($"Match Score: {similarity}");

        // Assert
        similarity.ShouldBeGreaterThan(expectedMinScore);
    }

    [Theory]
    [InlineData("William", "Bill", 0.8f)]
    [InlineData("William", "Will", 0.8f)]
    [InlineData("William", "Billy", 0.8f)]
    [InlineData("Bill", "Billy", 0.8f)]
    public void CalculateSamePerson_WilliamVariations_ReturnsHighScore(string firstName1,
        string firstName2,
        float expectedMinScore)
    {
        // Arrange
        var patient1 = new Patient
        {
            Demographics = new Demographics
            {
                FirstName = firstName1,
                LastName = "Williams",
                Sex = Demographics.SexEnum.Male,
                BirthDate = new DateTime(1980, 1, 1)
            }
        };

        var patient2 = new Patient
        {
            Demographics = new Demographics
            {
                FirstName = firstName2,
                LastName = "Williams",
                Sex = Demographics.SexEnum.Male,
                BirthDate = new DateTime(1980, 1, 1)
            }
        };

        // Act
        float similarity = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Output patient data
        Console.WriteLine("Patient 1:");
        Console.WriteLine(FormatPatientData(patient1));
        Console.WriteLine("Patient 2:");
        Console.WriteLine(FormatPatientData(patient2));
        Console.WriteLine($"Match Score: {similarity}");

        // Assert
        similarity.ShouldBeGreaterThan(expectedMinScore);
    }

    [Theory]
    [InlineData("Elizabeth", "Liz", 0.8f)]
    [InlineData("Elizabeth", "Beth", 0.8f)]
    [InlineData("Elizabeth", "Betty", 0.8f)]
    [InlineData("Elizabeth", "Eliza", 0.8f)]
    [InlineData("Liz", "Beth", 0.7f)]
    public void CalculateSamePerson_ElizabethVariations_ReturnsHighScore(string firstName1,
        string firstName2,
        float expectedMinScore)
    {
        // Arrange
        var patient1 = new Patient
        {
            Demographics = new Demographics
            {
                FirstName = firstName1,
                LastName = "Taylor",
                Sex = Demographics.SexEnum.Female,
                BirthDate = new DateTime(1980, 1, 1)
            }
        };

        var patient2 = new Patient
        {
            Demographics = new Demographics
            {
                FirstName = firstName2,
                LastName = "Taylor",
                Sex = Demographics.SexEnum.Female,
                BirthDate = new DateTime(1980, 1, 1)
            }
        };

        // Act
        float similarity = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Output patient data
        Console.WriteLine("Patient 1:");
        Console.WriteLine(FormatPatientData(patient1));
        Console.WriteLine("Patient 2:");
        Console.WriteLine(FormatPatientData(patient2));
        Console.WriteLine($"Match Score: {similarity}");

        // Assert
        similarity.ShouldBeGreaterThan(expectedMinScore);
    }
}