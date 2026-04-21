using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using AgentFrameworkSolution.Application.Errors;
using AgentFrameworkSolution.Application.Interfaces;
using AgentFrameworkSolution.Domain.ValueObjects;
using AgentFrameworkSolution.Infrastructure.Extensions;
using AgentFrameworkSolution.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgentFrameworkSolution.Infrastructure.Tests.Services;

public sealed class OllamaImageAnalyzerTests
{
    [Fact]
    public async Task AnalyzeAsync_UsesConfiguredModelAndTemperature_WhenModelNotProvided()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(new
            {
                message = new
                {
                    content = "{\"summary\":\"A summary\",\"insights\":[\"i1\"],\"tags\":[\"t1\"]}"
                }
            })
        });

        var sut = CreateAnalyzer(handler, new Dictionary<string, string?>
        {
            ["Ollama:Model"] = "vision-default",
            ["Ollama:Temperature"] = "0.7"
        });

        var result = await sut.AnalyzeAsync([1, 2, 3], "image/png", model: null, language: SupportedLanguage.Spanish);

        Assert.Equal("A summary", result.Summary);
        Assert.Equal(SupportedLanguage.Spanish, result.Language);

        var request = Assert.Single(handler.CapturedRequests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/chat", request.Path);

        var payload = JsonDocument.Parse(request.Body ?? "{}");
        Assert.Equal("vision-default", payload.RootElement.GetProperty("model").GetString());
        Assert.Equal(0.7, payload.RootElement.GetProperty("options").GetProperty("temperature").GetDouble(), 3);
    }

    [Fact]
    public async Task AnalyzeAsync_UsesTrimmedModelOverride_WhenProvided()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(new
            {
                message = new
                {
                    content = "{\"summary\":\"Model override\",\"insights\":[],\"tags\":[]}"
                }
            })
        });

        var sut = CreateAnalyzer(handler, new Dictionary<string, string?>
        {
            ["Ollama:Model"] = "vision-default"
        });

        var result = await sut.AnalyzeAsync([9], "image/png", model: "  custom-model  ", language: SupportedLanguage.English);

        Assert.Equal("Model override", result.Summary);

        var request = Assert.Single(handler.CapturedRequests);
        var payload = JsonDocument.Parse(request.Body ?? "{}");
        Assert.Equal("custom-model", payload.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task AnalyzeAsync_WhenOllamaReturnsError_ThrowsAnalysisFailedError()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("gateway down", Encoding.UTF8, "text/plain")
        });

        var sut = CreateAnalyzer(handler);

        var ex = await Assert.ThrowsAsync<AnalysisFailedError>(() =>
            sut.AnalyzeAsync([1], "image/png", cancellationToken: CancellationToken.None));

        Assert.Equal("ANALYSIS_FAILED", ex.Code);
        Assert.Contains("502", ex.Message);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenResponseMessageContentEmpty_ThrowsAnalysisFailedError()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(new { message = new { content = "" } })
        });

        var sut = CreateAnalyzer(handler);

        var ex = await Assert.ThrowsAsync<AnalysisFailedError>(() =>
            sut.AnalyzeAsync([1], "image/png", cancellationToken: CancellationToken.None));

        Assert.Equal("ANALYSIS_FAILED", ex.Code);
        Assert.Contains("empty response", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenResponseIsMalformed_ReturnsRawSummaryFallback()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(new { message = new { content = "not-json-response" } })
        });

        var sut = CreateAnalyzer(handler);

        var result = await sut.AnalyzeAsync([1], "image/png", language: SupportedLanguage.French);

        Assert.Equal("not-json-response", result.Summary);
        Assert.Empty(result.Insights);
        Assert.Empty(result.Tags);
        Assert.Equal(SupportedLanguage.French, result.Language);
    }

    [Fact]
    public async Task GetAvailableModelsAsync_ReturnsSortedVisionModels_FromCapabilityAndFallbacks()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath;

            if (path == "/api/tags")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent(new
                    {
                        models = new[]
                        {
                            new { name = "llava" },
                            new { name = "clip-only" },
                            new { name = "projector-model" },
                            new { name = "text-only" },
                            new { name = "LLAVA" }
                        }
                    })
                };
            }

            if (path == "/api/show")
            {
                var body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "{}";
                var doc = JsonDocument.Parse(body);
                var name = doc.RootElement.GetProperty("name").GetString();

                return name switch
                {
                    "llava" => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent(new { capabilities = new[] { "vision" } })
                    },
                    "clip-only" => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent(new { model_info = new Dictionary<string, object> { ["clip.embedding"] = 1 } })
                    },
                    "projector-model" => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent(new { projector_info = new Dictionary<string, object> { ["present"] = true } })
                    },
                    _ => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent(new { capabilities = new[] { "text" } })
                    }
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var sut = CreateAnalyzer(handler);

        var models = await sut.GetAvailableModelsAsync();

        Assert.Equal(new[] { "clip-only", "llava", "projector-model" }, models);
    }

    [Fact]
    public async Task GetAvailableModelsAsync_WhenTagsEndpointFails_ThrowsAnalysisFailedError()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var sut = CreateAnalyzer(handler);

        var ex = await Assert.ThrowsAsync<AnalysisFailedError>(() => sut.GetAvailableModelsAsync());

        Assert.Equal("ANALYSIS_FAILED", ex.Code);
        Assert.Contains("fetching models", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddInfrastructure_RegistersIImageAnalyzer_WithConfiguredBaseUrlAndTimeout()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ollama:BaseUrl"] = "http://localhost:12345",
                ["Ollama:Model"] = "configured-model",
                ["Ollama:Temperature"] = "0.4"
            })
            .Build();

        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructure(config);

        await using var provider = services.BuildServiceProvider();
        var analyzer = provider.GetRequiredService<IImageAnalyzer>();

        var client = GetHttpClient(analyzer);
        Assert.Equal(new Uri("http://localhost:12345"), client.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(120), client.Timeout);
    }

    [Fact]
    public async Task AddInfrastructure_UsesDefaultBaseUrl_WhenConfigMissing()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructure(config);

        await using var provider = services.BuildServiceProvider();
        var analyzer = provider.GetRequiredService<IImageAnalyzer>();

        var client = GetHttpClient(analyzer);
        Assert.Equal(new Uri("http://localhost:11434"), client.BaseAddress);
    }

    private static OllamaImageAnalyzer CreateAnalyzer(
        HttpMessageHandler handler,
        IDictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings ?? new Dictionary<string, string?>())
            .Build();

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };

        return new OllamaImageAnalyzer(client, configuration, NullLogger<OllamaImageAnalyzer>.Instance);
    }

    private static HttpClient GetHttpClient(IImageAnalyzer analyzer)
    {
        var field = analyzer.GetType().GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        var value = field.GetValue(analyzer) as HttpClient;
        Assert.NotNull(value);

        return value;
    }

    private static StringContent JsonContent(object value)
        => new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder = responder;
        public List<CapturedRequest> CapturedRequests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            CapturedRequests.Add(new CapturedRequest(request.Method, request.RequestUri?.AbsolutePath, body));
            return _responder(request);
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, string? Path, string? Body);
}
