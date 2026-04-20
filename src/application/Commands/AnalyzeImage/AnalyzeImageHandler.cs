using AgentFrameworkSolution.Application.DTOs;
using AgentFrameworkSolution.Application.Errors;
using AgentFrameworkSolution.Application.Interfaces;
using AgentFrameworkSolution.Domain.Errors;
using MediatR;

namespace AgentFrameworkSolution.Application.Commands.AnalyzeImage;

public sealed class AnalyzeImageHandler : IRequestHandler<AnalyzeImageCommand, ImageAnalysisDto>
{
    private static readonly HashSet<string> SupportedContentTypes =
        ["image/jpeg", "image/png", "image/webp", "image/gif"];

    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    private readonly IImageAnalyzer _imageAnalyzer;

    public AnalyzeImageHandler(IImageAnalyzer imageAnalyzer)
    {
        _imageAnalyzer = imageAnalyzer;
    }

    public async Task<ImageAnalysisDto> Handle(AnalyzeImageCommand request, CancellationToken cancellationToken)
    {
        if (request.ImageData.Length == 0)
            throw new InvalidImageError("Image data is empty.");

        if (request.ImageData.Length > MaxFileSizeBytes)
            throw new ImageTooLargeError(MaxFileSizeBytes);

        if (!SupportedContentTypes.Contains(request.ContentType))
            throw new UnsupportedImageFormatError(request.ContentType);

        var result = await _imageAnalyzer.AnalyzeAsync(
            request.ImageData,
            request.ContentType,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(result.Summary))
            throw new AnalysisFailedError("The model returned an empty analysis.");

        return new ImageAnalysisDto(
            Id: Guid.NewGuid(),
            FileName: request.FileName,
            Summary: result.Summary,
            Insights: result.Insights,
            Tags: result.Tags,
            AnalyzedAt: DateTime.UtcNow);
    }
}
