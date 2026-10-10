using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Phonebook.Api.Data;

namespace Phonebook.Api.Tests;

/// <summary>
/// Factory that creates a test server with an isolated in-memory database.
///
/// Each test class instance calls <see cref="CreateClient"/> exactly once in
/// its constructor, which builds a brand-new inner application with its own
/// in-memory store. Every test therefore runs against a clean database and
/// never observes rows written by another test, which makes the suite
/// independent of the test execution order (for example the pagination test
/// asserts an exact <c>X-Total-Count</c>).
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private WebApplicationFactory<Program>? _host;

    private WebApplicationFactory<Program> Host =>
        _host ??= BuildHost("PhonebookTestDb_" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Starts a fresh, isolated application. Any host created for a previous
    /// test is disposed first so the in-memory stores are released.
    /// </summary>
    private WebApplicationFactory<Program> BuildHost(string databaseName)
    {
        var previous = _host;
        _host = null;
        previous?.Dispose();

        return this.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // Remove every DbContextOptions registration so the
                // PostgreSQL registration from Program.cs cannot win.
                var descriptors = services.Where(
                    d => d.ServiceType == typeof(DbContextOptions<PhonebookDbContext>) ||
                         d.ServiceType == typeof(DbContextOptions)).ToList();

                foreach (var descriptor in descriptors)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<PhonebookDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));
            });
        });
    }

    /// <summary>The services of the application created for the current test.</summary>
    public new IServiceProvider Services => Host.Services;

    /// <summary>Creates a client bound to a fresh, isolated application.</summary>
    public new HttpClient CreateClient()
    {
        var host = BuildHost("PhonebookTestDb_" + Guid.NewGuid().ToString("N"));
        _host = host;
        return host.CreateClient();
    }

    public override async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
            _host = null;
        }

        await base.DisposeAsync();
    }
}
