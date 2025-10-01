using Hl7.Fhir.Model;

namespace ThirdOpinion.Common.Fhir.Helpers;

/// <summary>
/// Provides medical coding system constants and factory methods for FHIR CodeableConcept objects
/// </summary>
public static class FhirCodingHelper
{
    /// <summary>
    /// FHIR System URIs for various coding systems
    /// </summary>
    public static class Systems
    {
        public const string SNOMED_SYSTEM = "http://snomed.info/sct";
        public const string ICD10_SYSTEM = "http://hl7.org/fhir/sid/icd-10";
        public const string LOINC_SYSTEM = "http://loinc.org";
        public const string NCI_SYSTEM = "http://ncicb.nci.nih.gov/xml/owl/EVS/Thesaurus.owl";
    }

    /// <summary>
    /// SNOMED-CT codes commonly used in oncology
    /// </summary>
    public static class SnomedCodes
    {
        /// <summary>Androgen deprivation therapy</summary>
        public const string ADT_THERAPY = "413712001";

        /// <summary>Castration sensitive prostate cancer</summary>
        public const string CASTRATION_SENSITIVE = "1197209002";

        /// <summary>Castration resistant prostate cancer</summary>
        public const string CASTRATION_RESISTANT = "1197210007";

        /// <summary>Artificial intelligence algorithm</summary>
        public const string AI_ALGORITHM = "706689003";

        /// <summary>Active status</summary>
        public const string ACTIVE_STATUS = "385654001";

        /// <summary>Prostate cancer</summary>
        public const string PROSTATE_CANCER = "399068003";

        /// <summary>Clinical finding</summary>
        public const string CLINICAL_FINDING = "404684003";

        /// <summary>Procedure</summary>
        public const string PROCEDURE = "71388002";
    }

    /// <summary>
    /// ICD-10 codes for cancer diagnoses
    /// </summary>
    public static class IcdCodes
    {
        /// <summary>Malignant neoplasm of prostate</summary>
        public const string PROSTATE_CANCER = "C61";

        /// <summary>Hormone sensitive status</summary>
        public const string HORMONE_SENSITIVE = "Z19.1";

        /// <summary>Hormone resistant status</summary>
        public const string HORMONE_RESISTANT = "Z19.2";

        /// <summary>Malignant neoplasm of breast</summary>
        public const string BREAST_CANCER = "C50";

        /// <summary>Malignant neoplasm of lung</summary>
        public const string LUNG_CANCER = "C78.0";
    }

    /// <summary>
    /// LOINC codes for laboratory tests and clinical observations
    /// </summary>
    public static class LoincCodes
    {
        /// <summary>Cancer disease status</summary>
        public const string CANCER_DISEASE_STATUS = "21889-1";

        /// <summary>PSA total</summary>
        public const string PSA_TOTAL = "2857-1";

        /// <summary>PSA free</summary>
        public const string PSA_FREE = "19201-3";

        /// <summary>Testosterone</summary>
        public const string TESTOSTERONE = "2986-8";

        /// <summary>Gleason score</summary>
        public const string GLEASON_SCORE = "35266-6";

        /// <summary>Clinical stage TNM</summary>
        public const string CLINICAL_STAGE_TNM = "21902-2";

        /// <summary>Pathologic stage TNM</summary>
        public const string PATHOLOGIC_STAGE_TNM = "21899-0";

        /// <summary>Alkaline phosphatase</summary>
        public const string ALKALINE_PHOSPHATASE = "6768-6";

        /// <summary>Lactate dehydrogenase</summary>
        public const string LACTATE_DEHYDROGENASE = "14804-9";
    }

    /// <summary>
    /// NCI Thesaurus codes for oncology concepts
    /// </summary>
    public static class NciCodes
    {
        /// <summary>Prostate cancer</summary>
        public const string PROSTATE_CANCER = "C7378";

        /// <summary>Castration resistant prostate cancer</summary>
        public const string CRPC = "C130234";

        /// <summary>Metastatic castration resistant prostate cancer</summary>
        public const string MCRPC = "C132881";

        /// <summary>Androgen deprivation therapy</summary>
        public const string ADT = "C15667";

        /// <summary>Prostate specific antigen</summary>
        public const string PSA = "C25638";

        /// <summary>Complete response</summary>
        public const string COMPLETE_RESPONSE = "C4870";

        /// <summary>Partial response</summary>
        public const string PARTIAL_RESPONSE = "C18058";

        /// <summary>Stable disease</summary>
        public const string STABLE_DISEASE = "C18213";

        /// <summary>Progressive disease</summary>
        public const string PROGRESSIVE_DISEASE = "C35571";
    }

    /// <summary>
    /// Creates a CodeableConcept with a single coding
    /// </summary>
    /// <param name="system">The coding system URI</param>
    /// <param name="code">The code value</param>
    /// <param name="display">The display text (optional)</param>
    /// <returns>A new CodeableConcept</returns>
    public static CodeableConcept CreateCodeableConcept(string system, string code, string? display = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(system);
        ArgumentException.ThrowIfNullOrEmpty(code);

        var concept = new CodeableConcept
        {
            Coding = new List<Coding>
            {
                new Coding
                {
                    System = system,
                    Code = code,
                    Display = display
                }
            }
        };

        if (!string.IsNullOrEmpty(display))
        {
            concept.Text = display;
        }

        return concept;
    }

    /// <summary>
    /// Creates a SNOMED-CT CodeableConcept
    /// </summary>
    /// <param name="code">The SNOMED code</param>
    /// <param name="display">The display text (optional)</param>
    /// <returns>A new CodeableConcept with SNOMED coding</returns>
    public static CodeableConcept CreateSnomedConcept(string code, string? display = null)
    {
        return CreateCodeableConcept(Systems.SNOMED_SYSTEM, code, display);
    }

    /// <summary>
    /// Creates an ICD-10 CodeableConcept
    /// </summary>
    /// <param name="code">The ICD-10 code</param>
    /// <param name="display">The display text (optional)</param>
    /// <returns>A new CodeableConcept with ICD-10 coding</returns>
    public static CodeableConcept CreateIcd10Concept(string code, string? display = null)
    {
        return CreateCodeableConcept(Systems.ICD10_SYSTEM, code, display);
    }

    /// <summary>
    /// Creates a LOINC CodeableConcept
    /// </summary>
    /// <param name="code">The LOINC code</param>
    /// <param name="display">The display text (optional)</param>
    /// <returns>A new CodeableConcept with LOINC coding</returns>
    public static CodeableConcept CreateLoincConcept(string code, string? display = null)
    {
        return CreateCodeableConcept(Systems.LOINC_SYSTEM, code, display);
    }

    /// <summary>
    /// Creates an NCI Thesaurus CodeableConcept
    /// </summary>
    /// <param name="code">The NCI code</param>
    /// <param name="display">The display text (optional)</param>
    /// <returns>A new CodeableConcept with NCI coding</returns>
    public static CodeableConcept CreateNciConcept(string code, string? display = null)
    {
        return CreateCodeableConcept(Systems.NCI_SYSTEM, code, display);
    }

    /// <summary>
    /// Creates a CodeableConcept from a constant name using reflection
    /// </summary>
    /// <param name="constantName">The name of the constant to lookup</param>
    /// <param name="display">The display text (optional)</param>
    /// <returns>A new CodeableConcept if constant found, null otherwise</returns>
    public static CodeableConcept? CreateConceptFromConstant(string constantName, string? display = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(constantName);

        // Search in SNOMED codes
        var snomedField = typeof(SnomedCodes).GetField(constantName);
        if (snomedField != null)
        {
            var code = snomedField.GetValue(null)?.ToString();
            if (code != null)
            {
                return CreateSnomedConcept(code, display);
            }
        }

        // Search in ICD codes
        var icdField = typeof(IcdCodes).GetField(constantName);
        if (icdField != null)
        {
            var code = icdField.GetValue(null)?.ToString();
            if (code != null)
            {
                return CreateIcd10Concept(code, display);
            }
        }

        // Search in LOINC codes
        var loincField = typeof(LoincCodes).GetField(constantName);
        if (loincField != null)
        {
            var code = loincField.GetValue(null)?.ToString();
            if (code != null)
            {
                return CreateLoincConcept(code, display);
            }
        }

        // Search in NCI codes
        var nciField = typeof(NciCodes).GetField(constantName);
        if (nciField != null)
        {
            var code = nciField.GetValue(null)?.ToString();
            if (code != null)
            {
                return CreateNciConcept(code, display);
            }
        }

        return null;
    }

    /// <summary>
    /// Adds an additional coding to an existing CodeableConcept
    /// </summary>
    /// <param name="concept">The CodeableConcept to add to</param>
    /// <param name="system">The coding system URI</param>
    /// <param name="code">The code value</param>
    /// <param name="display">The display text (optional)</param>
    /// <returns>The updated CodeableConcept</returns>
    public static CodeableConcept AddCoding(CodeableConcept concept, string system, string code, string? display = null)
    {
        ArgumentNullException.ThrowIfNull(concept);
        ArgumentException.ThrowIfNullOrEmpty(system);
        ArgumentException.ThrowIfNullOrEmpty(code);

        concept.Coding ??= new List<Coding>();
        concept.Coding.Add(new Coding
        {
            System = system,
            Code = code,
            Display = display
        });

        return concept;
    }
}