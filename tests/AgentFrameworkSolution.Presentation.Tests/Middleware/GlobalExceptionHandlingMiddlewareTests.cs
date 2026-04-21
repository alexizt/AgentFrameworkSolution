using System.Text.Json;
using AgentFrameworkSolution.Application.Errors;
using AgentFrameworkSolution.Domain.Errors;
using AgentFrameworkSolution.Presentation.DTOs;
using AgentFrameworkSolution.Presentation.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AgentFrameworkSolution.Presentation.Tests.Middleware;

/// <summary>
/// Unit tests for GlobalExceptionHandlingMiddleware.
/// Verifies proper exception handling, logging, and response sanitization.
/// </summary>
public sealed class GlobalExceptionHandlingMiddlewareTests
{
    private readonly Mock<ILogger<GlobalExceptionHandlingMiddleware>> _mockLogger = new();

    /// <summary>
    /// Test that DomainError exceptions return 400 Bad Request with proper error details.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithDomainError_Returns400BadRequest()
    {
        // Arrange
        var domainError = new TestDomainError("Invalid email format", "INVALID_EMAIL");
        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => throw domainError,
            _mockLogger.Object);

        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        var response = await ReadErrorResponse(context);
        Assert.NotNull(response);
        Assert.Equal("Invalid email format", response.Error);
        Assert.Equal("INVALID_EMAIL", response.Code);
        Assert.Null(response.TraceId);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Test that ApplicationError exceptions return 500 Internal Server Error with proper error details.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithApplicationError_Returns500InternalServerError()
    {
        // Arrange
        var appError = new TestApplicationError("Database connection failed", "DB_ERROR");
        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => throw appError,
            _mockLogger.Object);

        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        var response = await ReadErrorResponse(context);
        Assert.NotNull(response);
        Assert.Equal("Database connection failed", response.Error);
        Assert.Equal("DB_ERROR", response.Code);
        Assert.Null(response.TraceId);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Test that generic exceptions return 500 with a generic message in Production environment.
    /// Stack trace should not be exposed.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithGenericException_InProduction_ReturnsSanitizedMessage()
    {
        // Arrange
        var innerException = new NullReferenceException("Object reference not set to an instance of an object.");
        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => throw innerException,
            _mockLogger.Object);

        var context = CreateHttpContext(isDevelopment: false);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        var response = await ReadErrorResponse(context);
        Assert.NotNull(response);
        Assert.Equal("An unexpected error occurred. Please contact support.", response.Error);
        Assert.Equal("INTERNAL_SERVER_ERROR", response.Code);
        Assert.NotNull(response.TraceId);
        Assert.DoesNotContain("NullReferenceException", response.Error); // Stack trace not exposed

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Test that generic exceptions return detailed message in Development environment.
    /// This aids debugging during development.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithGenericException_InDevelopment_ReturnsDetailedMessage()
    {
        // Arrange
        var innerException = new NullReferenceException("Object reference not set to an instance of an object.");
        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => throw innerException,
            _mockLogger.Object);

        var context = CreateHttpContext(isDevelopment: true);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        var response = await ReadErrorResponse(context);
        Assert.NotNull(response);
        Assert.Contains("Object reference not set to an instance", response.Error);
        Assert.Equal("INTERNAL_SERVER_ERROR", response.Code);
        Assert.NotNull(response.TraceId);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Test that successful requests (no exception) pass through the middleware unchanged.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithNoException_CallsNextMiddlewareAndReturnsOk()
    {
        // Arrange
        var nextCalled = false;
        RequestDelegate next = context =>
        {
            nextCalled = true;
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };

        var middleware = new GlobalExceptionHandlingMiddleware(next, _mockLogger.Object);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    /// <summary>
    /// Test that response content type is set to application/json.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithException_SetsContentTypeToJson()
    {
        // Arrange
        var domainError = new TestDomainError("Test error", "TEST_CODE");
        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => throw domainError,
            _mockLogger.Object);

        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.StartsWith("application/json", context.Response.ContentType);
    }

    /// <summary>
    /// Test that DomainError logs at Warning level.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithDomainError_LogsAtWarningLevel()
    {
        // Arrange
        var domainError = new TestDomainError("Validation failed", "VALIDATION_ERROR");
        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => throw domainError,
            _mockLogger.Object);

        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("VALIDATION_ERROR")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Test that ApplicationError logs at Error level.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithApplicationError_LogsAtErrorLevel()
    {
        // Arrange
        var appError = new TestApplicationError("Service unavailable", "SERVICE_ERROR");
        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => throw appError,
            _mockLogger.Object);

        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SERVICE_ERROR")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Test that response body is valid JSON that can be deserialized to ErrorResponse.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithException_ResponseBodyIsValidJson()
    {
        // Arrange
        var domainError = new TestDomainError("Invalid input", "INVALID_INPUT");
        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => throw domainError,
            _mockLogger.Object);

        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var response = await ReadErrorResponse(context);
        Assert.NotNull(response);
        Assert.IsType<ErrorResponse>(response);
    }

    // ========== Helper Methods ==========

    private static HttpContext CreateHttpContext(bool isDevelopment = true)
    {
        var services = new ServiceCollection();

        var hostEnvironment = new TestHostEnvironment(isDevelopment);
        services.AddSingleton<IHostEnvironment>(hostEnvironment);

        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        context.TraceIdentifier = "test-trace-id-12345";
        
        // Replace the response body with a memory stream so we can capture the response
        context.Response.Body = new MemoryStream();

        return context;
    }

    private static async Task<ErrorResponse?> ReadErrorResponse(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(json))
            return null;

        return JsonSerializer.Deserialize<ErrorResponse>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    // ========== Test Helpers ==========

    private sealed class TestDomainError : DomainError
    {
        public TestDomainError(string message, string code) : base(message, code)
        {
        }
    }

    private sealed class TestApplicationError : ApplicationError
    {
        public TestApplicationError(string message, string code) : base(message, code)
        {
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        private readonly bool _isDevelopment;

        public TestHostEnvironment(bool isDevelopment)
        {
            _isDevelopment = isDevelopment;
        }

        public string EnvironmentName
        {
            get => _isDevelopment ? Environments.Development : Environments.Production;
            set { }
        }

        public string ApplicationName { get; set; } = "TestApp";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        // Implement IsDevelopment as a property to work with the code that uses extension method
        public bool IsDevEnvironment => _isDevelopment;
    }
}
