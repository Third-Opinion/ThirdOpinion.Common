using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ThirdOpinion.Common.UnitTests;

/// <summary>
/// Base class for all unit tests providing common setup and utilities.
/// 
/// Test Naming Conventions:
/// - Test classes: {ClassName}Tests (e.g., HuidGeneratorTests)
/// - Test methods: {MethodName}_{Scenario}_{ExpectedBehavior} 
///   (e.g., GeneratePatientHuid_WithValidPatientId_ShouldReturnCorrectFormatAndLength)
/// 
/// Test Organization:
/// - Group tests by the class they're testing
/// - Use Theory/InlineData for multiple input scenarios
/// - Use descriptive test names that explain the scenario and expected outcome
/// - Arrange/Act/Assert pattern for test structure
/// </summary>
public abstract class TestBase : IDisposable
{
    protected static IConfiguration Configuration => Initialize.Configuration;
    protected static IServiceProvider ServiceProvider => Initialize.ServiceProvider;

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Dispose any test-specific resources
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Creates a new service scope for dependency injection in tests
    /// </summary>
    protected IServiceScope CreateScope()
    {
        return ServiceProvider.CreateScope();
    }

    /// <summary>
    /// Gets a service of type T from the service provider
    /// </summary>
    protected T GetService<T>() where T : notnull
    {
        return ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Helper method to create a mock of type T
    /// </summary>
    protected Mock<T> CreateMock<T>() where T : class
    {
        return new Mock<T>();
    }

    /// <summary>
    /// Helper method to create a strict mock of type T
    /// </summary>
    protected Mock<T> CreateStrictMock<T>() where T : class
    {
        return new Mock<T>(MockBehavior.Strict);
    }
}