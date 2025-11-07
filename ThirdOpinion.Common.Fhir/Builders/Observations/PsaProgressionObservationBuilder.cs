using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Builders.Base;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;

namespace ThirdOpinion.Common.Fhir.Builders.Observations;

/// <summary>
///     Builder for creating FHIR Observations for PSA progression assessment supporting ThirdOpinion.io and PCWG3 criteria
/// </summary>
public class PsaProgressionObservationBuilder : AiResourceBuilderBase<Observation>
{
    /// <summary>
    ///     Criteria types for PSA progression assessment
    /// </summary>
    public enum CriteriaType
    {
        /// <summary>
        ///     ThirdOpinion.io criteria
        /// </summary>
        ThirdOpinionIO,

        /// <summary>
        ///     Prostate Cancer Working Group 3 criteria
        /// </summary>
        PCWG3
    }

    private readonly List<Observation.ComponentComponent> _components;
    private readonly List<ResourceReference> _focusReferences;
    private readonly List<string> _notes;

    private readonly List<(ResourceReference reference, string role, decimal? value, string? unit)>
        _psaEvidence;

    private decimal? _absoluteChange;

    // Calculated values from PSA evidence
    private decimal? _baselinePsa;
    private float? _confidence;
    private CriteriaType? _criteriaType;
    private string? _criteriaVersion;
    private decimal? _currentPsa;
    private ResourceReference? _deviceReference;
    private FhirDateTime? _effectiveDate;
    private decimal? _nadirPsa;

    private ResourceReference? _patientReference;
    private decimal? _percentageChange;
    private string? _progressionStatus;

    /// <summary>
    ///     Creates a new PSA Progression Observation builder
    /// </summary>
    /// <param name="configuration">The AI inference configuration</param>
    public PsaProgressionObservationBuilder(AiInferenceConfiguration configuration)
        : base(configuration)
    {
        _focusReferences = new List<ResourceReference>();
        _psaEvidence = new List<(ResourceReference, string, decimal?, string?)>();
        _components = new List<Observation.ComponentComponent>();
        _notes = new List<string>();
    }

    /// <summary>
    ///     Override base class methods to maintain fluent interface
    /// </summary>
    public new PsaProgressionObservationBuilder WithInferenceId(string id)
    {
        base.WithInferenceId(id);
        return this;
    }

    /// <summary>
    ///     Override base class methods to maintain fluent interface
    /// </summary>
    public new PsaProgressionObservationBuilder WithCriteria(string id,
        string display,
        string? system = null)
    {
        base.WithCriteria(id, display, system);
        return this;
    }

    /// <summary>
    ///     Override base class methods to maintain fluent interface
    /// </summary>
    public new PsaProgressionObservationBuilder AddDerivedFrom(ResourceReference reference)
    {
        base.AddDerivedFrom(reference);
        return this;
    }

    /// <summary>
    ///     Override base class methods to maintain fluent interface
    /// </summary>
    public new PsaProgressionObservationBuilder AddDerivedFrom(string reference,
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
    public PsaProgressionObservationBuilder WithPatient(ResourceReference patient)
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
    ///     Sets the device reference that performed the assessment
    /// </summary>
    /// <param name="device">The device resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public PsaProgressionObservationBuilder WithDevice(ResourceReference device)
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
    ///     Sets the focus references for this observation
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
    ///     Sets the criteria type and version for PSA progression assessment
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
    ///     Adds PSA evidence observation reference
    /// </summary>
    /// <param name="psaObservation">The PSA observation reference</param>
    /// <param name="role">The role of this PSA value (e.g., "baseline", "nadir", "current")</param>
    /// <param name="value">Optional PSA value for calculations</param>
    /// <param name="unit">Optional unit of measurement for the PSA value (defaults to "ng/mL")</param>
    /// <returns>This builder instance for method chaining</returns>
    public PsaProgressionObservationBuilder AddPsaEvidence(ResourceReference psaObservation,
        string role,
        decimal? value = null,
        string? unit = "ng/mL")
    {
        if (psaObservation == null)
            throw new ArgumentNullException(nameof(psaObservation));
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role cannot be null or empty", nameof(role));

        _psaEvidence.Add((psaObservation, role, value, unit));

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
    ///     Sets the PSA progression status
    /// </summary>
    /// <param name="progressionStatus">The progression status: "true", "false", or "unknown"</param>
    /// <returns>This builder instance for method chaining</returns>
    /// <exception cref="ArgumentException">Thrown when progressionStatus is not a valid value</exception>
    public PsaProgressionObservationBuilder WithProgression(string progressionStatus)
    {
        if (string.IsNullOrWhiteSpace(progressionStatus))
            throw new ArgumentException("Progression status cannot be null or empty",
                nameof(progressionStatus));

        string normalizedStatus = progressionStatus.ToLowerInvariant();
        if (normalizedStatus != "true" && normalizedStatus != "false" &&
            normalizedStatus != "unknown")
            throw new ArgumentException("Progression status must be 'true', 'false', or 'unknown'",
                nameof(progressionStatus));

        _progressionStatus = normalizedStatus;
        return this;
    }

    /// <summary>
    ///     Adds a valid until component to the observation
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
                    new()
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
    ///     Adds a threshold met component to the observation
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
                    new()
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
    ///     Adds a detailed analysis note component
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
                        new()
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
    ///     Adds a most recent PSA measurement component with observation reference
    /// </summary>
    /// <param name="mostRecentDateTime">The date/time of the most recent measurement</param>
    /// <param name="mostRecentPsaValueText">The descriptive text for the most recent measurement</param>
    /// <param name="mostRecentDateTimeObservation">The observation reference for the most recent measurement</param>
    /// <returns>This builder instance for method chaining</returns>
    public PsaProgressionObservationBuilder WithMostRecentPsaValue(
        DateTime mostRecentDateTime,
        string mostRecentPsaValueText,
        ResourceReference mostRecentDateTimeObservation)
    {
        if (string.IsNullOrWhiteSpace(mostRecentPsaValueText))
            throw new ArgumentException("Most recent PSA value text cannot be null or empty",
                nameof(mostRecentPsaValueText));

        if (mostRecentDateTimeObservation == null)
            throw new ArgumentNullException(nameof(mostRecentDateTimeObservation));

        var component = new Observation.ComponentComponent
        {
            Code = new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new()
                    {
                        System = "https://thirdopinion.io/result-code",
                        Code = "mostRecentMeasurement_v1",
                        Display = "The most recent measurement used in the analysis"
                    }
                },
                Text = mostRecentPsaValueText
            },
            Value = new FhirDateTime(mostRecentDateTime),
            Extension = new List<Extension>
            {
                new()
                {
                    Url = "https://thirdopinion.io/fhir/StructureDefinition/source-observation",
                    Value = new ResourceReference
                    {
                        Reference = mostRecentDateTimeObservation.Reference,
                        Display = "The most recent result used in the analysis"
                    }
                }
            }
        };

        _components.Add(component);
        return this;
    }

    /// <summary>
    ///     Sets the effective date/time of this observation
    /// </summary>
    /// <param name="effectiveDate">The effective date/time</param>
    /// <returns>This builder instance for method chaining</returns>
    public PsaProgressionObservationBuilder WithEffectiveDate(DateTime effectiveDate)
    {
        _effectiveDate = new FhirDateTime(effectiveDate);
        return this;
    }

    /// <summary>
    ///     Sets the effective date/time of this observation
    /// </summary>
    /// <param name="effectiveDate">The effective date/time as DateTimeOffset</param>
    /// <returns>This builder instance for method chaining</returns>
    public PsaProgressionObservationBuilder WithEffectiveDate(DateTimeOffset effectiveDate)
    {
        _effectiveDate = new FhirDateTime(effectiveDate);
        return this;
    }

    /// <summary>
    ///     Adds a note to this observation
    /// </summary>
    /// <param name="noteText">The note text</param>
    /// <returns>This builder instance for method chaining</returns>
    public PsaProgressionObservationBuilder AddNote(string noteText)
    {
        if (!string.IsNullOrWhiteSpace(noteText)) _notes.Add(noteText);
        return this;
    }

    /// <summary>
    ///     Sets the AI confidence score for this observation
    /// </summary>
    /// <param name="confidence">The confidence score (0.0 to 1.0)</param>
    /// <returns>This builder instance for method chaining</returns>
    public PsaProgressionObservationBuilder WithConfidence(float confidence)
    {
        if (confidence < 0.0f || confidence > 1.0f)
            throw new ArgumentOutOfRangeException(nameof(confidence),
                "Confidence must be between 0.0 and 1.0");

        _confidence = confidence;
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

        if (_deviceReference == null)
            throw new InvalidOperationException(
                "Device reference is required. Call WithDevice() before Build().");

        if (string.IsNullOrWhiteSpace(_progressionStatus))
            throw new InvalidOperationException(
                "Progression status is required. Call WithProgression() before Build().");

        if (_psaEvidence.Count == 0)
            throw new InvalidOperationException(
                "At least one PSA evidence reference is required. Call AddPsaEvidence() before Build().");
    }

    /// <summary>
    ///     Builds the PSA Progression Observation
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
                new()
                {
                    Coding = new List<Coding>
                    {
                        new()
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
                    new()
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
            observation.Method = CreateMethodForCriteria();

        // Add PSA evidence to derivedFrom with extensions for role and value
        if (_psaEvidence.Any() || DerivedFromReferences.Any())
        {
            observation.DerivedFrom.Clear();

            // Add PSA evidence with role and value as extensions
            foreach ((ResourceReference reference, string role, decimal? value, string? unit)
                     evidence in _psaEvidence)
            {
                var reference = new ResourceReference(evidence.reference.Reference,
                    evidence.reference.Display);

                // Add role/type as extension
                reference.Extension = new List<Extension>
                {
                    new()
                    {
                        Url = "http://thirdopinion.ai/fhir/StructureDefinition/psa-evidence-role",
                        Value = new FhirString(evidence.role)
                    }
                };

                // Add value as extension if available
                if (evidence.value.HasValue)
                {
                    var quantity = new Quantity
                    {
                        Value = evidence.value.Value
                    };

                    // Add unit information if provided
                    if (!string.IsNullOrWhiteSpace(evidence.unit))
                    {
                        quantity.Unit = evidence.unit;
                        quantity.System = "http://unitsofmeasure.org";
                        quantity.Code = evidence.unit;
                    }

                    reference.Extension.Add(new Extension
                    {
                        Url = "http://thirdopinion.ai/fhir/StructureDefinition/psa-evidence-value",
                        Value = quantity
                    });
                }

                observation.DerivedFrom.Add(reference);
            }

            observation.DerivedFrom.AddRange(DerivedFromReferences);
        }

        // Add calculated components
        AddCalculatedComponents();

        // Add all components
        if (_components.Any()) observation.Component = _components;

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
                _percentageChange = (_currentPsa.Value - _nadirPsa.Value) / _nadirPsa.Value * 100;
        }
        else if (_baselinePsa.HasValue && _currentPsa.HasValue)
        {
            // ThirdOpinion.io or fallback: Calculate from baseline
            _absoluteChange = _currentPsa.Value - _baselinePsa.Value;
            if (_baselinePsa.Value > 0)
                _percentageChange = (_currentPsa.Value - _baselinePsa.Value) / _baselinePsa.Value *
                                    100;
        }
    }

    private CodeableConcept CreateProgressionValue()
    {
        return _progressionStatus switch
        {
            "true" => FhirCodingHelper.CreateSnomedConcept(
                "277022003",
                "Progressive disease"),
            "false" => FhirCodingHelper.CreateSnomedConcept(
                "359746009",
                "Stable disease"),
            "unknown" => FhirCodingHelper.CreateSnomedConcept(
                "261665006",
                "Unknown"),
            _ => throw new InvalidOperationException(
                $"Invalid progression status: {_progressionStatus}")
        };
    }

    private CodeableConcept CreateMethodForCriteria()
    {
        string code;
        string display;

        if (_criteriaType == CriteriaType.PCWG3)
        {
            code
                = $"psa-progression-pcwg3-{InferenceId ?? Guid.NewGuid().ToString()}-v{_criteriaVersion}";
            display = $"PSA Progression PCWG3 Criteria v{_criteriaVersion}";
        }
        else
        {
            code
                = $"psa-progression-{InferenceId ?? Guid.NewGuid().ToString()}-v{_criteriaVersion}";
            display = $"PSA Progression ThirdOpinion.io Criteria v{_criteriaVersion}";
        }

        return new CodeableConcept
        {
            Coding = new List<Coding>
            {
                new()
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
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new()
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

        // Add absolute change component if calculated
        if (_absoluteChange.HasValue)
            _components.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new()
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

        // Add threshold analysis for PCWG3 if applicable
        if (_criteriaType == CriteriaType.PCWG3 && _percentageChange.HasValue &&
            !_components.Any(c => c.Code.Coding.Any(cd => cd.Code == "threshold-met")))
        {
            // PCWG3 uses 25% increase from nadir as threshold
            bool meetsThreshold = _percentageChange.Value >= 25;
            AddThresholdMetComponent(meetsThreshold);
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
    }

    /// <summary>
    ///     Builds a FHIR Condition resource for PSA progression when progression is detected
    /// </summary>
    /// <param name="observation">The PSA progression observation to reference as evidence</param>
    /// <returns>A Condition resource indicating PSA progression</returns>
    public Condition? BuildCondition(Observation observation)
    {
        // Only create condition if progression is detected
        if (_progressionStatus != "true") return null;

        ValidateRequiredFields();

        var condition = new Condition
        {
            // Clinical status: active (the progression is currently present)
            ClinicalStatus = new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new()
                    {
                        System = "http://terminology.hl7.org/CodeSystem/condition-clinical",
                        Code = "active",
                        Display = "Active"
                    }
                }
            },

            // Verification status: confirmed (AI has confirmed the progression)
            VerificationStatus = new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new()
                    {
                        System = "http://terminology.hl7.org/CodeSystem/condition-ver-status",
                        Code = "confirmed",
                        Display = "Confirmed"
                    }
                }
            },

            // Category: encounter-diagnosis
            Category = new List<CodeableConcept>
            {
                new()
                {
                    Coding = new List<Coding>
                    {
                        new()
                        {
                            System = "http://terminology.hl7.org/CodeSystem/condition-category",
                            Code = "encounter-diagnosis",
                            Display = "Encounter Diagnosis"
                        }
                    }
                }
            },

            // Code: PSA progression using SNOMED codes
            Code = CreatePsaProgressionConditionCode(),

            // Subject (Patient)
            Subject = _patientReference,

            // Recorded date
            RecordedDate = _effectiveDate?.ToString() ??
                           DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),

            // Recorder (Device)
            Recorder = _deviceReference
        };

        // Add observation as evidence
        condition.Evidence = new List<Condition.EvidenceComponent>
        {
            new()
            {
                Detail = new List<ResourceReference>
                {
                    new()
                    {
                        Reference = $"Observation/{observation.Id}",
                        Display = "PSA Progression Assessment"
                    }
                }
            }
        };

        // Add AI inference extensions
        condition.Extension = new List<Extension>();

        // Add confidence as an extension if specified
        if (_confidence.HasValue)
            condition.Extension.Add(new Extension(
                "http://thirdopinion.ai/fhir/StructureDefinition/confidence",
                new FhirDecimal((decimal)_confidence.Value)));

        // Add criteria extension if specified
        if (!string.IsNullOrWhiteSpace(CriteriaId))
        {
            var criteriaExtension = new Extension
            {
                Url = "http://thirdopinion.ai/fhir/StructureDefinition/assessment-criteria"
            };
            criteriaExtension.Extension.Add(new Extension("id", new FhirString(CriteriaId)));
            criteriaExtension.Extension.Add(new Extension("display",
                new FhirString(CriteriaDisplay ?? "")));
            condition.Extension.Add(criteriaExtension);
        }

        // Add AI inference marker extension
        condition.Extension.Add(new Extension(
            "http://thirdopinion.ai/fhir/StructureDefinition/ai-inferred",
            new FhirBoolean(true)));

        // Add notes if any
        if (_notes.Any())
            condition.Note = _notes.Select(noteText => new Annotation
            {
                Time = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                Text = new Markdown(noteText)
            }).ToList();

        return condition;
    }

    /// <summary>
    ///     Builds both the PSA Progression Observation and associated Condition (if progression detected)
    /// </summary>
    /// <returns>A tuple containing the Observation and optional Condition</returns>
    public (Observation observation, Condition? condition) BuildWithCondition()
    {
        Observation observation = Build();
        Condition? condition = BuildCondition(observation);

        return (observation, condition);
    }

    private CodeableConcept CreatePsaProgressionConditionCode()
    {
        return new CodeableConcept
        {
            Coding = new List<Coding>
            {
                new()
                {
                    System = FhirCodingHelper.Systems.SNOMED_SYSTEM,
                    Code = "428119001",
                    Display = "Procedure to assess prostate specific antigen progression"
                },
                new()
                {
                    System = "http://hl7.org/fhir/sid/icd-10-cm",
                    Code = "R97.21",
                    Display = "Rising PSA following treatment for malignant neoplasm of prostate"
                }
            },
            Text = "PSA Progression"
        };
    }
}