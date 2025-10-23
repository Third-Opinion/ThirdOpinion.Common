using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Builders.Base;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Extensions;
using ThirdOpinion.Common.Fhir.Helpers;
using ThirdOpinion.Common.Fhir.Models;

namespace ThirdOpinion.Common.Fhir.Builders.Conditions;

/// <summary>
/// Builder for creating FHIR Condition resources for HSDM (Hormone Sensitivity Diagnosis Modifier)
/// Castration-Sensitive Prostate Cancer (CSPC) Assessment
/// </summary>
public class HsdmAssessmentConditionBuilder : AiResourceBuilderBase<Condition>
{
    private ResourceReference? _patientReference;
    private ResourceReference? _deviceReference;
    private readonly List<ResourceReference> _focusReferences;
    private readonly List<ResourceReference> _evidenceReferences;
    private readonly List<Fact> _facts;
    private readonly List<string> _notes;
    private string? _hsdmResult;
    private FhirDateTime? _effectiveDate;
    private float? _confidence;
    private string? _criteriaDescription;

    /// <summary>
    /// Valid HSDM result values
    /// </summary>
    public static class HsdmResults
    {
        public const string NonMetastaticBiochemicalRelapse = "nmCSPC_biochemical_relapse";
        public const string MetastaticCastrationSensitive = "mCSPC";
        public const string MetastaticCastrationResistant = "mCRPC";
    }

    /// <summary>
    /// Creates a new HSDM Assessment Condition builder
    /// </summary>
    /// <param name="configuration">The AI inference configuration</param>
    public HsdmAssessmentConditionBuilder(AiInferenceConfiguration configuration)
        : base(configuration)
    {
        _focusReferences = new List<ResourceReference>();
        _evidenceReferences = new List<ResourceReference>();
        _facts = new List<Fact>();
        _notes = new List<string>();
    }

    /// <summary>
    /// Sets the inference ID for this resource
    /// </summary>
    /// <param name="id">The inference ID</param>
    /// <returns>This builder instance for method chaining</returns>
    public new HsdmAssessmentConditionBuilder WithInferenceId(string id)
    {
        base.WithInferenceId(id);
        return this;
    }

    /// <summary>
    /// Sets the criteria information for this inference
    /// </summary>
    /// <param name="id">The criteria ID</param>
    /// <param name="display">The display text for the criteria</param>
    /// <param name="description">The criteria description</param>
    /// <returns>This builder instance for method chaining</returns>
    public new HsdmAssessmentConditionBuilder WithCriteria(string id, string display, string description)
    {
        base.WithCriteria(id, display);
        _criteriaDescription = description;
        return this;
    }

    /// <summary>
    /// Adds a resource reference that this assessment was derived from
    /// </summary>
    /// <param name="reference">The resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public new HsdmAssessmentConditionBuilder AddDerivedFrom(ResourceReference reference)
    {
        base.AddDerivedFrom(reference);
        return this;
    }

    /// <summary>
    /// Adds a resource reference that this assessment was derived from
    /// </summary>
    /// <param name="reference">The reference string</param>
    /// <param name="display">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public new HsdmAssessmentConditionBuilder AddDerivedFrom(string reference, string? display = null)
    {
        base.AddDerivedFrom(reference, display);
        return this;
    }

    /// <summary>
    /// Sets the patient reference for this condition
    /// </summary>
    /// <param name="patient">The patient resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public HsdmAssessmentConditionBuilder WithPatient(ResourceReference patient)
    {
        _patientReference = patient ?? throw new ArgumentNullException(nameof(patient));
        return this;
    }

    /// <summary>
    /// Sets the patient reference for this condition
    /// </summary>
    /// <param name="patientId">The patient ID</param>
    /// <param name="display">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public HsdmAssessmentConditionBuilder WithPatient(string patientId, string? display = null)
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
    public HsdmAssessmentConditionBuilder WithDevice(ResourceReference device)
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
    public HsdmAssessmentConditionBuilder WithDevice(string deviceId, string? display = null)
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
    /// Sets the focus reference for this condition (REQUIRED - must reference existing Condition)
    /// REQUIRED: Add clinical note doc ref and Facts doc ref
    /// </summary>
    /// <param name="existingConditionRef">The existing Condition resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public HsdmAssessmentConditionBuilder WithFocus(ResourceReference existingConditionRef)
    {
        if (existingConditionRef == null)
            throw new ArgumentNullException(nameof(existingConditionRef), "Focus reference cannot be null");

        // Validate that the reference is to a Condition resource
        if (!string.IsNullOrWhiteSpace(existingConditionRef.Reference) &&
            !existingConditionRef.Reference.StartsWith("Condition/"))
        {
            throw new ArgumentException(
                "Focus must reference a Condition resource. Reference must start with 'Condition/'",
                nameof(existingConditionRef));
        }

        _focusReferences.Clear();
        _focusReferences.Add(existingConditionRef);
        return this;
    }

    /// <summary>
    /// Sets the focus reference for this condition (REQUIRED - must reference existing Condition)
    /// </summary>
    /// <param name="conditionId">The existing Condition ID</param>
    /// <param name="display">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public HsdmAssessmentConditionBuilder WithFocus(string conditionId, string? display = null)
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
    /// Adds additional focus references (clinical notes, facts documents)
    /// </summary>
    /// <param name="reference">The reference to add</param>
    /// <returns>This builder instance for method chaining</returns>
    public HsdmAssessmentConditionBuilder AddFocus(ResourceReference reference)
    {
        if (reference != null)
        {
            _focusReferences.Add(reference);
        }
        return this;
    }

    /// <summary>
    /// Sets the HSDM result classification
    /// </summary>
    /// <param name="result">Must be one of: "nmCSPC_biochemical_relapse", "mCSPC", "mCRPC"</param>
    /// <returns>This builder instance for method chaining</returns>
    public HsdmAssessmentConditionBuilder WithHSDMResult(string result)
    {
        if (string.IsNullOrWhiteSpace(result))
            throw new ArgumentException("HSDM result cannot be null or empty", nameof(result));

        if (result != HsdmResults.NonMetastaticBiochemicalRelapse &&
            result != HsdmResults.MetastaticCastrationSensitive &&
            result != HsdmResults.MetastaticCastrationResistant)
        {
            throw new ArgumentException(
                $"Invalid HSDM result. Must be one of: {HsdmResults.NonMetastaticBiochemicalRelapse}, " +
                $"{HsdmResults.MetastaticCastrationSensitive}, {HsdmResults.MetastaticCastrationResistant}",
                nameof(result));
        }

        _hsdmResult = result;
        return this;
    }

    /// <summary>
    /// Adds evidence supporting this condition
    /// </summary>
    /// <param name="reference">The evidence resource reference</param>
    /// <param name="displayText">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public HsdmAssessmentConditionBuilder AddEvidence(ResourceReference reference, string? displayText = null)
    {
        if (reference != null)
        {
            if (!string.IsNullOrWhiteSpace(displayText) && string.IsNullOrWhiteSpace(reference.Display))
            {
                reference.Display = displayText;
            }
            _evidenceReferences.Add(reference);
        }
        return this;
    }

    /// <summary>
    /// Adds evidence supporting this condition
    /// </summary>
    /// <param name="referenceString">The evidence reference string</param>
    /// <param name="displayText">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public HsdmAssessmentConditionBuilder AddEvidence(string referenceString, string? displayText = null)
    {
        if (!string.IsNullOrWhiteSpace(referenceString))
        {
            var reference = new ResourceReference
            {
                Reference = referenceString,
                Display = displayText
            };
            _evidenceReferences.Add(reference);
        }
        return this;
    }

    /// <summary>
    /// Adds clinical facts as evidence (REQUIRED)
    /// </summary>
    /// <param name="facts">Array of clinical facts</param>
    /// <returns>This builder instance for method chaining</returns>
    public HsdmAssessmentConditionBuilder AddFactEvidence(params Fact[] facts)
    {
        if (facts == null || facts.Length == 0)
            throw new ArgumentException("At least one fact is required", nameof(facts));

        _facts.AddRange(facts.Where(f => f != null));

        // Add document references as evidence
        foreach (var fact in facts.Where(f => f != null && !string.IsNullOrWhiteSpace(f.factDocumentReference)))
        {
            AddEvidence(fact.factDocumentReference, $"Fact evidence: {fact.type}");
        }

        return this;
    }

    /// <summary>
    /// Sets the effective date/time of this condition
    /// </summary>
    /// <param name="effectiveDate">The effective date/time</param>
    /// <returns>This builder instance for method chaining</returns>
    public HsdmAssessmentConditionBuilder WithEffectiveDate(DateTime effectiveDate)
    {
        _effectiveDate = new FhirDateTime(effectiveDate);
        return this;
    }

    /// <summary>
    /// Sets the effective date/time of this condition
    /// </summary>
    /// <param name="effectiveDate">The effective date/time as DateTimeOffset</param>
    /// <returns>This builder instance for method chaining</returns>
    public HsdmAssessmentConditionBuilder WithEffectiveDate(DateTimeOffset effectiveDate)
    {
        _effectiveDate = new FhirDateTime(effectiveDate);
        return this;
    }

    /// <summary>
    /// Sets the AI confidence score for this assessment
    /// </summary>
    /// <param name="confidence">The confidence score (0.0 to 1.0)</param>
    /// <returns>This builder instance for method chaining</returns>
    public HsdmAssessmentConditionBuilder WithConfidence(float confidence)
    {
        if (confidence < 0.0f || confidence > 1.0f)
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be between 0.0 and 1.0");

        _confidence = confidence;
        return this;
    }

    /// <summary>
    /// Adds a summary note to this condition (REQUIRED)
    /// </summary>
    /// <param name="noteText">The summary note text</param>
    /// <returns>This builder instance for method chaining</returns>
    public HsdmAssessmentConditionBuilder WithSummary(string noteText)
    {
        if (string.IsNullOrWhiteSpace(noteText))
            throw new ArgumentException("Summary note text cannot be null or empty", nameof(noteText));

        _notes.Clear(); // Only keep the most recent summary
        _notes.Add(noteText);
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

        if (_focusReferences.Count == 0)
        {
            throw new InvalidOperationException(
                "Focus reference is required. Call WithFocus() before Build().");
        }

        if (string.IsNullOrWhiteSpace(_hsdmResult))
        {
            throw new InvalidOperationException(
                "HSDM result is required. Call WithHSDMResult() before Build().");
        }

        if (_facts.Count == 0)
        {
            throw new InvalidOperationException(
                "Fact evidence is required. Call AddFactEvidence() before Build().");
        }

        if (_notes.Count == 0)
        {
            throw new InvalidOperationException(
                "Summary note is required. Call WithSummary() before Build().");
        }
    }

    /// <summary>
    /// Builds the HSDM Assessment Condition
    /// </summary>
    /// <returns>The completed Condition resource</returns>
    protected override Condition BuildCore()
    {
        var condition = new Condition
        {
            // Clinical status: active (the condition is currently present)
            ClinicalStatus = new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new Coding
                    {
                        System = "http://terminology.hl7.org/CodeSystem/condition-clinical",
                        Code = "active",
                        Display = "Active"
                    }
                }
            },

            // Verification status: confirmed (the condition has been confirmed)
            VerificationStatus = new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new Coding
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
                new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new Coding
                        {
                            System = "http://terminology.hl7.org/CodeSystem/condition-category",
                            Code = "encounter-diagnosis",
                            Display = "Encounter Diagnosis"
                        }
                    }
                }
            },

            // Code: HSDM result with appropriate SNOMED and ICD-10 codes
            Code = CreateHsdmResultCode(),

            // Subject (Patient)
            Subject = _patientReference,

            // Recorded date
            RecordedDate = _effectiveDate?.ToString() ?? DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),

            // Recorder (Device)
            Recorder = _deviceReference
        };

        // Add evidence references
        if (_evidenceReferences.Any())
        {
            condition.Evidence = _evidenceReferences.Select(evidence => new Condition.EvidenceComponent
            {
                Detail = new List<ResourceReference> { evidence }
            }).ToList();
        }

        // Add notes
        if (_notes.Any())
        {
            condition.Note = _notes.Select(noteText => new Annotation
            {
                Time = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                Text = new Markdown(noteText)
            }).ToList();
        }

        // Add clinical fact extensions
        if (_facts.Any())
        {
            condition.Extension = condition.Extension ?? new List<Extension>();
            condition.Extension.AddRange(ClinicalFactExtension.CreateExtensions(_facts));
        }

        // Add confidence as an extension if specified
        if (_confidence.HasValue)
        {
            condition.Extension = condition.Extension ?? new List<Extension>();
            condition.Extension.Add(new Extension("http://thirdopinion.ai/fhir/StructureDefinition/confidence",
                new FhirDecimal((decimal)_confidence.Value)));
        }

        // Add criteria extension if specified
        if (!string.IsNullOrWhiteSpace(CriteriaId))
        {
            condition.Extension = condition.Extension ?? new List<Extension>();
            var criteriaExtension = new Extension
            {
                Url = "http://thirdopinion.ai/fhir/StructureDefinition/assessment-criteria"
            };
            criteriaExtension.Extension.Add(new Extension("id", new FhirString(CriteriaId)));
            criteriaExtension.Extension.Add(new Extension("display", new FhirString(CriteriaDisplay ?? "")));

            if (!string.IsNullOrWhiteSpace(_criteriaDescription))
            {
                criteriaExtension.Extension.Add(new Extension("description", new FhirString(_criteriaDescription)));
            }

            condition.Extension.Add(criteriaExtension);
        }

        return condition;
    }

    /// <summary>
    /// Creates the appropriate code for the HSDM result
    /// </summary>
    private CodeableConcept CreateHsdmResultCode()
    {
        return _hsdmResult switch
        {
            HsdmResults.NonMetastaticBiochemicalRelapse => new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new Coding
                    {
                        System = FhirCodingHelper.Systems.SNOMED_SYSTEM,
                        Code = "1197209002",
                        Display = "Castration-sensitive prostate cancer"
                    },
                    new Coding
                    {
                        System = "http://hl7.org/fhir/sid/icd-10-cm",
                        Code = "Z19.1",
                        Display = "Hormone sensitive malignancy status"
                    },
                    new Coding
                    {
                        System = "http://hl7.org/fhir/sid/icd-10-cm",
                        Code = "R97.21",
                        Display = "Rising PSA following treatment for malignant neoplasm of prostate"
                    }
                },
                Text = "Castration-Sensitive Prostate Cancer with Biochemical Relapse"
            },

            HsdmResults.MetastaticCastrationSensitive => new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new Coding
                    {
                        System = FhirCodingHelper.Systems.SNOMED_SYSTEM,
                        Code = "1197209002",
                        Display = "Castration-sensitive prostate cancer"
                    },
                    new Coding
                    {
                        System = "http://hl7.org/fhir/sid/icd-10-cm",
                        Code = "Z19.1",
                        Display = "Hormone sensitive malignancy status"
                    }
                },
                Text = "Castration-Sensitive Prostate Cancer (mCSPC)"
            },

            HsdmResults.MetastaticCastrationResistant => new CodeableConcept
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
                        Display = "Hormone resistant malignancy status"
                    }
                },
                Text = "Castration-Resistant Prostate Cancer (mCRPC)"
            },

            _ => throw new InvalidOperationException($"Unknown HSDM result: {_hsdmResult}")
        };
    }
}