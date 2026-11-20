using Prodexa.Core.Production;
using Xunit;

namespace Prodexa.Tests.Production;

/// <summary>
/// Verifies the remaining quantity and overdue rules for production task revisions.
/// </summary>
public sealed class PlanTaskRevisionTests
{
    /// <summary>
    /// Checks that remaining output cannot become negative when a task is overproduced.
    /// </summary>
    [Theory]
    [InlineData(60, 40)]
    [InlineData(100, 0)]
    [InlineData(120, 0)]
    public void GetRemainingQuantity_ReturnsOutstandingOutput(int acceptedOutput, int expected)
    {
        var revision = CreateRevision();

        var remaining = revision.GetRemainingQuantity(acceptedOutput);

        Assert.Equal(expected, remaining);
    }

    /// <summary>
    /// Rejects negative accepted output rather than treating it as unfulfilled production.
    /// </summary>
    [Fact]
    public void GetRemainingQuantity_RejectsNegativeOutput()
    {
        var revision = CreateRevision();

        Assert.Throws<ArgumentOutOfRangeException>(() => revision.GetRemainingQuantity(-1m));
    }

    /// <summary>
    /// Identifies a task that remains unfinished after its deadline.
    /// </summary>
    [Fact]
    public void IsOverdue_ReturnsTrueForUnfinishedTaskAfterDeadline()
    {
        var revision = CreateRevision();
        var at = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.FromHours(5));

        Assert.True(revision.IsOverdue(at, 60m));
    }

    /// <summary>
    /// The deadline itself is not considered overdue.
    /// </summary>
    [Fact]
    public void IsOverdue_ReturnsFalseAtDeadline()
    {
        var revision = CreateRevision();

        Assert.False(revision.IsOverdue(revision.DueAt, 60m));
    }

    /// <summary>
    /// A task with enough accepted output is not overdue.
    /// </summary>
    [Fact]
    public void IsOverdue_ReturnsFalseWhenPlanIsFulfilled()
    {
        var revision = CreateRevision();
        var at = revision.DueAt.AddHours(2);

        Assert.False(revision.IsOverdue(at, 100m));
    }

    /// <summary>
    /// Cancelled tasks are excluded from overdue calculations.
    /// </summary>
    [Fact]
    public void IsOverdue_ReturnsFalseForCancelledTask()
    {
        var revision = CreateRevision(PlanTaskStatus.Cancelled);
        var at = revision.DueAt.AddHours(2);

        Assert.False(revision.IsOverdue(at, 60m));
    }

    private PlanTaskRevision CreateRevision(PlanTaskStatus status = PlanTaskStatus.Active)
    {
        return new PlanTaskRevision
        {
            Id = "J-01-v1",
            TaskId = "J-01",
            RevisionNumber = 1,
            ProductId = "PROD-A",
            ProcessSegmentVersionId = "SEG-A-v1",
            PlannedQuantity = 100m,
            UnitId = "piece",
            DueAt = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.FromHours(5)),
            Status = status,
            EffectiveFrom = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(5))
        };
    }
}
