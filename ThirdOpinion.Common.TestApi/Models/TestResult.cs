namespace ThirdOpinion.Common.TestApi.Models;

public class TestResult
{
    public string TestName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Message { get; set; }
    public TimeSpan Duration { get; set; }
    public Dictionary<string, object>? Details { get; set; }
    public string? Error { get; set; }
}

public class TestSuiteResult
{
    public string ServiceName { get; set; } = string.Empty;
    public List<TestResult> Results { get; set; } = new();
    public int TotalTests => Results.Count;
    public int PassedTests => Results.Count(r => r.Success);
    public int FailedTests => Results.Count(r => !r.Success);
    public TimeSpan TotalDuration => TimeSpan.FromMilliseconds(Results.Sum(r => r.Duration.TotalMilliseconds));
    public bool AllTestsPassed => FailedTests == 0;
}