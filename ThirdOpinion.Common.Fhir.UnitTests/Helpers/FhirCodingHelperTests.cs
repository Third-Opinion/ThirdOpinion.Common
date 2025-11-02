using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using ThirdOpinion.Common.Fhir.Helpers;

namespace ThirdOpinion.Common.Fhir.UnitTests.Helpers;

public class FhirCodingHelperTests
{
    [Fact]
    public void SystemConstants_HaveCorrectValues()
    {
        // Assert
        FhirCodingHelper.Systems.SNOMED_SYSTEM.ShouldBe("http://snomed.info/sct");
        FhirCodingHelper.Systems.ICD10_SYSTEM.ShouldBe("http://hl7.org/fhir/sid/icd-10");
        FhirCodingHelper.Systems.LOINC_SYSTEM.ShouldBe("http://loinc.org");
        FhirCodingHelper.Systems.NCI_SYSTEM.ShouldBe(
            "http://ncicb.nci.nih.gov/xml/owl/EVS/Thesaurus.owl");
    }

    [Fact]
    public void SnomedCodes_HaveCorrectValues()
    {
        // Assert
        FhirCodingHelper.SnomedCodes.ADT_THERAPY.ShouldBe("413712001");
        FhirCodingHelper.SnomedCodes.CASTRATION_SENSITIVE.ShouldBe("1197209002");
        FhirCodingHelper.SnomedCodes.CASTRATION_RESISTANT.ShouldBe("1197210007");
        FhirCodingHelper.SnomedCodes.AI_ALGORITHM.ShouldBe("706689003");
        FhirCodingHelper.SnomedCodes.ACTIVE_STATUS.ShouldBe("385654001");
        FhirCodingHelper.SnomedCodes.PROSTATE_CANCER.ShouldBe("399068003");
    }

    [Fact]
    public void IcdCodes_HaveCorrectValues()
    {
        // Assert
        FhirCodingHelper.IcdCodes.PROSTATE_CANCER.ShouldBe("C61");
        FhirCodingHelper.IcdCodes.HORMONE_SENSITIVE.ShouldBe("Z19.1");
        FhirCodingHelper.IcdCodes.HORMONE_RESISTANT.ShouldBe("Z19.2");
        FhirCodingHelper.IcdCodes.BREAST_CANCER.ShouldBe("C50");
        FhirCodingHelper.IcdCodes.LUNG_CANCER.ShouldBe("C78.0");
    }

    [Fact]
    public void LoincCodes_HaveCorrectValues()
    {
        // Assert
        FhirCodingHelper.LoincCodes.CANCER_DISEASE_STATUS.ShouldBe("21889-1");
        FhirCodingHelper.LoincCodes.PSA_TOTAL.ShouldBe("2857-1");
        FhirCodingHelper.LoincCodes.PSA_FREE.ShouldBe("19201-3");
        FhirCodingHelper.LoincCodes.TESTOSTERONE.ShouldBe("2986-8");
        FhirCodingHelper.LoincCodes.GLEASON_SCORE.ShouldBe("35266-6");
    }

    [Fact]
    public void NciCodes_HaveCorrectValues()
    {
        // Assert
        FhirCodingHelper.NciCodes.PROSTATE_CANCER.ShouldBe("C7378");
        FhirCodingHelper.NciCodes.CRPC.ShouldBe("C130234");
        FhirCodingHelper.NciCodes.MCRPC.ShouldBe("C132881");
        FhirCodingHelper.NciCodes.ADT.ShouldBe("C15667");
        FhirCodingHelper.NciCodes.PSA.ShouldBe("C25638");
    }

    [Fact]
    public void CreateCodeableConcept_CreatesValidConcept()
    {
        // Act
        CodeableConcept concept = FhirCodingHelper.CreateCodeableConcept(
            FhirCodingHelper.Systems.SNOMED_SYSTEM,
            "12345",
            "Test Display");

        // Assert
        concept.ShouldNotBeNull();
        concept.Coding.ShouldHaveSingleItem();
        concept.Coding[0].System.ShouldBe(FhirCodingHelper.Systems.SNOMED_SYSTEM);
        concept.Coding[0].Code.ShouldBe("12345");
        concept.Coding[0].Display.ShouldBe("Test Display");
        concept.Text.ShouldBe("Test Display");
    }

    [Fact]
    public void CreateCodeableConcept_WithoutDisplay_OmitsText()
    {
        // Act
        CodeableConcept concept = FhirCodingHelper.CreateCodeableConcept(
            FhirCodingHelper.Systems.LOINC_SYSTEM,
            "98765");

        // Assert
        concept.Coding[0].Display.ShouldBeNull();
        concept.Text.ShouldBeNull();
    }

    [Fact]
    public void CreateSnomedConcept_CreatesCorrectConcept()
    {
        // Act
        CodeableConcept concept = FhirCodingHelper.CreateSnomedConcept(
            FhirCodingHelper.SnomedCodes.PROSTATE_CANCER,
            "Prostate Cancer");

        // Assert
        concept.Coding[0].System.ShouldBe(FhirCodingHelper.Systems.SNOMED_SYSTEM);
        concept.Coding[0].Code.ShouldBe("399068003");
        concept.Coding[0].Display.ShouldBe("Prostate Cancer");
    }

    [Fact]
    public void CreateIcd10Concept_CreatesCorrectConcept()
    {
        // Act
        CodeableConcept concept = FhirCodingHelper.CreateIcd10Concept(
            FhirCodingHelper.IcdCodes.PROSTATE_CANCER,
            "Malignant neoplasm of prostate");

        // Assert
        concept.Coding[0].System.ShouldBe(FhirCodingHelper.Systems.ICD10_SYSTEM);
        concept.Coding[0].Code.ShouldBe("C61");
        concept.Coding[0].Display.ShouldBe("Malignant neoplasm of prostate");
    }

    [Fact]
    public void CreateLoincConcept_CreatesCorrectConcept()
    {
        // Act
        CodeableConcept concept = FhirCodingHelper.CreateLoincConcept(
            FhirCodingHelper.LoincCodes.PSA_TOTAL,
            "PSA Total");

        // Assert
        concept.Coding[0].System.ShouldBe(FhirCodingHelper.Systems.LOINC_SYSTEM);
        concept.Coding[0].Code.ShouldBe("2857-1");
        concept.Coding[0].Display.ShouldBe("PSA Total");
    }

    [Fact]
    public void CreateNciConcept_CreatesCorrectConcept()
    {
        // Act
        CodeableConcept concept = FhirCodingHelper.CreateNciConcept(
            FhirCodingHelper.NciCodes.COMPLETE_RESPONSE,
            "Complete Response");

        // Assert
        concept.Coding[0].System.ShouldBe(FhirCodingHelper.Systems.NCI_SYSTEM);
        concept.Coding[0].Code.ShouldBe("C4870");
        concept.Coding[0].Display.ShouldBe("Complete Response");
    }

    [Fact]
    public void CreateConceptFromConstant_FindsSnomedCode()
    {
        // Act
        CodeableConcept? concept = FhirCodingHelper.CreateConceptFromConstant("ADT_THERAPY", "ADT");

        // Assert
        concept.ShouldNotBeNull();
        concept!.Coding[0].System.ShouldBe(FhirCodingHelper.Systems.SNOMED_SYSTEM);
        concept.Coding[0].Code.ShouldBe("413712001");
        concept.Coding[0].Display.ShouldBe("ADT");
    }

    [Fact]
    public void CreateConceptFromConstant_FindsIcdCode()
    {
        // Act
        CodeableConcept? concept = FhirCodingHelper.CreateConceptFromConstant("HORMONE_SENSITIVE");

        // Assert
        concept.ShouldNotBeNull();
        // Should find ICD code (unique to ICD)
        concept!.Coding[0].System.ShouldBe(FhirCodingHelper.Systems.ICD10_SYSTEM);
        concept.Coding[0].Code.ShouldBe("Z19.1");
    }

    [Fact]
    public void CreateConceptFromConstant_FindsLoincCode()
    {
        // Act
        CodeableConcept? concept
            = FhirCodingHelper.CreateConceptFromConstant("PSA_TOTAL", "PSA Total");

        // Assert
        concept.ShouldNotBeNull();
        concept!.Coding[0].System.ShouldBe(FhirCodingHelper.Systems.LOINC_SYSTEM);
        concept.Coding[0].Code.ShouldBe("2857-1");
    }

    [Fact]
    public void CreateConceptFromConstant_FindsNciCode()
    {
        // Act
        CodeableConcept? concept = FhirCodingHelper.CreateConceptFromConstant("CRPC");

        // Assert
        concept.ShouldNotBeNull();
        concept!.Coding[0].System.ShouldBe(FhirCodingHelper.Systems.NCI_SYSTEM);
        concept.Coding[0].Code.ShouldBe("C130234");
    }

    [Fact]
    public void CreateConceptFromConstant_UnknownConstant_ReturnsNull()
    {
        // Act
        CodeableConcept? concept = FhirCodingHelper.CreateConceptFromConstant("UNKNOWN_CONSTANT");

        // Assert
        concept.ShouldBeNull();
    }

    [Fact]
    public void AddCoding_AddsAdditionalCoding()
    {
        // Arrange
        CodeableConcept concept = FhirCodingHelper.CreateSnomedConcept(
            FhirCodingHelper.SnomedCodes.PROSTATE_CANCER,
            "Prostate Cancer");

        // Act
        FhirCodingHelper.AddCoding(
            concept,
            FhirCodingHelper.Systems.ICD10_SYSTEM,
            FhirCodingHelper.IcdCodes.PROSTATE_CANCER,
            "Malignant neoplasm of prostate");

        // Assert
        concept.Coding.Count.ShouldBe(2);
        concept.Coding[0].System.ShouldBe(FhirCodingHelper.Systems.SNOMED_SYSTEM);
        concept.Coding[1].System.ShouldBe(FhirCodingHelper.Systems.ICD10_SYSTEM);
        concept.Coding[1].Code.ShouldBe("C61");
    }

    [Fact]
    public void CreatedCodeableConcepts_SerializeCorrectly()
    {
        // Arrange
        CodeableConcept concept = FhirCodingHelper.CreateLoincConcept(
            FhirCodingHelper.LoincCodes.PSA_TOTAL,
            "Prostate specific antigen [Mass/volume] in Serum or Plasma");

        // Act
        var serializer = new FhirJsonSerializer(new SerializerSettings { Pretty = true });
        string json = serializer.SerializeToString(concept);

        // Assert
        json.ShouldNotBeNullOrEmpty();
        json.ShouldContain("\"system\": \"http://loinc.org\"");
        json.ShouldContain("\"code\": \"2857-1\"");
        json.ShouldContain(
            "\"display\": \"Prostate specific antigen [Mass/volume] in Serum or Plasma\"");
    }

    [Fact]
    public void CreateCodeableConcept_ValidatesParameters()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            FhirCodingHelper.CreateCodeableConcept(null!, "code"));
        Should.Throw<ArgumentException>(() =>
            FhirCodingHelper.CreateCodeableConcept("", "code"));
        Should.Throw<ArgumentException>(() =>
            FhirCodingHelper.CreateCodeableConcept("system", null!));
        Should.Throw<ArgumentException>(() =>
            FhirCodingHelper.CreateCodeableConcept("system", ""));
    }
}