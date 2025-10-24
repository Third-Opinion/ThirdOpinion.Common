using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Builders.Base;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Extensions;
using ThirdOpinion.Common.Fhir.Helpers;
using ThirdOpinion.Common.Fhir.Models;

namespace ThirdOpinion.Common.Fhir.Builders.Observations;

/// <summary>
/// Builder for creating FHIR Observations for PCWG3 bone scan progression assessment
/// </summary>
public class Pcwg3ProgressionObservationBuilder : AiResourceBuilderBase<Observation>
{
    private ResourceReference? _patientReference;
    private ResourceReference? _deviceReference;
    private readonly List<ResourceReference> _focusReferences;
    private bool? _identified;
    private string? _initialLesions;
    private DateTime? _confirmationDate;
    private string? _additionalLesions;
    private string? _timeBetweenScans;
    private readonly List<Fact> _supportingFacts;
    private float? _confidence;
    private FhirDateTime? _effectiveDate;
    private readonly List<ResourceReference> _evidenceReferences;
    private readonly List<Observation.ComponentComponent> _components;
    private readonly List<string> _notes;

    // Latest version fields
    private string? _determination;
    private string? _confidenceRationale;
    private string? _summary;
    private DateTime? _initialScanDate;
    private string? _confirmationLesions;
    private readonly List<Fact> _conflictingFacts;

    /// <summary>
    /// Creates a new PCWG3 Progression Observation builder
    /// </summary>
    /// <param name="configuration">The AI inference configuration</param>
    public Pcwg3ProgressionObservationBuilder(AiInferenceConfiguration configuration)
        : base(configuration)
    {
        _focusReferences = new List<ResourceReference>();
        _supportingFacts = new List<Fact>();
        _evidenceReferences = new List<ResourceReference>();
        _components = new List<Observation.ComponentComponent>();
        _notes = new List<string>();
        _conflictingFacts = new List<Fact>();
    }

    /// <summary>
    /// Override base class methods to maintain fluent interface
    /// </summary>
    public new Pcwg3ProgressionObservationBuilder WithInferenceId(string id)
    {
        base.WithInferenceId(id);
        return this;
    }

    /// <summary>
    /// Override base class methods to maintain fluent interface
    /// </summary>
    public new Pcwg3ProgressionObservationBuilder WithCriteria(string id, string display, string? system = null)
    {
        base.WithCriteria(id, display, system);
        return this;
    }

    /// <summary>
    /// Override base class methods to maintain fluent interface
    /// </summary>
    public new Pcwg3ProgressionObservationBuilder AddDerivedFrom(ResourceReference reference)
    {
        base.AddDerivedFrom(reference);
        return this;
    }

    /// <summary>
    /// Override base class methods to maintain fluent interface
    /// </summary>
    public new Pcwg3ProgressionObservationBuilder AddDerivedFrom(string reference, string? display = null)
    {
        base.AddDerivedFrom(reference, display);
        return this;
    }

    /// <summary>
    /// Sets the patient reference for this observation
    /// </summary>
    /// <param name="patient">The patient resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public Pcwg3ProgressionObservationBuilder WithPatient(ResourceReference patient)
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
    public Pcwg3ProgressionObservationBuilder WithPatient(string patientId, string? display = null)
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
    public Pcwg3ProgressionObservationBuilder WithDevice(ResourceReference device)
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
    public Pcwg3ProgressionObservationBuilder WithDevice(string deviceId, string? display = null)
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
    /// Sets the focus references for this observation (conditions/tumors being assessed)
    /// </summary>
    /// <param name="focus">The focus resource references</param>
    /// <returns>This builder instance for method chaining</returns>
    public Pcwg3ProgressionObservationBuilder WithFocus(params ResourceReference[] focus)
    {
        if (focus == null || focus.Length == 0)
            throw new ArgumentException("At least one focus reference is required", nameof(focus));

        _focusReferences.Clear();
        _focusReferences.AddRange(focus.Where(f => f != null));
        return this;
    }

    /// <summary>
    /// Sets whether PCWG3 progression is identified
    /// </summary>
    /// <param name="identified">True if progression is identified, false otherwise</param>
    /// <returns>This builder instance for method chaining</returns>
    public Pcwg3ProgressionObservationBuilder WithIdentified(bool identified)
    {
        _identified = identified;
        return this;
    }

    /// <summary>
    /// Sets the description of initial lesions detected
    /// </summary>
    /// <param name="initialLesions">Description of initial lesions (e.g., "new lesions")</param>
    /// <returns>This builder instance for method chaining</returns>
    public Pcwg3ProgressionObservationBuilder WithInitialLesions(string? initialLesions)
    {
        _initialLesions = initialLesions;
        return this;
    }

    /// <summary>
    /// Sets the confirmation date for progression (per PCWG3 confirmation requirement)
    /// </summary>
    /// <param name="confirmationDate">Date when progression was confirmed</param>
    /// <returns>This builder instance for method chaining</returns>
    public Pcwg3ProgressionObservationBuilder WithConfirmationDate(DateTime? confirmationDate)
    {
        _confirmationDate = confirmationDate;
        return this;
    }

    /// <summary>
    /// Sets the description of additional lesions detected in confirmation scan
    /// </summary>
    /// <param name="additionalLesions">Description of additional lesions</param>
    /// <returns>This builder instance for method chaining</returns>
    public Pcwg3ProgressionObservationBuilder WithAdditionalLesions(string? additionalLesions)
    {
        _additionalLesions = additionalLesions;
        return this;
    }

    /// <summary>
    /// Sets the time between initial and confirmation scans
    /// </summary>
    /// <param name="timeBetweenScans">Time interval description (e.g., "12 weeks")</param>
    /// <returns>This builder instance for method chaining</returns>
    public Pcwg3ProgressionObservationBuilder WithTimeBetweenScans(string? timeBetweenScans)
    {
        _timeBetweenScans = timeBetweenScans;
        return this;
    }

    /// <summary>
    /// Adds supporting clinical facts as evidence
    /// </summary>
    /// <param name="facts">Array of supporting clinical facts</param>
    /// <returns>This builder instance for method chaining</returns>
    public Pcwg3ProgressionObservationBuilder WithSupportingFacts(params Fact[] facts)
    {
        if (facts != null && facts.Length > 0)
        {
            _supportingFacts.AddRange(facts.Where(f => f != null));

            // Add document references as evidence
            foreach (var fact in facts.Where(f => f != null && !string.IsNullOrWhiteSpace(f.factDocumentReference)))
            {
                AddEvidence(fact.factDocumentReference, $"Supporting fact: {fact.type}");
            }
        }
        return this;
    }

    /// <summary>
    /// Adds evidence supporting this observation
    /// </summary>
    /// <param name="reference">The evidence resource reference</param>
    /// <param name="display">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public Pcwg3ProgressionObservationBuilder AddEvidence(ResourceReference reference, string? display = null)
    {
        if (reference != null)
        {
            if (!string.IsNullOrWhiteSpace(display) && string.IsNullOrWhiteSpace(reference.Display))
            {
                reference.Display = display;
            }
            _evidenceReferences.Add(reference);
        }
        return this;
    }

    /// <summary>
    /// Adds evidence supporting this observation
    /// </summary>
    /// <param name="referenceString">The evidence reference string</param>
    /// <param name="display">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public Pcwg3ProgressionObservationBuilder AddEvidence(string referenceString, string? display = null)
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

    /// <summary>
    /// Sets the AI confidence score for this observation
    /// </summary>
    /// <param name="confidence">The confidence score (0.0 to 1.0)</param>
    /// <returns>This builder instance for method chaining</returns>
    public Pcwg3ProgressionObservationBuilder WithConfidence(float confidence)
    {
        if (confidence < 0.0f || confidence > 1.0f)
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be between 0.0 and 1.0");

        _confidence = confidence;
        return this;
    }

    /// <summary>
    /// Sets the effective date/time of this observation
    /// </summary>
    /// <param name="effectiveDate">The effective date/time</param>
    /// <returns>This builder instance for method chaining</returns>
    public Pcwg3ProgressionObservationBuilder WithEffectiveDate(DateTime effectiveDate)
    {
        _effectiveDate = new FhirDateTime(effectiveDate);
        return this;
    }

    /// <summary>
    /// Sets the effective date/time of this observation
    /// </summary>
    /// <param name="effectiveDate">The effective date/time as DateTimeOffset</param>
    /// <returns>This builder instance for method chaining</returns>
    public Pcwg3ProgressionObservationBuilder WithEffectiveDate(DateTimeOffset effectiveDate)
    {
        _effectiveDate = new FhirDateTime(effectiveDate);
        return this;
    }

    /// <summary>
    /// Adds a note to this observation
    /// </summary>
    /// <param name="noteText">The note text</param>
    /// <returns>This builder instance for method chaining</returns>
    public Pcwg3ProgressionObservationBuilder AddNote(string noteText)
    {
        if (!string.IsNullOrWhiteSpace(noteText))
        {
            _notes.Add(noteText);
        }
        return this;
    }

    /// <summary>
    /// Sets the determination result (e.g., "Progressive Disease", "Stable Disease", "Inconclusive")
    /// </summary>
    /// <param name="determination">The determination result</param>
    /// <returns>This builder instance for method chaining</returns>
    public Pcwg3ProgressionObservationBuilder WithDetermination(string? determination)
    {
        _determination = determination;
        return this;
    }

    /// <summary>
    /// Sets the confidence rationale explaining the confidence score reasoning
    /// </summary>
    /// <param name="confidenceRationale">The confidence rationale text</param>
    /// <returns>This builder instance for method chaining</returns>
    public Pcwg3ProgressionObservationBuilder WithConfidenceRationale(string? confidenceRationale)
    {
        _confidenceRationale = confidenceRationale;
        return this;
    }

    /// <summary>
    /// Sets the detailed summary of the PCWG3 assessment
    /// </summary>
    /// <param name="summary">The assessment summary</param>
    /// <returns>This builder instance for method chaining</returns>
    public Pcwg3ProgressionObservationBuilder WithSummary(string? summary)
    {
        _summary = summary;
        return this;
    }

    /// <summary>
    /// Sets the initial scan date when baseline bone lesions were identified
    /// </summary>
    /// <param name="initialScanDate">The initial scan date</param>
    /// <returns>This builder instance for method chaining</returns>
    public Pcwg3ProgressionObservationBuilder WithInitialScanDate(DateTime? initialScanDate)
    {
        _initialScanDate = initialScanDate;
        return this;
    }

    /// <summary>
    /// Sets the number/description of confirmation lesions
    /// </summary>
    /// <param name="confirmationLesions">Description of confirmation lesions</param>
    /// <returns>This builder instance for method chaining</returns>
    public Pcwg3ProgressionObservationBuilder WithConfirmationLesions(string? confirmationLesions)
    {
        _confirmationLesions = confirmationLesions;
        return this;
    }

    /// <summary>
    /// Adds conflicting clinical facts that may contradict the assessment
    /// </summary>
    /// <param name="facts">Array of conflicting clinical facts</param>
    /// <returns>This builder instance for method chaining</returns>
    public Pcwg3ProgressionObservationBuilder WithConflictingFacts(params Fact[] facts)
    {
        if (facts != null && facts.Length > 0)
        {
            _conflictingFacts.AddRange(facts.Where(f => f != null));
        }
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

        if (!_identified.HasValue)
        {
            throw new InvalidOperationException("Identified status is required. Call WithIdentified() before Build().");
        }
    }

    /// <summary>
    /// Builds the PCWG3 Progression Observation
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

            // Code: LOINC code for bone scan findings with PCWG3 method
            Code = new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new Coding
                    {
                        System = FhirCodingHelper.Systems.LOINC_SYSTEM,
                        Code = "44667-7",
                        Display = "Bone scan findings"
                    }
                },
                Text = "PCWG3 bone scan progression assessment"
            },

            // Focus
            Focus = _focusReferences.Any() ? _focusReferences : null,

            // Subject (Patient)
            Subject = _patientReference,

            // Device
            Device = _deviceReference,

            // Effective date/time
            Effective = _effectiveDate ?? new FhirDateTime(DateTimeOffset.Now),

            // Value: Progression status
            Value = CreateProgressionValue()
        };

        // Add method based on criteria (PCWG3)
        observation.Method = CreatePcwg3Method();

        // Add evidence to derivedFrom
        if (_evidenceReferences.Any() || DerivedFromReferences.Any())
        {
            observation.DerivedFrom.Clear();
            observation.DerivedFrom.AddRange(_evidenceReferences);
            observation.DerivedFrom.AddRange(DerivedFromReferences);
        }

        // Add components
        AddComponents();
        if (_components.Any())
        {
            observation.Component = _components;
        }

        // Add supporting facts as extensions
        if (_supportingFacts.Any())
        {
            var factExtensions = ClinicalFactExtension.CreateExtensions(_supportingFacts);
            observation.Extension.AddRange(factExtensions);
        }

        // Add conflicting facts as extensions
        if (_conflictingFacts.Any())
        {
            var conflictingFactExtensions = CreateConflictingFactExtensions(_conflictingFacts);
            observation.Extension.AddRange(conflictingFactExtensions);
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

    private CodeableConcept CreateProgressionValue()
    {
        if (_identified == true)
        {
            // Progressive disease
            return FhirCodingHelper.CreateSnomedConcept(
                "277022003",
                "Progressive disease");
        }
        else
        {
            // Stable disease
            return FhirCodingHelper.CreateSnomedConcept(
                "359746009",
                "Stable disease");
        }
    }

    private CodeableConcept CreatePcwg3Method()
    {
        string code = $"pcwg3-bone-progression-{InferenceId ?? Guid.NewGuid().ToString()}";
        string display = "PCWG3 Bone Scan Progression Criteria";

        return new CodeableConcept
        {
            Coding = new List<Coding>
            {
                new Coding
                {
                    System = Configuration.CriteriaSystem,
                    Code = code,
                    Display = display
                }
            },
            Text = display
        };
    }

    private void AddComponents()
    {
        // Add initial lesions component
        if (!string.IsNullOrWhiteSpace(_initialLesions))
        {
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new Coding
                        {
                            System = "http://thirdopinion.ai/fhir/CodeSystem/pcwg3-components",
                            Code = "initial-lesions",
                            Display = "Initial Lesions"
                        }
                    }
                },
                Value = new FhirString(_initialLesions)
            });
        }

        // Add confirmation date component
        if (_confirmationDate.HasValue)
        {
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new Coding
                        {
                            System = "http://thirdopinion.ai/fhir/CodeSystem/pcwg3-components",
                            Code = "confirmation-date",
                            Display = "Confirmation Date"
                        }
                    }
                },
                Value = new FhirDateTime(_confirmationDate.Value)
            });
        }

        // Add additional lesions component
        if (!string.IsNullOrWhiteSpace(_additionalLesions))
        {
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new Coding
                        {
                            System = "http://thirdopinion.ai/fhir/CodeSystem/pcwg3-components",
                            Code = "additional-lesions",
                            Display = "Additional Lesions"
                        }
                    }
                },
                Value = new FhirString(_additionalLesions)
            });
        }

        // Add time between scans component
        if (!string.IsNullOrWhiteSpace(_timeBetweenScans))
        {
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new Coding
                        {
                            System = "http://thirdopinion.ai/fhir/CodeSystem/pcwg3-components",
                            Code = "time-between-scans",
                            Display = "Time Between Scans"
                        }
                    }
                },
                Value = new FhirString(_timeBetweenScans)
            });
        }

        // Add confidence component if specified
        if (_confidence.HasValue)
        {
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new Coding
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

        // Add determination component
        if (!string.IsNullOrWhiteSpace(_determination))
        {
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new Coding
                        {
                            System = "http://thirdopinion.ai/fhir/CodeSystem/pcwg3-components",
                            Code = "determination",
                            Display = "Determination"
                        }
                    }
                },
                Value = new FhirString(_determination)
            });
        }

        // Add confidence rationale component
        if (!string.IsNullOrWhiteSpace(_confidenceRationale))
        {
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new Coding
                        {
                            System = "http://thirdopinion.ai/fhir/CodeSystem/pcwg3-components",
                            Code = "confidence-rationale",
                            Display = "Confidence Rationale"
                        }
                    }
                },
                Value = new FhirString(_confidenceRationale)
            });
        }

        // Add summary component
        if (!string.IsNullOrWhiteSpace(_summary))
        {
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new Coding
                        {
                            System = "http://thirdopinion.ai/fhir/CodeSystem/pcwg3-components",
                            Code = "summary",
                            Display = "Assessment Summary"
                        }
                    }
                },
                Value = new FhirString(_summary)
            });
        }

        // Add initial scan date component
        if (_initialScanDate.HasValue)
        {
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new Coding
                        {
                            System = "http://thirdopinion.ai/fhir/CodeSystem/pcwg3-components",
                            Code = "initial-scan-date",
                            Display = "Initial Scan Date"
                        }
                    }
                },
                Value = new FhirDateTime(_initialScanDate.Value)
            });
        }

        // Add confirmation lesions component
        if (!string.IsNullOrWhiteSpace(_confirmationLesions))
        {
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new Coding
                        {
                            System = "http://thirdopinion.ai/fhir/CodeSystem/pcwg3-components",
                            Code = "confirmation-lesions",
                            Display = "Confirmation Lesions"
                        }
                    }
                },
                Value = new FhirString(_confirmationLesions)
            });
        }
    }

    /// <summary>
    /// Creates FHIR Extensions for conflicting clinical facts
    /// </summary>
    /// <param name="facts">The conflicting clinical facts</param>
    /// <returns>List of FHIR Extensions for conflicting facts</returns>
    private List<Extension> CreateConflictingFactExtensions(IEnumerable<Fact> facts)
    {
        var extensions = new List<Extension>();

        foreach (var fact in facts.Where(f => f != null))
        {
            var extension = new Extension
            {
                Url = "https://thirdopinion.io/conflicting-fact"
            };

            // Add fact GUID
            if (!string.IsNullOrWhiteSpace(fact.factGuid))
            {
                extension.Extension.Add(new Extension("factGuid", new FhirString(fact.factGuid)));
            }

            // Add document reference
            if (!string.IsNullOrWhiteSpace(fact.factDocumentReference))
            {
                extension.Extension.Add(new Extension("factDocumentReference", new FhirString(fact.factDocumentReference)));
            }

            // Add fact type
            if (!string.IsNullOrWhiteSpace(fact.type))
            {
                extension.Extension.Add(new Extension("type", new FhirString(fact.type)));
            }

            // Add fact text
            if (!string.IsNullOrWhiteSpace(fact.fact))
            {
                extension.Extension.Add(new Extension("fact", new FhirString(fact.fact)));
            }

            // Add references
            if (fact.@ref != null && fact.@ref.Any())
            {
                foreach (var reference in fact.@ref)
                {
                    extension.Extension.Add(new Extension("ref", new FhirString(reference)));
                }
            }

            // Add time reference
            if (!string.IsNullOrWhiteSpace(fact.timeRef))
            {
                extension.Extension.Add(new Extension("timeRef", new FhirString(fact.timeRef)));
            }

            // Add relevance
            if (!string.IsNullOrWhiteSpace(fact.relevance))
            {
                extension.Extension.Add(new Extension("relevance", new FhirString(fact.relevance)));
            }

            extensions.Add(extension);
        }

        return extensions;
    }
}