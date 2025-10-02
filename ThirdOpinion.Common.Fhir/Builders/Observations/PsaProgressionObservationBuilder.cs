using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Builders.Base;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;

namespace ThirdOpinion.Common.Fhir.Builders.Observations;

/// <summary>
/// Builder for creating FHIR Observations for PSA progression assessment supporting ThirdOpinion.io and PCWG3 criteria
/// </summary>
public class PsaProgressionObservationBuilder : AiResourceBuilderBase<Observation>
{
    /// <summary>
    /// Criteria types for PSA progression assessment
    /// </summary>
    public enum CriteriaType
    {
        /// <summary>
        /// ThirdOpinion.io criteria
        /// </summary>
        ThirdOpinionIO,

        /// <summary>
        /// Prostate Cancer Working Group 3 criteria
        /// </summary>
        PCWG3
    }

    private ResourceReference? _patientReference;
    private ResourceReference? _deviceReference;
    private readonly List<ResourceReference> _focusReferences;
    private CriteriaType? _criteriaType;
    private string? _criteriaVersion;
    private bool? _hasProgression;
    private FhirDateTime? _effectiveDate;
    private readonly List<(ResourceReference reference, string role, decimal? value)> _psaEvidence;
    private readonly List<Observation.ComponentComponent> _components;
    private readonly List<string> _notes;
    private float? _confidence;

    // Calculated values from PSA evidence
    private decimal? _baselinePsa;
    private decimal? _nadirPsa;
    private decimal? _currentPsa;
    private decimal? _percentageChange;
    private decimal? _absoluteChange;

    /// <summary>
    /// Creates a new PSA Progression Observation builder
    /// </summary>
    /// <param name="configuration">The AI inference configuration</param>
    public PsaProgressionObservationBuilder(AiInferenceConfiguration configuration)
        : base(configuration)
    {
        _focusReferences = new List<ResourceReference>();
        _psaEvidence = new List<(ResourceReference, string, decimal?)>();
        _components = new List<Observation.ComponentComponent>();
        _notes = new List<string>();
    }

    /// <summary>
    /// Override base class methods to maintain fluent interface
    /// </summary>
    public new PsaProgressionObservationBuilder WithInferenceId(string id)
    {
        base.WithInferenceId(id);
        return this;
    }

    /// <summary>
    /// Override base class methods to maintain fluent interface
    /// </summary>
    public new PsaProgressionObservationBuilder WithCriteria(string id, string display, string? system = null)
    {
        base.WithCriteria(id, display, system);
        return this;
    }

    /// <summary>
    /// Override base class methods to maintain fluent interface
    /// </summary>
    public new PsaProgressionObservationBuilder AddDerivedFrom(ResourceReference reference)
    {
        base.AddDerivedFrom(reference);
        return this;
    }

    /// <summary>
    /// Override base class methods to maintain fluent interface
    /// </summary>
    public new PsaProgressionObservationBuilder AddDerivedFrom(string reference, string? display = null)
    {
        base.AddDerivedFrom(reference, display);
        return this;
    }

    /// <summary>
    /// Sets the patient reference for this observation
    /// </summary>
    /// <param name="patient">The patient resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public PsaProgressionObservationBuilder WithPatient(ResourceReference patient)
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
    public PsaProgressionObservationBuilder WithPatient(string patientId, string? display = null)
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
    public PsaProgressionObservationBuilder WithDevice(ResourceReference device)
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
    public PsaProgressionObservationBuilder WithDevice(string deviceId, string? display = null)
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
    /// Sets the focus references for this observation
    /// </summary>
    /// <param name="focus">The focus resource references</param>
    /// <returns>This builder instance for method chaining</returns>
    public PsaProgressionObservationBuilder WithFocus(params ResourceReference[] focus)
    {
        if (focus == null || focus.Length == 0)
            throw new ArgumentException("At least one focus reference is required", nameof(focus));

        _focusReferences.Clear();
        _focusReferences.AddRange(focus.Where(f => f != null));
        return this;
    }

    /// <summary>
    /// Sets the criteria type and version for PSA progression assessment
    /// </summary>
    /// <param name="criteriaType">The criteria type (ThirdOpinionIO or PCWG3)</param>
    /// <param name="version">The version of the criteria</param>
    /// <returns>This builder instance for method chaining</returns>
    public PsaProgressionObservationBuilder WithCriteria(CriteriaType criteriaType, string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Version cannot be null or empty", nameof(version));

        _criteriaType = criteriaType;
        _criteriaVersion = version;
        return this;
    }

    /// <summary>
    /// Adds PSA evidence observation reference
    /// </summary>
    /// <param name="psaObservation">The PSA observation reference</param>
    /// <param name="role">The role of this PSA value (e.g., "baseline", "nadir", "current")</param>
    /// <param name="value">Optional PSA value for calculations</param>
    /// <returns>This builder instance for method chaining</returns>
    public PsaProgressionObservationBuilder AddPsaEvidence(ResourceReference psaObservation, string role, decimal? value = null)
    {
        if (psaObservation == null)
            throw new ArgumentNullException(nameof(psaObservation));
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role cannot be null or empty", nameof(role));

        _psaEvidence.Add((psaObservation, role, value));

        // Update calculated values based on role
        switch (role.ToLowerInvariant())
        {
            case "baseline":
                _baselinePsa = value;
                break;
            case "nadir":
                _nadirPsa = value;
                break;
            case "current":
            case "latest":
                _currentPsa = value;
                break;
        }

        return this;
    }

    /// <summary>
    /// Sets whether PSA progression is detected
    /// </summary>
    /// <param name="hasProgression">True if progression is detected, false otherwise</param>
    /// <returns>This builder instance for method chaining</returns>
    public PsaProgressionObservationBuilder WithProgression(bool hasProgression)
    {
        _hasProgression = hasProgression;
        return this;
    }

    /// <summary>
    /// Adds a valid until component to the observation
    /// </summary>
    /// <param name="validUntil">The date until which the assessment is valid</param>
    /// <returns>This builder instance for method chaining</returns>
    public PsaProgressionObservationBuilder AddValidUntilComponent(DateTime validUntil)
    {
        var component = new Observation.ComponentComponent
        {
            Code = new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new Coding
                    {
                        System = "http://thirdopinion.ai/fhir/CodeSystem/psa-components",
                        Code = "valid-until",
                        Display = "Valid Until Date"
                    }
                }
            },
            Value = new Period
            {
                End = new FhirDateTime(validUntil).ToString()
            }
        };

        _components.Add(component);
        return this;
    }

    /// <summary>
    /// Adds a threshold met component to the observation
    /// </summary>
    /// <param name="thresholdMet">Whether the progression threshold was met</param>
    /// <returns>This builder instance for method chaining</returns>
    public PsaProgressionObservationBuilder AddThresholdMetComponent(bool thresholdMet)
    {
        var component = new Observation.ComponentComponent
        {
            Code = new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new Coding
                    {
                        System = "http://thirdopinion.ai/fhir/CodeSystem/psa-components",
                        Code = "threshold-met",
                        Display = "Progression Threshold Met"
                    }
                }
            },
            Value = new FhirBoolean(thresholdMet)
        };

        _components.Add(component);
        return this;
    }

    /// <summary>
    /// Adds a detailed analysis note component
    /// </summary>
    /// <param name="note">The analysis note text</param>
    /// <returns>This builder instance for method chaining</returns>
    public PsaProgressionObservationBuilder AddDetailedAnalysisNote(string note)
    {
        if (!string.IsNullOrWhiteSpace(note))
        {
            var component = new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new Coding
                        {
                            System = "http://thirdopinion.ai/fhir/CodeSystem/psa-components",
                            Code = "analysis-note",
                            Display = "Detailed Analysis Note"
                        }
                    }
                },
                Value = new FhirString(note)
            };

            _components.Add(component);
        }
        return this;
    }

    /// <summary>
    /// Sets the effective date/time of this observation
    /// </summary>
    /// <param name="effectiveDate">The effective date/time</param>
    /// <returns>This builder instance for method chaining</returns>
    public PsaProgressionObservationBuilder WithEffectiveDate(DateTime effectiveDate)
    {
        _effectiveDate = new FhirDateTime(effectiveDate);
        return this;
    }

    /// <summary>
    /// Sets the effective date/time of this observation
    /// </summary>
    /// <param name="effectiveDate">The effective date/time as DateTimeOffset</param>
    /// <returns>This builder instance for method chaining</returns>
    public PsaProgressionObservationBuilder WithEffectiveDate(DateTimeOffset effectiveDate)
    {
        _effectiveDate = new FhirDateTime(effectiveDate);
        return this;
    }

    /// <summary>
    /// Adds a note to this observation
    /// </summary>
    /// <param name="noteText">The note text</param>
    /// <returns>This builder instance for method chaining</returns>
    public PsaProgressionObservationBuilder AddNote(string noteText)
    {
        if (!string.IsNullOrWhiteSpace(noteText))
        {
            _notes.Add(noteText);
        }
        return this;
    }

    /// <summary>
    /// Sets the AI confidence score for this observation
    /// </summary>
    /// <param name="confidence">The confidence score (0.0 to 1.0)</param>
    /// <returns>This builder instance for method chaining</returns>
    public PsaProgressionObservationBuilder WithConfidence(float confidence)
    {
        if (confidence < 0.0f || confidence > 1.0f)
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be between 0.0 and 1.0");

        _confidence = confidence;
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

        if (!_hasProgression.HasValue)
        {
            throw new InvalidOperationException("Progression status is required. Call WithProgression() before Build().");
        }

        if (_psaEvidence.Count == 0)
        {
            throw new InvalidOperationException("At least one PSA evidence reference is required. Call AddPsaEvidence() before Build().");
        }
    }

    /// <summary>
    /// Builds the PSA Progression Observation
    /// </summary>
    /// <returns>The completed Observation resource</returns>
    protected override Observation BuildCore()
    {
        // Calculate PSA changes if values are available
        CalculatePsaChanges();

        var observation = new Observation
        {
            Status = ObservationStatus.Final,

            // Category: laboratory
            Category = new List<CodeableConcept>
            {
                new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new Coding
                        {
                            System = "http://terminology.hl7.org/CodeSystem/observation-category",
                            Code = "laboratory",
                            Display = "Laboratory"
                        }
                    }
                }
            },

            // Code: LOINC code for PSA progression
            Code = new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new Coding
                    {
                        System = FhirCodingHelper.Systems.LOINC_SYSTEM,
                        Code = "97509-4",
                        Display = "PSA progression"
                    }
                },
                Text = "PSA progression assessment"
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

        // Add method based on criteria type
        if (_criteriaType.HasValue && !string.IsNullOrWhiteSpace(_criteriaVersion))
        {
            observation.Method = CreateMethodForCriteria();
        }

        // Add PSA evidence to derivedFrom
        if (_psaEvidence.Any() || DerivedFromReferences.Any())
        {
            observation.DerivedFrom.Clear();
            observation.DerivedFrom.AddRange(_psaEvidence.Select(e => e.reference));
            observation.DerivedFrom.AddRange(DerivedFromReferences);
        }

        // Add calculated components
        AddCalculatedComponents();

        // Add all components
        if (_components.Any())
        {
            observation.Component = _components;
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

    private void CalculatePsaChanges()
    {
        // Calculate percentage and absolute changes based on available values
        if (_criteriaType == CriteriaType.PCWG3 && _nadirPsa.HasValue && _currentPsa.HasValue)
        {
            // PCWG3: Calculate from nadir
            _absoluteChange = _currentPsa.Value - _nadirPsa.Value;
            if (_nadirPsa.Value > 0)
            {
                _percentageChange = ((_currentPsa.Value - _nadirPsa.Value) / _nadirPsa.Value) * 100;
            }
        }
        else if (_baselinePsa.HasValue && _currentPsa.HasValue)
        {
            // ThirdOpinion.io or fallback: Calculate from baseline
            _absoluteChange = _currentPsa.Value - _baselinePsa.Value;
            if (_baselinePsa.Value > 0)
            {
                _percentageChange = ((_currentPsa.Value - _baselinePsa.Value) / _baselinePsa.Value) * 100;
            }
        }
    }

    private CodeableConcept CreateProgressionValue()
    {
        if (_hasProgression == true)
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

    private CodeableConcept CreateMethodForCriteria()
    {
        string code;
        string display;

        if (_criteriaType == CriteriaType.PCWG3)
        {
            code = $"psa-progression-pcwg3-{InferenceId ?? Guid.NewGuid().ToString()}-v{_criteriaVersion}";
            display = $"PSA Progression PCWG3 Criteria v{_criteriaVersion}";
        }
        else
        {
            code = $"psa-progression-{InferenceId ?? Guid.NewGuid().ToString()}-v{_criteriaVersion}";
            display = $"PSA Progression ThirdOpinion.io Criteria v{_criteriaVersion}";
        }

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

    private void AddCalculatedComponents()
    {
        // Add percentage change component if calculated
        if (_percentageChange.HasValue)
        {
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new Coding
                        {
                            System = "http://thirdopinion.ai/fhir/CodeSystem/psa-components",
                            Code = "percentage-change",
                            Display = "PSA Percentage Change"
                        }
                    }
                },
                Value = new Quantity
                {
                    Value = Math.Round(_percentageChange.Value, 2),
                    Unit = "%",
                    System = "http://unitsofmeasure.org",
                    Code = "%"
                }
            });
        }

        // Add absolute change component if calculated
        if (_absoluteChange.HasValue)
        {
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new Coding
                        {
                            System = "http://thirdopinion.ai/fhir/CodeSystem/psa-components",
                            Code = "absolute-change",
                            Display = "PSA Absolute Change"
                        }
                    }
                },
                Value = new Quantity
                {
                    Value = Math.Round(_absoluteChange.Value, 2),
                    Unit = "ng/mL",
                    System = "http://unitsofmeasure.org",
                    Code = "ng/mL"
                }
            });
        }

        // Add threshold analysis for PCWG3 if applicable
        if (_criteriaType == CriteriaType.PCWG3 && _percentageChange.HasValue && !_components.Any(c => c.Code.Coding.Any(cd => cd.Code == "threshold-met")))
        {
            // PCWG3 uses 25% increase from nadir as threshold
            bool meetsThreshold = _percentageChange.Value >= 25;
            AddThresholdMetComponent(meetsThreshold);
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
    }
}