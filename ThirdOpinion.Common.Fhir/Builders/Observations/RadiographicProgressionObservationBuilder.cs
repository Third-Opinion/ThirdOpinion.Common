using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Builders.Base;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Extensions;
using ThirdOpinion.Common.Fhir.Helpers;
using ThirdOpinion.Common.Fhir.Models;

namespace ThirdOpinion.Common.Fhir.Builders.Observations;

/// <summary>
///     Builder for creating FHIR Observations for radiographic disease progression assessment
///     when RECIST criteria are not applicable (e.g., insufficient baseline measurements)
/// </summary>
public class RadiographicProgressionObservationBuilder : AiResourceBuilderBase<Observation>
{
    private readonly List<Observation.ComponentComponent> _components;
    private readonly List<Fact> _conflictingFacts;
    private readonly List<ResourceReference> _focusReferences;
    private readonly List<ResourceReference> _imagingStudies;
    private readonly List<Annotation> _notes;
    private readonly List<ResourceReference> _radiologyReports;
    private readonly List<Fact> _supportingFacts;
    private CodeableConcept? _bodySite;
    private float? _confidence;
    private string? _confidenceRationale;
    private ResourceReference? _deviceReference;
    private DateTime? _imagingDate;
    private string? _imagingType;
    private ResourceReference? _patientReference;
    private CodeableConcept? _progressionStatus;
    private string? _qualitativeAssessment;

    /// <summary>
    ///     Creates a new Radiographic Progression Observation builder
    /// </summary>
    /// <param name="configuration">The AI inference configuration</param>
    public RadiographicProgressionObservationBuilder(AiInferenceConfiguration configuration)
        : base(configuration)
    {
        _focusReferences = new List<ResourceReference>();
        _imagingStudies = new List<ResourceReference>();
        _radiologyReports = new List<ResourceReference>();
        _components = new List<Observation.ComponentComponent>();
        _supportingFacts = new List<Fact>();
        _conflictingFacts = new List<Fact>();
        _notes = new List<Annotation>();
    }

    /// <summary>
    ///     Override base class methods to maintain fluent interface
    /// </summary>
    public new RadiographicProgressionObservationBuilder WithInferenceId(string id)
    {
        base.WithInferenceId(id);
        return this;
    }

    /// <summary>
    ///     Override base class methods to maintain fluent interface
    /// </summary>
    public new RadiographicProgressionObservationBuilder WithCriteria(string id,
        string display,
        string? system = null)
    {
        base.WithCriteria(id, display, system);
        return this;
    }

    /// <summary>
    ///     Override base class methods to maintain fluent interface
    /// </summary>
    public new RadiographicProgressionObservationBuilder AddDerivedFrom(ResourceReference reference)
    {
        base.AddDerivedFrom(reference);
        return this;
    }

    /// <summary>
    ///     Override base class methods to maintain fluent interface
    /// </summary>
    public new RadiographicProgressionObservationBuilder AddDerivedFrom(string reference,
        string? display = null)
    {
        base.AddDerivedFrom(reference, display);
        return this;
    }

    /// <summary>
    ///     Sets the patient reference for this observation
    /// </summary>
    /// <param name="patient">The patient resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicProgressionObservationBuilder WithPatient(ResourceReference patient)
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
    public RadiographicProgressionObservationBuilder WithPatient(string patientId, string? display = null)
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
    public RadiographicProgressionObservationBuilder WithDevice(ResourceReference device)
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
    public RadiographicProgressionObservationBuilder WithDevice(string deviceId, string? display = null)
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
    ///     Sets the focus references for this observation (tumors/lesions being assessed)
    /// </summary>
    /// <param name="focuses">The focus resource references</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicProgressionObservationBuilder WithFocus(params ResourceReference[] focuses)
    {
        if (focuses == null || focuses.Length == 0)
            throw new ArgumentException("At least one focus reference is required",
                nameof(focuses));

        _focusReferences.Clear();
        _focusReferences.AddRange(focuses.Where(f => f != null));
        return this;
    }

    /// <summary>
    ///     Sets the focus reference for a single condition/tumor
    /// </summary>
    /// <param name="conditionId">The condition ID</param>
    /// <param name="display">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicProgressionObservationBuilder WithFocus(string conditionId, string? display = null)
    {
        if (string.IsNullOrWhiteSpace(conditionId))
            throw new ArgumentException("Condition ID cannot be null or empty", nameof(conditionId));

        _focusReferences.Clear();
        _focusReferences.Add(new ResourceReference
        {
            Reference = conditionId.StartsWith("Condition/") ? conditionId : $"Condition/{conditionId}",
            Display = display
        });
        return this;
    }

    /// <summary>
    ///     Sets the progression status using SNOMED codes
    /// </summary>
    /// <param name="snomedCode">The SNOMED code (e.g., "162573006" for progression detected, "260415000" for not detected)</param>
    /// <param name="display">The display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicProgressionObservationBuilder WithProgressionStatus(string snomedCode, string display)
    {
        if (string.IsNullOrWhiteSpace(snomedCode))
            throw new ArgumentException("SNOMED code cannot be null or empty", nameof(snomedCode));
        if (string.IsNullOrWhiteSpace(display))
            throw new ArgumentException("Display cannot be null or empty", nameof(display));

        _progressionStatus = FhirCodingHelper.CreateSnomedConcept(snomedCode, display);
        return this;
    }

    /// <summary>
    ///     Sets progression detected (convenience method)
    /// </summary>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicProgressionObservationBuilder WithProgressionDetected()
    {
        _progressionStatus = FhirCodingHelper.CreateSnomedConcept("162573006", "Progression of disease");
        return this;
    }

    /// <summary>
    ///     Sets no progression detected (convenience method)
    /// </summary>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicProgressionObservationBuilder WithNoProgressionDetected()
    {
        _progressionStatus = FhirCodingHelper.CreateSnomedConcept("260415000", "Not detected");
        return this;
    }

    /// <summary>
    ///     Adds a note/annotation to the observation
    /// </summary>
    /// <param name="text">The note text</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicProgressionObservationBuilder AddNote(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Note text cannot be null or empty", nameof(text));

        _notes.Add(new Annotation
        {
            Text = text
        });
        return this;
    }

    /// <summary>
    ///     Adds a component with a Quantity value (for numeric measurements)
    /// </summary>
    /// <param name="code">The LOINC or SNOMED code for the component</param>
    /// <param name="value">The quantity value</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicProgressionObservationBuilder AddComponent(string code, Quantity value)
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
    ///     Adds a component with a boolean value (for yes/no indicators)
    /// </summary>
    /// <param name="code">The LOINC or SNOMED code for the component</param>
    /// <param name="value">The boolean value</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicProgressionObservationBuilder AddComponent(string code, bool value)
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
    ///     Adds a component with a CodeableConcept value (for coded responses)
    /// </summary>
    /// <param name="code">The LOINC or SNOMED code for the component</param>
    /// <param name="value">The CodeableConcept value</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicProgressionObservationBuilder AddComponent(string code, CodeableConcept value)
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
    ///     Adds an imaging study reference to derivedFrom
    /// </summary>
    /// <param name="imagingStudy">The imaging study resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicProgressionObservationBuilder AddImagingStudy(ResourceReference imagingStudy)
    {
        if (imagingStudy == null)
            throw new ArgumentNullException(nameof(imagingStudy));

        _imagingStudies.Add(imagingStudy);
        return this;
    }

    /// <summary>
    ///     Adds an imaging study reference to derivedFrom
    /// </summary>
    /// <param name="imagingStudyId">The imaging study ID</param>
    /// <param name="display">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicProgressionObservationBuilder AddImagingStudy(string imagingStudyId, string? display = null)
    {
        if (string.IsNullOrWhiteSpace(imagingStudyId))
            throw new ArgumentException("Imaging study ID cannot be null or empty", nameof(imagingStudyId));

        _imagingStudies.Add(new ResourceReference
        {
            Reference = imagingStudyId.StartsWith("ImagingStudy/")
                ? imagingStudyId
                : $"ImagingStudy/{imagingStudyId}",
            Display = display
        });
        return this;
    }

    /// <summary>
    ///     Adds a radiology report reference to derivedFrom
    /// </summary>
    /// <param name="report">The radiology report resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicProgressionObservationBuilder AddRadiologyReport(ResourceReference report)
    {
        if (report == null)
            throw new ArgumentNullException(nameof(report));

        _radiologyReports.Add(report);
        return this;
    }

    /// <summary>
    ///     Adds a radiology report reference to derivedFrom
    /// </summary>
    /// <param name="reportId">The radiology report ID</param>
    /// <param name="display">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicProgressionObservationBuilder AddRadiologyReport(string reportId, string? display = null)
    {
        if (string.IsNullOrWhiteSpace(reportId))
            throw new ArgumentException("Report ID cannot be null or empty", nameof(reportId));

        _radiologyReports.Add(new ResourceReference
        {
            Reference = reportId.StartsWith("DocumentReference/")
                ? reportId
                : $"DocumentReference/{reportId}",
            Display = display
        });
        return this;
    }

    /// <summary>
    ///     Adds body site(s) for tumor location using SNOMED codes
    /// </summary>
    /// <param name="snomedCode">The SNOMED code for the body site</param>
    /// <param name="display">The display text for the body site</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicProgressionObservationBuilder AddBodySite(string snomedCode, string display)
    {
        if (string.IsNullOrWhiteSpace(snomedCode))
            throw new ArgumentException("SNOMED code cannot be null or empty", nameof(snomedCode));
        if (string.IsNullOrWhiteSpace(display))
            throw new ArgumentException("Display cannot be null or empty", nameof(display));

        // For multiple body sites, we'll store them as components
        _components.Add(new Observation.ComponentComponent
        {
            Code = new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new()
                    {
                        System = "http://snomed.info/sct",
                        Code = "363698007",
                        Display = "Finding site"
                    }
                }
            },
            Value = FhirCodingHelper.CreateSnomedConcept(snomedCode, display)
        });

        // Also store the first one as the main bodySite
        if (_bodySite == null)
            _bodySite = FhirCodingHelper.CreateSnomedConcept(snomedCode, display);

        return this;
    }

    /// <summary>
    ///     Sets the AI confidence score for this observation
    /// </summary>
    /// <param name="confidence">The confidence score (0.0 to 1.0)</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicProgressionObservationBuilder WithConfidence(float confidence)
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
    public RadiographicProgressionObservationBuilder WithConfidenceRationale(string? confidenceRationale)
    {
        _confidenceRationale = confidenceRationale;
        return this;
    }

    /// <summary>
    ///     Sets the qualitative assessment description
    /// </summary>
    /// <param name="assessment">Description of the qualitative assessment</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicProgressionObservationBuilder WithQualitativeAssessment(string? assessment)
    {
        _qualitativeAssessment = assessment;
        return this;
    }

    /// <summary>
    ///     Sets the imaging type used for assessment
    /// </summary>
    /// <param name="imagingType">Type of imaging (e.g., "CT", "MRI", "PET")</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicProgressionObservationBuilder WithImagingType(string? imagingType)
    {
        _imagingType = imagingType;
        return this;
    }

    /// <summary>
    ///     Sets the imaging date when the assessment was performed
    /// </summary>
    /// <param name="imagingDate">The imaging date</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicProgressionObservationBuilder WithImagingDate(DateTime? imagingDate)
    {
        _imagingDate = imagingDate;
        return this;
    }

    /// <summary>
    ///     Adds supporting clinical facts as evidence
    /// </summary>
    /// <param name="facts">Array of supporting clinical facts</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicProgressionObservationBuilder WithSupportingFacts(params Fact[] facts)
    {
        if (facts != null && facts.Length > 0)
        {
            _supportingFacts.AddRange(facts.Where(f => f != null));

            // Add document references as evidence
            foreach (Fact fact in facts.Where(f =>
                         f != null && !string.IsNullOrWhiteSpace(f.factDocumentReference)))
                AddRadiologyReport(new ResourceReference(fact.factDocumentReference,
                    $"Supporting fact: {fact.type}"));
        }

        return this;
    }

    /// <summary>
    ///     Adds conflicting clinical facts that may contradict the assessment
    /// </summary>
    /// <param name="facts">Array of conflicting clinical facts</param>
    /// <returns>This builder instance for method chaining</returns>
    public RadiographicProgressionObservationBuilder WithConflictingFacts(params Fact[] facts)
    {
        if (facts != null && facts.Length > 0)
            _conflictingFacts.AddRange(facts.Where(f => f != null));
        return this;
    }

    /// <summary>
    ///     Validates that required fields are set before building
    /// </summary>
    protected override void ValidateRequiredFields()
    {
        if (_patientReference == null)
            throw new InvalidOperationException(
                "Patient reference is required. Call WithPatient() before Build().");

        if (_progressionStatus == null)
            throw new InvalidOperationException(
                "Progression status is required. Call WithProgressionStatus(), WithProgressionDetected(), or WithNoProgressionDetected() before Build().");
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

            // Code: SNOMED 246455001 (Recurrence status)
            Code = new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new()
                    {
                        System = "http://snomed.info/sct",
                        Code = "246455001",
                        Display = "Recurrence status"
                    }
                },
                Text = "Radiographic Disease Progression Assessment"
            },

            // Focus (tumors/lesions being assessed)
            Focus = _focusReferences.Any() ? _focusReferences : null,

            // Subject (Patient)
            Subject = _patientReference,

            // Device (if provided)
            Device = _deviceReference,

            // Effective date/time
            Effective = _imagingDate.HasValue
                ? new FhirDateTime(_imagingDate.Value)
                : new FhirDateTime(DateTimeOffset.Now),

            // Value: Progression status (detected or not detected)
            Value = _progressionStatus,

            // Method: Qualitative radiographic assessment
            Method = new CodeableConcept
            {
                Text = "Qualitative radiographic assessment (RECIST criteria not applicable)"
            }
        };

        // Add body site if set
        if (_bodySite != null)
            observation.BodySite = _bodySite;

        // Add notes
        if (_notes.Any())
            observation.Note = _notes;

        // Add derivedFrom references (imaging studies and reports, plus base derivedFrom)
        if (_imagingStudies.Any() || _radiologyReports.Any() || DerivedFromReferences.Any())
        {
            observation.DerivedFrom = new List<ResourceReference>();
            observation.DerivedFrom.AddRange(_imagingStudies);
            observation.DerivedFrom.AddRange(_radiologyReports);
            observation.DerivedFrom.AddRange(DerivedFromReferences);
        }

        // Add confidence component if specified
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

        // Add enhanced components
        AddEnhancedComponents();

        // Add components
        if (_components.Any())
            observation.Component = _components;

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

        return observation;
    }

    /// <summary>
    ///     Adds enhanced components for additional fields
    /// </summary>
    private void AddEnhancedComponents()
    {
        // Add qualitative assessment component
        if (!string.IsNullOrWhiteSpace(_qualitativeAssessment))
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new()
                        {
                            System = "http://thirdopinion.ai/fhir/CodeSystem/radiographic-components",
                            Code = "qualitative-assessment",
                            Display = "Qualitative Assessment"
                        }
                    }
                },
                Value = new FhirString(_qualitativeAssessment)
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
                            System = "http://thirdopinion.ai/fhir/CodeSystem/radiographic-components",
                            Code = "imaging-type",
                            Display = "Imaging Type"
                        }
                    }
                },
                Value = new FhirString(_imagingType)
            });

        // Add confidence rationale component
        if (!string.IsNullOrWhiteSpace(_confidenceRationale))
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new()
                        {
                            System = "http://thirdopinion.ai/fhir/CodeSystem/radiographic-components",
                            Code = "confidence-rationale",
                            Display = "Confidence Rationale"
                        }
                    }
                },
                Value = new FhirString(_confidenceRationale)
            });
    }

    /// <summary>
    ///     Creates a CodeableConcept for component codes, using appropriate system based on code format
    /// </summary>
    /// <param name="code">The code value</param>
    /// <returns>A CodeableConcept with appropriate system</returns>
    private CodeableConcept CreateComponentCode(string code)
    {
        // Determine system based on code format/content
        if (code.Contains("-") && char.IsDigit(code[0])) // LOINC format
            return FhirCodingHelper.CreateLoincConcept(code, code);

        if (char.IsDigit(code[0])) // SNOMED format
            return FhirCodingHelper.CreateSnomedConcept(code, code);

        // Default to custom system for other codes
        return new CodeableConcept
        {
            Coding = new List<Coding>
            {
                new()
                {
                    System = "http://thirdopinion.ai/fhir/CodeSystem/radiographic-components",
                    Code = code,
                    Display = code.Replace("-", " ").Replace("_", " ")
                }
            },
            Text = code
        };
    }

    /// <summary>
    ///     Creates FHIR Extensions for conflicting clinical facts
    /// </summary>
    /// <param name="facts">The conflicting clinical facts</param>
    /// <returns>List of FHIR Extensions for conflicting facts</returns>
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
}
