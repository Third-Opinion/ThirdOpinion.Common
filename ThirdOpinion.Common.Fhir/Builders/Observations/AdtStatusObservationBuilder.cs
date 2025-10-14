using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Builders.Base;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;

namespace ThirdOpinion.Common.Fhir.Builders.Observations;

/// <summary>
/// Builder for creating FHIR Observations that track Androgen Deprivation Therapy (ADT) status
/// </summary>
public class AdtStatusObservationBuilder : AiResourceBuilderBase<Observation>
{
    private ResourceReference? _patientReference;
    private ResourceReference? _deviceReference;
    private bool? _isReceivingAdt;
    private FhirDateTime? _effectiveDate;
    private readonly List<(ResourceReference reference, string? display)> _evidenceReferences;
    private readonly List<string> _notes;
    private float? _confidence;

    // Treatment start date information
    private DateTime? _treatmentStartDate;
    private string? _medicationReferenceId;
    private string? _treatmentStartDisplayText;

    /// <summary>
    /// Creates a new ADT Status Observation builder
    /// </summary>
    /// <param name="configuration">The AI inference configuration</param>
    public AdtStatusObservationBuilder(AiInferenceConfiguration configuration)
        : base(configuration)
    {
        _evidenceReferences = new List<(ResourceReference, string?)>();
        _notes = new List<string>();
    }

    /// <summary>
    /// Sets the inference ID for this resource
    /// </summary>
    /// <param name="id">The inference ID</param>
    /// <returns>This builder instance for method chaining</returns>
    public new AdtStatusObservationBuilder WithInferenceId(string id)
    {
        base.WithInferenceId(id);
        return this;
    }

    /// <summary>
    /// Sets the criteria information for this inference
    /// </summary>
    /// <param name="id">The criteria ID</param>
    /// <param name="display">The display text for the criteria</param>
    /// <param name="system">The criteria system URI (optional, uses configuration default if not provided)</param>
    /// <returns>This builder instance for method chaining</returns>
    public new AdtStatusObservationBuilder WithCriteria(string id, string display, string? system = null)
    {
        base.WithCriteria(id, display, system);
        return this;
    }

    /// <summary>
    /// Adds a resource reference that this inference was derived from
    /// </summary>
    /// <param name="reference">The resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public new AdtStatusObservationBuilder AddDerivedFrom(ResourceReference reference)
    {
        base.AddDerivedFrom(reference);
        return this;
    }

    /// <summary>
    /// Adds a resource reference that this inference was derived from
    /// </summary>
    /// <param name="reference">The reference string (e.g., "Patient/123")</param>
    /// <param name="display">Optional display text for the reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public new AdtStatusObservationBuilder AddDerivedFrom(string reference, string? display = null)
    {
        base.AddDerivedFrom(reference, display);
        return this;
    }

    /// <summary>
    /// Sets the patient reference for this observation
    /// </summary>
    /// <param name="patient">The patient resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public AdtStatusObservationBuilder WithPatient(ResourceReference patient)
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
    public AdtStatusObservationBuilder WithPatient(string patientId, string? display = null)
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
    /// Sets the device reference that performed the detection
    /// </summary>
    /// <param name="device">The device resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public AdtStatusObservationBuilder WithDevice(ResourceReference device)
    {
        _deviceReference = device ?? throw new ArgumentNullException(nameof(device));
        return this;
    }

    /// <summary>
    /// Sets the device reference that performed the detection
    /// </summary>
    /// <param name="deviceId">The device ID</param>
    /// <param name="display">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public AdtStatusObservationBuilder WithDevice(string deviceId, string? display = null)
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
    /// Sets the ADT therapy status
    /// </summary>
    /// <param name="isReceivingAdt">True if patient is receiving ADT, false otherwise</param>
    /// <returns>This builder instance for method chaining</returns>
    public AdtStatusObservationBuilder WithStatus(bool isReceivingAdt)
    {
        _isReceivingAdt = isReceivingAdt;
        return this;
    }

    /// <summary>
    /// Adds evidence supporting this observation
    /// </summary>
    /// <param name="reference">The evidence resource reference</param>
    /// <param name="display">Optional display text for the evidence</param>
    /// <returns>This builder instance for method chaining</returns>
    public AdtStatusObservationBuilder AddEvidence(ResourceReference reference, string? display = null)
    {
        if (reference != null)
        {
            _evidenceReferences.Add((reference, display ?? reference.Display));
        }
        return this;
    }

    /// <summary>
    /// Adds evidence supporting this observation
    /// </summary>
    /// <param name="referenceString">The evidence reference string (e.g., "DocumentReference/123")</param>
    /// <param name="display">Optional display text for the evidence</param>
    /// <returns>This builder instance for method chaining</returns>
    public AdtStatusObservationBuilder AddEvidence(string referenceString, string? display = null)
    {
        if (!string.IsNullOrWhiteSpace(referenceString))
        {
            var reference = new ResourceReference
            {
                Reference = referenceString,
                Display = display
            };
            _evidenceReferences.Add((reference, display));
        }
        return this;
    }

    /// <summary>
    /// Sets the effective date/time of this observation
    /// </summary>
    /// <param name="effectiveDate">The effective date/time</param>
    /// <returns>This builder instance for method chaining</returns>
    public AdtStatusObservationBuilder WithEffectiveDate(DateTime effectiveDate)
    {
        _effectiveDate = new FhirDateTime(effectiveDate);
        return this;
    }

    /// <summary>
    /// Sets the effective date/time of this observation
    /// </summary>
    /// <param name="effectiveDate">The effective date/time as DateTimeOffset</param>
    /// <returns>This builder instance for method chaining</returns>
    public AdtStatusObservationBuilder WithEffectiveDate(DateTimeOffset effectiveDate)
    {
        _effectiveDate = new FhirDateTime(effectiveDate);
        return this;
    }

    /// <summary>
    /// Adds a note to this observation
    /// </summary>
    /// <param name="noteText">The note text</param>
    /// <returns>This builder instance for method chaining</returns>
    public AdtStatusObservationBuilder AddNote(string noteText)
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
    public AdtStatusObservationBuilder WithConfidence(float confidence)
    {
        if (confidence < 0.0f || confidence > 1.0f)
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be between 0.0 and 1.0");

        _confidence = confidence;
        return this;
    }

    /// <summary>
    /// Sets the treatment start date information for this observation
    /// </summary>
    /// <param name="treatmentStartDate">The date treatment started</param>
    /// <param name="medicationReferenceId">The medication reference ID</param>
    /// <param name="displayText">The display text for the treatment start date</param>
    /// <returns>This builder instance for method chaining</returns>
    public AdtStatusObservationBuilder WithTreatmentStartDate(DateTime treatmentStartDate, string medicationReferenceId, string displayText)
    {
        if (string.IsNullOrWhiteSpace(medicationReferenceId))
            throw new ArgumentException("Medication reference ID cannot be null or empty", nameof(medicationReferenceId));

        if (string.IsNullOrWhiteSpace(displayText))
            throw new ArgumentException("Display text cannot be null or empty", nameof(displayText));

        _treatmentStartDate = treatmentStartDate;
        _medicationReferenceId = medicationReferenceId;
        _treatmentStartDisplayText = displayText;
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

        if (!_isReceivingAdt.HasValue)
        {
            throw new InvalidOperationException("ADT status is required. Call WithStatus() before Build().");
        }
    }

    /// <summary>
    /// Builds the ADT Status Observation
    /// </summary>
    /// <returns>The completed Observation resource</returns>
    protected override Observation BuildCore()
    {
        var observation = new Observation
        {
            Status = ObservationStatus.Final,

            // Category: therapy
            Category = new List<CodeableConcept>
            {
                new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new Coding
                        {
                            System = "http://terminology.hl7.org/CodeSystem/observation-category",
                            Code = "therapy",
                            Display = "Therapy"
                        }
                    }
                }
            },

            // Code: ADT therapy (SNOMED)
            Code = FhirCodingHelper.CreateSnomedConcept(
                FhirCodingHelper.SnomedCodes.ADT_THERAPY,
                "Androgen deprivation therapy"),

            // Subject (Patient)
            Subject = _patientReference,

            // Device
            Device = _deviceReference,

            // Effective date/time
            Effective = _effectiveDate ?? new FhirDateTime(DateTimeOffset.Now),

            // Value: Active or Inactive status
            Value = CreateStatusValue()
        };

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
        // Note: FHIR Observation initializes DerivedFrom as an empty list, not null
        // We only populate it if we have items to add
        if (_evidenceReferences.Any() || DerivedFromReferences.Any())
        {
            observation.DerivedFrom.Clear(); // Clear the default empty list

            // Add evidence references
            foreach (var (reference, display) in _evidenceReferences)
            {
                if (!string.IsNullOrWhiteSpace(display) && string.IsNullOrWhiteSpace(reference.Display))
                {
                    reference.Display = display;
                }
                observation.DerivedFrom.Add(reference);
            }

            // Add any additional derived from references from base class
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

        // Add treatment start date component if specified
        if (_treatmentStartDate.HasValue && !string.IsNullOrWhiteSpace(_medicationReferenceId) && !string.IsNullOrWhiteSpace(_treatmentStartDisplayText))
        {
            if (observation.Component == null)
                observation.Component = new List<Observation.ComponentComponent>();

            var treatmentStartComponent = new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new Coding
                        {
                            System = "https://thirdopinion.io/result-code",
                            Code = "treatmentStartDate_v1",
                            Display = "The date treatment started"
                        }
                    },
                    Text = _treatmentStartDisplayText
                },
                Value = new FhirDateTime(_treatmentStartDate.Value),
                Extension = new List<Extension>
                {
                    new Extension
                    {
                        Url = "https://thirdopinion.io/fhir/StructureDefinition/source-medication-reference",
                        Value = new ResourceReference
                        {
                            Reference = _medicationReferenceId,
                            Display = "The MedicationReference used in the analysis."
                        }
                    }
                }
            };

            observation.Component.Add(treatmentStartComponent);
        }

        // Add notes
        // Note: FHIR Observation initializes Note as an empty list, not null
        // We only populate it if we have notes to add
        if (_notes.Any())
        {
            observation.Note.Clear(); // Clear the default empty list
            observation.Note.AddRange(_notes.Select(noteText => new Annotation
            {
                Time = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                Text = new Markdown(noteText)
            }));
        }

        return observation;
    }

    /// <summary>
    /// Creates the appropriate value CodeableConcept based on ADT status
    /// </summary>
    private CodeableConcept CreateStatusValue()
    {
        if (_isReceivingAdt == true)
        {
            // Active status
            return FhirCodingHelper.CreateSnomedConcept(
                FhirCodingHelper.SnomedCodes.ACTIVE_STATUS,
                "Active");
        }
        else
        {
            // Inactive/Not receiving - use appropriate SNOMED code
            // Using "Inactive" status (385655000) from SNOMED
            return FhirCodingHelper.CreateSnomedConcept(
                "385655000",
                "Inactive");
        }
    }
}