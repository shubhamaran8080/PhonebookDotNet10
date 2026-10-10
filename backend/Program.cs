using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Phonebook.Api.Data;
using Phonebook.Api.Endpoints;
using Phonebook.Api.Middleware;
using Phonebook.Api.Models.DTOs;
using Phonebook.Api.Security;
using Phonebook.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// --- JSON: snake_case naming policy (matches the Java API contract) ---
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    // Case-insensitive matching so both "phone_number" and "phoneNumber"
    // are accepted from clients.
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

// --- Database: EF Core + PostgreSQL ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Database=phonebook_dotnet10;Username=phonebook;Password=phonebook";

if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<PhonebookDbContext>(options =>
        options.UseInMemoryDatabase("PhonebookTestDb"));
}
else
{
    builder.Services.AddDbContext<PhonebookDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// --- Security: JWT + BCrypt ---
// One shared instance for token creation and for token validation.
// Registering the same instance guarantees that issuing and validating
// always use the exact same signing key.
var jwtService = new JwtTokenService(builder.Configuration);
builder.Services.AddSingleton(jwtService);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Validation must mirror the token exactly: signature + lifetime.
        // Issuer/audience checks stay disabled because our tokens carry no
        // "iss"/"aud" claims (enabling them breaks every authenticated
        // request with IDX10208 "Unable to validate audience").
        options.TokenValidationParameters = jwtService.ValidationParameters();

        // Keep the raw claim names ("sub") so endpoints can read the
        // username directly instead of the legacy ClaimTypes.* URNs.
        options.MapInboundClaims = false;

        options.Events = new JwtBearerEvents
        {
            // Authentication failures are answered with the API's JSON
            // error body ({"detail": "..."}) instead of a bare 401.
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                context.Response.Headers.WWWAuthenticate = "Bearer";

                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("Not authenticated"),
                    ApiJson.Options);
            }
        };
    });

builder.Services.AddAuthorization();

// Form-data endpoints (CSV import) carry antiforgery metadata, so the
// antiforgery middleware must run — otherwise ASP.NET Core answers 500.
builder.Services.AddAntiforgery();

builder.Services.AddCors(options =>
{
    options.AddPolicy(ApiCors.PolicyName, policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// --- Application services ---
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ContactService>();
builder.Services.AddScoped<StatsService>();
builder.Services.AddScoped<CsvImportService>();
builder.Services.AddScoped<CsvExportService>();
builder.Services.AddScoped<SeedService>();

// --- OpenAPI ---
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// --- Middleware pipeline ---

// Global exception handling: must be first so it catches every error.
app.UseMiddleware<ErrorHandlingMiddleware>();

// Give responses that were produced without a body (404, 405, ...)
// the same {"detail": "..."} JSON shape as the Java backend.
app.UseStatusCodePages(new StatusCodePagesOptions
{
    HandleAsync = async context =>
    {
        var response = context.HttpContext.Response;
        if (response.HasStarted)
            return;

        response.ContentType = "application/json";
        var detail = StatusMessages.Reason(response.StatusCode);
        await response.WriteAsJsonAsync(new ErrorResponse(detail), ApiJson.Options);
    }
});

// CORS runs before authentication so pre-flight (OPTIONS) requests and
// cross-origin error responses carry the Access-Control-* headers.
app.UseCors(ApiCors.PolicyName);

app.UseAuthentication();
app.UseAuthorization();

// Form-data endpoints (CSV import uploads) carry antiforgery metadata,
// so the antiforgery middleware must be part of the pipeline.
app.UseAntiforgery();


// --- Endpoints ---
app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapContactEndpoints();
app.MapStatsEndpoints();
app.MapCsvEndpoints();

// --- Database migration + seed (safe, non-destructive) ---
// Skipped under test: the in-memory provider cannot run migrations.
// MigrateAsync only applies pending migrations, it never drops or resets
// an existing database.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PhonebookDbContext>();
    await WaitForDatabaseAsync(db);
    await db.Database.MigrateAsync();

    var seed = scope.ServiceProvider.GetRequiredService<SeedService>();
    await seed.RunAsync();

    // One-off mode: `docker compose run --rm -e Seed__Exit=true -e Seed__Count=1000 backend`
    // seeds the database (applying migrations first) and then shuts the
    // container down instead of starting Kestrel. Without this flag the app
    // runs normally.
    if (app.Configuration.GetValue("Seed:Exit", false))
    {
        app.Logger.LogInformation("Seed:Exit set — one-off run finished, shutting down");
        return;
    }
}

app.Run();

/// <summary>
/// Waits (without changing anything) until PostgreSQL accepts connections.
/// </summary>
static async Task WaitForDatabaseAsync(PhonebookDbContext db)
{
    const int maxAttempts = 10;
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            if (await db.Database.CanConnectAsync())
                return;
        }
        catch
        {
            // Retried below; MigrateAsync reports the real failure.
        }

        if (attempt >= maxAttempts)
            return;

        await Task.Delay(TimeSpan.FromSeconds(2));
    }
}
