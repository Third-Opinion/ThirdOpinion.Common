using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Builders.Base;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;

namespace ThirdOpinion.Common.Fhir.Builders.Observations;

/// <summary>
/// Builder for creating FHIR Observations for Castration-Sensitive Prostate Cancer (CSPC) assessment
/// </summary>
public class CspcAssessmentObservationBuilder : AiResourceBuilderBase<Observation>
{
    private ResourceReference? _focus;
    private ResourceReference? _patientReference;
    private ResourceReference? _deviceReference;
    private bool? _isCastrationSensitive;
    private FhirDateTime? _effectiveDate;
    private readonly List<ResourceReference> _evidenceReferences;
    private readonly List<string> _notes;
    private string? _interpretation;
    private float? _confidence;

    /// <summary>
    /// Creates a new CSPC Assessment Observation builder
    /// </summary>
    /// <param name="configuration">The AI inference configuration</param>
    public CspcAssessmentObservationBuilder(AiInferenceConfiguration configuration)
        : base(configuration)
    {
        _evidenceReferences = new List<ResourceReference>();
        _notes = new List<string>();
    }

    /// <summary>
    /// Sets the inference ID for this resource
    /// </summary>
    /// <param name="id">The inference ID</param>
    /// <returns>This builder instance for method chaining</returns>
    public new CspcAssessmentObservationBuilder WithInferenceId(string id)
    {
        base.WithInferenceId(id);
        return this;
    }

    /// <summary>
    /// Sets the criteria information for this inference
    /// </summary>
    /// <param name="id">The criteria ID</param>
    /// <param name="display">The display text for the criteria</param>
    /// <param name="system">The criteria system URI (optional)</param>
    /// <returns>This builder instance for method chaining</returns>
    public new CspcAssessmentObservationBuilder WithCriteria(string id, string display, string? system = null)
    {
        base.WithCriteria(id, display, system);
        return this;
    }

    /// <summary>
    /// Adds a resource reference that this inference was derived from
    /// </summary>
    /// <param name="reference">The resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public new CspcAssessmentObservationBuilder AddDerivedFrom(ResourceReference reference)
    {
        base.AddDerivedFrom(reference);
        return this;
    }

    /// <summary>
    /// Adds a resource reference that this inference was derived from
    /// </summary>
    /// <param name="reference">The reference string</param>
    /// <param name="display">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public new CspcAssessmentObservationBuilder AddDerivedFrom(string reference, string? display = null)
    {
        base.AddDerivedFrom(reference, display);
        return this;
    }

    /// <summary>
    /// Sets the focus reference for this observation (REQUIRED - must reference a Condition)
    /// </summary>
    /// <param name="focus">The Condition resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public CspcAssessmentObservationBuilder WithFocus(ResourceReference focus)
    {
        if (focus == null)
            throw new ArgumentNullException(nameof(focus), "Focus reference cannot be null");

        // Validate that the reference is to a Condition resource
        if (!string.IsNullOrWhiteSpace(focus.Reference) && !focus.Reference.StartsWith("Condition/"))
        {
            throw new ArgumentException(
                "Focus must reference a Condition resource. Reference must start with 'Condition/'",
                nameof(focus));
        }

        _focus = focus;
        return this;
    }

    /// <summary>
    /// Sets the focus reference for this observation (REQUIRED - must reference a Condition)
    /// </summary>
    /// <param name="conditionId">The Condition ID</param>
    /// <param name="display">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public CspcAssessmentObservationBuilder WithFocus(string conditionId, string? display = null)
    {
        if (string.IsNullOrWhiteSpace(conditionId))
            throw new ArgumentException("Condition ID cannot be null or empty", nameof(conditionId));

        var reference = new ResourceReference
        {
            Reference = conditionId.StartsWith("Condition/") ? conditionId : $"Condition/{conditionId}",
            Display = display
        };

        return WithFocus(reference);
    }

    /// <summary>
    /// Sets the patient reference for this observation
    /// </summary>
    /// <param name="patient">The patient resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public CspcAssessmentObservationBuilder WithPatient(ResourceReference patient)
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
    public CspcAssessmentObservationBuilder WithPatient(string patientId, string? display = null)
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
    public CspcAssessmentObservationBuilder WithDevice(ResourceReference device)
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
    public CspcAssessmentObservationBuilder WithDevice(string deviceId, string? display = null)
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
    /// Sets whether the cancer is castration-sensitive
    /// </summary>
    /// <param name="isSensitive">True if castration-sensitive, false if castration-resistant</param>
    /// <returns>This builder instance for method chaining</returns>
    public CspcAssessmentObservationBuilder WithCastrationSensitive(bool isSensitive)
    {
        _isCastrationSensitive = isSensitive;
        return this;
    }

    /// <summary>
    /// Adds evidence supporting this observation
    /// </summary>
    /// <param name="reference">The evidence resource reference</param>
    /// <param name="display">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public CspcAssessmentObservationBuilder AddEvidence(ResourceReference reference, string? display = null)
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
    public CspcAssessmentObservationBuilder AddEvidence(string referenceString, string? display = null)
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
    /// Sets the interpretation of the assessment
    /// </summary>
    /// <param name="interpretation">The interpretation text</param>
    /// <returns>This builder instance for method chaining</returns>
    public CspcAssessmentObservationBuilder WithInterpretation(string interpretation)
    {
        if (!string.IsNullOrWhiteSpace(interpretation))
        {
            _interpretation = interpretation;
        }
        return this;
    }

    /// <summary>
    /// Sets the effective date/time of this observation
    /// </summary>
    /// <param name="effectiveDate">The effective date/time</param>
    /// <returns>This builder instance for method chaining</returns>
    public CspcAssessmentObservationBuilder WithEffectiveDate(DateTime effectiveDate)
    {
        _effectiveDate = new FhirDateTime(effectiveDate);
        return this;
    }

    /// <summary>
    /// Sets the effective date/time of this observation
    /// </summary>
    /// <param name="effectiveDate">The effective date/time as DateTimeOffset</param>
    /// <returns>This builder instance for method chaining</returns>
    public CspcAssessmentObservationBuilder WithEffectiveDate(DateTimeOffset effectiveDate)
    {
        _effectiveDate = new FhirDateTime(effectiveDate);
        return this;
    }

    /// <summary>
    /// Adds a note to this observation
    /// </summary>
    /// <param name="noteText">The note text</param>
    /// <returns>This builder instance for method chaining</returns>
    public CspcAssessmentObservationBuilder AddNote(string noteText)
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
    public CspcAssessmentObservationBuilder WithConfidence(float confidence)
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
        if (_focus == null)
        {
            throw new InvalidOperationException(
                "CSPC assessment requires focus reference to existing Condition. Call WithFocus() before Build().");
        }

        if (_patientReference == null)
        {
            throw new InvalidOperationException("Patient reference is required. Call WithPatient() before Build().");
        }

        if (_deviceReference == null)
        {
            throw new InvalidOperationException("Device reference is required. Call WithDevice() before Build().");
        }

        if (!_isCastrationSensitive.HasValue)
        {
            throw new InvalidOperationException(
                "Castration sensitivity status is required. Call WithCastrationSensitive() before Build().");
        }
    }

    /// <summary>
    /// Builds the CSPC Assessment Observation
    /// </summary>
    /// <returns>The completed Observation resource</returns>
    protected override Observation BuildCore()
    {
        var observation = new Observation
        {
            Status = ObservationStatus.Final,

            // Category: exam
            Category = new List<CodeableConcept>
            {
                new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new Coding
                        {
                            System = "http://terminology.hl7.org/CodeSystem/observation-category",
                            Code = "exam",
                            Display = "Exam"
                        }
                    }
                }
            },

            // Code: LOINC code for cancer disease status
            Code = new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new Coding
                    {
                        System = FhirCodingHelper.Systems.LOINC_SYSTEM,
                        Code = "21889-1",
                        Display = "Cancer disease status"
                    }
                },
                Text = "Cancer disease status"
            },

            // Focus (required - references the Condition)
            Focus = new List<ResourceReference> { _focus! },

            // Subject (Patient)
            Subject = _patientReference,

            // Device
            Device = _deviceReference,

            // Effective date/time
            Effective = _effectiveDate ?? new FhirDateTime(DateTimeOffset.Now),

            // Value: Castration-sensitive or castration-resistant status
            Value = CreateCastrationSensitivityValue()
        };

        // Add interpretation if provided
        if (!string.IsNullOrWhiteSpace(_interpretation))
        {
            observation.Interpretation = new List<CodeableConcept>
            {
                new CodeableConcept
                {
                    Text = _interpretation
                }
            };
        }

        // Add method if criteria was set
        if (!string.IsNullOrWhiteSpace(CriteriaId))
        {
            observation.Method = new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new Coding
                    {
                        System = CriteriaSystem ?? Configuration.CriteriaSystem,
                        Code = CriteriaId,
                        Display = CriteriaDisplay
                    }
                },
                Text = CriteriaDisplay
            };
        }

        // Add evidence to derivedFrom
        // Note: FHIR Observation initializes DerivedFrom as an empty list
        if (_evidenceReferences.Any() || DerivedFromReferences.Any())
        {
            observation.DerivedFrom.Clear();
            observation.DerivedFrom.AddRange(_evidenceReferences);
            observation.DerivedFrom.AddRange(DerivedFromReferences);
        }

        // Add confidence component if specified
        if (_confidence.HasValue)
        {
            if (observation.Component == null)
                observation.Component = new List<Observation.ComponentComponent>();

            var confidenceComponent = new Observation.ComponentComponent
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
            };

            observation.Component.Add(confidenceComponent);
        }

        // Add notes
        // Note: FHIR Observation initializes Note as an empty list
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

    /// <summary>
    /// Creates the value CodeableConcept with both SNOMED and ICD-10 codes
    /// </summary>
    private CodeableConcept CreateCastrationSensitivityValue()
    {
        if (_isCastrationSensitive == true)
        {
            // Castration-sensitive: Use BOTH SNOMED and ICD-10 codes
            return new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new Coding
                    {
                        System = FhirCodingHelper.Systems.SNOMED_SYSTEM,
                        Code = "1197209002",
                        Display = "Castration sensitive prostate cancer"
                    },
                    new Coding
                    {
                        System = "http://hl7.org/fhir/sid/icd-10-cm",
                        Code = "Z19.1",
                        Display = "Hormone sensitive status"
                    }
                },
                Text = "Castration sensitive prostate cancer"
            };
        }
        else
        {
            // Castration-resistant: Use appropriate codes
            return new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new Coding
                    {
                        System = FhirCodingHelper.Systems.SNOMED_SYSTEM,
                        Code = "445848006",
                        Display = "Castration resistant prostate cancer"
                    },
                    new Coding
                    {
                        System = "http://hl7.org/fhir/sid/icd-10-cm",
                        Code = "Z19.2",
                        Display = "Hormone resistant status"
                    }
                },
                Text = "Castration resistant prostate cancer"
            };
        }
    }
}