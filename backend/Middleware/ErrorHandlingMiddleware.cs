using System.Text.Json;
using Microsoft.AspNetCore.Http.Extensions;
using Phonebook.Api.Models.DTOs;
using Phonebook.Api.Services;

namespace Phonebook.Api.Middleware;

/// <summary>
/// Global error handling middleware. Translates every error into the
/// standard {"detail": "message"} JSON response with the correct HTTP status.
/// Mirrors the Java GlobalExceptionHandler.
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
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
        catch (Exception ex)
        {
            // Once the response body is streaming it can no longer be
            // rewritten, so only log.
            if (context.Response.HasStarted)
            {
                _logger.LogError(ex, "Unhandled exception handling {Method} {Path}: {Message}",
                    context.Request.Method, context.Request.Path, ex.Message);
                return;
            }

            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var (statusCode, message) = MapException(ex);

        _logger.LogError(ex, "Request {Method} {Path} failed: {Message}",
            context.Request.Method, context.Request.Path, ex.Message);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        // Add WWW-Authenticate header for 401 responses (matching Java behavior)
        if (statusCode == StatusCodes.Status401Unauthorized)
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
        }

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(new ErrorResponse(message), ApiJson.Options));
    }

    /// <summary>
    /// Maps application exceptions (and malformed request bodies) to the
    /// status code / message the reference backends return.
    /// </summary>
    private static (int StatusCode, string Message) MapException(Exception ex) => ex switch
    {
        UnauthorizedException => (StatusCodes.Status401Unauthorized, ex.Message),
        NotFoundException => (StatusCodes.Status404NotFound, ex.Message),
        ConflictException => (StatusCodes.Status409Conflict, ex.Message),
        BadRequestException => (StatusCodes.Status400BadRequest, ex.Message),
        PayloadTooLargeException => (StatusCodes.Status413PayloadTooLarge, ex.Message),
        JsonException => (StatusCodes.Status400BadRequest, "Invalid request body"),
        BadHttpRequestException => (StatusCodes.Status400BadRequest, "Invalid request body"),
        OperationCanceledException => (StatusCodes.Status499ClientClosedRequest, "Request cancelled"),
        _ => (StatusCodes.Status500InternalServerError, "Internal server error")
    };
}
