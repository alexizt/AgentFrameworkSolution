using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Concurrent;
using AgentFrameworkSolution.Application.Errors;
using AgentFrameworkSolution.Application.Interfaces;
using AgentFrameworkSolution.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace AgentFrameworkSolution.Infrastructure.Services;

public sealed class OllamaImageAnalyzer : IImageAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly double _temperature;
    private readonly ILogger<OllamaImageAnalyzer> _logger;

    public OllamaImageAnalyzer(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OllamaImageAnalyzer> logger)
    {
        _httpClient = httpClient;
        _model = configuration["Ollama:Model"] ?? "gemma4:e4b";
        _temperature = ParseTemperature(configuration["Ollama:Temperature"]);
        _logger = logger;
    }

    public async Task<ImageAnalysisResult> AnalyzeAsync(
        byte[] imageData,
        string contentType,
        string? model = null,
        SupportedLanguage? language = null,
        CancellationToken cancellationToken = default)
    {
        var base64Image = Convert.ToBase64String(imageData);
        var effectiveModel = string.IsNullOrWhiteSpace(model) ? _model : model.Trim();
        var effectiveLanguage = language ?? SupportedLanguage.English;
        var languageName = effectiveLanguage.GetLanguageName();

        var promptContent = $$"""
            Analyze this image carefully and return a JSON object with exactly these fields:
            {
              "summary": "2-3 sentence description of what you see",
              "insights": ["insight 1", "insight 2", "insight 3"],
              "tags": ["tag1", "tag2", "tag3", "tag4", "tag5"]
            }
            Return only valid JSON. No markdown, no code blocks, no extra text.
            Respond in {{languageName}}.
            """;

        var requestBody = new OllamaChatRequest
        {
            Model = effectiveModel,
            Stream = false,
            Options = new OllamaChatOptions { Temperature = _temperature },
            Messages =
            [
                new OllamaMessage
                {
                    Role = "user",
                    Content = promptContent,
                    Images = [base64Image]
                }
            ]
        };

        var json = JsonSerializer.Serialize(requestBody, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation(
            "Sending image analysis request to Ollama (model: {Model}, language: {Language}, temperature: {Temperature})",
            effectiveModel,
            languageName,
            _temperature);

        var response = await _httpClient.PostAsync("/api/chat", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Ollama returned {StatusCode}: {Body}", response.StatusCode, errorBody);
            throw new AnalysisFailedError($"Ollama service returned {(int)response.StatusCode}.");
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var chatResponse = JsonSerializer.Deserialize<OllamaChatResponse>(responseJson, JsonOptions);
        var rawContent = chatResponse?.Message?.Content;

        if (string.IsNullOrWhiteSpace(rawContent))
            throw new AnalysisFailedError("Ollama returned an empty response.");

        return ParseAnalysisResponse(rawContent, effectiveLanguage);
    }

    public async Task<IReadOnlyList<string>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/api/tags", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Ollama models lookup returned {StatusCode}: {Body}", response.StatusCode, errorBody);
            throw new AnalysisFailedError($"Ollama service returned {(int)response.StatusCode} while fetching models.");
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var modelsResponse = JsonSerializer.Deserialize<OllamaModelsResponse>(responseJson, JsonOptions);
        var modelNames = modelsResponse?.Models?
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .Select(x => x.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];

        if (modelNames.Length == 0)
            return [];

        var visionModels = new ConcurrentBag<string>();
        using var semaphore = new SemaphoreSlim(4);

        var tasks = modelNames.Select(async modelName =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                if (await IsVisionModelAsync(modelName, cancellationToken))
                    visionModels.Add(modelName);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        return visionModels
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];
    }

    private async Task<bool> IsVisionModelAsync(string modelName, CancellationToken cancellationToken)
    {
        var request = new OllamaShowRequest { Name = modelName };
        var requestJson = JsonSerializer.Serialize(request, JsonOptions);
        using var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/api/show", requestContent, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Skipping model {Model} because capability lookup failed with status {StatusCode}.",
                modelName,
                response.StatusCode);
            return false;
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var showResponse = JsonSerializer.Deserialize<OllamaShowResponse>(responseJson, JsonOptions);

        var hasVisionCapability = showResponse?.Capabilities?
            .Any(x => string.Equals(x, "vision", StringComparison.OrdinalIgnoreCase))
            ?? false;

        if (hasVisionCapability)
            return true;

        // Compatibility fallback for older Ollama responses that expose projector/model metadata.
        var hasProjectorMetadata = showResponse?.ProjectorInfo?.Count > 0;
        if (hasProjectorMetadata)
            return true;

        var hasClipModelInfo = showResponse?.ModelInfo?
            .Keys
            .Any(x => x.Contains("clip", StringComparison.OrdinalIgnoreCase))
            ?? false;

        return hasClipModelInfo;
    }

    private ImageAnalysisResult ParseAnalysisResponse(string rawContent, SupportedLanguage language)
    {
        var json = ExtractJson(rawContent);
        try
        {
            var parsed = JsonSerializer.Deserialize<AnalysisJsonPayload>(json, JsonOptions);
            return new ImageAnalysisResult(
                Summary: parsed?.Summary ?? rawContent,
                Insights: parsed?.Insights ?? [],
                Tags: parsed?.Tags ?? [],
                Language: language);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse JSON from Ollama response; using raw text as summary.");
            return new ImageAnalysisResult(rawContent, [], [], language);
        }
    }

    private static string ExtractJson(string text)
    {
        var trimmed = text.Trim();

        // Strip markdown code fence if present
        if (trimmed.StartsWith("```"))
        {
            var firstBrace = trimmed.IndexOf('{');
            var lastBrace = trimmed.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
                return trimmed[firstBrace..(lastBrace + 1)];
        }

        // Find raw JSON object boundaries
        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
            return trimmed[start..(end + 1)];

        return trimmed;
    }

    private static double ParseTemperature(string? rawTemperature)
    {
        const double defaultTemperature = 0.2;

        if (string.IsNullOrWhiteSpace(rawTemperature))
            return defaultTemperature;

        if (!double.TryParse(rawTemperature, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return defaultTemperature;

        // Keep a safe range for model sampling behavior.
        return Math.Clamp(parsed, 0.0, 2.0);
    }

    // ── Private DTOs (infrastructure-internal) ──────────────────────────────

    private sealed class OllamaChatRequest
    {
        [JsonPropertyName("model")] public string Model { get; init; } = string.Empty;
        [JsonPropertyName("stream")] public bool Stream { get; init; }
        [JsonPropertyName("options")] public OllamaChatOptions? Options { get; init; }
        [JsonPropertyName("messages")] public OllamaMessage[] Messages { get; init; } = [];
    }

    private sealed class OllamaChatOptions
    {
        [JsonPropertyName("temperature")] public double Temperature { get; init; }
    }

    private sealed class OllamaMessage
    {
        [JsonPropertyName("role")] public string Role { get; init; } = string.Empty;
        [JsonPropertyName("content")] public string Content { get; init; } = string.Empty;
        [JsonPropertyName("images")] public string[]? Images { get; init; }
    }

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")] public OllamaMessage? Message { get; init; }
    }

    private sealed class AnalysisJsonPayload
    {
        [JsonPropertyName("summary")] public string? Summary { get; init; }
        [JsonPropertyName("insights")] public List<string>? Insights { get; init; }
        [JsonPropertyName("tags")] public List<string>? Tags { get; init; }
    }

    private sealed class OllamaModelsResponse
    {
        [JsonPropertyName("models")] public List<OllamaModelItem>? Models { get; init; }
    }

    private sealed class OllamaModelItem
    {
        [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    }

    private sealed class OllamaShowRequest
    {
        [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    }

    private sealed class OllamaShowResponse
    {
        [JsonPropertyName("capabilities")] public List<string>? Capabilities { get; init; }
        [JsonPropertyName("projector_info")] public Dictionary<string, JsonElement>? ProjectorInfo { get; init; }
        [JsonPropertyName("model_info")] public Dictionary<string, JsonElement>? ModelInfo { get; init; }
    }
}
