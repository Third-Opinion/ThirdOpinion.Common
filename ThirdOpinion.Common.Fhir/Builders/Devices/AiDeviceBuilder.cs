using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Builders.Base;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;

namespace ThirdOpinion.Common.Fhir.Builders.Devices;

/// <summary>
///     Builder for creating FHIR Device resources representing AI inference engines
/// </summary>
public class AiDeviceBuilder : AiResourceBuilderBase<Device, AiDeviceBuilder>
{
    private readonly List<Device.PropertyComponent> _properties;
    private readonly List<string> _versions;
    private string? _manufacturer;
    private string? _modelName;
    private string? _typeCode;

    /// <summary>
    ///     Creates a new AI Device builder
    /// </summary>
    /// <param name="configuration">The AI inference configuration</param>
    public AiDeviceBuilder(AiInferenceConfiguration configuration)
        : base(configuration)
    {
        _versions = new List<string>();
        _properties = new List<Device.PropertyComponent>();
    }

    /// <summary>
    ///     Sets the model name with an optional type code
    /// </summary>
    /// <param name="name">The model name</param>
    /// <param name="typeCode">The type code for the device name</param>
    /// <returns>This builder instance for method chaining</returns>
    public AiDeviceBuilder WithModelName(string name, string? typeCode = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Model name cannot be null or empty", nameof(name));

        _modelName = name;
        _typeCode = typeCode;
        return this;
    }

    /// <summary>
    ///     Sets the manufacturer of the AI system
    /// </summary>
    /// <param name="manufacturer">The manufacturer name</param>
    /// <returns>This builder instance for method chaining</returns>
    public AiDeviceBuilder WithManufacturer(string manufacturer)
    {
        if (string.IsNullOrWhiteSpace(manufacturer))
            throw new ArgumentException("Manufacturer cannot be null or empty",
                nameof(manufacturer));

        _manufacturer = manufacturer;
        return this;
    }

    /// <summary>
    ///     Adds a version to the device
    /// </summary>
    /// <param name="version">The version string</param>
    /// <returns>This builder instance for method chaining</returns>
    public AiDeviceBuilder WithVersion(string version)
    {
        if (!string.IsNullOrWhiteSpace(version)) _versions.Add(version);
        return this;
    }

    /// <summary>
    ///     Adds a property to the device with a Quantity value
    /// </summary>
    /// <param name="name">The property name</param>
    /// <param name="value">The quantity value</param>
    /// <returns>This builder instance for method chaining</returns>
    public AiDeviceBuilder AddProperty(string name, Quantity value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Property name cannot be null or empty", nameof(name));
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        var property = new Device.PropertyComponent
        {
            Type = new CodeableConcept
            {
                Text = name,
                Coding = new List<Coding>
                {
                    new()
                    {
                        Display = name
                    }
                }
            },
            ValueQuantity = new List<Quantity> { value }
        };

        _properties.Add(property);
        return this;
    }

    /// <summary>
    ///     Adds a property to the device with a decimal value and unit
    /// </summary>
    /// <param name="name">The property name</param>
    /// <param name="value">The decimal value</param>
    /// <param name="unit">The unit of measurement</param>
    /// <returns>This builder instance for method chaining</returns>
    public AiDeviceBuilder AddProperty(string name, decimal value, string unit)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Property name cannot be null or empty", nameof(name));
        if (string.IsNullOrWhiteSpace(unit))
            throw new ArgumentException("Unit cannot be null or empty", nameof(unit));

        var quantity = new Quantity
        {
            Value = value,
            Unit = unit,
            System = "http://unitsofmeasure.org", // UCUM system
            Code = unit
        };

        return AddProperty(name, quantity);
    }

    /// <summary>
    ///     Validates that required fields are set before building
    /// </summary>
    protected override void ValidateRequiredFields()
    {
        // Model name is recommended but not strictly required by FHIR
        // Status and type are set in BuildCore
    }

    /// <summary>
    ///     Builds the AI Device resource
    /// </summary>
    /// <returns>The completed Device resource</returns>
    protected override Device BuildCore()
    {
        var device = new Device
        {
            // Set status to active
            Status = Device.FHIRDeviceStatus.Active,

            // Set type with SNOMED code for AI algorithm
            Type = FhirCodingHelper.CreateSnomedConcept(
                "706689003",
                "Artificial intelligence algorithm")
        };

        // Add model name if provided
        if (!string.IsNullOrWhiteSpace(_modelName))
            device.DeviceName = new List<Device.DeviceNameComponent>
            {
                new()
                {
                    Name = _modelName,
                    Type = _typeCode != null
                        ? _typeCode switch
                        {
                            "manufacturer" => DeviceNameType.ManufacturerName,
                            "model" => DeviceNameType.ModelName,
                            "user-friendly" => DeviceNameType.UserFriendlyName,
                            _ => DeviceNameType.Other
                        }
                        : DeviceNameType.ModelName
                }
            };

        // Set manufacturer
        if (!string.IsNullOrWhiteSpace(_manufacturer)) device.Manufacturer = _manufacturer;

        // Add versions
        if (_versions.Any())
            device.Version = _versions.Select(v => new Device.VersionComponent
            {
                Value = v
            }).ToList();

        // Add identifier with model version from configuration
        var identifiers = new List<Identifier>();

        // Add model version identifier
        if (!string.IsNullOrWhiteSpace(Configuration.DefaultModelVersion))
            identifiers.Add(new Identifier
            {
                System = Configuration.ModelSystem ??
                         "https://thirdopinion.ai/device-model-version",
                Value = Configuration.DefaultModelVersion
            });

        // Add FHIR resource ID as identifier if available
        if (!string.IsNullOrWhiteSpace(FhirResourceId))
            identifiers.Add(new Identifier
            {
                System = Configuration.InferenceSystem,
                Value = FhirResourceId
            });

        if (identifiers.Any()) device.Identifier = identifiers;

        // Add properties
        if (_properties.Any()) device.Property = _properties;

        return device;
    }
}