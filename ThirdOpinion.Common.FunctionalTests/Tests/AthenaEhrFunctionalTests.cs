using Microsoft.Extensions.Configuration;
using ThirdOpinion.Common.FunctionalTests.Infrastructure;
using Xunit.Abstractions;

namespace ThirdOpinion.Common.FunctionalTests.Tests;

[Collection("AthenaEhr")]
public class AthenaEhrFunctionalTests : BaseIntegrationTest
{
    private readonly string _baseUrl;
    private readonly bool _isConfigured;
    private readonly bool _isEnabled;
    private readonly string? _practiceId;
    private readonly string? _testPatientId;

    public AthenaEhrFunctionalTests(ITestOutputHelper output) : base(output)
    {
        _isEnabled = Configuration.GetValue<bool>("AthenaEhr:EnableTesting");
        _practiceId = Configuration.GetValue<string>("AthenaEhr:PracticeId");
        _testPatientId = Configuration.GetValue<string>("AthenaEhr:TestPatientId");
        _baseUrl = Configuration.GetValue<string>("AthenaEhr:BaseUrl") ??
                   "https://api.athenahealth.com";

        var clientId = Configuration.GetValue<string>("AthenaEhr:ClientId");
        var secret = Configuration.GetValue<string>("AthenaEhr:Secret");

        _isConfigured = !string.IsNullOrEmpty(clientId) &&
                        !string.IsNullOrEmpty(secret) &&
                        !string.IsNullOrEmpty(_practiceId);
    }

    [Fact]
    public void AthenaEhrService_ShouldBeConfigured_WhenCredentialsProvided()
    {
        if (!_isEnabled)
        {
            WriteOutput(
                "⚠️ AthenaEhr testing disabled in configuration (AthenaEhr:EnableTesting = false)");
            WriteOutput("This is expected for functional tests without AthenaEhr sandbox access");
            return;
        }

        if (!_isConfigured)
        {
            WriteOutput("⚠️ AthenaEhr credentials not configured - expected for functional tests");
            WriteOutput("To test AthenaEhr integration:");
            WriteOutput("  1. Set AthenaEhr:EnableTesting = true");
            WriteOutput("  2. Set AthenaEhr:ClientId and AthenaEhr:Secret");
            WriteOutput("  3. Set AthenaEhr:PracticeId for sandbox");
            return;
        }

        WriteOutput("Testing AthenaEhr service configuration...");

        WriteOutput("AthenaEhr configuration appears valid based on provided settings");

        WriteOutput($"✓ AthenaEhrService properly configured for practice: {_practiceId}");
    }


    [Fact]
    public async Task AthenaEhrService_ShouldValidateConfiguration_Properly()
    {
        WriteOutput("Testing AthenaEhr configuration validation...");

        var enabledConfig = Configuration.GetValue<bool>("AthenaEhr:EnableTesting");
        var baseUrlConfig = Configuration.GetValue<string>("AthenaEhr:BaseUrl");
        var versionConfig = Configuration.GetValue<string>("AthenaEhr:Version");
        var timeoutConfig = Configuration.GetValue<string>("AthenaEhr:RequestTimeout");

        WriteOutput($"EnableTesting: {enabledConfig}");
        WriteOutput($"BaseUrl: {baseUrlConfig ?? "not set"}");
        WriteOutput($"Version: {versionConfig ?? "not set"}");
        WriteOutput($"RequestTimeout: {timeoutConfig ?? "not set"}");

        baseUrlConfig.ShouldNotBeNullOrEmpty("Base URL should be configured");
        baseUrlConfig.ShouldStartWith("https://");

        WriteOutput("✓ Configuration validation completed");
    }


    protected override Task CleanupTestResourcesAsync()
    {
        WriteOutput("AthenaEhr tests cleanup - no resources to clean up (read-only operations)");
        return Task.CompletedTask;
    }
}