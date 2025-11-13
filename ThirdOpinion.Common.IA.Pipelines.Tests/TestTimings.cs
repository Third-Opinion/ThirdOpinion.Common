namespace ThirdOpinion.Common.IA.Pipelines.Tests;

/// <summary>
/// Centralizes delay timings used in integration tests so they remain fast
/// while still modelling relative differences between pipeline stages.
/// </summary>
internal static class TestTimings
{
    public const int MinimalDelayMs = 1;
    public const int FastDelayMs = 2;
    public const int MediumDelayMs = 5;
    public const int SlowDelayMs = 10;
    public const int VerySlowDelayMs = 25;

    public const int ArtifactFlushBufferMs = 150;
    public const int FullFeatureDrainBufferMs = 300;
    public const int SlowStorageDelayMs = 150;
}

