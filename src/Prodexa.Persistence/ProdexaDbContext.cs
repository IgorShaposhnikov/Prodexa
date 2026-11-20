using Microsoft.EntityFrameworkCore;
using Prodexa.Core.Production;
using Prodexa.Core.Materials;
using Prodexa.Core.Resources;
using Prodexa.Persistence.Converters;

namespace Prodexa.Persistence;

/// <summary>
/// Provides relational access to the production records stored in PostgreSQL.
/// </summary>
public sealed class ProdexaDbContext : DbContext
{
    /// <summary>
    /// Creates a context using options supplied by the application.
    /// </summary>
    public ProdexaDbContext(DbContextOptions<ProdexaDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Stable production task identifiers.
    /// </summary>
    public DbSet<PlanTask> PlanTasks { get; set; } = null!;

    /// <summary>
    /// Versioned production plans.
    /// </summary>
    public DbSet<PlanTaskRevision> PlanTaskRevisions { get; set; } = null!;

    /// <summary>
    /// Production executions tied to their planning revisions.
    /// </summary>
    public DbSet<Execution> Executions { get; set; } = null!;

    /// <summary>
    /// Stable identifiers of equipment positions.
    /// </summary>
    public DbSet<Equipment> Equipment { get; set; } = null!;

    /// <summary>
    /// Recorded downtime events.
    /// </summary>
    public DbSet<Downtime> Downtimes { get; set; } = null!;

    /// <summary>
    /// Stable material definitions referenced by production lots.
    /// </summary>
    public DbSet<MaterialDefinition> MaterialDefinitions { get; set; } = null!;

    /// <summary>
    /// Physical or logical lots used by production events.
    /// </summary>
    public DbSet<MaterialLot> MaterialLots { get; set; } = null!;

    /// <summary>
    /// Accepted output, rejected output, consumption and returns.
    /// </summary>
    public DbSet<MaterialMovement> MaterialMovements { get; set; } = null!;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // PostgreSQL timestamptz stores instants, not the original source offsets.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProdexaDbContext).Assembly);
    }
}
