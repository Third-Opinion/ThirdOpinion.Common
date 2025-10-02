# Cognito Authentication Patterns and Best Practices

## Overview

The ThirdOpinion.Common Cognito service provides comprehensive authentication and authorization patterns for AWS Cognito, including user management, multi-factor authentication (MFA), OAuth flows, and custom authentication challenges.

## Core Features

- User registration and verification
- Multi-factor authentication (MFA)
- Password management and recovery
- OAuth 2.0 and OpenID Connect flows
- Custom authentication challenges
- User pool and identity pool integration
- Token management and refresh
- User attribute management

## Authentication Patterns

### 1. User Registration and Verification

```csharp
public class UserRegistrationService
{
    private readonly ICognitoService _cognito;
    private readonly IEmailService _emailService;
    private readonly string _clientId;
    private readonly string _userPoolId;
    
    public async Task<RegistrationResult> RegisterUserAsync(
        string email,
        string password,
        Dictionary<string, string> userAttributes = null)
    {
        try
        {
            // Prepare user attributes
            var attributes = new List<AttributeType>
            {
                new AttributeType { Name = "email", Value = email },
                new AttributeType { Name = "email_verified", Value = "false" }
            };
            
            if (userAttributes != null)
            {
                foreach (var attr in userAttributes)
                {
                    attributes.Add(new AttributeType { Name = attr.Key, Value = attr.Value });
                }
            }
            
            // Sign up user
            var signUpRequest = new SignUpRequest
            {
                ClientId = _clientId,
                Username = email,
                Password = password,
                UserAttributes = attributes
            };
            
            var response = await _cognito.SignUpAsync(signUpRequest);
            
            // Handle auto-verification if configured
            if (!response.UserConfirmed)
            {
                // Send custom verification email if needed
                await _emailService.SendVerificationEmailAsync(
                    email,
                    response.CodeDeliveryDetails);
            }
            
            return new RegistrationResult
            {
                Success = true,
                UserId = response.UserSub,
                RequiresVerification = !response.UserConfirmed,
                DeliveryMedium = response.CodeDeliveryDetails?.DeliveryMedium
            };
        }
        catch (UsernameExistsException)
        {
            return new RegistrationResult
            {
                Success = false,
                Error = "Email already registered"
            };
        }
        catch (InvalidPasswordException ex)
        {
            return new RegistrationResult
            {
                Success = false,
                Error = $"Password requirements not met: {ex.Message}"
            };
        }
    }
    
    public async Task<bool> VerifyEmailAsync(string email, string verificationCode)
    {
        var request = new ConfirmSignUpRequest
        {
            ClientId = _clientId,
            Username = email,
            ConfirmationCode = verificationCode
        };
        
        try
        {
            await _cognito.ConfirmSignUpAsync(request);
            
            // Update user attributes
            await _cognito.AdminUpdateUserAttributesAsync(new AdminUpdateUserAttributesRequest
            {
                UserPoolId = _userPoolId,
                Username = email,
                UserAttributes = new List<AttributeType>
                {
                    new AttributeType { Name = "email_verified", Value = "true" }
                }
            });
            
            return true;
        }
        catch (CodeMismatchException)
        {
            return false;
        }
    }
    
    public async Task ResendVerificationCodeAsync(string email)
    {
        var request = new ResendConfirmationCodeRequest
        {
            ClientId = _clientId,
            Username = email
        };
        
        await _cognito.ResendConfirmationCodeAsync(request);
    }
}
```

### 2. Secure Login with MFA

```csharp
public class SecureAuthenticationService
{
    private readonly ICognitoService _cognito;
    private readonly string _clientId;
    
    public async Task<AuthenticationResult> AuthenticateAsync(
        string username,
        string password,
        string deviceKey = null)
    {
        var authParameters = new Dictionary<string, string>
        {
            ["USERNAME"] = username,
            ["PASSWORD"] = password
        };
        
        if (!string.IsNullOrEmpty(deviceKey))
        {
            authParameters["DEVICE_KEY"] = deviceKey;
        }
        
        var request = new InitiateAuthRequest
        {
            ClientId = _clientId,
            AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
            AuthParameters = authParameters
        };
        
        var response = await _cognito.InitiateAuthAsync(request);
        
        // Handle different challenge types
        if (response.ChallengeName == ChallengeNameType.SMS_MFA ||
            response.ChallengeName == ChallengeNameType.SOFTWARE_TOKEN_MFA)
        {
            return new AuthenticationResult
            {
                RequiresMfa = true,
                Session = response.Session,
                ChallengeType = response.ChallengeName,
                ChallengeParameters = response.ChallengeParameters
            };
        }
        
        if (response.ChallengeName == ChallengeNameType.NEW_PASSWORD_REQUIRED)
        {
            return new AuthenticationResult
            {
                RequiresPasswordChange = true,
                Session = response.Session
            };
        }
        
        return new AuthenticationResult
        {
            Success = true,
            AccessToken = response.AuthenticationResult.AccessToken,
            IdToken = response.AuthenticationResult.IdToken,
            RefreshToken = response.AuthenticationResult.RefreshToken,
            ExpiresIn = response.AuthenticationResult.ExpiresIn
        };
    }
    
    public async Task<AuthenticationResult> CompleteMfaChallengeAsync(
        string session,
        string mfaCode,
        ChallengeNameType challengeType)
    {
        var request = new RespondToAuthChallengeRequest
        {
            ClientId = _clientId,
            ChallengeName = challengeType,
            Session = session,
            ChallengeResponses = new Dictionary<string, string>
            {
                [challengeType == ChallengeNameType.SMS_MFA ? "SMS_MFA_CODE" : "SOFTWARE_TOKEN_MFA_CODE"] = mfaCode
            }
        };
        
        var response = await _cognito.RespondToAuthChallengeAsync(request);
        
        return new AuthenticationResult
        {
            Success = true,
            AccessToken = response.AuthenticationResult.AccessToken,
            IdToken = response.AuthenticationResult.IdToken,
            RefreshToken = response.AuthenticationResult.RefreshToken,
            ExpiresIn = response.AuthenticationResult.ExpiresIn
        };
    }
    
    public async Task<SetupMfaResult> SetupTotpMfaAsync(string accessToken)
    {
        // Associate software token
        var associateRequest = new AssociateSoftwareTokenRequest
        {
            AccessToken = accessToken
        };
        
        var associateResponse = await _cognito.AssociateSoftwareTokenAsync(associateRequest);
        
        // Generate QR code URL
        var userName = GetUserNameFromToken(accessToken);
        var qrCodeUrl = GenerateTotpQrCode(
            associateResponse.SecretCode,
            userName,
            "ThirdOpinion");
        
        return new SetupMfaResult
        {
            SecretCode = associateResponse.SecretCode,
            QrCodeUrl = qrCodeUrl,
            Session = associateResponse.Session
        };
    }
}
```

### 3. OAuth and Social Login

```csharp
public class OAuthService
{
    private readonly ICognitoService _cognito;
    private readonly string _userPoolDomain;
    private readonly string _clientId;
    private readonly string _redirectUri;
    
    public string GetAuthorizationUrl(
        string provider, // "Google", "Facebook", "Amazon", etc.
        string state = null,
        string scope = "openid profile email")
    {
        var stateParam = state ?? GenerateSecureState();
        
        return $"https://{_userPoolDomain}/oauth2/authorize?" +
               $"identity_provider={provider}&" +
               $"redirect_uri={Uri.EscapeDataString(_redirectUri)}&" +
               $"response_type=code&" +
               $"client_id={_clientId}&" +
               $"scope={Uri.EscapeDataString(scope)}&" +
               $"state={stateParam}";
    }
    
    public async Task<TokenResponse> ExchangeCodeForTokensAsync(
        string authorizationCode,
        string codeVerifier = null)
    {
        using var client = new HttpClient();
        
        var tokenEndpoint = $"https://{_userPoolDomain}/oauth2/token";
        
        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = _clientId,
            ["code"] = authorizationCode,
            ["redirect_uri"] = _redirectUri
        };
        
        // Add PKCE verifier if using PKCE flow
        if (!string.IsNullOrEmpty(codeVerifier))
        {
            parameters["code_verifier"] = codeVerifier;
        }
        
        var content = new FormUrlEncodedContent(parameters);
        var response = await client.PostAsync(tokenEndpoint, content);
        
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TokenResponse>(json);
        }
        
        throw new AuthenticationException(
            $"Failed to exchange code: {response.StatusCode}");
    }
    
    public async Task<UserInfo> GetUserInfoAsync(string accessToken)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", accessToken);
        
        var userInfoEndpoint = $"https://{_userPoolDomain}/oauth2/userInfo";
        var response = await client.GetAsync(userInfoEndpoint);
        
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<UserInfo>(json);
        }
        
        throw new AuthenticationException(
            $"Failed to get user info: {response.StatusCode}");
    }
}
```

### 4. Token Management

```csharp
public class TokenManager
{
    private readonly ICognitoService _cognito;
    private readonly IMemoryCache _cache;
    private readonly string _clientId;
    
    public async Task<string> GetValidAccessTokenAsync(string refreshToken)
    {
        var cacheKey = $"token:{GetRefreshTokenHash(refreshToken)}";
        
        if (_cache.TryGetValue<TokenSet>(cacheKey, out var cached))
        {
            if (cached.ExpiresAt > DateTime.UtcNow.AddMinutes(5))
            {
                return cached.AccessToken;
            }
        }
        
        // Refresh the token
        var request = new InitiateAuthRequest
        {
            ClientId = _clientId,
            AuthFlow = AuthFlowType.REFRESH_TOKEN_AUTH,
            AuthParameters = new Dictionary<string, string>
            {
                ["REFRESH_TOKEN"] = refreshToken
            }
        };
        
        var response = await _cognito.InitiateAuthAsync(request);
        
        var tokenSet = new TokenSet
        {
            AccessToken = response.AuthenticationResult.AccessToken,
            IdToken = response.AuthenticationResult.IdToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(response.AuthenticationResult.ExpiresIn)
        };
        
        _cache.Set(cacheKey, tokenSet, TimeSpan.FromSeconds(
            response.AuthenticationResult.ExpiresIn - 300)); // Cache for 5 min less than expiry
        
        return tokenSet.AccessToken;
    }
    
    public async Task RevokeTokenAsync(string refreshToken)
    {
        var request = new RevokeTokenRequest
        {
            ClientId = _clientId,
            Token = refreshToken
        };
        
        await _cognito.RevokeTokenAsync(request);
        
        // Clear from cache
        var cacheKey = $"token:{GetRefreshTokenHash(refreshToken)}";
        _cache.Remove(cacheKey);
    }
    
    public ClaimsPrincipal ValidateIdToken(string idToken)
    {
        // Get JWKS from Cognito
        var jwksUrl = $"https://cognito-idp.{_region}.amazonaws.com/{_userPoolId}/.well-known/jwks.json";
        var jwks = GetJwks(jwksUrl);
        
        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = jwks,
            ValidateIssuer = true,
            ValidIssuer = $"https://cognito-idp.{_region}.amazonaws.com/{_userPoolId}",
            ValidateAudience = true,
            ValidAudience = _clientId,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
        
        var principal = tokenHandler.ValidateToken(
            idToken,
            validationParameters,
            out var validatedToken);
        
        return principal;
    }
}
```

### 5. Password Management

```csharp
public class PasswordManagementService
{
    private readonly ICognitoService _cognito;
    private readonly string _clientId;
    
    public async Task<bool> ChangePasswordAsync(
        string accessToken,
        string oldPassword,
        string newPassword)
    {
        var request = new ChangePasswordRequest
        {
            AccessToken = accessToken,
            PreviousPassword = oldPassword,
            ProposedPassword = newPassword
        };
        
        try
        {
            await _cognito.ChangePasswordAsync(request);
            return true;
        }
        catch (InvalidPasswordException ex)
        {
            throw new ValidationException(
                $"New password does not meet requirements: {ex.Message}");
        }
    }
    
    public async Task<string> InitiateForgotPasswordAsync(string username)
    {
        var request = new ForgotPasswordRequest
        {
            ClientId = _clientId,
            Username = username
        };
        
        var response = await _cognito.ForgotPasswordAsync(request);
        
        return response.CodeDeliveryDetails?.DeliveryMedium;
    }
    
    public async Task<bool> ConfirmForgotPasswordAsync(
        string username,
        string confirmationCode,
        string newPassword)
    {
        var request = new ConfirmForgotPasswordRequest
        {
            ClientId = _clientId,
            Username = username,
            ConfirmationCode = confirmationCode,
            Password = newPassword
        };
        
        try
        {
            await _cognito.ConfirmForgotPasswordAsync(request);
            return true;
        }
        catch (CodeMismatchException)
        {
            return false;
        }
    }
}
```

### 6. Custom Authentication Flow

```csharp
public class CustomAuthenticationFlow
{
    private readonly ICognitoService _cognito;
    private readonly string _clientId;
    
    public async Task<AuthChallengeResult> InitiateCustomAuthAsync(
        string username,
        Dictionary<string, string> clientMetadata = null)
    {
        var request = new InitiateAuthRequest
        {
            ClientId = _clientId,
            AuthFlow = AuthFlowType.CUSTOM_AUTH,
            AuthParameters = new Dictionary<string, string>
            {
                ["USERNAME"] = username
            },
            ClientMetadata = clientMetadata
        };
        
        var response = await _cognito.InitiateAuthAsync(request);
        
        return new AuthChallengeResult
        {
            Session = response.Session,
            ChallengeName = response.ChallengeName,
            ChallengeParameters = response.ChallengeParameters
        };
    }
    
    public async Task<AuthenticationResult> RespondToCustomChallengeAsync(
        string session,
        string challengeAnswer)
    {
        var request = new RespondToAuthChallengeRequest
        {
            ClientId = _clientId,
            ChallengeName = ChallengeNameType.CUSTOM_CHALLENGE,
            Session = session,
            ChallengeResponses = new Dictionary<string, string>
            {
                ["ANSWER"] = challengeAnswer
            }
        };
        
        var response = await _cognito.RespondToAuthChallengeAsync(request);
        
        if (response.AuthenticationResult != null)
        {
            return new AuthenticationResult
            {
                Success = true,
                AccessToken = response.AuthenticationResult.AccessToken,
                IdToken = response.AuthenticationResult.IdToken,
                RefreshToken = response.AuthenticationResult.RefreshToken
            };
        }
        
        // More challenges required
        return new AuthChallengeResult
        {
            Session = response.Session,
            ChallengeName = response.ChallengeName,
            ChallengeParameters = response.ChallengeParameters
        };
    }
}
```

## User Management Patterns

### 1. User Profile Management

```csharp
public class UserProfileService
{
    private readonly ICognitoService _cognito;
    private readonly string _userPoolId;
    
    public async Task<UserProfile> GetUserProfileAsync(string username)
    {
        var request = new AdminGetUserRequest
        {
            UserPoolId = _userPoolId,
            Username = username
        };
        
        var response = await _cognito.AdminGetUserAsync(request);
        
        return new UserProfile
        {
            Username = response.Username,
            Email = GetAttributeValue(response.UserAttributes, "email"),
            PhoneNumber = GetAttributeValue(response.UserAttributes, "phone_number"),
            Name = GetAttributeValue(response.UserAttributes, "name"),
            Picture = GetAttributeValue(response.UserAttributes, "picture"),
            CustomAttributes = GetCustomAttributes(response.UserAttributes),
            CreatedAt = response.UserCreateDate,
            UpdatedAt = response.UserLastModifiedDate,
            Status = response.UserStatus,
            MfaOptions = response.MFAOptions
        };
    }
    
    public async Task UpdateUserAttributesAsync(
        string accessToken,
        Dictionary<string, string> attributes)
    {
        var userAttributes = attributes.Select(a => 
            new AttributeType { Name = a.Key, Value = a.Value }).ToList();
        
        var request = new UpdateUserAttributesRequest
        {
            AccessToken = accessToken,
            UserAttributes = userAttributes
        };
        
        await _cognito.UpdateUserAttributesAsync(request);
    }
    
    public async Task DeleteUserAsync(string accessToken)
    {
        var request = new DeleteUserRequest
        {
            AccessToken = accessToken
        };
        
        await _cognito.DeleteUserAsync(request);
    }
}
```

### 2. Group-Based Authorization

```csharp
public class GroupAuthorizationService
{
    private readonly ICognitoService _cognito;
    private readonly string _userPoolId;
    
    public async Task CreateGroupAsync(
        string groupName,
        string description,
        string roleArn = null,
        int precedence = 0)
    {
        var request = new CreateGroupRequest
        {
            UserPoolId = _userPoolId,
            GroupName = groupName,
            Description = description,
            RoleArn = roleArn,
            Precedence = precedence
        };
        
        await _cognito.CreateGroupAsync(request);
    }
    
    public async Task AddUserToGroupAsync(string username, string groupName)
    {
        var request = new AdminAddUserToGroupRequest
        {
            UserPoolId = _userPoolId,
            Username = username,
            GroupName = groupName
        };
        
        await _cognito.AdminAddUserToGroupAsync(request);
    }
    
    public async Task<List<string>> GetUserGroupsAsync(string username)
    {
        var request = new AdminListGroupsForUserRequest
        {
            UserPoolId = _userPoolId,
            Username = username
        };
        
        var response = await _cognito.AdminListGroupsForUserAsync(request);
        
        return response.Groups.Select(g => g.GroupName).ToList();
    }
    
    public bool HasRole(ClaimsPrincipal user, string requiredRole)
    {
        var groups = user.Claims
            .Where(c => c.Type == "cognito:groups")
            .Select(c => c.Value);
        
        return groups.Contains(requiredRole);
    }
}
```

## Security Best Practices

### 1. Rate Limiting

```csharp
public class RateLimitedAuthService
{
    private readonly IMemoryCache _cache;
    private readonly ICognitoService _cognito;
    
    public async Task<AuthenticationResult> AuthenticateWithRateLimitAsync(
        string username,
        string password,
        string clientIp)
    {
        var attemptsKey = $"auth_attempts:{clientIp}:{username}";
        var attempts = _cache.Get<int>(attemptsKey);
        
        if (attempts >= 5)
        {
            throw new TooManyAttemptsException(
                "Too many failed attempts. Please try again later.");
        }
        
        try
        {
            var result = await AuthenticateAsync(username, password);
            
            // Clear attempts on success
            _cache.Remove(attemptsKey);
            
            return result;
        }
        catch (NotAuthorizedException)
        {
            // Increment attempts
            _cache.Set(attemptsKey, attempts + 1, TimeSpan.FromMinutes(15));
            throw;
        }
    }
}
```

### 2. Device Tracking

```csharp
public class DeviceTrackingService
{
    private readonly ICognitoService _cognito;
    
    public async Task<string> RegisterDeviceAsync(
        string accessToken,
        string deviceName,
        Dictionary<string, string> deviceMetadata)
    {
        var deviceKey = GenerateDeviceKey();
        
        var request = new ConfirmDeviceRequest
        {
            AccessToken = accessToken,
            DeviceKey = deviceKey,
            DeviceName = deviceName
        };
        
        await _cognito.ConfirmDeviceAsync(request);
        
        return deviceKey;
    }
    
    public async Task<List<Device>> GetUserDevicesAsync(string username)
    {
        var request = new AdminListDevicesRequest
        {
            UserPoolId = _userPoolId,
            Username = username
        };
        
        var response = await _cognito.AdminListDevicesAsync(request);
        
        return response.Devices.Select(d => new Device
        {
            DeviceKey = d.DeviceKey,
            LastAuthenticated = d.DeviceLastAuthenticatedDate,
            CreatedAt = d.DeviceCreateDate,
            Attributes = d.DeviceAttributes
        }).ToList();
    }
}
```

## Testing Patterns

```csharp
[Fact]
public async Task Authentication_WithValidCredentials_ShouldReturnTokens()
{
    // Arrange
    var username = "test@example.com";
    var password = "Test123!@#";
    
    // Act
    var result = await _authService.AuthenticateAsync(username, password);
    
    // Assert
    Assert.True(result.Success);
    Assert.NotNull(result.AccessToken);
    Assert.NotNull(result.IdToken);
    Assert.NotNull(result.RefreshToken);
}

[Fact]
public async Task MFA_WhenEnabled_ShouldRequireCode()
{
    // Arrange
    var username = "mfa-user@example.com";
    var password = "Test123!@#";
    
    // Act
    var result = await _authService.AuthenticateAsync(username, password);
    
    // Assert
    Assert.True(result.RequiresMfa);
    Assert.NotNull(result.Session);
    Assert.Equal(ChallengeNameType.SOFTWARE_TOKEN_MFA, result.ChallengeType);
}
```

## Common Issues and Solutions

1. **Token Expiration**: Implement automatic token refresh
2. **MFA Setup**: Provide clear user guidance and QR codes
3. **Password Requirements**: Display requirements clearly
4. **Rate Limiting**: Implement client-side and server-side limits
5. **Session Management**: Handle concurrent sessions appropriately

## Related Documentation

- [Getting Started](../getting-started.md)
- [S3 Patterns](s3-patterns.md)
- [DynamoDB Patterns](dynamodb-patterns.md)
- [Troubleshooting](../troubleshooting.md)