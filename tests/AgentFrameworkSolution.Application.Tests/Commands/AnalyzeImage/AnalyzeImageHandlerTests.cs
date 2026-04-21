using AgentFrameworkSolution.Application.Commands.AnalyzeImage;
using AgentFrameworkSolution.Application.Errors;
using AgentFrameworkSolution.Application.Interfaces;
using AgentFrameworkSolution.Domain.Errors;
using AgentFrameworkSolution.Domain.ValueObjects;
using Xunit;

namespace AgentFrameworkSolution.Application.Tests.Commands.AnalyzeImage;

public sealed class AnalyzeImageHandlerTests
{
    [Fact]
    public async Task Handle_WhenImageDataIsEmpty_ThrowsInvalidImageError()
    {
        var analyzer = new FakeImageAnalyzer();
        var sut = new AnalyzeImageHandler(analyzer);
        var command = new AnalyzeImageCommand([], "empty.png", "image/png", null, SupportedLanguage.English, "Digital Forensic Analyst");

        await Assert.ThrowsAsync<InvalidImageError>(() => sut.Handle(command, CancellationToken.None));
        Assert.False(analyzer.WasCalled);
    }

    [Fact]
    public async Task Handle_WhenImageTooLarge_ThrowsImageTooLargeError()
    {
        var analyzer = new FakeImageAnalyzer();
        var sut = new AnalyzeImageHandler(analyzer);
        var largePayload = new byte[10 * 1024 * 1024 + 1];
        var command = new AnalyzeImageCommand(largePayload, "large.png", "image/png", null, SupportedLanguage.English, "Digital Forensic Analyst");

        await Assert.ThrowsAsync<ImageTooLargeError>(() => sut.Handle(command, CancellationToken.None));
        Assert.False(analyzer.WasCalled);
    }

    [Fact]
    public async Task Handle_WhenContentTypeUnsupported_ThrowsUnsupportedImageFormatError()
    {
        var analyzer = new FakeImageAnalyzer();
        var sut = new AnalyzeImageHandler(analyzer);
        var command = new AnalyzeImageCommand([1, 2, 3], "file.bmp", "image/bmp", null, SupportedLanguage.English, "Digital Forensic Analyst");

        await Assert.ThrowsAsync<UnsupportedImageFormatError>(() => sut.Handle(command, CancellationToken.None));
        Assert.False(analyzer.WasCalled);
    }

    [Fact]
    public async Task Handle_WhenLanguageIsNull_UsesEnglishAndCallsAnalyzer()
    {
        var analyzer = new FakeImageAnalyzer
        {
            Result = new ImageAnalysisResult("summary", ["insight"], ["tag"], SupportedLanguage.English)
        };
        var sut = new AnalyzeImageHandler(analyzer);
        var command = new AnalyzeImageCommand([1, 2, 3], "photo.png", "image/png", "vision-model", null, "Computer Vision Specialist");
        using var cts = new CancellationTokenSource();

        var dto = await sut.Handle(command, cts.Token);

        Assert.True(analyzer.WasCalled);
        Assert.Equal(SupportedLanguage.English, analyzer.CapturedLanguage);
        Assert.Equal("Computer Vision Specialist", analyzer.CapturedRole);
        Assert.Equal("vision-model", analyzer.CapturedModel);
        Assert.Equal("image/png", analyzer.CapturedContentType);
        Assert.Equal([1, 2, 3], analyzer.CapturedImageData);
        Assert.Equal(cts.Token, analyzer.CapturedCancellationToken);

        Assert.Equal("summary", dto.Summary);
        Assert.Equal("photo.png", dto.FileName);
        Assert.Equal("English", dto.Language);
        Assert.Equal("Computer Vision Specialist", dto.Role);
        Assert.NotEqual(Guid.Empty, dto.Id);
    }

    [Fact]
    public async Task Handle_WhenRoleIsMissing_ThrowsAnalysisFailedError()
    {
        var analyzer = new FakeImageAnalyzer();
        var sut = new AnalyzeImageHandler(analyzer);
        var command = new AnalyzeImageCommand([1, 2], "photo.png", "image/png", null, SupportedLanguage.English, "   ");

        var ex = await Assert.ThrowsAsync<AnalysisFailedError>(() => sut.Handle(command, CancellationToken.None));

        Assert.Equal("ANALYSIS_FAILED", ex.Code);
        Assert.False(analyzer.WasCalled);
    }

    [Fact]
    public async Task Handle_WhenAnalyzerReturnsEmptySummary_ThrowsAnalysisFailedError()
    {
        var analyzer = new FakeImageAnalyzer
        {
            Result = new ImageAnalysisResult("   ", [], [], SupportedLanguage.Italian)
        };
        var sut = new AnalyzeImageHandler(analyzer);
        var command = new AnalyzeImageCommand([9, 9], "photo.png", "image/png", null, SupportedLanguage.Italian, "Art Critic or Curator");

        var ex = await Assert.ThrowsAsync<AnalysisFailedError>(() => sut.Handle(command, CancellationToken.None));

        Assert.Equal("ANALYSIS_FAILED", ex.Code);
    }

    [Fact]
    public async Task Handle_WhenValidRequest_ReturnsMappedDto()
    {
        var analyzer = new FakeImageAnalyzer
        {
            Result = new ImageAnalysisResult("A cat on a sofa", ["indoor", "pet"], ["cat", "sofa"], SupportedLanguage.German)
        };
        var sut = new AnalyzeImageHandler(analyzer);
        var command = new AnalyzeImageCommand([5, 4, 3], "cat.jpg", "image/jpeg", "gemma4:e4b", SupportedLanguage.German, "UX/UI Designer");
        var before = DateTime.UtcNow;

        var dto = await sut.Handle(command, CancellationToken.None);

        Assert.Equal("cat.jpg", dto.FileName);
        Assert.Equal("A cat on a sofa", dto.Summary);
        Assert.Equal(["indoor", "pet"], dto.Insights);
        Assert.Equal(["cat", "sofa"], dto.Tags);
        Assert.Equal("German", dto.Language);
        Assert.Equal("UX/UI Designer", dto.Role);
        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.InRange(dto.AnalyzedAt, before, DateTime.UtcNow.AddSeconds(1));
    }

    private sealed class FakeImageAnalyzer : IImageAnalyzer
    {
        public bool WasCalled { get; private set; }
        public byte[]? CapturedImageData { get; private set; }
        public string? CapturedContentType { get; private set; }
        public string? CapturedModel { get; private set; }
        public SupportedLanguage? CapturedLanguage { get; private set; }
        public string? CapturedRole { get; private set; }
        public CancellationToken CapturedCancellationToken { get; private set; }

        public ImageAnalysisResult Result { get; set; } = new("summary", [], [], SupportedLanguage.English);

        public Task<ImageAnalysisResult> AnalyzeAsync(
            byte[] imageData,
            string contentType,
            string? model = null,
            SupportedLanguage? language = null,
            string? role = null,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            CapturedImageData = imageData;
            CapturedContentType = contentType;
            CapturedModel = model;
            CapturedLanguage = language;
            CapturedRole = role;
            CapturedCancellationToken = cancellationToken;

            return Task.FromResult(Result);
        }

        public Task<IReadOnlyList<string>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(["gemma4:e4b"]);
    }
}
