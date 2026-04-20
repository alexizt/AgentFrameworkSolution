namespace AgentFrameworkSolution.Domain.Errors;

public sealed class UnsupportedImageFormatError : DomainError
{
    public UnsupportedImageFormatError(string contentType)
        : base($"Image format '{contentType}' is not supported. Use JPEG, PNG, WEBP, or GIF.", "UNSUPPORTED_FORMAT") { }
}
