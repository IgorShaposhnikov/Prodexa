using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Prodexa.Persistence;

/// <summary>
/// Creates a PostgreSQL context for EF Core migration commands.
/// </summary>
public sealed class ProdexaDbContextFactory : IDesignTimeDbContextFactory<ProdexaDbContext>
{
    /// <summary>
    /// Builds a context without starting the API or an Aspire host.
    /// </summary>
    public ProdexaDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("PRODEXA_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = "Host=localhost;Database=prodexa;Username=postgres";
        }

        var options = new DbContextOptionsBuilder<ProdexaDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ProdexaDbContext(options);
    }
}
