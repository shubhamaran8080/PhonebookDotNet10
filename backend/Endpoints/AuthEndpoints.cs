using Microsoft.AspNetCore.Authentication.JwtBearer;
using Phonebook.Api.Models.DTOs;
using Phonebook.Api.Security;
using Phonebook.Api.Services;
using Phonebook.Api.Validation;

namespace Phonebook.Api.Endpoints;

/// <summary>
/// Authentication endpoints: /auth/login, /auth/me, /auth/logout.
/// Mirrors the Java AuthController.
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth");

        // POST /auth/login — public
        group.MapPost("/login", async (
            LoginRequest request,
            AuthService authService,
            CancellationToken ct) =>
        {
            var response = await authService.LoginAsync(request, ct);
            return Results.Ok(response);
        })
        .AddEndpointFilter<DtoValidationFilter>();

        // GET /auth/me — requires valid JWT
        group.MapGet("/me", async (
            HttpContext http,
            AuthService authService,
            CancellationToken ct) =>
        {
            // The username lives in the "sub" claim of the access token.
            var username = JwtTokenService.GetUsername(http.User);
            if (string.IsNullOrEmpty(username))
                return Results.Unauthorized();

            var user = await authService.FindByUsernameAsync(username, ct);
            if (user is null)
                return Results.Unauthorized();

            return Results.Ok(UserResponse.FromEntity(user));
        })
        .RequireAuthorization();

        // POST /auth/logout — public (client discards token)
        group.MapPost("/logout", () => Results.Ok(new { message = "Logged out" }));
    }
}
