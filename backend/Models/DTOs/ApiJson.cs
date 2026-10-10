using System.Text.Json;

namespace Phonebook.Api.Models.DTOs;

/// <summary>
/// JsonSerializerOptions used wherever the API writes JSON by hand
/// (error bodies, challenge responses) so the shape always matches the
/// minimal-API default: snake_case property names.
/// </summary>
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };
}

/// <summary>
/// Name of the CORS policy applied to every endpoint.
/// </summary>
public static class ApiCors
{
    public const string PolicyName = "AllowAllOrigins";
}

/// <summary>
/// Short human-readable reason phrases used for status codes that
/// the framework produced without a response body.
/// </summary>
public static class StatusMessages
{
    public static string Reason(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        401 => "Not authenticated",
        403 => "Forbidden",
        404 => "Not Found",
        405 => "Method Not Allowed",
        406 => "Not Acceptable",
        408 => "Request Timeout",
        409 => "Conflict",
        413 => "Payload Too Large",
        415 => "Unsupported Media Type",
        422 => "Unprocessable Entity",
        429 => "Too Many Requests",
        500 => "Internal server error",
        502 => "Bad Gateway",
        503 => "Service Unavailable",
        _ => $"Status code {statusCode}"
    };
}
