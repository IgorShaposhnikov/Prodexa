using Xunit;

namespace Prodexa.IntegrationTests.Database;

/// <summary>
/// Shares a single migrated PostgreSQL database between integration test classes.
/// </summary>
[CollectionDefinition("PostgreSQL")]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
}
