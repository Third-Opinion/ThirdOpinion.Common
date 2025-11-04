namespace ThirdOpinion.Common.Fhir.Builders.Observations;

/// <summary>
///     Enum representing the radiographic progression assessment standard to use
/// </summary>
public enum RadiographicStandard
{
    /// <summary>
    ///     RECIST 1.1 (Response Evaluation Criteria in Solid Tumors version 1.1)
    ///     Used for solid tumor response assessment
    /// </summary>
    RECIST_1_1,

    /// <summary>
    ///     PCWG3 (Prostate Cancer Working Group 3) bone scan progression criteria
    ///     Used for bone scan progression in prostate cancer
    /// </summary>
    PCWG3,

    /// <summary>
    ///     Observed radiographic progression without specific criteria
    ///     Used for general radiographic findings
    /// </summary>
    Observed
}
