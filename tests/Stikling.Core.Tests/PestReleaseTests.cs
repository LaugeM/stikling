using Stikling.Core.Models;
using Stikling.Core.Pests;
using Stikling.Core.Rooms;

namespace Stikling.Core.Tests;

public class PestReleaseTests
{
    private static readonly DateOnly Today = new(2026, 9, 20);

    private readonly FakePlaceRepository places = new();

    private static PestTreatment Release(Guid caseId, DateOnly on, string? what = "Predatory mites, 2 sachets") =>
        new() { CaseId = caseId, OccurredOn = on, What = what, IsRelease = true };

    [Fact]
    public void A_release_with_text_is_valid()
    {
        Assert.Empty(Release(Guid.NewGuid(), Today).Validate(Today));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void A_release_needs_text(string? what)
    {
        Assert.Contains("Write what was released.", Release(Guid.NewGuid(), Today, what).Validate(Today));
    }

    [Fact]
    public void A_release_cannot_use_a_recipe()
    {
        var release = Release(Guid.NewGuid(), Today);
        release.RecipeId = Guid.NewGuid();
        Assert.Contains("A release doesn't use a recipe.", release.Validate(Today));

        release.RecipeId = null;
        release.Method = "Spray";
        Assert.Contains("A release doesn't use a recipe.", release.Validate(Today));
    }

    [Theory]
    [InlineData(PestTreatmentKind.Checked)]
    [InlineData(PestTreatmentKind.TrapCount)]
    public void A_release_is_only_a_treatment(PestTreatmentKind kind)
    {
        var release = Release(Guid.NewGuid(), Today);
        release.Kind = kind;

        Assert.Contains(release.Validate(Today), e => e.StartsWith("Only a treatment can be a release"));
    }

    [Fact]
    public void A_skipped_step_cannot_be_a_release()
    {
        var release = Release(Guid.NewGuid(), Today, null);
        release.PlanStepId = Guid.NewGuid();
        release.Skipped = true;

        Assert.Contains(release.Validate(Today), e => e.StartsWith("Only a treatment can be a release"));
    }

    [Fact]
    public void A_release_moves_the_next_due_date_like_a_treatment()
    {
        var item = new PestCase { Scope = PestScope.Everywhere, StartedOn = Today.AddDays(-10), IntervalDays = 7 };
        var on = Today.AddDays(-2);

        Assert.Equal(on.AddDays(7), PestService.NextDue(item, [Release(item.Id, on)]));
    }

    [Fact]
    public void A_release_counts_for_the_clean_days_of_a_watched_case()
    {
        var item = new PestCase { Scope = PestScope.Everywhere, Status = PestCaseStatus.Monitoring, StartedOn = Today.AddDays(-40) };
        var treatments = new[] { Release(item.Id, Today.AddDays(-9)) };

        Assert.Equal(Today.AddDays(-9), PestService.CleanSince(item, treatments));
        Assert.Equal(9, PestService.Describe(item, [], treatments, places.Current, Today).CleanDays);
    }

    [Fact]
    public void The_next_treatment_does_not_start_from_a_release()
    {
        var item = new PestCase { Scope = PestScope.Everywhere, StartedOn = Today.AddDays(-10) };
        var draft = PestService.StartTreatment(item, [Release(item.Id, Today.AddDays(-1))], Today);

        Assert.Null(draft.What);
        Assert.False(draft.IsRelease);
    }
}
