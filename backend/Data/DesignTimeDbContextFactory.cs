using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Phonebook.Api.Data;

namespace Phonebook.Api;

/// <summary>
/// Design-time factory used by `dotnet ef` (migrations bundle, tooling).
/// It builds the PostgreSQL-configured context outside of the running API
/// so migrations can be created without a live server or a live database.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PhonebookDbContext>
{
    private const string DefaultConnection =
        "Host=localhost;Port=5432;Database=phonebook_dotnet10;Username=phonebook;Password=phonebook";

    public PhonebookDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings:DefaultConnection")
            ?? DefaultConnection;

        var options = new DbContextOptionsBuilder<PhonebookDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new PhonebookDbContext(options);
    }
}
