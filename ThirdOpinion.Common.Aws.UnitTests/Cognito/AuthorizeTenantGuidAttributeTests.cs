using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using ThirdOpinion.Common.Cognito;

namespace ThirdOpinion.Common.Aws.Tests.Cognito;

public class AuthorizeTenantGuidAttributeTests
{
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly Mock<IOptions<GlobalAppSettingsOptions>> _optionsMock;
    private readonly Mock<HttpContext> _httpContextMock;
    private readonly GlobalAppSettingsOptions _globalSettings;
    private readonly AuthorizeTenantGuidAttribute _attribute;

    public AuthorizeTenantGuidAttributeTests()
    {
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _optionsMock = new Mock<IOptions<GlobalAppSettingsOptions>>();
        _httpContextMock = new Mock<HttpContext>();
        
        _globalSettings = new GlobalAppSettingsOptions
        {
            Tenants = new GlobalAppSettingsOptions.TenantOptions
            {
                TenantGroups = new Dictionary<string, List<string>>
                {
                    { "550e8400-e29b-41d4-a716-446655440000", new List<string> { "Admin", "User" } },
                    { "550e8400-e29b-41d4-a716-446655440001", new List<string> { "Manager" } }
                }
            }
        };
        
        _optionsMock.Setup(o => o.Value).Returns(_globalSettings);
        _attribute = new AuthorizeTenantGuidAttribute();
    }

    [Fact]
    public void IsValid_ValidTenantGuidAndMatchingGroups_ReturnsSuccess()
    {
        // Arrange
        var tenantGuid = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
        var validationContext = CreateValidationContext(new List<string> { "Admin", "TestGroup" }, true);

        // Act
        var result = _attribute.GetValidationResult(tenantGuid, validationContext);

        // Assert
        result.ShouldBe(ValidationResult.Success);
    }

    [Fact]
    public void IsValid_ValidTenantGuidButNoMatchingGroups_ReturnsValidationError()
    {
        // Arrange
        var tenantGuid = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
        var validationContext = CreateValidationContext(new List<string> { "WrongGroup" }, true);

        // Act
        var result = _attribute.GetValidationResult(tenantGuid, validationContext);

        // Assert
        result.ShouldNotBe(ValidationResult.Success);
        result!.ErrorMessage.ShouldBe("TenantGuid does not match the authenticated user.");
    }

    [Fact]
    public void IsValid_UnauthenticatedUser_ReturnsValidationError()
    {
        // Arrange
        var tenantGuid = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
        var validationContext = CreateValidationContext(new List<string> { "Admin" }, false);

        // Act
        var result = _attribute.GetValidationResult(tenantGuid, validationContext);

        // Assert
        result.ShouldNotBe(ValidationResult.Success);
        result!.ErrorMessage.ShouldBe("User is not authenticated");
    }

    [Fact]
    public void IsValid_UserHasNoGroups_ReturnsValidationError()
    {
        // Arrange
        var tenantGuid = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
        var validationContext = CreateValidationContext(null, true);

        // Act
        var result = _attribute.GetValidationResult(tenantGuid, validationContext);

        // Assert
        result.ShouldNotBe(ValidationResult.Success);
        result!.ErrorMessage.ShouldBe("User does not have a groups claim");
    }

    [Fact]
    public void IsValid_TenantGuidNotInConfig_ReturnsValidationError()
    {
        // Arrange
        var tenantGuid = Guid.Parse("550e8400-e29b-41d4-a716-446655440999");
        var validationContext = CreateValidationContext(new List<string> { "Admin" }, true);

        // Act
        var result = _attribute.GetValidationResult(tenantGuid, validationContext);

        // Assert
        result.ShouldNotBe(ValidationResult.Success);
        result!.ErrorMessage.ShouldContain("not found in configuration");
    }

    [Fact]
    public void IsValid_EmptyTenantGroups_ReturnsValidationError()
    {
        // Arrange
        _globalSettings.Tenants.TenantGroups["550e8400-e29b-41d4-a716-446655440002"] = new List<string>();
        var tenantGuid = Guid.Parse("550e8400-e29b-41d4-a716-446655440002");
        var validationContext = CreateValidationContext(new List<string> { "Admin" }, true);

        // Act
        var result = _attribute.GetValidationResult(tenantGuid, validationContext);

        // Assert
        result.ShouldNotBe(ValidationResult.Success);
        result!.ErrorMessage.ShouldBe("TenantGuid does not match the authenticated user.");
    }

    [Fact]
    public void IsValid_MultiTenantScenario_ValidatesCorrectly()
    {
        // Arrange
        var tenantGuid = Guid.Parse("550e8400-e29b-41d4-a716-446655440001");
        var validationContext = CreateValidationContext(new List<string> { "Manager", "User" }, true);

        // Act
        var result = _attribute.GetValidationResult(tenantGuid, validationContext);

        // Assert
        result.ShouldBe(ValidationResult.Success);
    }

    private ValidationContext CreateValidationContext(List<string>? userGroups, bool isAuthenticated)
    {
        var claims = new List<Claim>();
        if (userGroups != null)
        {
            claims.Add(new Claim("cognito:groups", string.Join(",", userGroups)));
        }

        var claimsIdentity = isAuthenticated 
            ? new ClaimsIdentity(claims, "TestAuthType") 
            : new ClaimsIdentity(claims);
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
        
        _httpContextMock.Setup(h => h.User).Returns(claimsPrincipal);
        _httpContextAccessorMock.Setup(h => h.HttpContext).Returns(_httpContextMock.Object);

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(IHttpContextAccessor)))
            .Returns(_httpContextAccessorMock.Object);
        serviceProvider.Setup(sp => sp.GetService(typeof(IOptions<GlobalAppSettingsOptions>)))
            .Returns(_optionsMock.Object);

        return new ValidationContext(new object(), serviceProvider.Object, new Dictionary<object, object?>());
    }
}