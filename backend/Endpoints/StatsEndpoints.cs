using Microsoft.AspNetCore.Authentication.JwtBearer;
using Phonebook.Api.Services;

namespace Phonebook.Api.Endpoints;

/// <summary>
/// Dashboard statistics endpoint. Mirrors the Java StatsController.
/// </summary>
public static class StatsEndpoints
{
    public static void MapStatsEndpoints(this WebApplication app)
    {
        app.MapGet("/stats", async (
            StatsService statsService,
            CancellationToken ct) =>
        {
            var stats = await statsService.GetStatsAsync(ct);
            return Results.Ok(stats);
        }).RequireAuthorization();
    }
}
