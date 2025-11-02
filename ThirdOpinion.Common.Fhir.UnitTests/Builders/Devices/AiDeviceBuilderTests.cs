using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using ThirdOpinion.Common.Fhir.Builders.Devices;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;

namespace ThirdOpinion.Common.Fhir.UnitTests.Builders.Devices;

public class AiDeviceBuilderTests
{
    private readonly AiInferenceConfiguration _configuration;

    public AiDeviceBuilderTests()
    {
        _configuration = AiInferenceConfiguration.CreateDefault();
    }

    [Fact]
    public void Build_WithDefaults_CreatesActiveDeviceWithAiAlgorithmCode()
    {
        // Arrange
        var builder = new AiDeviceBuilder(_configuration);

        // Act
        Device device = builder.Build();

        // Assert
        device.ShouldNotBeNull();
        device.Status.ShouldBe(Device.FHIRDeviceStatus.Active);

        // Check SNOMED code for AI algorithm
        device.Type.ShouldNotBeNull();
        device.Type.Coding.ShouldNotBeNull();
        device.Type.Coding.Count.ShouldBeGreaterThan(0);
        device.Type.Coding[0].System.ShouldBe(FhirCodingHelper.Systems.SNOMED_SYSTEM);
        device.Type.Coding[0].Code.ShouldBe("706689003");
        device.Type.Coding[0].Display.ShouldBe("Artificial intelligence algorithm");
    }

    [Fact]
    public void WithModelName_SetsDeviceNameCorrectly()
    {
        // Arrange
        var builder = new AiDeviceBuilder(_configuration);

        // Act
        Device device = builder
            .WithModelName("GPT-4 Vision", "model")
            .Build();

        // Assert
        device.DeviceName.ShouldNotBeNull();
        device.DeviceName.Count.ShouldBe(1);
        device.DeviceName[0].Name.ShouldBe("GPT-4 Vision");
        device.DeviceName[0].Type.ShouldBe(DeviceNameType.ModelName);
    }

    [Fact]
    public void WithModelName_DefaultsToModelType_WhenTypeCodeNotProvided()
    {
        // Arrange
        var builder = new AiDeviceBuilder(_configuration);

        // Act
        Device device = builder
            .WithModelName("Claude 3 Opus")
            .Build();

        // Assert
        device.DeviceName.ShouldNotBeNull();
        device.DeviceName.Count.ShouldBe(1);
        device.DeviceName[0].Name.ShouldBe("Claude 3 Opus");
        device.DeviceName[0].Type.ShouldBe(DeviceNameType.ModelName);
    }

    [Fact]
    public void WithModelName_HandlesVariousTypeCodes()
    {
        // Arrange & Act
        Device manufacturerDevice = new AiDeviceBuilder(_configuration)
            .WithModelName("Test Model", "manufacturer")
            .Build();

        Device userFriendlyDevice = new AiDeviceBuilder(_configuration)
            .WithModelName("Test Model", "user-friendly")
            .Build();

        Device unknownDevice = new AiDeviceBuilder(_configuration)
            .WithModelName("Test Model", "custom-type")
            .Build();

        // Assert
        manufacturerDevice.DeviceName[0].Type.ShouldBe(DeviceNameType.ManufacturerName);
        userFriendlyDevice.DeviceName[0].Type.ShouldBe(DeviceNameType.UserFriendlyName);
        unknownDevice.DeviceName[0].Type.ShouldBe(DeviceNameType.Other);
    }

    [Fact]
    public void WithManufacturer_SetsManufacturerField()
    {
        // Arrange
        var builder = new AiDeviceBuilder(_configuration);

        // Act
        Device device = builder
            .WithManufacturer("OpenAI")
            .Build();

        // Assert
        device.Manufacturer.ShouldBe("OpenAI");
    }

    [Fact]
    public void WithVersion_AddsToVersionArray()
    {
        // Arrange
        var builder = new AiDeviceBuilder(_configuration);

        // Act
        Device device = builder
            .WithVersion("1.0.0")
            .WithVersion("1.0.1-beta")
            .Build();

        // Assert
        device.Version.ShouldNotBeNull();
        device.Version.Count.ShouldBe(2);
        device.Version[0].Value.ShouldBe("1.0.0");
        device.Version[1].Value.ShouldBe("1.0.1-beta");
    }

    [Fact]
    public void AddProperty_WithQuantity_AddsToPropertyArray()
    {
        // Arrange
        var builder = new AiDeviceBuilder(_configuration);
        var quantity = new Quantity
        {
            Value = 0.95m,
            Unit = "confidence",
            System = "http://unitsofmeasure.org",
            Code = "1"
        };

        // Act
        Device device = builder
            .AddProperty("Confidence Score", quantity)
            .Build();

        // Assert
        device.Property.ShouldNotBeNull();
        device.Property.Count.ShouldBe(1);
        device.Property[0].Type.Text.ShouldBe("Confidence Score");
        device.Property[0].ValueQuantity.ShouldNotBeNull();
        device.Property[0].ValueQuantity.Count.ShouldBe(1);
        Quantity? propValue = device.Property[0].ValueQuantity[0];
        propValue.ShouldNotBeNull();
        propValue.Value.ShouldBe(0.95m);
    }

    [Fact]
    public void AddProperty_WithDecimalAndUnit_CreatesQuantity()
    {
        // Arrange
        var builder = new AiDeviceBuilder(_configuration);

        // Act
        Device device = builder
            .AddProperty("Processing Time", 1.234m, "ms")
            .AddProperty("Accuracy", 98.5m, "%")
            .Build();

        // Assert
        device.Property.ShouldNotBeNull();
        device.Property.Count.ShouldBe(2);

        // Check first property
        device.Property[0].Type.Text.ShouldBe("Processing Time");
        device.Property[0].ValueQuantity.ShouldNotBeNull();
        device.Property[0].ValueQuantity.Count.ShouldBe(1);
        Quantity? time = device.Property[0].ValueQuantity[0];
        time.ShouldNotBeNull();
        time.Value.ShouldBe(1.234m);
        time.Unit.ShouldBe("ms");
        time.System.ShouldBe("http://unitsofmeasure.org");

        // Check second property
        device.Property[1].Type.Text.ShouldBe("Accuracy");
        device.Property[1].ValueQuantity.ShouldNotBeNull();
        device.Property[1].ValueQuantity.Count.ShouldBe(1);
        Quantity? accuracy = device.Property[1].ValueQuantity[0];
        accuracy.ShouldNotBeNull();
        accuracy.Value.ShouldBe(98.5m);
        accuracy.Unit.ShouldBe("%");
    }

    [Fact]
    public void Build_AddsModelVersionIdentifier_FromConfiguration()
    {
        // Arrange
        var builder = new AiDeviceBuilder(_configuration);

        // Act
        Device device = builder.Build();

        // Assert
        device.Identifier.ShouldNotBeNull();
        device.Identifier.Any(i =>
            i.Value == _configuration.DefaultModelVersion &&
            (i.System == _configuration.ModelSystem || i.System.Contains("model"))).ShouldBeTrue();
    }

    [Fact]
    public void Build_AddsInferenceIdIdentifier_WhenProvided()
    {
        // Arrange
        var builder = new AiDeviceBuilder(_configuration);
        var inferenceId = "test-inference-123";

        // Act
        Device device = builder
            .WithInferenceId(inferenceId)
            .Build();

        // Assert
        device.Id.ShouldBe(inferenceId);
        device.Identifier.ShouldNotBeNull();
        device.Identifier.Any(i =>
            i.Value == inferenceId &&
            i.System == _configuration.InferenceSystem).ShouldBeTrue();
    }

    [Fact]
    public void Build_AppliesAiastSecurityLabel()
    {
        // Arrange
        var builder = new AiDeviceBuilder(_configuration);

        // Act
        Device device = builder.Build();

        // Assert
        device.Meta.ShouldNotBeNull();
        device.Meta.Security.ShouldNotBeNull();
        device.Meta.Security.Any(s => s.Code == "AIAST").ShouldBeTrue();
    }

    [Fact]
    public void FluentInterface_SupportsCompleteChaining()
    {
        // Arrange & Act
        Device device = new AiDeviceBuilder(_configuration)
            .WithInferenceId("inf-001")
            .WithModelName("Test AI Model", "model")
            .WithManufacturer("Test Corp")
            .WithVersion("2.0.0")
            .WithVersion("2.0.1")
            .AddProperty("Confidence", 0.99m, "1")
            .AddProperty("Latency", 50m, "ms")
            .WithCriteria("criteria-001", "Test Criteria")
            .AddDerivedFrom("Patient/123")
            .Build();

        // Assert
        device.Id.ShouldBe("inf-001");
        device.DeviceName[0].Name.ShouldBe("Test AI Model");
        device.Manufacturer.ShouldBe("Test Corp");
        device.Version.Count.ShouldBe(2);
        device.Property.Count.ShouldBe(2);
    }

    [Fact]
    public void WithModelName_EmptyString_ThrowsArgumentException()
    {
        // Arrange
        var builder = new AiDeviceBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentException>(() => builder.WithModelName(""));
        Should.Throw<ArgumentException>(() => builder.WithModelName("   "));
    }

    [Fact]
    public void WithManufacturer_EmptyString_ThrowsArgumentException()
    {
        // Arrange
        var builder = new AiDeviceBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentException>(() => builder.WithManufacturer(""));
        Should.Throw<ArgumentException>(() => builder.WithManufacturer("   "));
    }

    [Fact]
    public void AddProperty_NullName_ThrowsArgumentException()
    {
        // Arrange
        var builder = new AiDeviceBuilder(_configuration);
        var quantity = new Quantity { Value = 1.0m };

        // Act & Assert
        Should.Throw<ArgumentException>(() => builder.AddProperty("", quantity));
        Should.Throw<ArgumentException>(() => builder.AddProperty(null!, quantity));
    }

    [Fact]
    public void AddProperty_NullQuantity_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new AiDeviceBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => builder.AddProperty("Test", null!));
    }

    [Fact]
    public void AddProperty_EmptyUnit_ThrowsArgumentException()
    {
        // Arrange
        var builder = new AiDeviceBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentException>(() => builder.AddProperty("Test", 1.0m, ""));
        Should.Throw<ArgumentException>(() => builder.AddProperty("Test", 1.0m, "   "));
    }

    [Fact]
    public void Build_GeneratesValidFhirJson()
    {
        // Arrange
        var builder = new AiDeviceBuilder(_configuration);
        Device device = builder
            .WithModelName("GPT-4", "model")
            .WithManufacturer("OpenAI")
            .WithVersion("4.0")
            .AddProperty("Temperature", 0.7m, "1")
            .Build();

        // Act
        var serializer = new FhirJsonSerializer(new SerializerSettings { Pretty = true });
        string json = serializer.SerializeToString(device);

        // Assert
        json.ShouldNotBeNullOrEmpty();
        json.ShouldContain("\"resourceType\": \"Device\"");
        json.ShouldContain("\"status\": \"active\"");
        json.ShouldContain("706689003"); // SNOMED code
        json.ShouldContain("GPT-4");
        json.ShouldContain("OpenAI");

        // Verify it can be deserialized
        var parser = new FhirJsonParser();
        var deserializedDevice = parser.Parse<Device>(json);
        deserializedDevice.ShouldNotBeNull();
        deserializedDevice.Status.ShouldBe(Device.FHIRDeviceStatus.Active);
    }

    [Fact]
    public void Build_WithoutModelName_StillCreatesValidDevice()
    {
        // Arrange
        var builder = new AiDeviceBuilder(_configuration);

        // Act
        Device device = builder
            .WithManufacturer("Anonymous AI Corp")
            .WithVersion("1.0.0")
            .Build();

        // Assert
        device.ShouldNotBeNull();
        device.Status.ShouldBe(Device.FHIRDeviceStatus.Active);
        device.Type.Coding[0].Code.ShouldBe("706689003");
        device.Manufacturer.ShouldBe("Anonymous AI Corp");
        device.DeviceName.ShouldBeEmpty();
    }

    [Fact]
    public void WithVersion_EmptyString_IsIgnored()
    {
        // Arrange
        var builder = new AiDeviceBuilder(_configuration);

        // Act
        Device device = builder
            .WithVersion("")
            .WithVersion("   ")
            .WithVersion("1.0.0")
            .Build();

        // Assert
        device.Version.ShouldNotBeNull();
        device.Version.Count.ShouldBe(1);
        device.Version[0].Value.ShouldBe("1.0.0");
    }

    [Fact]
    public void Build_MultipleProperties_AllAddedToDevice()
    {
        // Arrange
        var builder = new AiDeviceBuilder(_configuration);
        var customQuantity = new Quantity
        {
            Value = 100m,
            Unit = "iterations",
            System = "http://example.org/units"
        };

        // Act
        Device device = builder
            .AddProperty("MaxTokens", 2048m, "tokens")
            .AddProperty("Temperature", 0.8m, "1")
            .AddProperty("Iterations", customQuantity)
            .Build();

        // Assert
        device.Property.Count.ShouldBe(3);
        device.Property[0].Type.Text.ShouldBe("MaxTokens");
        device.Property[1].Type.Text.ShouldBe("Temperature");
        device.Property[2].Type.Text.ShouldBe("Iterations");

        // Check the custom quantity maintains its system
        device.Property[2].ValueQuantity.ShouldNotBeNull();
        device.Property[2].ValueQuantity.Count.ShouldBe(1);
        Quantity? iterationsProp = device.Property[2].ValueQuantity[0];
        iterationsProp.ShouldNotBeNull();
        iterationsProp.System.ShouldBe("http://example.org/units");
    }
}