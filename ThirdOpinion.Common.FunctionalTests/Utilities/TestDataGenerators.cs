using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Amazon.SQS.Model;
using Bogus;

namespace ThirdOpinion.Common.FunctionalTests.Utilities;

public static class TestDataGenerators
{
    private static readonly Faker _faker = new();

    // Utility methods
    public static string GenerateSecureToken(int length = 32)
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[length];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_")
            .Replace("=", "")[..length];
    }

    public static string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static string GenerateUniqueName(string prefix = "test")
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string randomSuffix = _faker.Random.AlphaNumeric(8).ToLowerInvariant();
        return $"{prefix}-{timestamp}-{randomSuffix}";
    }

    public static class Users
    {
        public static (string email, string password, Dictionary<string, string> attributes)
            CreateTestUser(string? emailPrefix = null)
        {
            string? firstName = _faker.Name.FirstName();
            string? lastName = _faker.Name.LastName();
            string? email = emailPrefix != null
                ? $"{emailPrefix}+{_faker.Random.AlphaNumeric(8)}@example.com"
                : _faker.Internet.Email(firstName, lastName);

            string password = GenerateValidPassword();

            var attributes = new Dictionary<string, string>
            {
                ["given_name"] = firstName,
                ["family_name"] = lastName,
                ["email"] = email,
                ["phone_number"] = _faker.Phone.PhoneNumber("+1##########"),
                ["birthdate"] = _faker.Date.Past(50, DateTime.Now.AddYears(-18))
                    .ToString("yyyy-MM-dd"),
                ["gender"] = _faker.PickRandom("male", "female", "other"),
                ["locale"] = "en_US",
                ["zoneinfo"] = _faker.Date.TimeZoneString()
            };

            return (email, password, attributes);
        }

        public static List<(string email, string password, Dictionary<string, string> attributes)>
            CreateTestUsers(int count, string? emailPrefix = null)
        {
            return Enumerable.Range(0, count)
                .Select(_ => CreateTestUser(emailPrefix))
                .ToList();
        }

        private static string GenerateValidPassword()
        {
            // Generate password that meets Cognito requirements
            var password = new StringBuilder();

            // Add required character types
            password.Append(_faker.Random.String2(2, "ABCDEFGHIJKLMNOPQRSTUVWXYZ")); // Uppercase
            password.Append(_faker.Random.String2(2)); // Lowercase
            password.Append(_faker.Random.String2(2, "0123456789")); // Numbers
            password.Append(_faker.Random.String2(2, "!@#$%^&*")); // Special characters

            // Fill to minimum length
            while (password.Length < 12)
                password.Append(_faker.Random.String2(1,
                    "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*"));

            // Shuffle the characters
            char[] chars = password.ToString().ToCharArray();
            _faker.Random.Shuffle(chars);

            return new string(chars);
        }
    }

    public static class DynamoDb
    {
        public static Dictionary<string, object> CreateTestRecord(string? id = null)
        {
            return new Dictionary<string, object>
            {
                ["Id"] = id ?? Guid.NewGuid().ToString(),
                ["Name"] = _faker.Name.FullName(),
                ["Email"] = _faker.Internet.Email(),
                ["Age"] = _faker.Random.Int(18, 80),
                ["Department"] = _faker.Commerce.Department(),
                ["Salary"] = _faker.Random.Decimal(30000, 150000),
                ["IsActive"] = _faker.Random.Bool(),
                ["CreatedAt"] = _faker.Date.Past(),
                ["UpdatedAt"] = DateTime.UtcNow,
                ["Tags"] = _faker.Make(3, () => _faker.Lorem.Word()),
                ["Metadata"] = new Dictionary<string, object>
                {
                    ["version"] = _faker.System.Version().ToString(),
                    ["source"] = _faker.System.FileName(),
                    ["category"] = _faker.Commerce.Categories(1).First()
                }
            };
        }

        public static List<Dictionary<string, object>> CreateTestRecords(int count)
        {
            return _faker.Make(count, () => CreateTestRecord()).ToList();
        }

        public static Dictionary<string, object> CreateUserProfile(string userId)
        {
            return new Dictionary<string, object>
            {
                ["Id"] = userId,
                ["FirstName"] = _faker.Name.FirstName(),
                ["LastName"] = _faker.Name.LastName(),
                ["Email"] = _faker.Internet.Email(),
                ["Phone"] = _faker.Phone.PhoneNumber(),
                ["DateOfBirth"] = _faker.Date.Past(50, DateTime.Now.AddYears(-18)),
                ["Address"] = new Dictionary<string, object>
                {
                    ["Street"] = _faker.Address.StreetAddress(),
                    ["City"] = _faker.Address.City(),
                    ["State"] = _faker.Address.StateAbbr(),
                    ["ZipCode"] = _faker.Address.ZipCode(),
                    ["Country"] = "US"
                },
                ["Preferences"] = new Dictionary<string, object>
                {
                    ["Language"] = _faker.Locale,
                    ["Timezone"] = _faker.Date.TimeZoneString(),
                    ["NotificationsEnabled"] = _faker.Random.Bool(),
                    ["Theme"] = _faker.PickRandom("light", "dark", "auto")
                },
                ["CreatedAt"] = DateTime.UtcNow,
                ["LastLoginAt"] = _faker.Date.Recent(),
                ["IsVerified"] = _faker.Random.Bool(0.8f),
                ["Roles"] = _faker.Make(_faker.Random.Int(1, 3),
                    () => _faker.PickRandom("user", "admin", "moderator", "viewer"))
            };
        }
    }

    public static class S3
    {
        public static byte[] CreateBinaryData(int sizeInBytes)
        {
            var data = new byte[sizeInBytes];
            new Random().NextBytes(data);
            return data;
        }

        public static string CreateTextContent(int wordCount = 100)
        {
            return _faker.Lorem.Words(wordCount).Aggregate((a, b) => $"{a} {b}");
        }

        public static byte[] CreateImageData(int width = 100, int height = 100)
        {
            // Create a simple bitmap-like structure for testing
            var headerSize = 54;
            int pixelDataSize = width * height * 3; // 24-bit RGB
            int totalSize = headerSize + pixelDataSize;

            var data = new byte[totalSize];

            // Simple BMP header (simplified for testing)
            data[0] = 0x42; // 'B'
            data[1] = 0x4D; // 'M'
            BitConverter.GetBytes(totalSize).CopyTo(data, 2);
            BitConverter.GetBytes(headerSize).CopyTo(data, 10);
            BitConverter.GetBytes(40).CopyTo(data, 14); // DIB header size
            BitConverter.GetBytes(width).CopyTo(data, 18);
            BitConverter.GetBytes(height).CopyTo(data, 22);

            // Fill with random pixel data
            var pixelData = new byte[pixelDataSize];
            new Random().NextBytes(pixelData);
            pixelData.CopyTo(data, headerSize);

            return data;
        }

        public static string CreateCsvContent(int rows = 10)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Id,Name,Email,Age,Department,Salary,CreatedAt");

            for (var i = 0; i < rows; i++)
                csv.AppendLine(
                    $"{i + 1},{_faker.Name.FullName()},{_faker.Internet.Email()},{_faker.Random.Int(22, 65)},{_faker.Commerce.Department()},{_faker.Random.Decimal(35000, 120000)},{_faker.Date.Past().ToString("yyyy-MM-dd")}");

            return csv.ToString();
        }

        public static string CreateJsonContent(object? data = null)
        {
            object objectToSerialize = data ?? new
            {
                Id = Guid.NewGuid(),
                Name = _faker.Name.FullName(),
                Email = _faker.Internet.Email(),
                CreatedAt = DateTime.UtcNow,
                Data = _faker.Make(5, () => new
                {
                    Key = _faker.Lorem.Word(),
                    Value = _faker.Lorem.Sentence()
                })
            };

            return JsonSerializer.Serialize(objectToSerialize, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        public static Dictionary<string, string> CreateMetadata()
        {
            return new Dictionary<string, string>
            {
                ["content-type"] = _faker.PickRandom("application/json", "text/plain", "image/jpeg",
                    "application/pdf"),
                ["author"] = _faker.Name.FullName(),
                ["created-by"] = _faker.Internet.UserName(),
                ["department"] = _faker.Commerce.Department(),
                ["version"] = _faker.System.Version().ToString(),
                ["category"] = _faker.Commerce.Categories(1).First(),
                ["tags"] = string.Join(",", _faker.Make(3, () => _faker.Lorem.Word()))
            };
        }
    }

    public static class SQS
    {
        public static string CreateMessageBody(string? messageType = null)
        {
            string? type = messageType ??
                           _faker.PickRandom("notification", "processing", "webhook", "alert");

            var message = new
            {
                Id = Guid.NewGuid(),
                Type = type,
                Timestamp = DateTime.UtcNow,
                Source = _faker.Internet.DomainName(),
                Data = (object)(type switch
                {
                    "notification" => new
                    {
                        UserId = Guid.NewGuid(),
                        Title = _faker.Lorem.Sentence(3),
                        Message = _faker.Lorem.Paragraph(),
                        Priority = _faker.PickRandom("low", "medium", "high"),
                        Channel = _faker.PickRandom("email", "sms", "push")
                    },
                    "processing" => new
                    {
                        JobId = Guid.NewGuid(),
                        TaskType = _faker.PickRandom("image-resize", "document-convert",
                            "data-export"),
                        InputPath = $"input/{_faker.System.FileName()}",
                        OutputPath = $"output/{_faker.System.FileName()}",
                        Parameters = _faker.Make<object>(3,
                            () => new { Key = _faker.Lorem.Word(), Value = _faker.Lorem.Word() })
                    },
                    "webhook" => new
                    {
                        EventType = _faker.PickRandom("user.created", "order.completed",
                            "payment.processed"),
                        EntityId = Guid.NewGuid(),
                        Url = _faker.Internet.Url(),
                        Headers = new { Authorization = $"Bearer {_faker.Random.AlphaNumeric(32)}" }
                    },
                    _ => new
                    {
                        Level = _faker.PickRandom("info", "warning", "error", "critical"),
                        Component = _faker.PickRandom("api", "database", "cache", "queue"),
                        Message = _faker.Lorem.Sentence(),
                        Details = _faker.Lorem.Paragraph()
                    }
                })
            };

            return JsonSerializer.Serialize(message);
        }

        public static Dictionary<string, MessageAttributeValue> CreateMessageAttributes()
        {
            return new Dictionary<string, MessageAttributeValue>
            {
                ["MessageType"] = new()
                {
                    DataType = "String",
                    StringValue
                        = _faker.PickRandom("notification", "processing", "webhook", "alert")
                },
                ["Priority"] = new()
                {
                    DataType = "String", StringValue = _faker.PickRandom("low", "medium", "high")
                },
                ["Source"] = new()
                    { DataType = "String", StringValue = _faker.Internet.DomainName() },
                ["UserId"] = new() { DataType = "String", StringValue = Guid.NewGuid().ToString() },
                ["Timestamp"] = new()
                {
                    DataType = "Number",
                    StringValue = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()
                },
                ["RetryCount"] = new()
                    { DataType = "Number", StringValue = _faker.Random.Int(0, 3).ToString() }
            };
        }

        public static List<string> CreateBatchMessages(int count, string? messageType = null)
        {
            return _faker.Make(count, () => CreateMessageBody(messageType)).ToList();
        }
    }

    public static class CrossService
    {
        public static class UserWorkflow
        {
            public static (string userId, Dictionary<string, object> profile, byte[] profileImage,
                string welcomeMessage) CreateUserRegistrationData()
            {
                var userId = Guid.NewGuid().ToString();
                Dictionary<string, object> profile = DynamoDb.CreateUserProfile(userId);
                byte[] profileImage = S3.CreateImageData(200, 200);

                string welcomeMessage = JsonSerializer.Serialize(new
                {
                    UserId = userId,
                    UserName = profile["FirstName"] + " " + profile["LastName"],
                    Email = profile["Email"],
                    Action = "UserRegistered",
                    Timestamp = DateTime.UtcNow,
                    WelcomeMessage
                        = $"Welcome {profile["FirstName"]}! Your account has been created successfully."
                });

                return (userId, profile, profileImage, welcomeMessage);
            }
        }

        public static class DocumentProcessing
        {
            public static (string documentId, string content, Dictionary<string, object> jobRecord,
                string processingMessage) CreateDocumentProcessingData(string userId)
            {
                var documentId = Guid.NewGuid().ToString();
                string content = S3.CreateTextContent(500);

                var jobRecord = new Dictionary<string, object>
                {
                    ["Id"] = Guid.NewGuid().ToString(),
                    ["DocumentId"] = documentId,
                    ["UserId"] = userId,
                    ["FileName"] = _faker.System.FileName("txt"),
                    ["FileSize"] = content.Length,
                    ["Status"] = "Pending",
                    ["ProcessingType"] = _faker.PickRandom("ocr", "translation", "summarization"),
                    ["CreatedAt"] = DateTime.UtcNow,
                    ["Priority"] = _faker.PickRandom("low", "medium", "high"),
                    ["EstimatedDuration"] = _faker.Random.Int(60, 3600) // seconds
                };

                string processingMessage = JsonSerializer.Serialize(new
                {
                    JobId = jobRecord["Id"],
                    DocumentId = documentId,
                    UserId = userId,
                    Action = "ProcessDocument",
                    ProcessingType = jobRecord["ProcessingType"],
                    Priority = jobRecord["Priority"],
                    Timestamp = DateTime.UtcNow
                });

                return (documentId, content, jobRecord, processingMessage);
            }
        }

        public static class DataBackup
        {
            public static (Dictionary<string, object> originalData, string backupData, string
                notificationMessage) CreateBackupData()
            {
                Dictionary<string, object> originalData = DynamoDb.CreateTestRecord();
                string backupData = JsonSerializer.Serialize(originalData,
                    new JsonSerializerOptions { WriteIndented = true });

                string notificationMessage = JsonSerializer.Serialize(new
                {
                    OriginalId = originalData["Id"],
                    BackupId = Guid.NewGuid(),
                    BackupDate = DateTime.UtcNow,
                    BackupSize = backupData.Length,
                    Action = "DataBackup",
                    Status = "Completed",
                    RetentionDays = _faker.Random.Int(30, 365)
                });

                return (originalData, backupData, notificationMessage);
            }
        }
    }

    public static class Performance
    {
        public static IEnumerable<T> CreateLargeDataset<T>(Func<T> generator, int count)
        {
            for (var i = 0; i < count; i++) yield return generator();
        }

        public static byte[] CreateLargeFile(int sizeInMB)
        {
            int sizeInBytes = sizeInMB * 1024 * 1024;
            return S3.CreateBinaryData(sizeInBytes);
        }

        public static List<Dictionary<string, object>> CreateBulkDynamoDbRecords(int count)
        {
            return CreateLargeDataset(() => DynamoDb.CreateTestRecord(), count).ToList();
        }

        public static List<string> CreateBulkSqsMessages(int count, string? messageType = null)
        {
            return CreateLargeDataset(() => SQS.CreateMessageBody(messageType), count).ToList();
        }
    }
}