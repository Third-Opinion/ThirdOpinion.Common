using System.Text.Json;
using Bogus;
using Misc.patients.PatientHuid;

namespace ThirdOpinion.Common.FunctionalTests.Infrastructure;

/// <summary>
///     Builder for generating consistent test data across functional tests
/// </summary>
public static class TestDataBuilder
{
    private static readonly Faker _faker = new();

    /// <summary>
    ///     Create a test patient with realistic data
    /// </summary>
    public static Patient CreateTestPatient(
        string? firstName = null,
        string? lastName = null,
        string? middleName = null,
        DateTime? birthDate = null,
        Demographics.SexEnum? sex = null,
        string? phoneNumber = null)
    {
        var patientFaker = new Faker<Patient>()
            .RuleFor(p => p.TenantGuid, _ => Guid.NewGuid())
            .RuleFor(p => p.PatientGuid, _ => Guid.NewGuid())
            .RuleFor(p => p.PatientHuid, f => $"P{f.Random.AlphaNumeric(10).ToUpper()}")
            .RuleFor(p => p.Provenance, f => f.Company.CompanyName())
            .RuleFor(p => p.Demographics, _ => new Demographics
            {
                FirstName = firstName ?? _faker.Name.FirstName(),
                LastName = lastName ?? _faker.Name.LastName(),
                MiddleName = middleName ?? (_faker.Random.Bool() ? null : _faker.Name.FirstName()),
                Sex = sex ?? _faker.PickRandom<Demographics.SexEnum>(),
                BirthDate = birthDate ?? _faker.Date.Between(DateTime.Now.AddYears(-80),
                    DateTime.Now.AddYears(-18)),
                PhoneNumber = phoneNumber ?? GenerateValidPhoneNumber()
            });

        return patientFaker.Generate();
    }

    /// <summary>
    ///     Create a list of test patients
    /// </summary>
    public static List<Patient> CreateTestPatients(int count)
    {
        return Enumerable.Range(0, count)
            .Select(_ => CreateTestPatient())
            .ToList();
    }

    /// <summary>
    ///     Create test data for AWS DynamoDB
    /// </summary>
    public static Dictionary<string, object> CreateDynamoDbTestData(string? id = null)
    {
        return new Dictionary<string, object>
        {
            ["Id"] = id ?? Guid.NewGuid().ToString(),
            ["Name"] = _faker.Name.FullName(),
            ["Email"] = _faker.Internet.Email(),
            ["CreatedAt"] = DateTime.UtcNow.ToString("O"),
            ["Age"] = _faker.Random.Int(18, 80),
            ["IsActive"] = _faker.Random.Bool(),
            ["Tags"] = _faker.Lorem.Words().Distinct().ToList(),
            ["Metadata"] = new Dictionary<string, object>
            {
                ["Source"] = "FunctionalTest",
                ["Version"] = "1.0",
                ["LastUpdated"] = DateTime.UtcNow.ToString("O")
            }
        };
    }

    /// <summary>
    ///     Create test file content for S3 testing
    /// </summary>
    public static byte[] CreateBinaryTestData(int sizeInBytes = 1024)
    {
        return _faker.Random.Bytes(sizeInBytes);
    }

    /// <summary>
    ///     Create test JSON content
    /// </summary>
    public static string CreateTestJsonContent()
    {
        var testObject = new
        {
            Id = Guid.NewGuid(),
            Name = _faker.Name.FullName(),
            Email = _faker.Internet.Email(),
            CreatedAt = DateTime.UtcNow,
            Data = new
            {
                Description = _faker.Lorem.Paragraph(),
                Tags = _faker.Lorem.Words(5),
                Score = _faker.Random.Double(0, 100)
            }
        };

        return JsonSerializer.Serialize(testObject, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    /// <summary>
    ///     Create test message for SQS
    /// </summary>
    public static string CreateTestMessage(string? messageType = null)
    {
        var message = new
        {
            Id = Guid.NewGuid(),
            Type = messageType ?? "TestMessage",
            Timestamp = DateTime.UtcNow,
            Data = new
            {
                Message = _faker.Lorem.Sentence(),
                Priority = _faker.PickRandom("High", "Medium", "Low"),
                Source = "FunctionalTest"
            }
        };

        return JsonSerializer.Serialize(message);
    }

    /// <summary>
    ///     Create test user data for Cognito
    /// </summary>
    public static (string email, string password, Dictionary<string, string> attributes)
        CreateTestUser()
    {
        string? email = _faker.Internet.Email();
        string password = GenerateValidPassword();
        var attributes = new Dictionary<string, string>
        {
            ["given_name"] = _faker.Name.FirstName(),
            ["family_name"] = _faker.Name.LastName(),
            ["phone_number"] = GenerateValidPhoneNumber()
        };

        return (email, password, attributes);
    }

    /// <summary>
    ///     Generate a password that meets common requirements
    /// </summary>
    private static string GenerateValidPassword()
    {
        // Generate a simple password without problematic regex
        string? basePassword = _faker.Internet.Password(8);

        // Build a password that meets AWS Cognito requirements
        var password = string.Empty;
        password += _faker.Random.Char('A', 'Z'); // Uppercase
        password += _faker.Random.Char('a', 'z'); // Lowercase  
        password += _faker.Random.Char('0', '9'); // Number
        password += _faker.PickRandom("!@#$%^&*"); // Special character

        // Add random alphanumeric characters to reach minimum length
        for (var i = 4; i < 8; i++) password += _faker.Random.AlphaNumeric(1);

        // Shuffle the password characters to randomize order
        return new string(password.OrderBy(x => _faker.Random.Int()).ToArray());
    }

    /// <summary>
    ///     Generate a phone number that meets AWS Cognito requirements (E.164 format)
    /// </summary>
    private static string GenerateValidPhoneNumber()
    {
        // Generate a valid US phone number in E.164 format (+1XXXXXXXXXX)
        int areaCode = _faker.Random.Int(201, 999); // Valid US area codes
        int exchangeCode = _faker.Random.Int(200, 999); // Valid exchange codes
        int lineNumber = _faker.Random.Int(1000, 9999); // Valid line numbers

        return $"+1{areaCode}{exchangeCode}{lineNumber}";
    }

    /// <summary>
    ///     Create test configuration data
    /// </summary>
    public static Dictionary<string, string> CreateTestConfiguration()
    {
        return new Dictionary<string, string>
        {
            ["TestRunId"] = Guid.NewGuid().ToString(),
            ["Environment"] = "FunctionalTest",
            ["CreatedAt"] = DateTime.UtcNow.ToString("O"),
            ["CreatedBy"] = Environment.UserName,
            ["MachineName"] = Environment.MachineName
        };
    }
}