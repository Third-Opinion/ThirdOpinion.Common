using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Microsoft.Extensions.Configuration;
using ThirdOpinion.Common.FunctionalTests.Infrastructure;
using Xunit.Abstractions;

namespace ThirdOpinion.Common.FunctionalTests.Tests;

[Collection("Cognito")]
public class CognitoFunctionalTests : BaseIntegrationTest
{
    private string? _userPoolId;
    private string? _clientId;
    private readonly string _testPrefix;
    
    public CognitoFunctionalTests(ITestOutputHelper output) : base(output)
    {
        _testPrefix = Configuration.GetValue<string>("TestSettings:TestResourcePrefix") ?? "functest";
    }

    protected override async Task SetupTestResourcesAsync()
    {
        await base.SetupTestResourcesAsync();
        
        // Create test user pool
        var userPoolName = $"{_testPrefix}-{GenerateTestResourceName("pool")}";
        await CreateTestUserPoolAsync(userPoolName);
        
        WriteOutput($"Created test user pool: {userPoolName}");
    }

    protected override async Task CleanupTestResourcesAsync()
    {
        try
        {
            if (!string.IsNullOrEmpty(_userPoolId))
            {
                await CognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest
                {
                    UserPoolId = _userPoolId
                });
                WriteOutput($"Deleted user pool: {_userPoolId}");
            }
        }
        catch (Exception ex)
        {
            WriteOutput($"Warning: Failed to cleanup user pool {_userPoolId}: {ex.Message}");
        }
        
        await base.CleanupTestResourcesAsync();
    }

    [Fact]
    public async Task CreateUserPool_WithValidConfiguration_ShouldSucceed()
    {
        // Arrange
        var poolName = GenerateTestResourceName("test-pool");
        
        // Act
        var createRequest = new CreateUserPoolRequest
        {
            PoolName = poolName,
            Policies = new UserPoolPolicyType
            {
                PasswordPolicy = new PasswordPolicyType
                {
                    MinimumLength = 8,
                    RequireUppercase = true,
                    RequireLowercase = true,
                    RequireNumbers = true,
                    RequireSymbols = false
                }
            },
            UsernameAttributes = new List<string> { "email" },
            AutoVerifiedAttributes = new List<string> { "email" }
        };
        
        var response = await CognitoClient.CreateUserPoolAsync(createRequest);
        
        // Assert
        response.UserPool.ShouldNotBeNull();
        response.UserPool.Name.ShouldBe(poolName);
        response.UserPool.Id.ShouldNotBeNullOrEmpty();
        
        // Cleanup
        await CognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest
        {
            UserPoolId = response.UserPool.Id
        });
        
        WriteOutput($"Successfully created and deleted user pool: {poolName}");
    }

    [Fact]
    public async Task CreateUser_InUserPool_ShouldSucceed()
    {
        // Arrange
        _userPoolId.ShouldNotBeNullOrEmpty();
        var (email, password, attributes) = TestDataBuilder.CreateTestUser();
        
        // Act
        var createUserRequest = new AdminCreateUserRequest
        {
            UserPoolId = _userPoolId,
            Username = email,
            MessageAction = MessageActionType.SUPPRESS,
            TemporaryPassword = password,
            UserAttributes = attributes.Select(attr => new AttributeType
            {
                Name = attr.Key,
                Value = attr.Value
            }).ToList()
        };
        
        var createResponse = await CognitoClient.AdminCreateUserAsync(createUserRequest);
        
        // Set permanent password
        await CognitoClient.AdminSetUserPasswordAsync(new AdminSetUserPasswordRequest
        {
            UserPoolId = _userPoolId,
            Username = createResponse.User.Username, // Use the generated username
            Password = password,
            Permanent = true
        });
        
        // Assert
        createResponse.User.ShouldNotBeNull();
        createResponse.User.Username.ShouldNotBeNullOrEmpty(); // Username will be a UUID when email is used as username attribute
        createResponse.User.UserStatus.ShouldBe(UserStatusType.FORCE_CHANGE_PASSWORD);
        
        // Verify user can be retrieved using the generated username
        var getUserResponse = await CognitoClient.AdminGetUserAsync(new AdminGetUserRequest
        {
            UserPoolId = _userPoolId,
            Username = createResponse.User.Username // Use the actual generated username
        });
        
        getUserResponse.Username.ShouldBe(createResponse.User.Username);
        getUserResponse.UserAttributes.ShouldNotBeEmpty();
        
        // Verify email is in the user attributes
        var emailAttribute = getUserResponse.UserAttributes.FirstOrDefault(attr => attr.Name == "email");
        emailAttribute.ShouldNotBeNull();
        emailAttribute.Value.ShouldBe(email);
        
        WriteOutput($"Successfully created user: {email}");
    }

    [Fact]
    public async Task AuthenticateUser_WithValidCredentials_ShouldReturnTokens()
    {
        // Arrange
        _userPoolId.ShouldNotBeNullOrEmpty();
        _clientId.ShouldNotBeNullOrEmpty();
        
        var (email, password, attributes) = TestDataBuilder.CreateTestUser();
        
        // Create user
        var createResponse = await CognitoClient.AdminCreateUserAsync(new AdminCreateUserRequest
        {
            UserPoolId = _userPoolId,
            Username = email,
            MessageAction = MessageActionType.SUPPRESS,
            TemporaryPassword = password,
            UserAttributes = attributes.Select(attr => new AttributeType
            {
                Name = attr.Key,
                Value = attr.Value
            }).ToList()
        });
        
        // Set permanent password
        await CognitoClient.AdminSetUserPasswordAsync(new AdminSetUserPasswordRequest
        {
            UserPoolId = _userPoolId,
            Username = createResponse.User.Username, // Use generated username
            Password = password,
            Permanent = true
        });
        
        // Act
        var authRequest = new AdminInitiateAuthRequest
        {
            UserPoolId = _userPoolId,
            ClientId = _clientId,
            AuthFlow = AuthFlowType.ADMIN_USER_PASSWORD_AUTH,
            AuthParameters = new Dictionary<string, string>
            {
                ["USERNAME"] = createResponse.User.Username, // Use generated username
                ["PASSWORD"] = password
            }
        };
        
        var authResponse = await CognitoClient.AdminInitiateAuthAsync(authRequest);
        
        // Assert
        authResponse.AuthenticationResult.ShouldNotBeNull();
        authResponse.AuthenticationResult.AccessToken.ShouldNotBeNullOrEmpty();
        authResponse.AuthenticationResult.IdToken.ShouldNotBeNullOrEmpty();
        authResponse.AuthenticationResult.RefreshToken.ShouldNotBeNullOrEmpty();
        authResponse.AuthenticationResult.TokenType.ShouldBe("Bearer");
        authResponse.AuthenticationResult.ExpiresIn.ShouldNotBeNull();
        authResponse.AuthenticationResult.ExpiresIn.Value.ShouldBeGreaterThan(0);
        
        WriteOutput($"Successfully authenticated user: {email}");
    }

    [Fact]
    public async Task RefreshToken_WithValidRefreshToken_ShouldReturnNewTokens()
    {
        // Arrange
        _userPoolId.ShouldNotBeNullOrEmpty();
        _clientId.ShouldNotBeNullOrEmpty();
        
        var (email, password, attributes) = TestDataBuilder.CreateTestUser();
        
        // Create and authenticate user
        var username = await CreateAndAuthenticateUserAsync(email, password, attributes);
        
        var authResponse = await CognitoClient.AdminInitiateAuthAsync(new AdminInitiateAuthRequest
        {
            UserPoolId = _userPoolId,
            ClientId = _clientId,
            AuthFlow = AuthFlowType.ADMIN_USER_PASSWORD_AUTH,
            AuthParameters = new Dictionary<string, string>
            {
                ["USERNAME"] = username, // Use generated username
                ["PASSWORD"] = password
            }
        });
        
        var refreshToken = authResponse.AuthenticationResult.RefreshToken;
        
        // Act
        var refreshRequest = new AdminInitiateAuthRequest
        {
            UserPoolId = _userPoolId,
            ClientId = _clientId,
            AuthFlow = AuthFlowType.REFRESH_TOKEN,
            AuthParameters = new Dictionary<string, string>
            {
                ["REFRESH_TOKEN"] = refreshToken
            }
        };
        
        var refreshResponse = await CognitoClient.AdminInitiateAuthAsync(refreshRequest);
        
        // Assert
        refreshResponse.AuthenticationResult.ShouldNotBeNull();
        refreshResponse.AuthenticationResult.AccessToken.ShouldNotBeNullOrEmpty();
        refreshResponse.AuthenticationResult.IdToken.ShouldNotBeNullOrEmpty();
        refreshResponse.AuthenticationResult.AccessToken.ShouldNotBe(authResponse.AuthenticationResult.AccessToken);
        
        WriteOutput($"Successfully refreshed tokens for user: {email}");
    }

    [Fact]
    public async Task ListUsers_InUserPool_ShouldReturnCreatedUsers()
    {
        // Arrange
        _userPoolId.ShouldNotBeNullOrEmpty();
        
        var userCount = 3;
        var createdUsernames = new List<string>();
        var createdEmails = new List<string>();
        
        for (int i = 0; i < userCount; i++)
        {
            var (email, password, attributes) = TestDataBuilder.CreateTestUser();
            var username = await CreateAndAuthenticateUserAsync(email, password, attributes);
            createdUsernames.Add(username);
            createdEmails.Add(email);
        }
        
        // Act
        var listResponse = await CognitoClient.ListUsersAsync(new ListUsersRequest
        {
            UserPoolId = _userPoolId
        });
        
        // Assert
        listResponse.Users.ShouldNotBeEmpty();
        listResponse.Users.Count.ShouldBeGreaterThanOrEqualTo(userCount);
        
        // Check that all created users are in the list (by username)
        foreach (var username in createdUsernames)
        {
            listResponse.Users.ShouldContain(u => u.Username == username);
        }
        
        // Also verify emails are in the user attributes
        foreach (var email in createdEmails)
        {
            listResponse.Users.ShouldContain(u => u.Attributes.Any(attr => attr.Name == "email" && attr.Value == email));
        }
        
        WriteOutput($"Successfully listed {listResponse.Users.Count} users in user pool");
    }

    private async Task CreateTestUserPoolAsync(string poolName)
    {
        var createRequest = new CreateUserPoolRequest
        {
            PoolName = poolName,
            Policies = new UserPoolPolicyType
            {
                PasswordPolicy = new PasswordPolicyType
                {
                    MinimumLength = 8,
                    RequireUppercase = true,
                    RequireLowercase = true,
                    RequireNumbers = true,
                    RequireSymbols = false
                }
            },
            UsernameAttributes = new List<string> { "email" },
            AutoVerifiedAttributes = new List<string> { "email" }
        };
        
        var poolResponse = await CognitoClient.CreateUserPoolAsync(createRequest);
        _userPoolId = poolResponse.UserPool.Id;
        
        // Create user pool client
        var clientRequest = new CreateUserPoolClientRequest
        {
            UserPoolId = _userPoolId,
            ClientName = $"{poolName}-client",
            ExplicitAuthFlows = new List<string>
            {
                "ALLOW_ADMIN_USER_PASSWORD_AUTH",
                "ALLOW_REFRESH_TOKEN_AUTH"
            },
            GenerateSecret = false
        };
        
        var clientResponse = await CognitoClient.CreateUserPoolClientAsync(clientRequest);
        _clientId = clientResponse.UserPoolClient.ClientId;
    }

    private async Task<string> CreateAndAuthenticateUserAsync(string email, string password, Dictionary<string, string> attributes)
    {
        var createResponse = await CognitoClient.AdminCreateUserAsync(new AdminCreateUserRequest
        {
            UserPoolId = _userPoolId,
            Username = email,
            MessageAction = MessageActionType.SUPPRESS,
            TemporaryPassword = password,
            UserAttributes = attributes.Select(attr => new AttributeType
            {
                Name = attr.Key,
                Value = attr.Value
            }).ToList()
        });
        
        await CognitoClient.AdminSetUserPasswordAsync(new AdminSetUserPasswordRequest
        {
            UserPoolId = _userPoolId,
            Username = createResponse.User.Username, // Use generated username
            Password = password,
            Permanent = true
        });
        
        return createResponse.User.Username; // Return the generated username
    }
}