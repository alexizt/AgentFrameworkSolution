using AgentFrameworkSolution.Application.Interfaces;
using AgentFrameworkSolution.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentFrameworkSolution.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var ollamaBaseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";

        services.AddHttpClient<IImageAnalyzer, OllamaImageAnalyzer>(client =>
        {
            client.BaseAddress = new Uri(ollamaBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(120);
        });

        return services;
    }
}
