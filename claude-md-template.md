# CLAUDE.md - thirdopinion.common Package Context

## Package Overview

thirdopinion.common is a reusable .NET library providing unified interfaces for AWS services (S3, DynamoDB, Cognito)
with built-in retry logic, logging, and error handling optimized for production workloads.

## Installation & Setup

### 1. Install Package

```bash
dotnet add package thirdopinion.common --version 1.0.0
```

### 2. Required Dependencies

```xml
<PackageReference Include="AWSSDK.S3" Version="3.7.*" />
<PackageReference Include="AWSSDK.DynamoDBv2" Version="3.7.*" />
<PackageReference Include="AWSSDK.CognitoIdentityProvider" Version="3.7.*" />
<PackageReference Include="AWSSDK.Extensions.NETCore.Setup" Version="3.7.*" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.*" />
```

### 3. Configure in Program.cs (NET 8)

```csharp
using ThirdOpinion.Common;
using ThirdOpinion.Common.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Configure AWS SDK
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());

// Register thirdopinion.common services
builder.Services.AddThirdOpinionAWS(builder.Configuration);

var app = builder.Build();
```

### 4. appsettings.json Configuration

```json
{
  "AWS": {
    "Profile": "default",
    "Region": "us-east-2",
    "S3": {
      "DefaultBucket": "thirdopinion-storage",
      "UseTransferUtility": true,
      "MaxRetries": 3,
      "Timeout": 30
    },
    "DynamoDB": {
      "TablePrefix": "thirdopinion_",
      "ConsistentRead": false,
      "MaxBatchSize": 25
    },
    "Cognito": {
      "UserPoolId": "us-east-2_xxxxx",
      "ClientId": "xxxxxxxxxxxxx",
      "ClientSecret": "xxxxxxxxxxxxx"
    }
  }
}
```

## Service Interfaces

### IS3Service - S3 Operations

```csharp
public interface IS3Service
{
    // Upload file with automatic retry and progress tracking
    Task<S3UploadResult> UploadAsync(string bucketName, string key, Stream stream, 
        Dictionary<string, string> metadata = null, IProgress<long> progress = null,
        CancellationToken cancellationToken = default);
    
    // Download with resume support
    Task<Stream> DownloadAsync(string bucketName, string key, 
        CancellationToken cancellationToken = default);
    
    // Generate presigned URLs
    Task<string> GetPresignedUrlAsync(string bucketName, string key, 
        TimeSpan expiration, HttpVerb verb = HttpVerb.GET);
    
    // List objects with pagination
    IAsyncEnumerable<S3Object> ListObjectsAsync(string bucketName, string prefix = null,
        CancellationToken cancellationToken = default);
    
    // Delete with versioning support
    Task<bool> DeleteAsync(string bucketName, string key, string versionId = null,
        CancellationToken cancellationToken = default);
}
```

### IDynamoDBService - DynamoDB Operations

```csharp
public interface IDynamoDBService
{
    // Generic CRUD operations with automatic serialization
    Task<T> GetItemAsync<T>(string tableName, object hashKey, object rangeKey = null,
        CancellationToken cancellationToken = default) where T : class;
    
    Task<bool> PutItemAsync<T>(string tableName, T item,
        CancellationToken cancellationToken = default) where T : class;
    
    Task<bool> UpdateItemAsync<T>(string tableName, object hashKey, 
        Dictionary<string, object> updates, object rangeKey = null,
        CancellationToken cancellationToken = default) where T : class;
    
    Task<bool> DeleteItemAsync(string tableName, object hashKey, object rangeKey = null,
        CancellationToken cancellationToken = default);
    
    // Query with automatic pagination
    IAsyncEnumerable<T> QueryAsync<T>(string tableName, string indexName = null,
        Dictionary<string, object> conditions = null,
        CancellationToken cancellationToken = default) where T : class;
    
    // Batch operations
    Task<BatchWriteResult> BatchWriteAsync<T>(string tableName, 
        IEnumerable<T> itemsToPut = null, IEnumerable<object> keysToDelete = null,
        CancellationToken cancellationToken = default) where T : class;
}
```

### ICognitoService - Authentication & User Management

```csharp
public interface ICognitoService
{
    // Authentication
    Task<AuthenticationResult> AuthenticateAsync(string username, string password,
        CancellationToken cancellationToken = default);
    
    Task<AuthenticationResult> RefreshTokenAsync(string refreshToken,
        CancellationToken cancellationToken = default);
    
    // User management
    Task<CognitoUser> CreateUserAsync(string username, string email, 
        string temporaryPassword = null, Dictionary<string, string> attributes = null,
        CancellationToken cancellationToken = default);
    
    Task<bool> ConfirmUserAsync(string username, string confirmationCode,
        CancellationToken cancellationToken = default);
    
    Task<bool> ChangePasswordAsync(string accessToken, string oldPassword, 
        string newPassword, CancellationToken cancellationToken = default);
    
    // Password recovery
    Task<bool> InitiatePasswordResetAsync(string username,
        CancellationToken cancellationToken = default);
    
    Task<bool> ConfirmPasswordResetAsync(string username, string confirmationCode,
        string newPassword, CancellationToken cancellationToken = default);
    
    // User attributes
    Task<Dictionary<string, string>> GetUserAttributesAsync(string accessToken,
        CancellationToken cancellationToken = default);
    
    Task<bool> UpdateUserAttributesAsync(string accessToken, 
        Dictionary<string, string> attributes,
        CancellationToken cancellationToken = default);
}
```

## Common Usage Patterns

### Pattern 1: File Upload with Progress

```csharp
[ApiController]
[Route("api/[controller]")]
public class DocumentController : ControllerBase
{
    private readonly IS3Service _s3Service;
    
    public DocumentController(IS3Service s3Service)
    {
        _s3Service = s3Service;
    }
    
    [HttpPost("upload")]
    public async Task<IActionResult> UploadDocument(IFormFile file)
    {
        var key = $"documents/{Guid.NewGuid()}/{file.FileName}";
        
        var progress = new Progress<long>(bytes => 
            Console.WriteLine($"Uploaded {bytes} of {file.Length} bytes"));
        
        using var stream = file.OpenReadStream();
        var result = await _s3Service.UploadAsync(
            "thirdopinion-storage", 
            key, 
            stream,
            new Dictionary<string, string> 
            {
                ["content-type"] = file.ContentType,
                ["original-name"] = file.FileName
            },
            progress);
        
        return Ok(new { key, etag = result.ETag, versionId = result.VersionId });
    }
}
```

### Pattern 2: DynamoDB CRUD Operations

```csharp
public class UserRepository
{
    private readonly IDynamoDBService _dynamoDB;
    private const string TableName = "Users";
    
    public UserRepository(IDynamoDBService dynamoDB)
    {
        _dynamoDB = dynamoDB;
    }
    
    public async Task<User> GetUserAsync(string userId)
    {
        return await _dynamoDB.GetItemAsync<User>(TableName, userId);
    }
    
    public async Task<bool> CreateUserAsync(User user)
    {
        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        return await _dynamoDB.PutItemAsync(TableName, user);
    }
    
    public async Task<bool> UpdateUserEmailAsync(string userId, string newEmail)
    {
        var updates = new Dictionary<string, object>
        {
            ["Email"] = newEmail,
            ["UpdatedAt"] = DateTime.UtcNow
        };
        
        return await _dynamoDB.UpdateItemAsync<User>(TableName, userId, updates);
    }
    
    public async IAsyncEnumerable<User> GetActiveUsersAsync()
    {
        var conditions = new Dictionary<string, object>
        {
            ["Status"] = "Active"
        };
        
        await foreach (var user in _dynamoDB.QueryAsync<User>(
            TableName, "StatusIndex", conditions))
        {
            yield return user;
        }
    }
}
```

### Pattern 3: Cognito Authentication Flow

```csharp
public class AuthService
{
    private readonly ICognitoService _cognito;
    
    public AuthService(ICognitoService cognito)
    {
        _cognito = cognito;
    }
    
    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        try
        {
            var result = await _cognito.AuthenticateAsync(
                request.Username, 
                request.Password);
            
            return new LoginResponse
            {
                AccessToken = result.AccessToken,
                RefreshToken = result.RefreshToken,
                ExpiresIn = result.ExpiresIn,
                TokenType = result.TokenType
            };
        }
        catch (NotAuthorizedException)
        {
            throw new UnauthorizedAccessException("Invalid credentials");
        }
        catch (UserNotConfirmedException)
        {
            throw new InvalidOperationException("Please confirm your email first");
        }
    }
    
    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        var attributes = new Dictionary<string, string>
        {
            ["email"] = request.Email,
            ["name"] = request.FullName,
            ["preferred_username"] = request.Username
        };
        
        var user = await _cognito.CreateUserAsync(
            request.Username,
            request.Email,
            attributes: attributes);
        
        // Send confirmation email automatically via Cognito
        return new RegisterResponse
        {
            UserId = user.Username,
            RequiresConfirmation = true
        };
    }
}
```

## Error Handling

All services throw specific exceptions that should be handled:

```csharp
try
{
    await _s3Service.UploadAsync(bucket, key, stream);
}
catch (Amazon.S3.AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
{
    // Bucket doesn't exist
    _logger.LogError("Bucket {Bucket} not found", bucket);
}
catch (Amazon.S3.AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
{
    // Access denied
    _logger.LogError("Access denied to bucket {Bucket}", bucket);
}
catch (ThirdOpinion.Common.Exceptions.RetryExhaustedException ex)
{
    // All retries failed
    _logger.LogError(ex, "Upload failed after {RetryCount} attempts", ex.RetryCount);
}
```

## Testing & Local Development

### LocalStack Configuration

```json
{
  "AWS": {
    "ServiceURL": "http://localhost:4566",
    "UseLocalStack": true,
    "ForcePathStyle": true
  }
}
```

### Unit Testing with Mocks

```csharp
[Test]
public async Task UploadAsync_ValidFile_ReturnsResult()
{
    // Arrange
    var mockS3Service = new Mock<IS3Service>();
    mockS3Service.Setup(x => x.UploadAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Stream>(),
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<IProgress<long>>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(new S3UploadResult 
        { 
            ETag = "abc123",
            VersionId = "v1"
        });
    
    var controller = new DocumentController(mockS3Service.Object);
    
    // Act & Assert
    // ... test implementation
}
```

## IAM Permissions Required

### S3 Service

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "s3:GetObject",
        "s3:PutObject",
        "s3:DeleteObject",
        "s3:ListBucket"
      ],
      "Resource": [
        "arn:aws:s3:::thirdopinion-storage",
        "arn:aws:s3:::thirdopinion-storage/*"
      ]
    }
  ]
}
```

### DynamoDB Service

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "dynamodb:GetItem",
        "dynamodb:PutItem",
        "dynamodb:UpdateItem",
        "dynamodb:DeleteItem",
        "dynamodb:Query",
        "dynamodb:Scan",
        "dynamodb:BatchWriteItem"
      ],
      "Resource": "arn:aws:dynamodb:us-east-2:*:table/thirdopinion_*"
    }
  ]
}
```

### Cognito Service

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "cognito-idp:AdminInitiateAuth",
        "cognito-idp:AdminCreateUser",
        "cognito-idp:AdminGetUser",
        "cognito-idp:AdminUpdateUserAttributes",
        "cognito-idp:AdminSetUserPassword"
      ],
      "Resource": "arn:aws:cognito-idp:us-east-2:*:userpool/*"
    }
  ]
}
```

## Performance Considerations

- **S3Service**: Uses TransferUtility for files > 5MB, automatic multipart uploads
- **DynamoDBService**: Implements connection pooling, batch operations limited to 25 items
- **CognitoService**: Caches JWT tokens, automatic refresh before expiration

## Troubleshooting

### Common Issues

1. **"Unable to find credentials"**
    - Check AWS profile in appsettings.json
    - Verify environment variables: AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY
    - Ensure IAM role attached if running on EC2/ECS

2. **"The specified bucket does not exist"**
    - Verify bucket name in configuration
    - Check region settings match bucket location
    - Ensure bucket exists and is accessible

3. **"User pool does not exist"**
    - Confirm UserPoolId in configuration
    - Verify region matches Cognito user pool region
    - Check ClientId and ClientSecret are correct

## Package Maintainers

- Repository: https://github.com/thirdopinion/thirdopinion.common
- NuGet: https://www.nuget.org/packages/thirdopinion.common
- Issues: https://github.com/thirdopinion/thirdopinion.common/issues

## Version History

- 1.0.0: Initial release with S3, DynamoDB, Cognito support
- 1.1.0: Added retry policies and circuit breaker patterns
- 1.2.0: Performance improvements and batch operation support