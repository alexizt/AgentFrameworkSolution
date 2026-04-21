using AgentFrameworkSolution.Domain.Errors;
using Xunit;

namespace AgentFrameworkSolution.Domain.Tests.Errors;

public sealed class DomainErrorsTests
{
    [Fact]
    public void ImageTooLargeError_SetsExpectedCodeMessageAndBaseState()
    {
        var error = new ImageTooLargeError(10 * 1024 * 1024);

        Assert.Equal("IMAGE_TOO_LARGE", error.Code);
        Assert.Equal("Image exceeds the maximum allowed size of 10 MB.", error.Message);
        Assert.Equal(-1, error.HResult);
        Assert.IsAssignableFrom<DomainError>(error);
    }

    [Fact]
    public void InvalidImageError_SetsExpectedCodeMessageAndBaseState()
    {
        var error = new InvalidImageError("corrupted header");

        Assert.Equal("INVALID_IMAGE", error.Code);
        Assert.Equal("The provided image is invalid: corrupted header", error.Message);
        Assert.Equal(-1, error.HResult);
        Assert.IsAssignableFrom<DomainError>(error);
    }

    [Fact]
    public void UnsupportedImageFormatError_SetsExpectedCodeMessageAndBaseState()
    {
        var error = new UnsupportedImageFormatError("image/bmp");

        Assert.Equal("UNSUPPORTED_FORMAT", error.Code);
        Assert.Equal("Image format 'image/bmp' is not supported. Use JPEG, PNG, WEBP, or GIF.", error.Message);
        Assert.Equal(-1, error.HResult);
        Assert.IsAssignableFrom<DomainError>(error);
    }
}