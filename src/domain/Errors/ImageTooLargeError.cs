namespace AgentFrameworkSolution.Domain.Errors;

public sealed class ImageTooLargeError : DomainError
{
    public ImageTooLargeError(long maxBytes)
        : base($"Image exceeds the maximum allowed size of {maxBytes / (1024 * 1024)} MB.", "IMAGE_TOO_LARGE") { }
}
