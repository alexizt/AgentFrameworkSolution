using AgentFrameworkSolution.Application.Commands.AnalyzeImage;
using AgentFrameworkSolution.Application.DTOs;
using AgentFrameworkSolution.Application.Interfaces;
using AgentFrameworkSolution.Presentation.Controllers;
using MediatR;
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

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task Analyze_WhenRoleMatchesAllowListCaseInsensitive_UsesConfiguredCanonicalRole()
    {
        var sut = CreateController(
            roles: ["Digital Forensic Analyst"],
            out var mediatorMock,
            out _);

        mediatorMock
            .Setup(x => x.Send(It.IsAny<AnalyzeImageCommand>(), It.IsAny<CancellationToken>()))
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
            x.Send(
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
        var bytes = new byte[] { 1, 2, 3, 4 };
        var stream = new MemoryStream(bytes);

        return new FormFile(stream, 0, bytes.Length, "file", "photo.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };
    }
}
