using Hl7.Fhir.Model;
using Microsoft.Extensions.DependencyInjection;
using ThirdOpinion.Common.Aws.HealthLake;
using ThirdOpinion.Common.FunctionalTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace ThirdOpinion.Common.FunctionalTests.Tests;

/// <summary>
///     Integration tests for AWS HealthLake FHIR operations
/// </summary>
public class HealthLakeIntegrationTests : BaseIntegrationTest
{
    private readonly IFhirSourceService? _fhirSourceService;

    private readonly string[] _testPatientIds =
    {
        "a-15454.E-464973",
        "a-15454.E-337187",
        "a-15454.E-527614",
        "a-15454.E-490596"
    };

    public HealthLakeIntegrationTests(ITestOutputHelper output) : base(output)
    {
        _fhirSourceService = ServiceProvider.GetService<IFhirSourceService>();
    }

    /// <summary>
    ///     Test retrieving real patient resources from HealthLake
    /// </summary>
    [Fact]
    public async Task GetResourceAsync_WithRealPatientIds_ShouldRetrievePatients()
    {
        // Arrange
        if (_fhirSourceService == null)
        {
            WriteOutput("IFhirSourceService not configured, skipping test");
            return;
        }

        WriteOutput($"Testing retrieval of {_testPatientIds.Length} patient resources");

        // Act & Assert
        foreach (string patientId in _testPatientIds)
        {
            WriteOutput($"Retrieving patient: {patientId}");

            try
            {
                // Use dynamic type since we don't have FHIR models defined
                var patient = await _fhirSourceService.GetResourceAsync<Patient>(
                    "Patient",
                    patientId,
                    CancellationToken.None);

                // Assert
                Assert.NotNull(patient);
                WriteOutput($"✓ Successfully retrieved patient: {patientId}");

                // Log some basic info if available
                string patientInfo = patient.ToString();
                WriteOutput($"  Patient data length: {patientInfo?.Length ?? 0} characters");
            }
            catch (Exception ex)
            {
                WriteOutput($"✗ Failed to retrieve patient {patientId}: {ex.Message}");
                throw;
            }
        }

        WriteOutput($"Successfully retrieved all {_testPatientIds.Length} patients");
    }

    /// <summary>
    ///     Test retrieving a non-existent patient should throw appropriate exception
    /// </summary>
    [Fact]
    public async Task GetResourceAsync_WithNonExistentId_ShouldThrowException()
    {
        // Arrange
        if (_fhirSourceService == null)
        {
            WriteOutput("IFhirSourceService not configured, skipping test");
            return;
        }

        var nonExistentId = $"non-existent-{Guid.NewGuid():N}";
        WriteOutput($"Testing retrieval of non-existent patient: {nonExistentId}");

        // Act & Assert
        var exception = await Should.ThrowAsync<Exception>(async () =>
        {
            await _fhirSourceService.GetResourceAsync<Patient>(
                "Patient",
                nonExistentId,
                CancellationToken.None);
        });

        // Verify we got an appropriate error
        Assert.NotNull(exception);
        WriteOutput($"✓ Correctly threw exception for non-existent patient: {exception.Message}");
    }

    /// <summary>
    ///     Test full round-trip: PUT a new patient, then GET it back
    /// </summary>
    [Fact]
    public async Task PutAndGetResource_NewPatient_ShouldRoundTrip()
    {
        // Arrange
        if (HealthLakeFhirService == null || _fhirSourceService == null)
        {
            WriteOutput("FHIR services not configured, skipping test");
            return;
        }

        var testPatientId = GenerateTestResourceName("patient");
        WriteOutput($"Testing round-trip with new patient: {testPatientId}");

        // Create a minimal valid FHIR Patient resource
        var patientJson = $$"""
            {
              "resourceType": "Patient",
              "id": "{{testPatientId}}",
              "active": true,
              "name": [{
                "use": "official",
                "family": "TestPatient",
                "given": ["Integration", "Test"]
              }],
              "gender": "unknown",
              "birthDate": "2000-01-01"
            }
            """;

        try
        {
            // Act - Write the patient
            WriteOutput($"Writing patient resource...");
            await HealthLakeFhirService.PutResourceAsync(
                "Patient",
                testPatientId,
                patientJson,
                CancellationToken.None);

            WriteOutput($"✓ Successfully wrote patient: {testPatientId}");

            // Act - Read the patient back
            WriteOutput($"Reading patient resource back...");
            var retrievedPatient = await _fhirSourceService.GetResourceAsync<Patient>(
                "Patient",
                testPatientId,
                CancellationToken.None);

            // Assert
            Assert.NotNull(retrievedPatient);
            WriteOutput($"✓ Successfully retrieved patient: {testPatientId}");

            // Verify patient properties
            Assert.Equal(testPatientId, retrievedPatient.Id);
            Assert.True(retrievedPatient.Active == true);
            Assert.NotEmpty(retrievedPatient.Name);
            Assert.Equal("TestPatient", retrievedPatient.Name[0].Family);
            WriteOutput($"✓ Patient data contains expected ID and resourceType");
        }
        catch (Exception ex)
        {
            WriteOutput($"✗ Round-trip test failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    ///     Test retrieving multiple patients in parallel
    /// </summary>
    [Fact]
    public async Task GetResourceAsync_MultiplePatients_ShouldRetrieveInParallel()
    {
        // Arrange
        if (_fhirSourceService == null)
        {
            WriteOutput("IFhirSourceService not configured, skipping test");
            return;
        }

        WriteOutput($"Testing parallel retrieval of {_testPatientIds.Length} patients");

        // Act
        var startTime = DateTime.UtcNow;
        List<Task<Patient>> tasks = _testPatientIds
            .Select(patientId => _fhirSourceService.GetResourceAsync<Patient>(
                "Patient",
                patientId,
                CancellationToken.None))
            .ToList();

        dynamic[] results = await Task.WhenAll(tasks);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        Assert.Equal(_testPatientIds.Length, results.Length);
        foreach (var patient in results) Assert.NotNull(patient);

        WriteOutput(
            $"✓ Successfully retrieved {results.Length} patients in parallel in {elapsed.TotalMilliseconds:F0}ms");
    }

    /// <summary>
    ///     Cleanup any test resources created during tests
    /// </summary>
    protected override async Task CleanupTestResourcesAsync()
    {
        WriteOutput("Cleaning up test resources...");
        // Note: HealthLake doesn't support DELETE operations via REST API
        // Test resources will need to be cleaned up manually or via AWS Console
        await Task.CompletedTask;
    }
}