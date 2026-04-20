using AgentFrameworkSolution.Application.DTOs;
using MediatR;

namespace AgentFrameworkSolution.Application.Commands.AnalyzeImage;

public record AnalyzeImageCommand(
    byte[] ImageData,
    string FileName,
    string ContentType) : IRequest<ImageAnalysisDto>;
