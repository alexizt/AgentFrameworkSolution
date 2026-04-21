using System.Text.Json;
using AgentFrameworkSolution.Application.Errors;
using AgentFrameworkSolution.Domain.Errors;
using AgentFrameworkSolution.Presentation.DTOs;

namespace AgentFrameworkSolution.Presentation.Middleware;

/// <summary>
/// Global exception handling middleware that catches unhandled exceptions and returns sanitized responses.
/// Logs full exception details internally while exposing only safe information to clients.
/// </summary>
public sealed class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, errorResponse) = exception switch
        {
            DomainError domainError => HandleDomainError(domainError),
            ApplicationError appError => HandleApplicationError(appError),
            _ => HandleUnexpectedException(exception, context)
        };

        context.Response.StatusCode = statusCode;

        return context.Response.WriteAsJsonAsync(errorResponse);
    }

    private (int StatusCode, ErrorResponse Response) HandleDomainError(DomainError domainError)
    {
        _logger.LogWarning(
            domainError,
            "Domain error occurred: {Code} - {Message}",
            domainError.Code,
            domainError.Message);

        return (StatusCodes.Status400BadRequest, new ErrorResponse(
            Error: domainError.Message,
            Code: domainError.Code,
            TraceId: null
        ));
    }

    private (int StatusCode, ErrorResponse Response) HandleApplicationError(ApplicationError appError)
    {
        _logger.LogError(
            appError,
            "Application error occurred: {Code} - {Message}",
            appError.Code,
            appError.Message);

        return (StatusCodes.Status500InternalServerError, new ErrorResponse(
            Error: appError.Message,
            Code: appError.Code,
            TraceId: null
        ));
    }

    private (int StatusCode, ErrorResponse Response) HandleUnexpectedException(Exception exception, HttpContext context)
    {
        // Log the full exception with stack trace for debugging
        _logger.LogError(
            exception,
            "Unhandled exception occurred: {Type} - {Message}",
            exception.GetType().Name,
            exception.Message);

        // In production, don't expose internal details; in development, include more info
        var isDevelopment = context.RequestServices
            .GetRequiredService<IHostEnvironment>()
            .IsDevelopment();

        var errorMessage = isDevelopment
            ? $"An unexpected error occurred: {exception.Message}"
            : "An unexpected error occurred. Please contact support.";

        var traceId = context.TraceIdentifier;

        return (StatusCodes.Status500InternalServerError, new ErrorResponse(
            Error: errorMessage,
            Code: "INTERNAL_SERVER_ERROR",
            TraceId: traceId
        ));
    }
}
