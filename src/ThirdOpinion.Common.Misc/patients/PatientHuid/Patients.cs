using Amazon.DynamoDBv2.DataModel;
using ThirdOpinion.Common.Aws.DynamoDb.TypeConverters;

namespace Misc.patients.PatientHuid;

public class Patient
{
    public Guid TenantGuid { get; init; }
    public Guid PatientGuid { get; init; }
    public string PatientHuid { get; init; } = string.Empty;
    public string Provenance { get; init; } = string.Empty;
    public DateTime CreatedDateTime { get; private set; }
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

public class Demographics
{
    public enum SexEnum
    {
        Male = 1,
        Female = 2,
        Unknown = 0,
        Null = 3
    }

    public string? Prefix { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? MiddleName { get; set; }
    public string? Suffix { get; set; }

    [DynamoDBProperty("Sex", typeof(NullableEnumConverter<SexEnum>))]
    public SexEnum? Sex { get; set; }

    public DateTime? BirthDate { get; set; }
    public int? Age { get; set; }
    public DateTime? DeathDate { get; set; }
    public string? PhoneNumber { get; set; }
}

public class PostPatient
{
    public Guid TenantGuid { get; init; }
    public virtual Guid? PatientGuid { get; private set; }
    public required string Provenance { get; init; }

    public required Demographics Demographics { get; set; }

    public void SetPatientGuid()
    {
        PatientGuid = Guid.NewGuid();
    }
}

public class MatchPatient
{
    public Guid TenantGuid { get; init; }
    public string? Prefix { get; set; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? MiddleName { get; set; }
    public required Demographics.SexEnum Sex { get; init; }
    public DateTime? BirthDate { get; set; }
    public DateTime? DeathDate { get; set; }
    public string? PhoneNumber { get; set; }
}