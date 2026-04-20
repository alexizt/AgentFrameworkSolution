using AgentFrameworkSolution.Application.DTOs;
using AgentFrameworkSolution.Domain.ValueObjects;
using MediatR;

namespace AgentFrameworkSolution.Application.Commands.AnalyzeImage;

public record AnalyzeImageCommand(
    byte[] ImageData,
    string FileName,
    string ContentType,
    string? Model,
    SupportedLanguage? Language = null) : IRequest<ImageAnalysisDto>;
