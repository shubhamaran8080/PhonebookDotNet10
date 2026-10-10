using Microsoft.AspNetCore.Authentication.JwtBearer;
using Phonebook.Api.Models.DTOs;
using Phonebook.Api.Services;
using Phonebook.Api.Validation;

namespace Phonebook.Api.Endpoints;

/// <summary>
/// Contact management endpoints. Mirrors the Java ContactController.
/// </summary>
public static class ContactEndpoints
{
    public static void MapContactEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/contacts").RequireAuthorization();

        // GET /contacts — list with pagination and search
        group.MapGet("/", async (
            HttpContext http,
            ContactService contactService,
            CancellationToken ct) =>
        {
            var page = http.Request.Query.TryGetValue("page", out var p) && int.TryParse(p, out var pv) ? pv : 1;
            var pageSize = http.Request.Query.TryGetValue("page_size", out var ps) && int.TryParse(ps, out var psv) ? psv : 10;
            var q = http.Request.Query.TryGetValue("q", out var qv) ? qv.ToString() : null;

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var (items, total) = await contactService.SearchAsync(q, page, pageSize, ct);

            http.Response.Headers["X-Total-Count"] = total.ToString();
            http.Response.Headers["X-Page"] = page.ToString();
            http.Response.Headers["X-Page-Size"] = pageSize.ToString();

            return Results.Ok(items.Select(ContactResponse.FromEntity));
        });

        // POST /contacts — create (returns 201)
        group.MapPost("/", async (
            HttpContext http,
            ContactCreateRequest request,
            ContactService contactService,
            CancellationToken ct) =>
        {
            var response = await contactService.CreateAsync(request, ct);
            return Results.Created($"/contacts/{response.Id}", response);
        })
        .AddEndpointFilter<DtoValidationFilter>();

        // GET /contacts/{id} — retrieve one
        group.MapGet("/{id:int}", async (
            int id,
            ContactService contactService,
            CancellationToken ct) =>
        {
            var contact = await contactService.GetContactAsync(id, ct);
            return Results.Ok(ContactResponse.FromEntity(contact));
        });

        // PUT /contacts/{id} — partial update
        group.MapPut("/{id:int}", async (
            int id,
            ContactUpdateRequest request,
            ContactService contactService,
            CancellationToken ct) =>
        {
            var response = await contactService.UpdateAsync(id, request, ct);
            return Results.Ok(response);
        })
        .AddEndpointFilter<DtoValidationFilter>();

        // DELETE /contacts/{id} — delete (returns 204)
        group.MapDelete("/{id:int}", async (
            int id,
            ContactService contactService,
            CancellationToken ct) =>
        {
            await contactService.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }
}
