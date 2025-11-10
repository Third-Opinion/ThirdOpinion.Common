using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Builders.Base;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Extensions;
using ThirdOpinion.Common.Fhir.Helpers;
using ThirdOpinion.Common.Fhir.Models;

namespace ThirdOpinion.Common.Fhir.Builders.Observations;

/// <summary>
///     Unified builder for creating FHIR Observations for radiographic progression assessment
///     Supports RECIST 1.1, PCWG3, and Observed standards
/// </summary>
public class RadiographicObservationBuilder : AiResourceBuilderBase<Observation>
{
    private readonly List<Observation.ComponentComponent> _components;
    private readonly List<Fact> _conflictingFacts;
    private readonly List<ResourceReference> _evidenceReferences;
    private readonly List<ResourceReference> _focusReferences;
    private readonly List<string> _notes;
    private readonly List<ResourceReference> _radiologyReports;
    private readonly RadiographicStandard _standard;
    private readonly List<Fact> _supportingFacts;

    // Common fields
    private float? _confidence;
    private string? _confidenceRationale;
    private DateTime? _confirmationDate;
    private string? _determination;
    private ResourceReference? _deviceReference;
    private FhirDateTime? _effectiveDate;
    private ResourceReference? _patientReference;
    private string? _summary;

    // PCWG3-specific fields
    private string? _additionalLesions;
    private string? _confirmationLesions;
    private string? _initialLesions;
    private DateTime? _initialScanDate;
    private string? _timeBetweenScans;

    // RECIST-specific fields
    private CodeableConcept? _bodySite;
    private string? _criteriaVersion;
    private DateTime? _imagingDate;
    private string? _imagingType;
    private string? _measurementChange;
    private CodeableConcept? _recistResponse;
    private string? _recistTimepointsJson;

    // Observed-specific fields
    private string? _observedChanges;

    /// <summary>
    ///     Creates a new Radiographic Observation builder
    /// </summary>
    /// <param name="configuration">The AI inference configuration</param>
    /// <param name="standard">The radiographic assessment standard to use</param>
    public RadiographicObservationBuilder(
        AiInferenceConfiguration configuration,
        RadiographicStandard standard)
        : base(configuration)
    {
        _standard = standard;
        _focusReferences = new List<ResourceReference>();
        _radiologyReports = new List<ResourceReference>();
        _evidenceReferences = new List<ResourceReference>();
        _components = new List<Observation.ComponentComponent>();
        _supportingFacts = new List<Fact>();
        _conflictingFacts = new List<Fact>();
        _notes = new List<string>();
    }

    #region Fluent Interface Overrides

    /// <summary>
    ///     Override base class methods to maintain fluent interface
    /// </summary>
    public new RadiographicObservationBuilder WithInferenceId(string id)
    {
        base.WithInferenceId(id);
        return this;
    }

    /// <summary>
    ///     Override base class methods to maintain fluent interface
    /// </summary>
    public new RadiographicObservationBuilder WithCriteria(string id,
        string display,
        string? system = null)
    {
        base.WithCriteria(id, display, system);
        return this;
    }

    /// <summary>
    ///     Override base class methods to maintain fluent interface
    /// </summary>
    public new RadiographicObservationBuilder AddDerivedFrom(ResourceReference reference)
    {
        base.AddDerivedFrom(reference);
        return this;
    }

    /// <summary>
    ///     Override base class methods to maintain fluent interface
    /// </summary>
    public new RadiographicObservationBuilder AddDerivedFrom(string reference,
        string? display = null)
    {
        base.AddDerivedFrom(reference, display);
        return this;
    }

    #endregion

    #region Common Methods

    /// <summary>
    ///     Sets the patient reference for this observation
    /// </summary>
    /// <param name="patient">The patient resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithPatient(ResourceReference patient)
    {
        _patientReference = patient ?? throw new ArgumentNullException(nameof(patient));
        return this;
    }

    /// <summary>
    ///     Sets the patient reference for this observation
    /// </summary>
    /// <param name="patientId">The patient ID</param>
    /// <param name="display">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithPatient(string patientId, string? display = null)
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
    ///     Sets the device reference that performed the assessment
    /// </summary>
    /// <param name="device">The device resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithDevice(ResourceReference device)
    {
        _deviceReference = device ?? throw new ArgumentNullException(nameof(device));
        return this;
    }

    /// <summary>
    ///     Sets the device reference that performed the assessment
    /// </summary>
    /// <param name="deviceId">The device ID</param>
    /// <param name="display">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithDevice(string deviceId, string? display = null)
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
    ///     Sets the focus references for this observation (conditions/tumors/lesions being assessed)
    /// </summary>
    /// <param name="focuses">The focus resource references</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithFocus(params ResourceReference[] focuses)
    {
        if (focuses == null || focuses.Length == 0)
            throw new ArgumentException("At least one focus reference is required", nameof(focuses));

        _focusReferences.Clear();
        _focusReferences.AddRange(focuses.Where(f => f != null));
        return this;
    }

    /// <summary>
    ///     Sets the AI confidence score for this observation
    /// </summary>
    /// <param name="confidence">The confidence score (0.0 to 1.0)</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithConfidence(float confidence)
    {
        if (confidence < 0.0f || confidence > 1.0f)
            throw new ArgumentOutOfRangeException(nameof(confidence),
                "Confidence must be between 0.0 and 1.0");

        _confidence = confidence;
        return this;
    }

    /// <summary>
    ///     Sets the confidence rationale explaining the confidence score reasoning
    /// </summary>
    /// <param name="confidenceRationale">The confidence rationale text</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithConfidenceRationale(string? confidenceRationale)
    {
        _confidenceRationale = confidenceRationale;
        return this;
    }

    /// <summary>
    ///     Sets the confirmation date for progression
    /// </summary>
    /// <param name="confirmationDate">Date when progression was confirmed</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithConfirmationDate(DateTime? confirmationDate)
    {
        _confirmationDate = confirmationDate;
        return this;
    }

    /// <summary>
    ///     Sets the determination result (CR, PR, SD, PD, Baseline, Inconclusive)
    /// </summary>
    /// <param name="determination">The determination value: CR, PR, SD, PD, Baseline, Inconclusive</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithDetermination(string? determination)
    {
        if (determination != null &&
            !new[] { "CR", "PR", "SD", "PD", "Baseline", "Inconclusive" }.Contains(determination))
            throw new ArgumentException(
                $"Invalid determination value: {determination}. Must be one of: CR, PR, SD, PD, Baseline, Inconclusive.",
                nameof(determination));
        _determination = determination;
        return this;
    }

    /// <summary>
    ///     Sets the effective date/time of this observation
    /// </summary>
    /// <param name="effectiveDate">The effective date/time</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithEffectiveDate(DateTime effectiveDate)
    {
        _effectiveDate = new FhirDateTime(effectiveDate);
        return this;
    }

    /// <summary>
    ///     Sets the effective date/time of this observation
    /// </summary>
    /// <param name="effectiveDate">The effective date/time as DateTimeOffset</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithEffectiveDate(DateTimeOffset effectiveDate)
    {
        _effectiveDate = new FhirDateTime(effectiveDate);
        return this;
    }

    /// <summary>
    ///     Sets the detailed summary of the assessment
    /// </summary>
    /// <param name="summary">The assessment summary</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithSummary(string? summary)
    {
        _summary = summary;
        return this;
    }

    /// <summary>
    ///     Sets the observed changes description (Observed standard)
    /// </summary>
    /// <param name="observedChanges">Description of observed changes (e.g., "Progression", "Stable", "Regression")</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithObservedChanges(string? observedChanges)
    {
        _observedChanges = observedChanges;
        return this;
    }

    /// <summary>
    ///     Adds a note to this observation
    /// </summary>
    /// <param name="noteText">The note text</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder AddNote(string noteText)
    {
        if (!string.IsNullOrWhiteSpace(noteText)) _notes.Add(noteText);
        return this;
    }

    /// <summary>
    ///     Adds supporting clinical facts as evidence
    /// </summary>
    /// <param name="facts">Array of supporting clinical facts</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithSupportingFacts(params Fact[] facts)
    {
        if (facts != null && facts.Length > 0)
        {
            _supportingFacts.AddRange(facts.Where(f => f != null));

            // Add document references as evidence
            foreach (Fact fact in facts.Where(f =>
                         f != null && !string.IsNullOrWhiteSpace(f.factDocumentReference)))
            {
                if (_standard == RadiographicStandard.RECIST_1_1)
                    AddRadiologyReport(new ResourceReference(fact.factDocumentReference,
                        $"Supporting fact: {fact.type}"));
                else
                    AddEvidence(fact.factDocumentReference, $"Supporting fact: {fact.type}");
            }
        }

        return this;
    }

    /// <summary>
    ///     Adds conflicting clinical facts that may contradict the assessment
    /// </summary>
    /// <param name="facts">Array of conflicting clinical facts</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithConflictingFacts(params Fact[] facts)
    {
        if (facts != null && facts.Length > 0)
            _conflictingFacts.AddRange(facts.Where(f => f != null));
        return this;
    }

    /// <summary>
    ///     Adds a radiology report reference to derivedFrom
    /// </summary>
    /// <param name="report">The radiology report resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder AddRadiologyReport(ResourceReference report)
    {
        if (report == null)
            throw new ArgumentNullException(nameof(report));

        _radiologyReports.Add(report);
        return this;
    }

    #endregion

    #region PCWG3-Specific Methods

    /// <summary>
    ///     Sets the description of initial lesions detected (PCWG3)
    /// </summary>
    /// <param name="initialLesions">Description of initial lesions (e.g., "new lesions")</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithInitialLesions(string? initialLesions)
    {
        _initialLesions = initialLesions;
        return this;
    }

    /// <summary>
    ///     Sets the description of additional lesions detected in confirmation scan (PCWG3)
    /// </summary>
    /// <param name="additionalLesions">Description of additional lesions</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithAdditionalLesions(string? additionalLesions)
    {
        _additionalLesions = additionalLesions;
        return this;
    }

    /// <summary>
    ///     Sets the time between initial and confirmation scans (PCWG3)
    /// </summary>
    /// <param name="timeBetweenScans">Time interval description (e.g., "12 weeks")</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithTimeBetweenScans(string? timeBetweenScans)
    {
        _timeBetweenScans = timeBetweenScans;
        return this;
    }

    /// <summary>
    ///     Sets the initial scan date when baseline bone lesions were identified (PCWG3)
    /// </summary>
    /// <param name="initialScanDate">The initial scan date</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithInitialScanDate(DateTime? initialScanDate)
    {
        _initialScanDate = initialScanDate;
        return this;
    }

    /// <summary>
    ///     Sets the number/description of confirmation lesions (PCWG3)
    /// </summary>
    /// <param name="confirmationLesions">Description of confirmation lesions</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithConfirmationLesions(string? confirmationLesions)
    {
        _confirmationLesions = confirmationLesions;
        return this;
    }

    /// <summary>
    ///     Adds evidence supporting this observation (PCWG3)
    /// </summary>
    /// <param name="reference">The evidence resource reference</param>
    /// <param name="display">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder AddEvidence(ResourceReference reference,
        string? display = null)
    {
        if (reference != null)
        {
            if (!string.IsNullOrWhiteSpace(display) && string.IsNullOrWhiteSpace(reference.Display))
                reference.Display = display;
            _evidenceReferences.Add(reference);
        }

        return this;
    }

    /// <summary>
    ///     Adds evidence supporting this observation (PCWG3)
    /// </summary>
    /// <param name="referenceString">The evidence reference string</param>
    /// <param name="display">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder AddEvidence(string referenceString,
        string? display = null)
    {
        if (!string.IsNullOrWhiteSpace(referenceString))
        {
            var reference = new ResourceReference
            {
                Reference = referenceString,
                Display = display
            };
            _evidenceReferences.Add(reference);
        }

        return this;
    }

    #endregion

    #region RECIST-Specific Methods

    /// <summary>
    ///     Sets the RECIST criteria version for this assessment (RECIST)
    /// </summary>
    /// <param name="criteria">The RECIST criteria version (e.g., "1.1")</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithRecistCriteria(string criteria)
    {
        if (string.IsNullOrWhiteSpace(criteria))
            throw new ArgumentException("Criteria cannot be null or empty", nameof(criteria));

        _criteriaVersion = criteria;
        return this;
    }

    /// <summary>
    ///     Sets the overall RECIST 1.1 response category using NCI terminology (RECIST)
    /// </summary>
    /// <param name="nciCode">The NCI code (e.g., "C35571" for Progressive Disease)</param>
    /// <param name="display">The display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithRecistResponse(string nciCode, string display)
    {
        if (string.IsNullOrWhiteSpace(nciCode))
            throw new ArgumentException("NCI code cannot be null or empty", nameof(nciCode));
        if (string.IsNullOrWhiteSpace(display))
            throw new ArgumentException("Display cannot be null or empty", nameof(display));

        _recistResponse = FhirCodingHelper.CreateNciConcept(nciCode, display);
        return this;
    }

    /// <summary>
    ///     Sets the body site for tumor location using SNOMED codes (RECIST)
    /// </summary>
    /// <param name="snomedCode">The SNOMED code for the body site</param>
    /// <param name="display">The display text for the body site</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithBodySite(string snomedCode, string display)
    {
        if (string.IsNullOrWhiteSpace(snomedCode))
            throw new ArgumentException("SNOMED code cannot be null or empty", nameof(snomedCode));
        if (string.IsNullOrWhiteSpace(display))
            throw new ArgumentException("Display cannot be null or empty", nameof(display));

        _bodySite = FhirCodingHelper.CreateSnomedConcept(snomedCode, display);
        return this;
    }

    /// <summary>
    ///     Sets the measurement change description (RECIST)
    /// </summary>
    /// <param name="measurementChange">Description of measurement changes</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithMeasurementChange(string? measurementChange)
    {
        _measurementChange = measurementChange;
        return this;
    }

    /// <summary>
    ///     Sets the imaging type used for assessment (RECIST)
    /// </summary>
    /// <param name="imagingType">Type of imaging (e.g., "CT", "MRI")</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithImagingType(string? imagingType)
    {
        _imagingType = imagingType;
        return this;
    }

    /// <summary>
    ///     Sets the imaging date when the assessment was performed (RECIST)
    /// </summary>
    /// <param name="imagingDate">The imaging date</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithImagingDate(DateTime? imagingDate)
    {
        _imagingDate = imagingDate;
        return this;
    }

    /// <summary>
    ///     Sets the RECIST timepoints JSON data for this observation (RECIST)
    ///     This stores the complete RECIST assessment timepoints data structure including
    ///     baseline and follow-up measurements, lesion tracking, and response assessments.
    /// </summary>
    /// <param name="timepointsJson">The RECIST timepoints data as JSON string</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder WithRecistTimepointsJson(string? timepointsJson)
    {
        _recistTimepointsJson = timepointsJson;
        return this;
    }

    /// <summary>
    ///     Adds a component with a Quantity value (for numeric measurements) (RECIST)
    /// </summary>
    /// <param name="code">The LOINC or SNOMED code for the component</param>
    /// <param name="value">The quantity value</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder AddComponent(string code, Quantity value)
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
    ///     Adds a component with a boolean value (for yes/no indicators) (RECIST)
    /// </summary>
    /// <param name="code">The LOINC or SNOMED code for the component</param>
    /// <param name="value">The boolean value</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder AddComponent(string code, bool value)
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
    ///     Adds a component with a CodeableConcept value (for coded responses) (RECIST)
    /// </summary>
    /// <param name="code">The LOINC or SNOMED code for the component</param>
    /// <param name="value">The CodeableConcept value</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicObservationBuilder AddComponent(string code, CodeableConcept value)
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

    #endregion

    #region Validation and Build

    /// <summary>
    ///     Validates that required fields are set before building
    /// </summary>
    protected override void ValidateRequiredFields()
    {
        if (_patientReference == null)
            throw new InvalidOperationException(
                "Patient reference is required. Call WithPatient() before Build().");

        if (_deviceReference == null)
            throw new InvalidOperationException(
                "Device reference is required. Call WithDevice() before Build().");
    }

    /// <summary>
    ///     Builds the Radiographic Progression Observation
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
                new()
                {
                    Coding = new List<Coding>
                    {
                        new()
                        {
                            System = "http://terminology.hl7.org/CodeSystem/observation-category",
                            Code = "imaging",
                            Display = "Imaging"
                        }
                    }
                }
            },

            // Code varies by standard
            Code = CreateObservationCode(),

            // Focus
            Focus = _focusReferences.Any() ? _focusReferences : null,

            // Subject (Patient)
            Subject = _patientReference,

            // Device
            Device = _deviceReference,

            // Effective date/time
            Effective = _effectiveDate ?? new FhirDateTime(DateTimeOffset.Now),

            // Value
            Value = CreateProgressionValue()
        };

        // Add body site if set (RECIST)
        if (_bodySite != null) observation.BodySite = _bodySite;

        // Add method based on standard
        observation.Method = CreateMethodCodeableConcept();

        // Add derivedFrom references
        BuildDerivedFromReferences(observation);

        // Add components
        AddStandardComponents();
        if (_components.Any()) observation.Component = _components;

        // Add supporting facts as extensions
        if (_supportingFacts.Any())
        {
            List<Extension> factExtensions
                = ClinicalFactExtension.CreateExtensions(_supportingFacts);
            observation.Extension.AddRange(factExtensions);
        }

        // Add conflicting facts as extensions
        if (_conflictingFacts.Any())
        {
            List<Extension> conflictingFactExtensions
                = CreateConflictingFactExtensions(_conflictingFacts);
            observation.Extension.AddRange(conflictingFactExtensions);
        }

        // Add RECIST timepoints JSON extension if provided (RECIST)
        if (!string.IsNullOrWhiteSpace(_recistTimepointsJson))
        {
            observation.Extension.Add(RecistTimepointsExtension.CreateExtension(_recistTimepointsJson));
        }

        // Add notes
        if (_notes.Any())
        {
            observation.Note.Clear();
            observation.Note.AddRange(_notes.Select(noteText => new Annotation
            {
                Time = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                Text = new Markdown(noteText)
            }));
        }

        return observation;
    }

    #endregion

    #region Private Helper Methods

    private CodeableConcept CreateObservationCode()
    {
        return _standard switch
        {
            RadiographicStandard.PCWG3 => new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new()
                    {
                        System = FhirCodingHelper.Systems.LOINC_SYSTEM,
                        Code = "44667-7",
                        Display = "Bone scan findings"
                    }
                },
                Text = "PCWG3 bone scan progression assessment"
            },
            RadiographicStandard.RECIST_1_1 => new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new()
                    {
                        System = FhirCodingHelper.Systems.LOINC_SYSTEM,
                        Code = "21976-6",
                        Display = "Cancer disease status"
                    },
                    new()
                    {
                        System = FhirCodingHelper.Systems.NCI_SYSTEM,
                        Code = "C111544",
                        Display = "RECIST 1.1"
                    }
                },
                Text = "RECIST 1.1 progression assessment"
            },
            RadiographicStandard.Observed => new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new()
                    {
                        System = FhirCodingHelper.Systems.LOINC_SYSTEM,
                        Code = "59462-2",
                        Display = "Imaging study Observations"
                    }
                },
                Text = "Observed radiographic progression"
            },
            _ => throw new InvalidOperationException($"Unsupported standard: {_standard}")
        };
    }

    private CodeableConcept? CreateProgressionValue()
    {
        if (string.IsNullOrWhiteSpace(_determination))
        {
            // For RECIST, use _recistResponse if available
            if (_standard == RadiographicStandard.RECIST_1_1 && _recistResponse != null)
                return _recistResponse;

            return null; // No value if determination not set
        }

        // Support RECIST response categories: CR, PR, SD, PD, Baseline, Inconclusive
        return _determination switch
        {
            // Complete Response
            "CR" => FhirCodingHelper.CreateSnomedConcept(
                "268910001",
                "Complete response"),

            // Partial Response
            "PR" => FhirCodingHelper.CreateSnomedConcept(
                "268905007",
                "Partial response"),

            // Stable Disease
            "SD" => FhirCodingHelper.CreateSnomedConcept(
                "359746009",
                "Stable disease"),

            // Progressive Disease
            "PD" => FhirCodingHelper.CreateSnomedConcept(
                "277022003",
                "Progressive disease"),

            // Baseline
            "Baseline" => FhirCodingHelper.CreateSnomedConcept(
                "261935009",
                "Baseline (qualifier value)"),

            // Inconclusive
            "Inconclusive" => FhirCodingHelper.CreateSnomedConcept(
                "419984006",
                "Inconclusive (qualifier value)"),

            // Unknown value
            _ => throw new ArgumentException(
                $"Invalid determination value: {_determination}. Must be one of: CR, PR, SD, PD, Baseline, Inconclusive.",
                nameof(_determination))
        };
    }

    /// <summary>
    ///     Creates a CodeableConcept for observed changes with appropriate SNOMED codes
    /// </summary>
    private CodeableConcept? CreateObservedChangesValue()
    {
        if (string.IsNullOrWhiteSpace(_observedChanges))
            return null;

        // Map common observed changes values to SNOMED codes
        return _observedChanges.Trim().ToLowerInvariant() switch
        {
            "progression" => FhirCodingHelper.CreateSnomedConcept(
                "444391001",
                "Malignant tumor progression (finding)"),
            "stable" => FhirCodingHelper.CreateSnomedConcept(
                "713837000",
                "Neoplasm stable (finding)"),
            "regression" => FhirCodingHelper.CreateSnomedConcept(
                "265743007",
                "Regression of neoplasm (finding)"),
            // Fallback to text-only CodeableConcept for unmapped values
            _ => new CodeableConcept { Text = _observedChanges }
        };
    }

    private CodeableConcept CreateMethodCodeableConcept()
    {
        return _standard switch
        {
            RadiographicStandard.PCWG3 => new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new()
                    {
                        System = Configuration.CriteriaSystem,
                        Code = "pcwg3-bone-progression",
                        Display = "PCWG3 Bone Scan Progression Criteria"
                    }
                },
                Text = "PCWG3 Bone Scan Progression Criteria"
            },
            RadiographicStandard.RECIST_1_1 => new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new()
                    {
                        System = Configuration.CriteriaSystem,
                        Code = "recist-1.1",
                        Display = "RECIST 1.1"
                    }
                },
                Text = "RECIST 1.1"
            },
            RadiographicStandard.Observed => new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new()
                    {
                        System = Configuration.CriteriaSystem,
                        Code = "observed-radiographic",
                        Display = "Observed Radiographic Assessment"
                    }
                },
                Text = "Observed Radiographic Assessment"
            },
            _ => throw new InvalidOperationException($"Unsupported standard: {_standard}")
        };
    }

    private void BuildDerivedFromReferences(Observation observation)
    {
        var allDerivedFrom = new List<ResourceReference>();

        // Add evidence references (PCWG3)
        allDerivedFrom.AddRange(_evidenceReferences);

        // Add radiology reports (RECIST)
        allDerivedFrom.AddRange(_radiologyReports);

        // Add base derivedFrom references
        allDerivedFrom.AddRange(DerivedFromReferences);

        if (allDerivedFrom.Any())
        {
            // Deduplicate by Reference string (case-sensitive)
            // Take first occurrence to preserve original display text
            var uniqueReferences = allDerivedFrom
                .GroupBy(r => r.Reference)
                .Select(g => g.First())
                .ToList();

            observation.DerivedFrom.Clear();
            observation.DerivedFrom.AddRange(uniqueReferences);
        }
    }

    private void AddStandardComponents()
    {
        // Common components
        AddConfidenceComponent();
        AddDeterminationComponent();
        AddConfidenceRationaleComponent();
        AddSummaryComponent();

        // Standard-specific components
        switch (_standard)
        {
            case RadiographicStandard.PCWG3:
                AddPcwg3SpecificComponents();
                break;
            case RadiographicStandard.RECIST_1_1:
                AddRecistSpecificComponents();
                break;
            case RadiographicStandard.Observed:
                AddObservedSpecificComponents();
                break;
        }
    }

    private void AddConfidenceComponent()
    {
        if (_confidence.HasValue)
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new()
                        {
                            System = "http://loinc.org",
                            Code = "LA11892-6",
                            Display = "Probability"
                        }
                    },
                    Text = "AI Confidence Score"
                },
                Value = new Quantity
                {
                    Value = (decimal)_confidence.Value,
                    Unit = "probability",
                    System = "http://unitsofmeasure.org",
                    Code = "1"
                }
            });
    }

    private void AddDeterminationComponent()
    {
        if (!string.IsNullOrWhiteSpace(_determination))
        {
            var componentSystem = GetComponentSystem();
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new()
                        {
                            System = componentSystem,
                            Code = "determination",
                            Display = "Determination"
                        }
                    }
                },
                Value = new FhirString(_determination)
            });
        }
    }

    private void AddConfidenceRationaleComponent()
    {
        if (!string.IsNullOrWhiteSpace(_confidenceRationale))
        {
            var componentSystem = GetComponentSystem();
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new()
                        {
                            System = componentSystem,
                            Code = "confidence-rationale",
                            Display = "Confidence Rationale"
                        }
                    }
                },
                Value = new FhirString(_confidenceRationale)
            });
        }
    }

    private void AddSummaryComponent()
    {
        if (!string.IsNullOrWhiteSpace(_summary))
        {
            var componentSystem = GetComponentSystem();
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new()
                        {
                            System = componentSystem,
                            Code = "summary",
                            Display = "Assessment Summary"
                        }
                    }
                },
                Value = new FhirString(_summary)
            });
        }
    }

    private void AddPcwg3SpecificComponents()
    {
        const string componentSystem = "http://thirdopinion.ai/fhir/CodeSystem/pcwg3-components";

        // Add initial lesions component
        if (!string.IsNullOrWhiteSpace(_initialLesions))
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new()
                        {
                            System = componentSystem,
                            Code = "initial-lesions",
                            Display = "Initial Lesions"
                        }
                    }
                },
                Value = new FhirString(_initialLesions)
            });

        // Add confirmation date component
        if (_confirmationDate.HasValue)
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new()
                        {
                            System = componentSystem,
                            Code = "confirmation-date",
                            Display = "Confirmation Date"
                        }
                    }
                },
                Value = new FhirDateTime(_confirmationDate.Value)
            });

        // Add additional lesions component
        if (!string.IsNullOrWhiteSpace(_additionalLesions))
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new()
                        {
                            System = componentSystem,
                            Code = "additional-lesions",
                            Display = "Additional Lesions"
                        }
                    }
                },
                Value = new FhirString(_additionalLesions)
            });

        // Add time between scans component
        if (!string.IsNullOrWhiteSpace(_timeBetweenScans))
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new()
                        {
                            System = componentSystem,
                            Code = "time-between-scans",
                            Display = "Time Between Scans"
                        }
                    }
                },
                Value = new FhirString(_timeBetweenScans)
            });

        // Add initial scan date component
        if (_initialScanDate.HasValue)
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new()
                        {
                            System = componentSystem,
                            Code = "initial-scan-date",
                            Display = "Initial Scan Date"
                        }
                    }
                },
                Value = new FhirDateTime(_initialScanDate.Value)
            });

        // Add confirmation lesions component
        if (!string.IsNullOrWhiteSpace(_confirmationLesions))
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new()
                        {
                            System = componentSystem,
                            Code = "confirmation-lesions",
                            Display = "Confirmation Lesions"
                        }
                    }
                },
                Value = new FhirString(_confirmationLesions)
            });
    }

    private void AddRecistSpecificComponents()
    {
        const string componentSystem = "http://thirdopinion.ai/fhir/CodeSystem/recist-components";

        // Add measurement change component
        if (!string.IsNullOrWhiteSpace(_measurementChange))
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new()
                        {
                            System = componentSystem,
                            Code = "measurement-change",
                            Display = "Measurement Change"
                        }
                    }
                },
                Value = new FhirString(_measurementChange)
            });

        // Add imaging type component
        if (!string.IsNullOrWhiteSpace(_imagingType))
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new()
                        {
                            System = componentSystem,
                            Code = "imaging-type",
                            Display = "Imaging Type"
                        }
                    }
                },
                Value = new FhirString(_imagingType)
            });

        // Add imaging date component
        if (_imagingDate.HasValue)
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new()
                        {
                            System = componentSystem,
                            Code = "imaging-date",
                            Display = "Imaging Date"
                        }
                    }
                },
                Value = new FhirDateTime(_imagingDate.Value)
            });
    }

    private void AddObservedSpecificComponents()
    {
        const string componentSystem = "http://thirdopinion.ai/fhir/CodeSystem/radiographic-components";

        // Add observed changes component with SNOMED-coded value
        if (!string.IsNullOrWhiteSpace(_observedChanges))
        {
            var observedChangesValue = CreateObservedChangesValue();
            if (observedChangesValue != null)
                _components.Add(new Observation.ComponentComponent
                {
                    Code = new CodeableConcept
                    {
                        Coding = new List<Coding>
                        {
                            new()
                            {
                                System = componentSystem,
                                Code = "observed-changes",
                                Display = "Observed Changes"
                            }
                        }
                    },
                    Value = observedChangesValue
                });
        }
    }

    private string GetComponentSystem()
    {
        return _standard switch
        {
            RadiographicStandard.PCWG3 => "http://thirdopinion.ai/fhir/CodeSystem/pcwg3-components",
            RadiographicStandard.RECIST_1_1 => "http://thirdopinion.ai/fhir/CodeSystem/recist-components",
            _ => "http://thirdopinion.ai/fhir/CodeSystem/radiographic-components"
        };
    }

    private CodeableConcept CreateComponentCode(string code)
    {
        // Determine system based on code format/content
        if (code.Contains("-") && char.IsDigit(code[0])) // LOINC format
            return GetLoincComponentCode(code);

        if (char.IsDigit(code[0])) // SNOMED format
            return FhirCodingHelper.CreateSnomedConcept(code, GetSnomedDisplayForCode(code));

        // Default to custom system for other codes
        return new CodeableConcept
        {
            Coding = new List<Coding>
            {
                new()
                {
                    System = GetComponentSystem(),
                    Code = code,
                    Display = GetDefaultDisplayForCode(code)
                }
            }
        };
    }

    private CodeableConcept GetLoincComponentCode(string code)
    {
        string display = code switch
        {
            "33359-2" => "Percent change",
            "33728-8" => "Sum of longest diameters",
            "44666-9" => "New lesions",
            _ => "RECIST measurement"
        };

        return FhirCodingHelper.CreateLoincConcept(code, display);
    }

    private string GetSnomedDisplayForCode(string code)
    {
        return code switch
        {
            "371508000" => "Sum of longest diameters",
            "260405006" => "Absolute change",
            _ => "RECIST measurement"
        };
    }

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

    private List<Extension> CreateConflictingFactExtensions(IEnumerable<Fact> facts)
    {
        var extensions = new List<Extension>();

        foreach (Fact fact in facts.Where(f => f != null))
        {
            var extension = new Extension
            {
                Url = "https://thirdopinion.io/conflicting-fact"
            };

            // Add fact GUID
            if (!string.IsNullOrWhiteSpace(fact.factGuid))
                extension.Extension.Add(new Extension("factGuid", new FhirString(fact.factGuid)));

            // Add document reference
            if (!string.IsNullOrWhiteSpace(fact.factDocumentReference))
                extension.Extension.Add(new Extension("factDocumentReference",
                    new FhirString(fact.factDocumentReference)));

            // Add fact type
            if (!string.IsNullOrWhiteSpace(fact.type))
                extension.Extension.Add(new Extension("type", new FhirString(fact.type)));

            // Add fact text
            if (!string.IsNullOrWhiteSpace(fact.fact))
                extension.Extension.Add(new Extension("fact", new FhirString(fact.fact)));

            // Add references
            if (fact.@ref != null && fact.@ref.Any())
                foreach (string reference in fact.@ref)
                    extension.Extension.Add(new Extension("ref", new FhirString(reference)));

            // Add time reference
            if (!string.IsNullOrWhiteSpace(fact.timeRef))
                extension.Extension.Add(new Extension("timeRef", new FhirString(fact.timeRef)));

            // Add relevance
            if (!string.IsNullOrWhiteSpace(fact.relevance))
                extension.Extension.Add(new Extension("relevance", new FhirString(fact.relevance)));

            extensions.Add(extension);
        }

        return extensions;
    }

    #endregion
}
