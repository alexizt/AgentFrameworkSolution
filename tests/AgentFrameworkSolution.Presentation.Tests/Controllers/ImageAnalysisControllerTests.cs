using AgentFrameworkSolution.Application.Commands.AnalyzeImage;
using AgentFrameworkSolution.Application.DTOs;
using AgentFrameworkSolution.Application.Interfaces;
using Cortex.Mediator;
using AgentFrameworkSolution.Presentation.Controllers;
using AgentFrameworkSolution.Presentation.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace AgentFrameworkSolution.Presentation.Tests.Controllers;

public sealed class ImageAnalysisControllerTests
{
    [Fact]
    public void GetRoles_WhenConfigured_ReturnsConfiguredRoles()
    {
        var sut = CreateController(
            roles: [
                "Digital Forensic Analyst",
                "Computer Vision Specialist"
            ],
            out _,
            out _);

        var result = sut.GetRoles();

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<string[]>(ok.Value);
        Assert.Equal(new[] { "Digital Forensic Analyst", "Computer Vision Specialist" }, payload);
    }

    [Fact]
    public async Task Analyze_WhenRoleMissing_ReturnsBadRequest()
    {
        var sut = CreateController(
            roles: ["Digital Forensic Analyst"],
            out _,
            out _);

        var file = CreateFormFile();

        var result = await sut.Analyze(file, "gemma4:e4b", "English", null, CancellationToken.None);

        AssertBadRequestError(result, "Role is required.", "ROLE_REQUIRED");
    }

    [Fact]
    public async Task Analyze_WhenNoFileProvided_ReturnsBadRequestWithStandardErrorResponse()
    {
        var sut = CreateController(
            roles: ["Digital Forensic Analyst"],
            out _,
            out _);

        var result = await sut.Analyze(null, "gemma4:e4b", "English", "Digital Forensic Analyst", CancellationToken.None);

        AssertBadRequestError(result, "No file was provided.", "FILE_REQUIRED");
    }

    [Fact]
    public async Task Analyze_WhenFileTooLarge_ReturnsBadRequestWithStandardErrorResponse()
    {
        var sut = CreateController(
            roles: ["Digital Forensic Analyst"],
            out _,
            out _);

        var file = CreateFormFile(length: 10L * 1024 * 1024 + 1);

        var result = await sut.Analyze(file, "gemma4:e4b", "English", "Digital Forensic Analyst", CancellationToken.None);

        AssertBadRequestError(result, "File size exceeds the 10 MB limit.", "IMAGE_TOO_LARGE");
    }

    [Fact]
    public async Task Analyze_WhenContentTypeUnsupported_ReturnsBadRequestWithStandardErrorResponse()
    {
        var sut = CreateController(
            roles: ["Digital Forensic Analyst"],
            out _,
            out _);

        var file = CreateFormFile(contentType: "text/plain");

        var result = await sut.Analyze(file, "gemma4:e4b", "English", "Digital Forensic Analyst", CancellationToken.None);

        AssertBadRequestError(result, "'text/plain' is not supported. Use JPEG, PNG, WEBP, or GIF.", "UNSUPPORTED_FORMAT");
    }

    [Fact]
    public async Task Analyze_WhenLanguageInvalid_ReturnsBadRequestWithStandardErrorResponse()
    {
        var sut = CreateController(
            roles: ["Digital Forensic Analyst"],
            out _,
            out _);

        var file = CreateFormFile();

        var result = await sut.Analyze(file, "gemma4:e4b", "Klingon", "Digital Forensic Analyst", CancellationToken.None);

        AssertBadRequestError(result, "Invalid language. Supported languages: English, Spanish, Italian, French, German.", "INVALID_LANGUAGE");
    }

    [Fact]
    public async Task Analyze_WhenRoleNotInAllowList_ReturnsBadRequestWithStandardErrorResponse()
    {
        var sut = CreateController(
            roles: ["Digital Forensic Analyst"],
            out _,
            out _);

        var file = CreateFormFile();

        var result = await sut.Analyze(file, "gemma4:e4b", "English", "Nurse", CancellationToken.None);

        AssertBadRequestError(result, "Invalid role. Select a role from the configured list.", "INVALID_ROLE");
    }

    [Fact]
    public async Task Analyze_WhenRoleMatchesAllowListCaseInsensitive_UsesConfiguredCanonicalRole()
    {
        var sut = CreateController(
            roles: ["Digital Forensic Analyst"],
            out var mediatorMock,
            out _);

        mediatorMock
            .Setup(x => x.SendCommandAsync(It.IsAny<AnalyzeImageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageAnalysisDto(
                Id: Guid.NewGuid(),
                FileName: "photo.png",
                Summary: "summary",
                Insights: ["insight"],
                Tags: ["tag"],
                Language: "English",
                Role: "Digital Forensic Analyst",
                AnalyzedAt: DateTime.UtcNow));

        var file = CreateFormFile();

        var result = await sut.Analyze(file, "gemma4:e4b", "English", "  digital forensic analyst  ", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);

        mediatorMock.Verify(x =>
            x.SendCommandAsync(
                It.Is<AnalyzeImageCommand>(cmd => cmd.Role == "Digital Forensic Analyst"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static ImageAnalysisController CreateController(
        string[] roles,
        out Mock<IMediator> mediatorMock,
        out Mock<IImageAnalyzer> analyzerMock)
    {
        mediatorMock = new Mock<IMediator>();
        analyzerMock = new Mock<IImageAnalyzer>();
        analyzerMock
            .Setup(x => x.GetAvailableModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(["gemma4:e4b"]);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Analysis:Roles:0"] = roles.ElementAtOrDefault(0),
                ["Analysis:Roles:1"] = roles.ElementAtOrDefault(1),
                ["Analysis:Roles:2"] = roles.ElementAtOrDefault(2),
                ["Analysis:Roles:3"] = roles.ElementAtOrDefault(3),
                ["Analysis:Roles:4"] = roles.ElementAtOrDefault(4)
            })
            .Build();

        return new ImageAnalysisController(mediatorMock.Object, analyzerMock.Object, config);
    }

    private static IFormFile CreateFormFile()
    {
        return CreateFormFile("image/png", 4);
    }

    private static IFormFile CreateFormFile(string contentType = "image/png", long length = 4)
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var stream = new MemoryStream(bytes);

        return new FormFile(stream, 0, length, "file", "photo.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static void AssertBadRequestError(IActionResult result, string expectedError, string expectedCode)
    {
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var payload = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal(expectedError, payload.Error);
        Assert.Equal(expectedCode, payload.Code);
        Assert.Null(payload.TraceId);
    }
}
