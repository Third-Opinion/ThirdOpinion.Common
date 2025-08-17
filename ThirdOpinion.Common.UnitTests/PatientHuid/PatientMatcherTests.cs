using Misc.patients.PatientHuid;
using Xunit;

namespace ThirdOpinion.Common.UnitTests.PatientHuid;

public class PatientMatcherTests
{
    [Fact]
    public void CalculateSamePerson_ExactMatch_ReturnsHighScore()
    {
        // Arrange
        var patient1 = CreateTestPatient("John", "Smith", "Michael", Demographics.SexEnum.Male, new DateTime(1980, 5, 15), "555-123-4567");
        var patient2 = CreateTestPatient("John", "Smith", "Michael", Demographics.SexEnum.Male, new DateTime(1980, 5, 15), "555-123-4567");

        // Act
        var score = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Assert
        score.ShouldBeGreaterThanOrEqualTo(0.95f, $"Expected high match score, got {score}");
    }

    [Fact]
    public void CalculateSamePerson_CompletelyDifferent_ReturnsLowScore()
    {
        // Arrange
        var patient1 = CreateTestPatient("John", "Smith", "Michael", Demographics.SexEnum.Male, new DateTime(1980, 5, 15), "555-123-4567");
        var patient2 = CreateTestPatient("Jane", "Doe", "Elizabeth", Demographics.SexEnum.Female, new DateTime(1975, 10, 20), "555-987-6543");

        // Act
        var score = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Assert
        score.ShouldBeLessThanOrEqualTo(0.2f, $"Expected low match score, got {score}");
    }

    [Fact]
    public void CalculateSamePerson_SameNameDifferentSex_ReturnsLowScore()
    {
        // Arrange
        var patient1 = CreateTestPatient("John", "Smith", "Michael", Demographics.SexEnum.Male, new DateTime(1980, 5, 15));
        var patient2 = CreateTestPatient("John", "Smith", "Michael", Demographics.SexEnum.Female, new DateTime(1980, 5, 15));

        // Act
        var score = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Assert
        score.ShouldBeLessThanOrEqualTo(0.3f, $"Expected low match score due to sex difference, got {score}");
    }

    [Fact]
    public void CalculateSamePerson_CommonNicknames_ReturnsHighScore()
    {
        // Arrange
        var patient1 = CreateTestPatient("Robert", "Johnson", null, Demographics.SexEnum.Male, new DateTime(1985, 3, 10));
        var patient2 = CreateTestPatient("Bob", "Johnson", null, Demographics.SexEnum.Male, new DateTime(1985, 3, 10));

        // Act
        var score = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Assert
        (score >= 0.8f).ShouldBeTrue($"Expected high match score for nickname match, got {score}");
    }

    [Fact]
    public void CalculateSamePerson_MinorNameTypo_ReturnsGoodScore()
    {
        // Arrange
        var patient1 = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15));
        var patient2 = CreateTestPatient("Jon", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15));

        // Act
        var score = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Assert
        (score >= 0.7f).ShouldBeTrue($"Expected good match score for minor typo, got {score}");
    }

    [Fact]
    public void CalculateSamePerson_DateTransposition_ReturnsHighScore()
    {
        // Arrange - MM/DD vs DD/MM format confusion: 05/03 vs 03/05
        var patient1 = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 3)); // May 3rd
        var patient2 = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 3, 5)); // March 5th (day/month swapped)

        // Act
        var score = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Assert - This should get moderate score due to different dates but same name
        (score >= 0.5f).ShouldBeTrue($"Expected moderate match score for date confusion, got {score}");
    }

    [Fact]
    public void CalculateSamePerson_SameDateDifferentYear_ReturnsModerateScore()
    {
        // Arrange
        var patient1 = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15));
        var patient2 = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1981, 5, 15)); // Year typo

        // Act
        var score = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Assert
        (score >= 0.6f).ShouldBeTrue($"Expected moderate-high match score for year typo, got {score}");
    }

    [Fact]
    public void CalculateSamePerson_PhoneNumberMatch_IncreasesScore()
    {
        // Arrange
        var patient1 = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15), "555-123-4567");
        var patient2 = CreateTestPatient("Jon", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15), "555-123-4567");

        var patient1NoPhone = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15));
        var patient2NoPhone = CreateTestPatient("Jon", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15));

        // Act
        var scoreWithPhone = PatientMatcher.CalculateSamePerson(patient1, patient2);
        var scoreWithoutPhone = PatientMatcher.CalculateSamePerson(patient1NoPhone, patient2NoPhone);

        // Assert
        (scoreWithPhone > scoreWithoutPhone).ShouldBeTrue("Phone number match should increase score");
    }

    [Fact]
    public void CalculateSamePerson_PhoneNumberFormatDifference_StillMatches()
    {
        // Arrange
        var patient1 = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15), "555-123-4567");
        var patient2 = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15), "(555) 123-4567");

        // Act
        var score = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Assert
        (score >= 0.9f).ShouldBeTrue($"Expected high match score for same phone in different format, got {score}");
    }

    [Fact]
    public void CalculateSamePerson_FirstInitialOnly_ReturnsModerateScore()
    {
        // Arrange
        var patient1 = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15));
        var patient2 = CreateTestPatient("J", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15));

        // Act
        var score = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Assert
        (score >= 0.6f && score <= 0.9f).ShouldBeTrue($"Expected moderate match score for first initial, got {score}");
    }

    [Fact]
    public void CalculateSamePerson_MiddleInitialMatch_IncreasesScore()
    {
        // Arrange
        var patient1 = CreateTestPatient("John", "Smith", "Michael", Demographics.SexEnum.Male, new DateTime(1980, 5, 15));
        var patient2 = CreateTestPatient("John", "Smith", "M", Demographics.SexEnum.Male, new DateTime(1980, 5, 15));

        var patient1NoMiddle = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15));
        var patient2NoMiddle = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15));

        // Act
        var scoreWithMiddleInitial = PatientMatcher.CalculateSamePerson(patient1, patient2);
        var scoreWithoutMiddle = PatientMatcher.CalculateSamePerson(patient1NoMiddle, patient2NoMiddle);

        // Assert
        (scoreWithMiddleInitial > scoreWithoutMiddle).ShouldBeTrue("Middle initial match should increase score");
    }

    [Fact]
    public void CalculateSamePerson_UnknownSex_NeutralImpact()
    {
        // Arrange
        var patient1 = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15));
        var patient2 = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Unknown, new DateTime(1980, 5, 15));

        // Act
        var score = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Assert
        (score >= 0.7f).ShouldBeTrue($"Expected good match score with unknown sex, got {score}");
    }

    [Fact]
    public void CalculateSamePerson_DeathDateMismatch_ReducesScore()
    {
        // Arrange
        var patient1 = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15));
        patient1.Demographics!.DeathDate = new DateTime(2020, 1, 1);
        
        var patient2 = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15));
        patient2.Demographics!.DeathDate = new DateTime(2021, 1, 1);

        var patient1Alive = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15));
        var patient2Alive = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15));

        // Act
        var scoreWithDeathDateMismatch = PatientMatcher.CalculateSamePerson(patient1, patient2);
        var scoreAlive = PatientMatcher.CalculateSamePerson(patient1Alive, patient2Alive);

        // Assert
        (scoreWithDeathDateMismatch < scoreAlive).ShouldBeTrue("Death date mismatch should reduce score");
    }

    [Fact]
    public void CalculateSamePerson_DeathDateMatch_IncreasesScore()
    {
        // Arrange
        var deathDate = new DateTime(2020, 6, 15);
        var patient1 = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15));
        patient1.Demographics!.DeathDate = deathDate;
        
        var patient2 = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15));
        patient2.Demographics!.DeathDate = deathDate;

        // Act
        var score = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Assert
        (score >= 0.95f).ShouldBeTrue($"Expected very high match score with matching death date, got {score}");
    }

    [Fact]
    public void CalculateSamePerson_PrefixSuffixMatch_IncreasesScore()
    {
        // Arrange
        var patient1 = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15));
        patient1.Demographics!.Prefix = "Dr.";
        patient1.Demographics.Suffix = "Jr.";
        
        var patient2 = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15));
        patient2.Demographics!.Prefix = "Dr.";
        patient2.Demographics.Suffix = "Jr.";

        var patient1Plain = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15));
        var patient2Plain = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15));

        // Act
        var scoreWithPrefixSuffix = PatientMatcher.CalculateSamePerson(patient1, patient2);
        var scorePlain = PatientMatcher.CalculateSamePerson(patient1Plain, patient2Plain);

        // Assert
        (scoreWithPrefixSuffix > scorePlain).ShouldBeTrue("Matching prefix and suffix should increase score");
    }

    [Fact]
    public void CalculateSamePerson_NearbyDates_ReturnsHighScore()
    {
        // Arrange - Dates within 3 days
        var patient1 = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15));
        var patient2 = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 17)); // 2 days difference

        // Act
        var score = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Assert
        (score >= 0.8f).ShouldBeTrue($"Expected high match score for nearby dates, got {score}");
    }

    [Fact]
    public void CalculateSamePerson_EmptyNames_HandlesGracefully()
    {
        // Arrange
        var patient1 = CreateTestPatient("", "", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15));
        var patient2 = CreateTestPatient("", "", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15));

        // Act
        var score = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Assert
        (score >= 0.0f && score <= 1.0f).ShouldBeTrue("Score should be valid range even with empty names");
    }

    [Fact]
    public void CalculateSamePerson_PhoneNumberTransposition_ReturnsGoodScore()
    {
        // Arrange - Phone numbers with digit transposition
        var patient1 = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15), "555-123-4567");
        var patient2 = CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15), "555-132-4567"); // 2nd and 3rd digits swapped

        // Act
        var score = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Assert
        (score >= 0.7f).ShouldBeTrue($"Expected good match score for phone transposition, got {score}");
    }

    [Theory]
    [InlineData("William", "Bill")]
    [InlineData("Robert", "Bob")]
    [InlineData("James", "Jim")]
    [InlineData("Elizabeth", "Liz")]
    [InlineData("Katherine", "Kate")]
    public void CalculateSamePerson_CommonNicknameVariations_ReturnsHighScore(string formalName, string nickname)
    {
        // Arrange
        var patient1 = CreateTestPatient(formalName, "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15));
        var patient2 = CreateTestPatient(nickname, "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15));

        // Act
        var score = PatientMatcher.CalculateSamePerson(patient1, patient2);

        // Assert
        (score >= 0.8f).ShouldBeTrue($"Expected high match score for {formalName}/{nickname} pair, got {score}");
    }

    [Fact]
    public void CalculateSamePerson_ScoreRange_IsValid()
    {
        // Test various combinations to ensure score is always between 0 and 1
        var testCases = new[]
        {
            (CreateTestPatient("John", "Smith", null, Demographics.SexEnum.Male, new DateTime(1980, 5, 15)),
             CreateTestPatient("Jane", "Doe", null, Demographics.SexEnum.Female, new DateTime(1975, 10, 20))),
            
            (CreateTestPatient("", "", null, Demographics.SexEnum.Unknown, null),
             CreateTestPatient("", "", null, Demographics.SexEnum.Unknown, null)),
            
            (CreateTestPatient("John", "Smith", "Michael", Demographics.SexEnum.Male, new DateTime(1980, 5, 15), "555-123-4567"),
             CreateTestPatient("John", "Smith", "Michael", Demographics.SexEnum.Male, new DateTime(1980, 5, 15), "555-123-4567"))
        };

        foreach (var (patient1, patient2) in testCases)
        {
            // Act
            var score = PatientMatcher.CalculateSamePerson(patient1, patient2);

            // Assert
            (score >= 0.0f && score <= 1.0f).ShouldBeTrue($"Score {score} is outside valid range [0,1]");
        }
    }

    private Patient CreateTestPatient(string firstName, string lastName, string? middleName, Demographics.SexEnum sex, DateTime? birthDate, string? phoneNumber = null)
    {
        return new Patient
        {
            TenantGuid = Guid.NewGuid(),
            PatientGuid = Guid.NewGuid(),
            PatientHuid = "P123-456-7890",
            Provenance = "test",
            Demographics = new Demographics
            {
                FirstName = firstName,
                LastName = lastName,
                MiddleName = middleName,
                Sex = sex,
                BirthDate = birthDate,
                PhoneNumber = phoneNumber
            }
        };
    }
}