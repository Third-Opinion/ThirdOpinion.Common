using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Builders.Base;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;

namespace ThirdOpinion.Common.Fhir.Builders.Observations;

/// <summary>
///     Builder for creating FHIR Observations that track Androgen Deprivation Therapy (ADT) status
/// </summary>
public class AdtStatusObservationBuilder : AiResourceBuilderBase<Observation, AdtStatusObservationBuilder>
{
    private FhirDateTime? _effectiveDate;
    private bool? _isReceivingAdt;
    private string? _medicationReferenceId;

    // Treatment start date information
    private DateTime? _treatmentStartDate;
    private string? _treatmentStartDisplayText;

    /// <summary>
    ///     Creates a new ADT Status Observation builder
    /// </summary>
    /// <param name="configuration">The AI inference configuration</param>
    public AdtStatusObservationBuilder(AiInferenceConfiguration configuration)
        : base(configuration)
    {
    }

    /// <summary>
    ///     Sets the ADT therapy status
    /// </summary>
    /// <param name="isReceivingAdt">True if patient is receiving ADT, false otherwise</param>
    /// <returns>This builder instance for method chaining</returns>
    public AdtStatusObservationBuilder WithStatus(bool isReceivingAdt)
    {
        _isReceivingAdt = isReceivingAdt;
        return this;
    }

    /// <summary>
    ///     Sets the effective date/time of this observation
    /// </summary>
    /// <param name="effectiveDate">The effective date/time</param>
    /// <returns>This builder instance for method chaining</returns>
    public AdtStatusObservationBuilder WithEffectiveDate(DateTime effectiveDate)
    {
        _effectiveDate = new FhirDateTime(effectiveDate);
        return this;
    }

    /// <summary>
    ///     Sets the effective date/time of this observation
    /// </summary>
    /// <param name="effectiveDate">The effective date/time as DateTimeOffset</param>
    /// <returns>This builder instance for method chaining</returns>
    public AdtStatusObservationBuilder WithEffectiveDate(DateTimeOffset effectiveDate)
    {
        _effectiveDate = new FhirDateTime(effectiveDate);
        return this;
    }

    /// <summary>
    ///     Sets the treatment start date information for this observation
    /// </summary>
    /// <param name="treatmentStartDate">The date treatment started</param>
    /// <param name="medicationReferenceId">The medication reference ID</param>
    /// <param name="displayText">The display text for the treatment start date</param>
    /// <returns>This builder instance for method chaining</returns>
    public AdtStatusObservationBuilder WithTreatmentStartDate(DateTime treatmentStartDate,
        string medicationReferenceId,
        string displayText)
    {
        if (string.IsNullOrWhiteSpace(medicationReferenceId))
            throw new ArgumentException("Medication reference ID cannot be null or empty",
                nameof(medicationReferenceId));

        if (string.IsNullOrWhiteSpace(displayText))
            throw new ArgumentException("Display text cannot be null or empty",
                nameof(displayText));

        _treatmentStartDate = treatmentStartDate;
        _medicationReferenceId = medicationReferenceId;
        _treatmentStartDisplayText = displayText;
        return this;
    }

    /// <summary>
    ///     Validates that required fields are set before building
    /// </summary>
    protected override void ValidateRequiredFields()
    {
        if (PatientReference == null)
            throw new InvalidOperationException(
                "Patient reference is required. Call WithPatient() before Build().");

        if (DeviceReference == null)
            throw new InvalidOperationException(
                "Device reference is required. Call WithDevice() before Build().");

        if (!_isReceivingAdt.HasValue)
            throw new InvalidOperationException(
                "ADT status is required. Call WithStatus() before Build().");
    }

    /// <summary>
    ///     Builds the ADT Status Observation
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
                new()
                {
                    Coding = new List<Coding>
                    {
                        new()
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
            Subject = PatientReference,

            // Device
            Device = DeviceReference,

            // Effective date/time
            Effective = _effectiveDate ?? new FhirDateTime(DateTimeOffset.Now),

            // Value: Active or Inactive status
            Value = CreateStatusValue()
        };

        // Add method if criteria was set
        if (!string.IsNullOrWhiteSpace(CriteriaId))
            observation.Method = new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new()
                    {
                        System = CriteriaSystem ?? Configuration.CriteriaSystem,
                        Code = CriteriaId,
                        Display = CriteriaDisplay
                    }
                },
                Text = CriteriaDisplay
            };

        // Add evidence to derivedFrom
        // Note: FHIR Observation initializes DerivedFrom as an empty list, not null
        // We only populate it if we have items to add
        if (EvidenceReferences.Any() || DerivedFromReferences.Any())
        {
            observation.DerivedFrom.Clear(); // Clear the default empty list

            // Add evidence references
            observation.DerivedFrom.AddRange(EvidenceReferences);

            // Add any additional derived from references from base class
            observation.DerivedFrom.AddRange(DerivedFromReferences);
        }

        // Add confidence component if specified
        if (Confidence.HasValue)
        {
            if (observation.Component == null)
                observation.Component = new List<Observation.ComponentComponent>();

            var confidenceComponent = new Observation.ComponentComponent
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
                    Value = (decimal)Confidence.Value,
                    Unit = "probability",
                    System = "http://unitsofmeasure.org",
                    Code = "1"
                }
            };

            observation.Component.Add(confidenceComponent);
        }

        // Add treatment start date component if specified
        if (_treatmentStartDate.HasValue && !string.IsNullOrWhiteSpace(_medicationReferenceId) &&
            !string.IsNullOrWhiteSpace(_treatmentStartDisplayText))
        {
            if (observation.Component == null)
                observation.Component = new List<Observation.ComponentComponent>();

            var treatmentStartComponent = new Observation.ComponentComponent
            {
                Code = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new()
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
                    new()
                    {
                        Url
                            = "https://thirdopinion.io/fhir/StructureDefinition/source-medication-reference",
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
        if (Notes.Any())
        {
            observation.Note.Clear(); // Clear the default empty list
            observation.Note.AddRange(Notes.Select(noteText => new Annotation
            {
                Time = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                Text = new Markdown(noteText)
            }));
        }

        return observation;
    }

    /// <summary>
    ///     Creates the appropriate value CodeableConcept based on ADT status
    /// </summary>
    private CodeableConcept CreateStatusValue()
    {
        if (_isReceivingAdt == true)
            // Active status
            return FhirCodingHelper.CreateSnomedConcept(
                FhirCodingHelper.SnomedCodes.ACTIVE_STATUS,
                "Active");

        // Inactive/Not receiving - use appropriate SNOMED code
        // Using "Inactive" status (385655000) from SNOMED
        return FhirCodingHelper.CreateSnomedConcept(
            "385655000",
            "Inactive");
    }
}