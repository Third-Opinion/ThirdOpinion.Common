using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Builders.Base;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;

namespace ThirdOpinion.Common.Fhir.Builders.Observations;

/// <summary>
/// Builder for creating FHIR Observations for RECIST 1.1 radiographic progression with imaging references
/// </summary>
public class RecistProgressionObservationBuilder : AiResourceBuilderBase<Observation>
{
    private ResourceReference? _patientReference;
    private ResourceReference? _deviceReference;
    private readonly List<ResourceReference> _focusReferences;
    private string? _criteriaVersion;
    private readonly List<ResourceReference> _imagingStudies;
    private readonly List<ResourceReference> _radiologyReports;
    private CodeableConcept? _bodySite;
    private readonly List<Observation.ComponentComponent> _components;
    private CodeableConcept? _recistResponse;

    /// <summary>
    /// Creates a new RECIST Progression Observation builder
    /// </summary>
    /// <param name="configuration">The AI inference configuration</param>
    public RecistProgressionObservationBuilder(AiInferenceConfiguration configuration)
        : base(configuration)
    {
        _focusReferences = new List<ResourceReference>();
        _imagingStudies = new List<ResourceReference>();
        _radiologyReports = new List<ResourceReference>();
        _components = new List<Observation.ComponentComponent>();
    }

    /// <summary>
    /// Override base class methods to maintain fluent interface
    /// </summary>
    public new RecistProgressionObservationBuilder WithInferenceId(string id)
    {
        base.WithInferenceId(id);
        return this;
    }

    /// <summary>
    /// Override base class methods to maintain fluent interface
    /// </summary>
    public new RecistProgressionObservationBuilder WithCriteria(string id, string display, string? system = null)
    {
        base.WithCriteria(id, display, system);
        return this;
    }

    /// <summary>
    /// Override base class methods to maintain fluent interface
    /// </summary>
    public new RecistProgressionObservationBuilder AddDerivedFrom(ResourceReference reference)
    {
        base.AddDerivedFrom(reference);
        return this;
    }

    /// <summary>
    /// Override base class methods to maintain fluent interface
    /// </summary>
    public new RecistProgressionObservationBuilder AddDerivedFrom(string reference, string? display = null)
    {
        base.AddDerivedFrom(reference, display);
        return this;
    }

    /// <summary>
    /// Sets the patient reference for this observation
    /// </summary>
    /// <param name="patient">The patient resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public RecistProgressionObservationBuilder WithPatient(ResourceReference patient)
    {
        _patientReference = patient ?? throw new ArgumentNullException(nameof(patient));
        return this;
    }

    /// <summary>
    /// Sets the patient reference for this observation
    /// </summary>
    /// <param name="patientId">The patient ID</param>
    /// <param name="display">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public RecistProgressionObservationBuilder WithPatient(string patientId, string? display = null)
    {
        if (string.IsNullOrWhiteSpace(patientId))
            throw new ArgumentException("Patient ID cannot be null or empty", nameof(patientId));

        _patientReference = new ResourceReference
        {
            Reference = patientId.StartsWith("Patient/") ? patientId : $"Patient/{patientId}",
            Display = display
        };
        return this;
    }

    /// <summary>
    /// Sets the device reference that performed the assessment
    /// </summary>
    /// <param name="device">The device resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public RecistProgressionObservationBuilder WithDevice(ResourceReference device)
    {
        _deviceReference = device ?? throw new ArgumentNullException(nameof(device));
        return this;
    }

    /// <summary>
    /// Sets the device reference that performed the assessment
    /// </summary>
    /// <param name="deviceId">The device ID</param>
    /// <param name="display">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public RecistProgressionObservationBuilder WithDevice(string deviceId, string? display = null)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("Device ID cannot be null or empty", nameof(deviceId));

        _deviceReference = new ResourceReference
        {
            Reference = deviceId.StartsWith("Device/") ? deviceId : $"Device/{deviceId}",
            Display = display
        };
        return this;
    }

    /// <summary>
    /// Sets the focus references for this observation (tumors/lesions being assessed)
    /// </summary>
    /// <param name="focuses">The focus resource references</param>
    /// <returns>This builder instance for method chaining</returns>
    public RecistProgressionObservationBuilder WithFocus(params ResourceReference[] focuses)
    {
        if (focuses == null || focuses.Length == 0)
            throw new ArgumentException("At least one focus reference is required", nameof(focuses));

        _focusReferences.Clear();
        _focusReferences.AddRange(focuses.Where(f => f != null));
        return this;
    }

    /// <summary>
    /// Sets the RECIST criteria version for this assessment
    /// </summary>
    /// <param name="criteria">The RECIST criteria version (e.g., "1.1")</param>
    /// <returns>This builder instance for method chaining</returns>
    public RecistProgressionObservationBuilder WithCriteria(string criteria)
    {
        if (string.IsNullOrWhiteSpace(criteria))
            throw new ArgumentException("Criteria cannot be null or empty", nameof(criteria));

        _criteriaVersion = criteria;
        return this;
    }

    /// <summary>
    /// Adds a component with a Quantity value (for numeric measurements)
    /// </summary>
    /// <param name="code">The LOINC or SNOMED code for the component</param>
    /// <param name="value">The quantity value</param>
    /// <returns>This builder instance for method chaining</returns>
    public RecistProgressionObservationBuilder AddComponent(string code, Quantity value)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code cannot be null or empty", nameof(code));
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        var component = new Observation.ComponentComponent
        {
            Code = CreateComponentCode(code),
            Value = value
        };

        _components.Add(component);
        return this;
    }

    /// <summary>
    /// Adds a component with a boolean value (for yes/no indicators)
    /// </summary>
    /// <param name="code">The LOINC or SNOMED code for the component</param>
    /// <param name="value">The boolean value</param>
    /// <returns>This builder instance for method chaining</returns>
    public RecistProgressionObservationBuilder AddComponent(string code, bool value)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code cannot be null or empty", nameof(code));

        var component = new Observation.ComponentComponent
        {
            Code = CreateComponentCode(code),
            Value = new FhirBoolean(value)
        };

        _components.Add(component);
        return this;
    }

    /// <summary>
    /// Adds a component with a CodeableConcept value (for coded responses)
    /// </summary>
    /// <param name="code">The LOINC or SNOMED code for the component</param>
    /// <param name="value">The CodeableConcept value</param>
    /// <returns>This builder instance for method chaining</returns>
    public RecistProgressionObservationBuilder AddComponent(string code, CodeableConcept value)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code cannot be null or empty", nameof(code));
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        var component = new Observation.ComponentComponent
        {
            Code = CreateComponentCode(code),
            Value = value
        };

        _components.Add(component);
        return this;
    }

    /// <summary>
    /// Adds an imaging study reference to derivedFrom
    /// </summary>
    /// <param name="imagingStudy">The imaging study resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public RecistProgressionObservationBuilder AddImagingStudy(ResourceReference imagingStudy)
    {
        if (imagingStudy == null)
            throw new ArgumentNullException(nameof(imagingStudy));

        _imagingStudies.Add(imagingStudy);
        return this;
    }

    /// <summary>
    /// Adds a radiology report reference to derivedFrom
    /// </summary>
    /// <param name="report">The radiology report resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public RecistProgressionObservationBuilder AddRadiologyReport(ResourceReference report)
    {
        if (report == null)
            throw new ArgumentNullException(nameof(report));

        _radiologyReports.Add(report);
        return this;
    }

    /// <summary>
    /// Sets the overall RECIST 1.1 response category using NCI terminology
    /// </summary>
    /// <param name="nciCode">The NCI code (e.g., "C35571" for Progressive Disease)</param>
    /// <param name="display">The display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public RecistProgressionObservationBuilder WithRecistResponse(string nciCode, string display)
    {
        if (string.IsNullOrWhiteSpace(nciCode))
            throw new ArgumentException("NCI code cannot be null or empty", nameof(nciCode));
        if (string.IsNullOrWhiteSpace(display))
            throw new ArgumentException("Display cannot be null or empty", nameof(display));

        _recistResponse = FhirCodingHelper.CreateNciConcept(nciCode, display);
        return this;
    }

    /// <summary>
    /// Sets the body site for tumor location using SNOMED codes
    /// </summary>
    /// <param name="snomedCode">The SNOMED code for the body site</param>
    /// <param name="display">The display text for the body site</param>
    /// <returns>This builder instance for method chaining</returns>
    public RecistProgressionObservationBuilder WithBodySite(string snomedCode, string display)
    {
        if (string.IsNullOrWhiteSpace(snomedCode))
            throw new ArgumentException("SNOMED code cannot be null or empty", nameof(snomedCode));
        if (string.IsNullOrWhiteSpace(display))
            throw new ArgumentException("Display cannot be null or empty", nameof(display));

        _bodySite = FhirCodingHelper.CreateSnomedConcept(snomedCode, display);
        return this;
    }

    /// <summary>
    /// Validates that required fields are set before building
    /// </summary>
    protected override void ValidateRequiredFields()
    {
        if (_patientReference == null)
        {
            throw new InvalidOperationException("Patient reference is required. Call WithPatient() before Build().");
        }

        if (_deviceReference == null)
        {
            throw new InvalidOperationException("Device reference is required. Call WithDevice() before Build().");
        }
    }

    /// <summary>
    /// Builds the RECIST Progression Observation
    /// </summary>
    /// <returns>The completed Observation resource</returns>
    protected override Observation BuildCore()
    {
        var observation = new Observation
        {
            Status = ObservationStatus.Final,

            // Category: imaging
            Category = new List<CodeableConcept>
            {
                new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new Coding
                        {
                            System = "http://terminology.hl7.org/CodeSystem/observation-category",
                            Code = "imaging",
                            Display = "Imaging"
                        }
                    }
                }
            },

            // Code: LOINC 21976-6 (Cancer disease status) with NCI C111544 (RECIST 1.1)
            Code = new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new Coding
                    {
                        System = FhirCodingHelper.Systems.LOINC_SYSTEM,
                        Code = "21976-6",
                        Display = "Cancer disease status"
                    },
                    new Coding
                    {
                        System = FhirCodingHelper.Systems.NCI_SYSTEM,
                        Code = "C111544",
                        Display = "RECIST 1.1"
                    }
                },
                Text = "RECIST 1.1 progression assessment"
            },

            // Focus (tumors/lesions being assessed)
            Focus = _focusReferences.Any() ? _focusReferences : null,

            // Subject (Patient)
            Subject = _patientReference,

            // Device
            Device = _deviceReference,

            // Effective date/time
            Effective = new FhirDateTime(DateTimeOffset.Now),

            // Value: RECIST response if set
            Value = _recistResponse
        };

        // Add body site if set
        if (_bodySite != null)
        {
            observation.BodySite = _bodySite;
        }

        // Add derivedFrom references (imaging studies and reports, plus base derivedFrom)
        if (_imagingStudies.Any() || _radiologyReports.Any() || DerivedFromReferences.Any())
        {
            observation.DerivedFrom = new List<ResourceReference>();
            observation.DerivedFrom.AddRange(_imagingStudies);
            observation.DerivedFrom.AddRange(_radiologyReports);
            observation.DerivedFrom.AddRange(DerivedFromReferences);
        }

        // Add components
        if (_components.Any())
        {
            observation.Component = _components;
        }

        return observation;
    }

    /// <summary>
    /// Creates a CodeableConcept for component codes, using appropriate system based on code format
    /// </summary>
    /// <param name="code">The code value</param>
    /// <returns>A CodeableConcept with appropriate system</returns>
    private CodeableConcept CreateComponentCode(string code)
    {
        // Determine system based on code format/content
        if (code.Contains("-") && char.IsDigit(code[0])) // LOINC format
        {
            return GetLoincComponentCode(code);
        }
        else if (char.IsDigit(code[0])) // SNOMED format
        {
            return FhirCodingHelper.CreateSnomedConcept(code, GetSnomedDisplayForCode(code));
        }
        else
        {
            // Default to custom system for other codes
            return new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new Coding
                    {
                        System = "http://thirdopinion.ai/fhir/CodeSystem/recist-components",
                        Code = code,
                        Display = GetDefaultDisplayForCode(code)
                    }
                }
            };
        }
    }

    /// <summary>
    /// Gets LOINC component codes with appropriate display text
    /// </summary>
    /// <param name="code">The LOINC code</param>
    /// <returns>A LOINC CodeableConcept</returns>
    private CodeableConcept GetLoincComponentCode(string code)
    {
        var display = code switch
        {
            "33359-2" => "Percent change",
            "33728-8" => "Sum of longest diameters",
            "44666-9" => "New lesions",
            _ => "RECIST measurement"
        };

        return FhirCodingHelper.CreateLoincConcept(code, display);
    }

    /// <summary>
    /// Gets display text for SNOMED codes
    /// </summary>
    /// <param name="code">The SNOMED code</param>
    /// <returns>Display text</returns>
    private string GetSnomedDisplayForCode(string code)
    {
        return code switch
        {
            "371508000" => "Sum of longest diameters",
            "260405006" => "Absolute change",
            _ => "RECIST measurement"
        };
    }

    /// <summary>
    /// Gets default display text for custom codes
    /// </summary>
    /// <param name="code">The code</param>
    /// <returns>Display text</returns>
    private string GetDefaultDisplayForCode(string code)
    {
        return code switch
        {
            "nadir-sld" => "Nadir sum of longest diameters",
            "new-lesions" => "New lesions detected",
            "absolute-change" => "Absolute change in SLD",
            "percent-change" => "Percent change in SLD",
            _ => code.Replace("-", " ").Replace("_", " ")
        };
    }
}