namespace Phonebook.Api.Endpoints;

/// <summary>
/// Health check endpoint. Mirrors the Java HealthController.
/// </summary>
public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
    }
}
