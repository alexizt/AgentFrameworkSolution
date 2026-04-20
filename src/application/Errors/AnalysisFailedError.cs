namespace AgentFrameworkSolution.Application.Errors;

public sealed class AnalysisFailedError : ApplicationError
{
    public AnalysisFailedError(string reason)
        : base($"Image analysis failed: {reason}", "ANALYSIS_FAILED") { }
}
