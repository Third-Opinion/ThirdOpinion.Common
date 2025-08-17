using System.Security.Claims;
using ThirdOpinion.Common.Cognito;

namespace ThirdOpinion.Common.Aws.Tests.Cognito;

public class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetUserId_WithNameIdentifierClaim_ReturnsCorrectValue()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "user123")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = principal.GetUserId();

        // Assert
        result.ShouldBe("user123");
    }

    [Fact]
    public void GetUserId_WithSubClaim_ReturnsCorrectValue()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new("sub", "sub456")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = principal.GetUserId();

        // Assert
        result.ShouldBe("sub456");
    }

    [Fact]
    public void GetUserId_WithBothClaims_PrefersNameIdentifier()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "user123"),
            new("sub", "sub456")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = principal.GetUserId();

        // Assert
        result.ShouldBe("user123");
    }

    [Fact]
    public void GetUserId_WithNoClaims_ReturnsEmptyString()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = principal.GetUserId();

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void GetUsername_WithCognitoUsernameClaim_ReturnsCorrectValue()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new("cognito:username", "johndoe")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = principal.GetUsername();

        // Assert
        result.ShouldBe("johndoe");
    }

    [Fact]
    public void GetUsername_WithNameClaim_ReturnsCorrectValue()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "john.doe")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = principal.GetUsername();

        // Assert
        result.ShouldBe("john.doe");
    }

    [Fact]
    public void GetEmail_WithEmailClaim_ReturnsCorrectValue()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, "john.doe@example.com")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = principal.GetEmail();

        // Assert
        result.ShouldBe("john.doe@example.com");
    }

    [Fact]
    public void GetEmail_WithAlternateEmailClaim_ReturnsCorrectValue()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new("email", "john.doe@test.com")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = principal.GetEmail();

        // Assert
        result.ShouldBe("john.doe@test.com");
    }

    [Fact]
    public void GetFirstName_WithGivenNameClaim_ReturnsCorrectValue()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.GivenName, "John")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = principal.GetFirstName();

        // Assert
        result.ShouldBe("John");
    }

    [Fact]
    public void GetLastName_WithSurnameClaim_ReturnsCorrectValue()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.Surname, "Doe")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = principal.GetLastName();

        // Assert
        result.ShouldBe("Doe");
    }

    [Fact]
    public void GetGroups_WithCognitoGroupsClaim_ReturnsCorrectList()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new("cognito:groups", "Admin,User,Manager")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = principal.GetGroups();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        result.ShouldContain("Admin");
        result.ShouldContain("User");
        result.ShouldContain("Manager");
    }

    [Fact]
    public void GetGroups_WithGroupsClaimAndSpaces_TrimsSpaces()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new("groups", "Admin , User , Manager ")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = principal.GetGroups();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        result.ShouldContain("Admin");
        result.ShouldContain("User");
        result.ShouldContain("Manager");
    }

    [Fact]
    public void GetGroups_WithNoGroupsClaim_ReturnsNull()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = principal.GetGroups();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetGroups_WithEmptyGroupsClaim_ReturnsNull()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new("cognito:groups", "")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = principal.GetGroups();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetTenantGuid_WithValidCustomTenantGuid_ReturnsCorrectGuid()
    {
        // Arrange
        var expectedGuid = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new("custom:tenantGuid", expectedGuid.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = principal.GetTenantGuid();

        // Assert
        result.ShouldBe(expectedGuid);
    }

    [Fact]
    public void GetTenantGuid_WithAlternateTenantGuidClaim_ReturnsCorrectGuid()
    {
        // Arrange
        var expectedGuid = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new("tenantGuid", expectedGuid.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = principal.GetTenantGuid();

        // Assert
        result.ShouldBe(expectedGuid);
    }

    [Fact]
    public void GetTenantGuid_WithInvalidGuid_ReturnsNull()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new("custom:tenantGuid", "invalid-guid")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = principal.GetTenantGuid();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetTenantGuid_WithNoTenantGuidClaim_ReturnsNull()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = principal.GetTenantGuid();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetTenantName_WithCustomTenantNameClaim_ReturnsCorrectValue()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new("custom:tenantName", "Acme Corp")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = principal.GetTenantName();

        // Assert
        result.ShouldBe("Acme Corp");
    }

    [Fact]
    public void GetTenantName_WithAlternateTenantNameClaim_ReturnsCorrectValue()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new("tenantName", "Test Company")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = principal.GetTenantName();

        // Assert
        result.ShouldBe("Test Company");
    }

    [Fact]
    public void IsInGroup_WithMatchingGroup_ReturnsTrue()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new("cognito:groups", "Admin")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = principal.IsInGroup("Admin");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsInGroup_WithNonMatchingGroup_ReturnsFalse()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new("cognito:groups", "User")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = principal.IsInGroup("Admin");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsInGroup_WithNoGroupsClaim_ReturnsFalse()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = principal.IsInGroup("Admin");

        // Assert
        result.ShouldBeFalse();
    }
}