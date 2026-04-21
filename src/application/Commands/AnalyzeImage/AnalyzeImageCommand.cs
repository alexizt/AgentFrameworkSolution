using AgentFrameworkSolution.Application.DTOs;
using AgentFrameworkSolution.Domain.ValueObjects;
using Cortex.Mediator.Commands;

namespace AgentFrameworkSolution.Application.Commands.AnalyzeImage;

public record AnalyzeImageCommand(
    byte[] ImageData,
    string FileName,
    string ContentType,
    string? Model,
    SupportedLanguage? Language = null,
    string? Role = null) : ICommand<ImageAnalysisDto>;
