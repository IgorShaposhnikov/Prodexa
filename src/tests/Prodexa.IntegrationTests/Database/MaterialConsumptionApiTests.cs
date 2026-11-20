using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Prodexa.Core.Materials;
using Prodexa.Core.Production;
using Prodexa.Materials.Models;
using Xunit;

namespace Prodexa.IntegrationTests.Database;

/// <summary>
/// Verifies material consumption variance analytics against committed PostgreSQL migrations.
/// </summary>
[Collection("PostgreSQL")]
public sealed class MaterialConsumptionApiTests(PostgreSqlFixture database)
{
    /// <summary>
    /// Calculates net consumption and its variance from the norm bound to the production plan.
    /// </summary>
    [Theory]
    [InlineData(115, 5, 110, 10, 10)]
    [InlineData(95, 5, 90, -10, -10)]
    [InlineData(105, 5, 100, 0, 0)]
    public async Task GetConsumptionVariance_ComparesNetUsageWithBoundNorm(
        int consumedQuantity,
        int returnedQuantity,
        int expectedNet,
        int expectedVariance,
        int expectedPercent)
    {
        var id = Guid.NewGuid().ToString("N");
        var taskId = $"J-02-{id}";
        var productId = $"PROD-A-{id}";
        var inputMaterialId = $"MAT-A-{id}";
        var asOf = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.FromHours(5));

        var task = new PlanTask { Id = taskId };
        var revision = CreateRevision(taskId, productId, asOf);
        var execution = new Execution
        {
            Id = $"EX-{id}",
            TaskId = taskId,
            TaskRevisionId = revision.Id,
            StartAt = asOf.AddHours(-3),
            Status = ExecutionStatus.Completed
        };

        var product = new MaterialDefinition { Id = productId };
        var inputMaterial = new MaterialDefinition { Id = inputMaterialId };
        var productVersion = CreateVersion($"{productId}-v1", productId, "piece", asOf.AddDays(-10));
        var inputVersion = CreateVersion($"{inputMaterialId}-v1", inputMaterialId, "kg", asOf.AddDays(-10));
        var productLot = CreateLot($"LOT-PROD-{id}", productId, "piece", asOf.AddDays(-2));
        var inputLot = CreateLot($"LOT-MAT-{id}", inputMaterialId, "kg", asOf.AddDays(-2));

        var boundNorm = CreateNorm($"N-01-v1-{id}", $"N-01-{id}", 1, productVersion.Id, inputVersion.Id, 2m, asOf.AddDays(-10));
        var newerNorm = CreateNorm($"N-01-v2-{id}", $"N-01-{id}", 2, productVersion.Id, inputVersion.Id, 4m, asOf.AddDays(-1));
        var binding = new PlanTaskNorm
        {
            TaskRevisionId = revision.Id,
            MaterialId = inputMaterialId,
            NormVersionId = boundNorm.Id
        };

        // The assigned norm remains valid for this task even if a newer norm was published.
        var acceptedOutput = CreateMovement(execution.Id, productLot.Id, MaterialMovementKind.Output, 50m, "piece", asOf.AddHours(-2), OutputQuality.Good);
        var rejectedOutput = CreateMovement(execution.Id, productLot.Id, MaterialMovementKind.Output, 30m, "piece", asOf.AddHours(-2), OutputQuality.Reject);
        var futureOutput = CreateMovement(execution.Id, productLot.Id, MaterialMovementKind.Output, 10m, "piece", asOf.AddMinutes(5), OutputQuality.Good);
        var consumption = CreateMovement(execution.Id, inputLot.Id, MaterialMovementKind.Consume, consumedQuantity, "kg", asOf.AddHours(-2));
        var returned = CreateMovement(execution.Id, inputLot.Id, MaterialMovementKind.Return, returnedQuantity, "kg", asOf.AddHours(-1), reverseId: consumption.Id);
        var futureReturn = CreateMovement(execution.Id, inputLot.Id, MaterialMovementKind.Return, 2m, "kg", asOf.AddMinutes(10), reverseId: consumption.Id);
        var wrongUnitConsumption = CreateMovement(execution.Id, inputLot.Id, MaterialMovementKind.Consume, 20m, "piece", asOf.AddHours(-1));

        await using (var context = database.CreateContext())
        {
            context.PlanTasks.Add(task);
            context.PlanTaskRevisions.Add(revision);
            context.Executions.Add(execution);
            context.MaterialDefinitions.AddRange(product, inputMaterial);
            context.MaterialDefinitionVersions.AddRange(productVersion, inputVersion);
            context.ConsumptionNorms.AddRange(boundNorm, newerNorm);
            context.PlanTaskNorms.Add(binding);
            context.MaterialLots.AddRange(productLot, inputLot);
            context.MaterialMovements.AddRange(
                acceptedOutput, rejectedOutput, futureOutput, consumption, returned, futureReturn, wrongUnitConsumption);

            await context.SaveChangesAsync();
        }

        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var asOfParameter = Uri.EscapeDataString(asOf.ToString("O"));

        var response = await client.GetAsync($"/api/materials/consumption-variance?asOf={asOfParameter}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reports = await response.Content.ReadFromJsonAsync<List<MaterialConsumptionVariance>>();

        Assert.NotNull(reports);

        var report = Assert.Single(reports.Where(item => item.TaskId == taskId));

        Assert.Equal(revision.Id, report.TaskRevisionId);
        Assert.Equal(boundNorm.Id, report.NormVersionId);
        Assert.Equal(inputMaterialId, report.MaterialId);
        Assert.Equal(productId, report.ProductId);
        Assert.Equal(50m, report.AcceptedOutput);
        Assert.Equal(2m, report.QuantityPerOutput);
        Assert.Equal(100m, report.ExpectedConsumption);
        Assert.Equal((decimal)consumedQuantity, report.ConsumedQuantity);
        Assert.Equal((decimal)returnedQuantity, report.ReturnedQuantity);
        Assert.Equal((decimal)expectedNet, report.NetConsumption);
        Assert.Equal((decimal)expectedVariance, report.VarianceQuantity);
        Assert.Equal((decimal)expectedPercent, report.VariancePercent);
        Assert.Equal(expectedVariance > 0, report.IsOverconsumption);
        Assert.Equal("kg", report.InputUnitId);
        Assert.Equal("piece", report.OutputUnitId);
        Assert.Equal(asOf.ToUniversalTime(), report.AsOf);
    }

    /// <summary>
    /// Consumption without accepted output has no meaningful percentage denominator.
    /// </summary>
    [Fact]
    public async Task GetConsumptionVariance_ReportsConsumptionWithoutAcceptedOutput()
    {
        var id = Guid.NewGuid().ToString("N");
        var taskId = $"J-ZERO-{id}";
        var productId = $"PROD-{id}";
        var materialId = $"MAT-{id}";
        var asOf = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.FromHours(5));

        var task = new PlanTask { Id = taskId };
        var revision = CreateRevision(taskId, productId, asOf);
        var execution = new Execution
        {
            Id = $"EX-{id}",
            TaskId = taskId,
            TaskRevisionId = revision.Id,
            StartAt = asOf.AddHours(-2),
            Status = ExecutionStatus.Started
        };

        var product = new MaterialDefinition { Id = productId };
        var material = new MaterialDefinition { Id = materialId };
        var productVersion = CreateVersion($"{productId}-v1", productId, "piece", asOf.AddDays(-5));
        var materialVersion = CreateVersion($"{materialId}-v1", materialId, "kg", asOf.AddDays(-5));
        var norm = CreateNorm($"N-{id}", $"N-{id}", 1, productVersion.Id, materialVersion.Id, 2m, asOf.AddDays(-5));
        var binding = new PlanTaskNorm
        {
            TaskRevisionId = revision.Id,
            MaterialId = materialId,
            NormVersionId = norm.Id
        };
        var lot = CreateLot($"LOT-{id}", materialId, "kg", asOf.AddDays(-1));
        var consumption = CreateMovement(execution.Id, lot.Id, MaterialMovementKind.Consume, 10m, "kg", asOf.AddHours(-1));

        await using (var context = database.CreateContext())
        {
            context.PlanTasks.Add(task);
            context.PlanTaskRevisions.Add(revision);
            context.Executions.Add(execution);
            context.MaterialDefinitions.AddRange(product, material);
            context.MaterialDefinitionVersions.AddRange(productVersion, materialVersion);
            context.ConsumptionNorms.Add(norm);
            context.PlanTaskNorms.Add(binding);
            context.MaterialLots.Add(lot);
            context.MaterialMovements.Add(consumption);
            await context.SaveChangesAsync();
        }

        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var parameter = Uri.EscapeDataString(asOf.ToString("O"));

        var response = await client.GetAsync($"/api/materials/consumption-variance?asOf={parameter}");
        var reports = await response.Content.ReadFromJsonAsync<List<MaterialConsumptionVariance>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(reports);

        var report = Assert.Single(reports.Where(item => item.TaskId == taskId));

        Assert.Equal(0m, report.AcceptedOutput);
        Assert.Equal(0m, report.ExpectedConsumption);
        Assert.Equal(10m, report.NetConsumption);
        Assert.Equal(10m, report.VarianceQuantity);
        Assert.Null(report.VariancePercent);
        Assert.True(report.IsOverconsumption);
    }

    /// <summary>
    /// An explicit reporting time is required for reproducible material calculations.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("?asOf=invalid")]
    public async Task GetConsumptionVariance_RequiresValidReportingTime(string query)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/materials/consumption-variance{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        var connectionString = Environment.GetEnvironmentVariable("PRODEXA_TEST_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("PRODEXA_TEST_CONNECTION_STRING must point to a dedicated test database.");
        }

        return new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Prodexa"] = connectionString
                });
            });
        });
    }

    private PlanTaskRevision CreateRevision(string taskId, string productId, DateTimeOffset asOf)
    {
        return new PlanTaskRevision
        {
            Id = $"{taskId}-v1",
            TaskId = taskId,
            RevisionNumber = 1,
            ProductId = productId,
            ProcessSegmentVersionId = "SEG-A-v1",
            PlannedQuantity = 100m,
            UnitId = "piece",
            DueAt = asOf.AddHours(-1),
            Status = PlanTaskStatus.Active,
            EffectiveFrom = asOf.AddDays(-5)
        };
    }

    private MaterialDefinitionVersion CreateVersion(string id, string materialId, string baseUnitId, DateTimeOffset validFrom)
    {
        return new MaterialDefinitionVersion
        {
            Id = id,
            MaterialId = materialId,
            VersionNumber = 1,
            Name = materialId,
            MaterialClassId = "ProductOrMaterial",
            BaseUnitId = baseUnitId,
            ValidFrom = validFrom,
            SourceRecordId = $"SOURCE-{id}"
        };
    }

    private ConsumptionNorm CreateNorm(
        string id,
        string normId,
        int versionNumber,
        string productVersionId,
        string materialVersionId,
        decimal quantityPerOutput,
        DateTimeOffset validFrom)
    {
        return new ConsumptionNorm
        {
            Id = id,
            NormId = normId,
            VersionNumber = versionNumber,
            ProductVersionId = productVersionId,
            MaterialVersionId = materialVersionId,
            ProcessSegmentVersionId = "SEG-A-v1",
            QuantityPerOutput = quantityPerOutput,
            InputUnitId = "kg",
            OutputUnitId = "piece",
            ValidFrom = validFrom
        };
    }

    private MaterialLot CreateLot(string id, string materialId, string unitId, DateTimeOffset createdAt)
    {
        return new MaterialLot
        {
            Id = id,
            MaterialId = materialId,
            CreatedAt = createdAt,
            UnitId = unitId,
            Status = "Active"
        };
    }

    private MaterialMovement CreateMovement(
        string executionId,
        string lotId,
        MaterialMovementKind kind,
        decimal quantity,
        string unitId,
        DateTimeOffset occurredAt,
        OutputQuality? quality = null,
        string? reverseId = null)
    {
        return new MaterialMovement
        {
            Id = $"MOV-{Guid.NewGuid():N}",
            ExecutionId = executionId,
            MaterialLotId = lotId,
            Kind = kind,
            Quantity = quantity,
            UnitId = unitId,
            OccurredAt = occurredAt,
            OutputQuality = quality,
            ReversesMovementId = reverseId
        };
    }
}
