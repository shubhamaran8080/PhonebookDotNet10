using Microsoft.AspNetCore.Authentication.JwtBearer;
using Phonebook.Api.Models.DTOs;
using Phonebook.Api.Services;

namespace Phonebook.Api.Endpoints;

/// <summary>
/// CSV import/export endpoints. Mirrors the Java ContactController CSV actions.
/// </summary>
public static class CsvEndpoints
{
    public static void MapCsvEndpoints(this WebApplication app)
    {
        // POST /contacts/import — CSV import (multipart field "file")
        app.MapPost("/contacts/import", async (
            IFormFile file,
            CsvImportService csvImportService,
            CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
                return Results.BadRequest(new ErrorResponse("Missing required part: file"));

            await using var stream = file.OpenReadStream();
            var result = await csvImportService.ImportAsync(stream, file.FileName, ct);
            return Results.Ok(result);
        })
        // The reference backends have no CSRF protection and the SPA
        // sends no antiforgery token, so it must stay disabled here.
        .DisableAntiforgery()
        .RequireAuthorization();

        // GET /contacts/export — CSV export (optional q filter)
        app.MapGet("/contacts/export", async (
            HttpContext http,
            CsvExportService csvExportService,
            CancellationToken ct) =>
        {
            var q = http.Request.Query.TryGetValue("q", out var qv) ? qv.ToString() : null;
            var filename = $"phonebook-export-{DateTimeOffset.UtcNow.ToString("yyyy-MM-dd")}.csv";

            var content = await csvExportService.BuildAsync(q, ct);

            http.Response.Headers.ContentDisposition = $"attachment; filename=\"{filename}\"";
            return Results.File(content, "text/csv", filename);
        }).RequireAuthorization();
    }
}
