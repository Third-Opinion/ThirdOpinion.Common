using Amazon.DynamoDBv2.DataModel;
using ThirdOpinion.Common.Aws.DynamoDb.TypeConverters;

namespace Misc.patients.PatientHuid;

/// <summary>
///     Represents a patient entity with unique identifiers and demographic information
/// </summary>
public class Patient
{
    /// <summary>
    ///     Gets the unique identifier for the tenant associated with this patient
    /// </summary>
    public Guid TenantGuid { get; init; }

    /// <summary>
    ///     Gets the unique identifier for this patient
    /// </summary>
    public Guid PatientGuid { get; init; }

    /// <summary>
    ///     Gets the human-readable unique identifier (HUID) for this patient
    /// </summary>
    public string PatientHuid { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the source or origin of this patient record
    /// </summary>
    public string Provenance { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the date and time when this patient record was created
    /// </summary>
    public DateTime CreatedDateTime { get; private set; }

    /// <summary>
    ///     Gets or sets the demographic information for this patient
    /// </summary>
    public Demographics? Demographics { get; set; } = new();

    private static string ParseHuidId(string patientHuidId, out Guid tenantGuid)
    {
        string[] parts = patientHuidId.Split(':');
        tenantGuid = Guid.Parse(parts[0]);
        return parts[1];
    }

//     public static Patient Create(DynamoPatient dynamoPatient)
//     {
//         DynamoDocumentsBase.ParseHashKey(dynamoPatient.PatientId, out var tenantGuid,
//             out var patientGuid);
//         string huid = ParseHuidId(dynamoPatient.PatientHuidId!, out tenantGuid);
//
//         return new Patient
//         {
//             TenantGuid = tenantGuid,
//             PatientGuid = patientGuid,
//             Provenance = dynamoPatient.Provenance,
//             CreatedDateTime = dynamoPatient.CreatedDateTime,
//             Demographics = dynamoPatient.Demographics,
//             PatientHuid = huid
//         };
//     }
}

/// <summary>
///     Contains demographic information for a patient
/// </summary>
public class Demographics
{
    /// <summary>
    ///     Enumeration representing biological sex
    /// </summary>
    public enum SexEnum
    {
        /// <summary>
        ///     Male sex
        /// </summary>
        Male = 1,

        /// <summary>
        ///     Female sex
        /// </summary>
        Female = 2,

        /// <summary>
        ///     Unknown or unspecified sex
        /// </summary>
        Unknown = 0,

        /// <summary>
        ///     Null or not provided
        /// </summary>
        Null = 3
    }

    /// <summary>
    ///     Gets or sets the name prefix (e.g., Mr., Mrs., Dr.)
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    ///     Gets or sets the first name
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    ///     Gets or sets the last name (family name)
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    ///     Gets or sets the middle name
    /// </summary>
    public string? MiddleName { get; set; }

    /// <summary>
    ///     Gets or sets the name suffix (e.g., Jr., Sr., III)
    /// </summary>
    public string? Suffix { get; set; }

    /// <summary>
    ///     Gets or sets the biological sex
    /// </summary>
    [DynamoDBProperty("Sex", typeof(NullableEnumConverter<SexEnum>))]
    public SexEnum? Sex { get; set; }

    /// <summary>
    ///     Gets or sets the date of birth
    /// </summary>
    public DateTime? BirthDate { get; set; }

    /// <summary>
    ///     Gets or sets the age in years
    /// </summary>
    public int? Age { get; set; }

    /// <summary>
    ///     Gets or sets the date of death, if applicable
    /// </summary>
    public DateTime? DeathDate { get; set; }

    /// <summary>
    ///     Gets or sets the primary phone number
    /// </summary>
    public string? PhoneNumber { get; set; }
}

/// <summary>
///     Represents a patient record for creation operations
/// </summary>
public class PostPatient
{
    /// <summary>
    ///     Gets the unique identifier for the tenant
    /// </summary>
    public Guid TenantGuid { get; init; }

    /// <summary>
    ///     Gets the unique identifier for the patient (set when created)
    /// </summary>
    public virtual Guid? PatientGuid { get; private set; }

    /// <summary>
    ///     Gets the source or origin of this patient record
    /// </summary>
    public required string Provenance { get; init; }

    /// <summary>
    ///     Gets or sets the demographic information for this patient
    /// </summary>
    public required Demographics Demographics { get; set; }

    /// <summary>
    ///     Generates and sets a new GUID for the patient
    /// </summary>
    public void SetPatientGuid()
    {
        PatientGuid = Guid.NewGuid();
    }
}

/// <summary>
///     Represents patient data used for matching and deduplication operations
/// </summary>
public class MatchPatient
{
    /// <summary>
    ///     Gets the unique identifier for the tenant
    /// </summary>
    public Guid TenantGuid { get; init; }

    /// <summary>
    ///     Gets or sets the name prefix (e.g., Mr., Mrs., Dr.)
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    ///     Gets the first name (required for matching)
    /// </summary>
    public required string FirstName { get; init; }

    /// <summary>
    ///     Gets the last name (required for matching)
    /// </summary>
    public required string LastName { get; init; }

    /// <summary>
    ///     Gets or sets the middle name
    /// </summary>
    public string? MiddleName { get; set; }

    /// <summary>
    ///     Gets the biological sex (required for matching)
    /// </summary>
    public required Demographics.SexEnum Sex { get; init; }

    /// <summary>
    ///     Gets or sets the date of birth
    /// </summary>
    public DateTime? BirthDate { get; set; }

    /// <summary>
    ///     Gets or sets the date of death, if applicable
    /// </summary>
    public DateTime? DeathDate { get; set; }

    /// <summary>
    ///     Gets or sets the primary phone number
    /// </summary>
    public string? PhoneNumber { get; set; }
}