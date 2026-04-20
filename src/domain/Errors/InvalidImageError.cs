namespace AgentFrameworkSolution.Domain.Errors;

public sealed class InvalidImageError : DomainError
{
    public InvalidImageError(string reason)
        : base($"The provided image is invalid: {reason}", "INVALID_IMAGE") { }
}
