using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ThirdOpinion.Common.Cognito;

namespace ThirdOpinion.Common.Aws.Tests.Cognito;

public class AuthorizePersonGuidAttributeTests
{
    private readonly Mock<ILogger<AuthorizeTenantGuidPersonAttribute>> _loggerMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<HttpContext> _httpContextMock;
    private readonly Mock<IOptions<GlobalAppSettingsOptions>> _optionsMock;
    private readonly GlobalAppSettingsOptions _globalSettings;
    private readonly AuthorizeTenantGuidPersonAttribute _attribute;

    public AuthorizePersonGuidAttributeTests()
    {
        _loggerMock = new Mock<ILogger<AuthorizeTenantGuidPersonAttribute>>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _httpContextMock = new Mock<HttpContext>();
        _optionsMock = new Mock<IOptions<GlobalAppSettingsOptions>>();
        
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
        _attribute = new AuthorizeTenantGuidPersonAttribute();
    }

    [Fact]
    public void OnActionExecuting_ValidTenantGuidAndMatchingGroups_AllowsAccess()
    {
        // Arrange
        var tenantGuid = "550e8400-e29b-41d4-a716-446655440000";
        var context = CreateActionExecutingContext(tenantGuid, new List<string> { "Admin", "TestGroup" }, true);

        // Act
        _attribute.OnActionExecuting(context);

        // Assert
        context.Result.ShouldBeNull();
    }

    [Fact]
    public void OnActionExecuting_ValidTenantGuidButNoMatchingGroups_ReturnsForbitResult()
    {
        // Arrange
        var tenantGuid = "550e8400-e29b-41d4-a716-446655440000";
        var context = CreateActionExecutingContext(tenantGuid, new List<string> { "WrongGroup" }, true);

        // Act
        _attribute.OnActionExecuting(context);

        // Assert
        context.Result.ShouldBeOfType<ForbidResult>();
    }

    [Fact]
    public void OnActionExecuting_MissingTenantGuid_ReturnsBadRequest()
    {
        // Arrange
        var context = CreateActionExecutingContext(null, new List<string> { "Admin" }, true);

        // Act
        _attribute.OnActionExecuting(context);

        // Assert
        context.Result.ShouldBeOfType<BadRequestResult>();
    }

    [Fact]
    public void OnActionExecuting_InvalidTenantGuidFormat_ReturnsBadRequest()
    {
        // Arrange
        var context = CreateActionExecutingContext("invalid-guid", new List<string> { "Admin" }, true);

        // Act
        _attribute.OnActionExecuting(context);

        // Assert
        context.Result.ShouldBeOfType<BadRequestResult>();
    }

    [Fact]
    public void OnActionExecuting_UnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        var tenantGuid = "550e8400-e29b-41d4-a716-446655440000";
        var context = CreateActionExecutingContext(tenantGuid, new List<string> { "Admin" }, false);

        // Act
        _attribute.OnActionExecuting(context);

        // Assert
        context.Result.ShouldBeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void OnActionExecuting_UserHasNoGroups_ReturnsForbitResult()
    {
        // Arrange
        var tenantGuid = "550e8400-e29b-41d4-a716-446655440000";
        var context = CreateActionExecutingContext(tenantGuid, null, true);

        // Act
        _attribute.OnActionExecuting(context);

        // Assert
        context.Result.ShouldBeOfType<ForbidResult>();
    }

    [Fact]
    public void OnActionExecuting_TenantGuidNotInConfig_ReturnsForbitResult()
    {
        // Arrange
        var tenantGuid = "550e8400-e29b-41d4-a716-446655440999";
        var context = CreateActionExecutingContext(tenantGuid, new List<string> { "Admin" }, true);

        // Act
        _attribute.OnActionExecuting(context);

        // Assert
        context.Result.ShouldBeOfType<ForbidResult>();
    }

    [Fact]
    public void OnActionExecuting_ExceptionThrown_ReturnsStatusCode500()
    {
        // Arrange
        var tenantGuid = "550e8400-e29b-41d4-a716-446655440000";
        var context = CreateActionExecutingContext(tenantGuid, new List<string> { "Admin" }, true);
        
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IOptions<GlobalAppSettingsOptions>)))
            .Throws(new Exception("Test exception"));

        // Act
        _attribute.OnActionExecuting(context);

        // Assert
        var statusCodeResult = context.Result.ShouldBeOfType<StatusCodeResult>();
        statusCodeResult.StatusCode.ShouldBe(500);
    }

    private ActionExecutingContext CreateActionExecutingContext(string? tenantGuid, List<string>? userGroups, bool isAuthenticated)
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
        _httpContextMock.Setup(h => h.RequestServices).Returns(_serviceProviderMock.Object);

        _serviceProviderMock.Setup(sp => sp.GetService(typeof(ILogger<AuthorizeTenantGuidPersonAttribute>)))
            .Returns(_loggerMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IOptions<GlobalAppSettingsOptions>)))
            .Returns(_optionsMock.Object);

        var routeData = new RouteData();
        if (tenantGuid != null)
        {
            routeData.Values.Add("tenantGuid", tenantGuid);
        }

        var actionContext = new ActionContext
        {
            HttpContext = _httpContextMock.Object,
            RouteData = routeData,
            ActionDescriptor = new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()
        };

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());
    }
}