using AgentFrameworkSolution.Application.Commands.AnalyzeImage;
using AgentFrameworkSolution.Application.Errors;
using AgentFrameworkSolution.Application.Interfaces;
using AgentFrameworkSolution.Domain.Errors;
using AgentFrameworkSolution.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AgentFrameworkSolution.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ImageAnalysisController : ControllerBase
{
    private static readonly HashSet<string> AllowedContentTypes =
        ["image/jpeg", "image/png", "image/webp", "image/gif"];

    private const long MaxFileSizeBytes = 10L * 1024 * 1024; // 10 MB

    private readonly IMediator _mediator;
    private readonly IImageAnalyzer _imageAnalyzer;

    public ImageAnalysisController(IMediator mediator, IImageAnalyzer imageAnalyzer)
    {
        _mediator = mediator;
        _imageAnalyzer = imageAnalyzer;
    }

    /// <summary>Returns the available Ollama model names configured in the local Ollama instance.</summary>
    [HttpGet("models")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetModels(CancellationToken cancellationToken)
    {
        var models = await _imageAnalyzer.GetAvailableModelsAsync(cancellationToken);
        return Ok(models);
    }

    /// <summary>Analyzes an uploaded image using the configured Ollama vision model.</summary>
    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Analyze(
        IFormFile? file,
        [FromForm] string? model,
        [FromForm] string? language,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file was provided." });

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new { error = "File size exceeds the 10 MB limit." });

        if (!AllowedContentTypes.Contains(file.ContentType))
            return BadRequest(new { error = $"'{file.ContentType}' is not supported. Use JPEG, PNG, WEBP, or GIF." });

        if (!SupportedLanguageExtensions.TryParse(language, out var parsedLanguage))
            return BadRequest(new { error = "Invalid language. Supported languages: English, Spanish, Italian, French, German." });

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);

        var command = new AnalyzeImageCommand(
            ImageData: memoryStream.ToArray(),
            FileName: file.FileName,
            ContentType: file.ContentType,
            Model: model,
            Language: parsedLanguage);

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}
