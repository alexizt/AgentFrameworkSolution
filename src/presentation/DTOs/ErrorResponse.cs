namespace AgentFrameworkSolution.Presentation.DTOs;

/// <summary>
/// Standardized error response returned to clients.
/// Provides error message, error code, and optional trace ID for debugging.
/// </summary>
public sealed record ErrorResponse(
    string Error,
    string Code,
    string? TraceId);
