using Microsoft.EntityFrameworkCore;
using Prodexa.Persistence;
using Xunit;

namespace Prodexa.IntegrationTests.Database;

/// <summary>
/// Applies the actual EF Core migrations to a PostgreSQL test database.
/// </summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly DbContextOptions<ProdexaDbContext> _options;

    public PostgreSqlFixture()
    {
        var connectionString = Environment.GetEnvironmentVariable("PRODEXA_TEST_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Set PRODEXA_TEST_CONNECTION_STRING to a dedicated PostgreSQL test database.");
        }

        _options = new DbContextOptionsBuilder<ProdexaDbContext>()
            .UseNpgsql(connectionString)
            .Options;
    }

    /// <summary>
    /// Creates a context connected to the isolated test database.
    /// </summary>
    public ProdexaDbContext CreateContext()
    {
        return new ProdexaDbContext(_options);
    }

    /// <summary>
    /// Applies all committed migrations before the tests run.
    /// </summary>
    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    /// <summary>
    /// Leaves the database intact for local debugging. CI provides a disposable server.
    /// </summary>
    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
