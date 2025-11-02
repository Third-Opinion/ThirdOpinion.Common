using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ThirdOpinion.Common.Cognito;

namespace ThirdOpinion.Common.Aws.Tests.Cognito;

public class GlobalAppSettingsOptionsTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Act
        var options = new GlobalAppSettingsOptions();

        // Assert
        options.TablePrefix.ShouldBe(string.Empty);
        options.Cognito.ShouldNotBeNull();
        options.Tenants.ShouldNotBeNull();
        options.Tenants.TenantGroups.ShouldNotBeNull();
        options.Tenants.TenantGroups.ShouldBeEmpty();
    }

    [Fact]
    public void CognitoOptions_Constructor_SetsDefaultValues()
    {
        // Act
        var cognitoOptions = new GlobalAppSettingsOptions.CognitoOptions();

        // Assert
        cognitoOptions.Region.ShouldBe(string.Empty);
        cognitoOptions.ClientId.ShouldBe(string.Empty);
        cognitoOptions.Authority.ShouldBe(string.Empty);
    }

    [Fact]
    public void TenantOptions_Constructor_SetsDefaultValues()
    {
        // Act
        var tenantOptions = new GlobalAppSettingsOptions.TenantOptions();

        // Assert
        tenantOptions.TenantGroups.ShouldNotBeNull();
        tenantOptions.TenantGroups.ShouldBeEmpty();
    }

    [Fact]
    public void ConfigurationBinding_BindsCorrectly()
    {
        // Arrange
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "TablePrefix", "TestPrefix_" },
                { "Cognito:Region", "us-east-1" },
                { "Cognito:ClientId", "test-client-id" },
                {
                    "Cognito:Authority",
                    "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_TestPool"
                },
                { "Tenants:TenantGroups:tenant1:0", "Admin" },
                { "Tenants:TenantGroups:tenant1:1", "User" },
                { "Tenants:TenantGroups:tenant2:0", "Manager" }
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<GlobalAppSettingsOptions>(configuration);
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<GlobalAppSettingsOptions>>()
            .Value;

        // Assert
        options.TablePrefix.ShouldBe("TestPrefix_");
        options.Cognito.Region.ShouldBe("us-east-1");
        options.Cognito.ClientId.ShouldBe("test-client-id");
        options.Cognito.Authority.ShouldBe(
            "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_TestPool");
        options.Tenants.TenantGroups.Count.ShouldBe(2);
        options.Tenants.TenantGroups.Keys.ShouldContain("tenant1");
        options.Tenants.TenantGroups.Keys.ShouldContain("tenant2");
        options.Tenants.TenantGroups["tenant1"].Count.ShouldBe(2);
        options.Tenants.TenantGroups["tenant1"].ShouldContain("Admin");
        options.Tenants.TenantGroups["tenant1"].ShouldContain("User");
        options.Tenants.TenantGroups["tenant2"].ShouldHaveSingleItem();
        options.Tenants.TenantGroups["tenant2"].ShouldContain("Manager");
    }

    [Fact]
    public void ConfigurationBinding_WithMissingValues_UsesDefaults()
    {
        // Arrange
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.Configure<GlobalAppSettingsOptions>(configuration);
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<GlobalAppSettingsOptions>>()
            .Value;

        // Assert
        options.TablePrefix.ShouldBe(string.Empty);
        options.Cognito.Region.ShouldBe(string.Empty);
        options.Cognito.ClientId.ShouldBe(string.Empty);
        options.Cognito.Authority.ShouldBe(string.Empty);
        options.Tenants.TenantGroups.ShouldBeEmpty();
    }

    [Fact]
    public void ConfigurationBinding_WithPartialValues_BindsAvailableValues()
    {
        // Arrange
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "TablePrefix", "Partial_" },
                { "Cognito:Region", "eu-west-1" }
                // Missing other Cognito values and Tenants
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<GlobalAppSettingsOptions>(configuration);
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<GlobalAppSettingsOptions>>()
            .Value;

        // Assert
        options.TablePrefix.ShouldBe("Partial_");
        options.Cognito.Region.ShouldBe("eu-west-1");
        options.Cognito.ClientId.ShouldBe(string.Empty);
        options.Cognito.Authority.ShouldBe(string.Empty);
        options.Tenants.TenantGroups.ShouldBeEmpty();
    }

    [Fact]
    public void TenantGroups_CanAddAndRetrieveGroups()
    {
        // Arrange
        var options = new GlobalAppSettingsOptions();
        var tenantId = "test-tenant-123";
        var groups = new List<string> { "Admin", "User", "Manager" };

        // Act
        options.Tenants.TenantGroups[tenantId] = groups;

        // Assert
        options.Tenants.TenantGroups.ContainsKey(tenantId).ShouldBeTrue();
        options.Tenants.TenantGroups[tenantId].ShouldBe(groups);
        options.Tenants.TenantGroups[tenantId].Count.ShouldBe(3);
    }

    [Fact]
    public void TenantGroups_TryGetValue_WorksCorrectly()
    {
        // Arrange
        var options = new GlobalAppSettingsOptions();
        var tenantId = "existing-tenant";
        var groups = new List<string> { "Admin" };
        options.Tenants.TenantGroups[tenantId] = groups;

        // Act & Assert - Existing tenant
        var foundExisting
            = options.Tenants.TenantGroups.TryGetValue(tenantId, out var existingGroups);
        foundExisting.ShouldBeTrue();
        existingGroups.ShouldBe(groups);

        // Act & Assert - Non-existing tenant
        var foundNonExisting
            = options.Tenants.TenantGroups.TryGetValue("non-existing", out var nonExistingGroups);
        foundNonExisting.ShouldBeFalse();
        nonExistingGroups.ShouldBeNull();
    }

    [Fact]
    public void PropertySetters_SetValuesCorrectly()
    {
        // Arrange
        var options = new GlobalAppSettingsOptions();
        const string expectedTablePrefix = "NewPrefix_";
        const string expectedRegion = "ap-southeast-1";
        const string expectedClientId = "new-client-id";
        const string expectedAuthority = "https://new-authority.com";

        // Act
        options.TablePrefix = expectedTablePrefix;
        options.Cognito.Region = expectedRegion;
        options.Cognito.ClientId = expectedClientId;
        options.Cognito.Authority = expectedAuthority;

        // Assert
        options.TablePrefix.ShouldBe(expectedTablePrefix);
        options.Cognito.Region.ShouldBe(expectedRegion);
        options.Cognito.ClientId.ShouldBe(expectedClientId);
        options.Cognito.Authority.ShouldBe(expectedAuthority);
    }
}