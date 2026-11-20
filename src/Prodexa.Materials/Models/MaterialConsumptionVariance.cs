namespace Prodexa.Materials.Models;

/// <summary>
/// Compares net material usage with the norm assigned to a production task revision.
/// </summary>
public sealed class MaterialConsumptionVariance
{
    /// <summary>
    /// Stable identifier of the production task.
    /// </summary>
    public required string TaskId { get; init; }

    /// <summary>
    /// Plan revision used for the analysis.
    /// </summary>
    public required string TaskRevisionId { get; init; }

    /// <summary>
    /// Identifier of the immutable consumption norm version assigned to the plan.
    /// </summary>
    public required string NormVersionId { get; init; }

    /// <summary>
    /// Input material being analyzed.
    /// </summary>
    public required string MaterialId { get; init; }

    /// <summary>
    /// Product to which accepted output is attributed.
    /// </summary>
    public required string ProductId { get; init; }

    /// <summary>
    /// Accepted good output for executions of this plan revision.
    /// </summary>
    public required decimal AcceptedOutput { get; init; }

    /// <summary>
    /// Material consumed per unit of accepted output under the bound norm.
    /// </summary>
    public required decimal QuantityPerOutput { get; init; }

    /// <summary>
    /// Material usage expected for the accepted output.
    /// </summary>
    public required decimal ExpectedConsumption { get; init; }

    /// <summary>
    /// Gross material consumption recorded by production events.
    /// </summary>
    public required decimal ConsumedQuantity { get; init; }

    /// <summary>
    /// Material returned from production.
    /// </summary>
    public required decimal ReturnedQuantity { get; init; }

    /// <summary>
    /// Gross consumption minus returned material.
    /// </summary>
    public required decimal NetConsumption { get; init; }

    /// <summary>
    /// Actual net consumption minus expected consumption.
    /// </summary>
    public required decimal VarianceQuantity { get; init; }

    /// <summary>
    /// Percentage deviation; null when the accepted output has no expected material usage.
    /// </summary>
    public decimal? VariancePercent { get; init; }

    /// <summary>
    /// Whether actual net consumption exceeds the assigned norm.
    /// </summary>
    public required bool IsOverconsumption { get; init; }

    /// <summary>
    /// Unit used for input material quantities.
    /// </summary>
    public required string InputUnitId { get; init; }

    /// <summary>
    /// Unit used for accepted product quantities.
    /// </summary>
    public required string OutputUnitId { get; init; }

    /// <summary>
    /// Reporting instant used to restrict the production events.
    /// </summary>
    public required DateTimeOffset AsOf { get; init; }
}
