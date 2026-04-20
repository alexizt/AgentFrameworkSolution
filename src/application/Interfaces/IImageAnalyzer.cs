using AgentFrameworkSolution.Domain.ValueObjects;

namespace AgentFrameworkSolution.Application.Interfaces;

public interface IImageAnalyzer
{
    Task<ImageAnalysisResult> AnalyzeAsync(
        byte[] imageData,
        string contentType,
        CancellationToken cancellationToken = default);
}
