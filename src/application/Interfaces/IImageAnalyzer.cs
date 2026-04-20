using AgentFrameworkSolution.Domain.ValueObjects;

namespace AgentFrameworkSolution.Application.Interfaces;

public interface IImageAnalyzer
{
    Task<ImageAnalysisResult> AnalyzeAsync(
        byte[] imageData,
        string contentType,
        string? model = null,
        SupportedLanguage? language = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetAvailableModelsAsync(
        CancellationToken cancellationToken = default);
}
