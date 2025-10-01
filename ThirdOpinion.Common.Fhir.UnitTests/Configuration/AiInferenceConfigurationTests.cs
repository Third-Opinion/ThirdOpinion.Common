using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Configuration;

namespace ThirdOpinion.Common.Fhir.UnitTests.Configuration;

public class AiInferenceConfigurationTests
{
    [Fact]
    public void Constructor_WithDefaults_SetsDefaultValues()
    {
        // Act
        var config = new AiInferenceConfiguration();

        // Assert
        config.InferenceSystem.ShouldBe("http://thirdopinion.ai/fhir/CodeSystem/inference");
        config.CriteriaSystem.ShouldBe("http://thirdopinion.ai/fhir/CodeSystem/criteria");
        config.ModelSystem.ShouldBe("http://thirdopinion.ai/fhir/CodeSystem/model");
        config.DocumentTrackingSystem.ShouldBe("http://thirdopinion.ai/fhir/CodeSystem/document-tracking");
        config.ProvenanceSystem.ShouldBe("http://thirdopinion.ai/fhir/CodeSystem/provenance");
        config.DefaultModelVersion.ShouldBe("v1.0");
        config.OrganizationReference.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithAllParameters_SetsProvidedValues()
    {
        // Arrange
        var orgRef = new ResourceReference { Reference = "Organization/test" };

        // Act
        var config = new AiInferenceConfiguration(
            "http://example.org/inference",
            "http://example.org/criteria",
            "http://example.org/model",
            "http://example.org/tracking",
            "http://example.org/provenance",
            "v2.0",
            orgRef);

        // Assert
        config.InferenceSystem.ShouldBe("http://example.org/inference");
        config.CriteriaSystem.ShouldBe("http://example.org/criteria");
        config.ModelSystem.ShouldBe("http://example.org/model");
        config.DocumentTrackingSystem.ShouldBe("http://example.org/tracking");
        config.ProvenanceSystem.ShouldBe("http://example.org/provenance");
        config.DefaultModelVersion.ShouldBe("v2.0");
        config.OrganizationReference.ShouldBe(orgRef);
    }

    [Fact]
    public void Constructor_WithInvalidUri_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new AiInferenceConfiguration(
            "not-a-valid-uri",
            "http://example.org/criteria",
            "http://example.org/model",
            "http://example.org/tracking",
            "http://example.org/provenance",
            "v1.0"));

        Should.Throw<ArgumentException>(() => new AiInferenceConfiguration(
            "http://example.org/inference",
            "",
            "http://example.org/model",
            "http://example.org/tracking",
            "http://example.org/provenance",
            "v1.0"));
    }

    [Fact]
    public void Constructor_WithNullModelVersion_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new AiInferenceConfiguration(
            "http://example.org/inference",
            "http://example.org/criteria",
            "http://example.org/model",
            "http://example.org/tracking",
            "http://example.org/provenance",
            null!));
    }

    [Fact]
    public void CreateDefault_ReturnsConfigWithOrganizationReference()
    {
        // Act
        var config = AiInferenceConfiguration.CreateDefault();

        // Assert
        config.ShouldNotBeNull();
        config.OrganizationReference.ShouldNotBeNull();
        config.OrganizationReference.Reference.ShouldBe("Organization/thirdopinion-ai");
        config.OrganizationReference.Display.ShouldBe("Third Opinion AI");
        config.InferenceSystem.ShouldNotBeNullOrEmpty();
        config.DefaultModelVersion.ShouldBe("v1.0");
    }

    [Fact]
    public void Properties_CanBeSetIndividually()
    {
        // Arrange
        var config = new AiInferenceConfiguration();
        var orgRef = new ResourceReference { Reference = "Organization/new" };

        // Act
        config.InferenceSystem = "http://new.example.org/inference";
        config.CriteriaSystem = "http://new.example.org/criteria";
        config.ModelSystem = "http://new.example.org/model";
        config.DocumentTrackingSystem = "http://new.example.org/tracking";
        config.ProvenanceSystem = "http://new.example.org/provenance";
        config.DefaultModelVersion = "v3.0";
        config.OrganizationReference = orgRef;

        // Assert
        config.InferenceSystem.ShouldBe("http://new.example.org/inference");
        config.CriteriaSystem.ShouldBe("http://new.example.org/criteria");
        config.ModelSystem.ShouldBe("http://new.example.org/model");
        config.DocumentTrackingSystem.ShouldBe("http://new.example.org/tracking");
        config.ProvenanceSystem.ShouldBe("http://new.example.org/provenance");
        config.DefaultModelVersion.ShouldBe("v3.0");
        config.OrganizationReference.ShouldBe(orgRef);
    }
}