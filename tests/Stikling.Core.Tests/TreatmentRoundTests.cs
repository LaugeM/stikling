using Stikling.Core.Models;
using Stikling.Core.Pests;
using Stikling.Core.Rooms;

namespace Stikling.Core.Tests;

public class TreatmentRoundTests
{
    private static readonly DateOnly Day = new(2026, 11, 15);
    private readonly FakePlaceRepository places = new();

    private Plant Plant(string name, string? location = null) =>
        new() { Nickname = name, PlaceId = places.IdOf(location) };

    private static PestCase Case() => new() { Scope = PestScope.Everywhere };

    private static PestTreatment Round(PestCase item, DateOnly? on = null, List<Guid>? ticked = null) =>
        new() { CaseId = item.Id, OccurredOn = on ?? Day, TreatedPlantIds = ticked ?? [] };

    private static TreatmentRecipe Recipe(string name, string ingredient, string? method = null) =>
        new() { Name = name, Ingredients = [new RecipeIngredient { Name = ingredient }], Method = method };

    private static ChecklistGroup Group(params Plant[] plants) =>
        new("Room", false, [.. plants.Select(p => new ChecklistRow(p, false))]);

    [Fact]
    public void Today_finds_only_todays_ticking_treatment()
    {
        var item = Case();
        var round = Round(item);

        Assert.Same(round, TreatmentRound.Today(item, [round], Day));
        Assert.Null(TreatmentRound.Today(item, [Round(item, Day.AddDays(-1))], Day));
        Assert.Null(TreatmentRound.Today(item, [new PestTreatment { CaseId = item.Id, OccurredOn = Day }], Day));
        Assert.Null(TreatmentRound.Today(item, [new PestTreatment { CaseId = item.Id, OccurredOn = Day, TreatedPlantIds = [], IsRelease = true, What = "Mites" }], Day));
        Assert.Null(TreatmentRound.Today(item, [new PestTreatment { CaseId = Guid.NewGuid(), OccurredOn = Day, TreatedPlantIds = [] }], Day));

        var deleted = Round(item);
        deleted.DeletedAt = DateTimeOffset.UtcNow;
        Assert.Null(TreatmentRound.Today(item, [deleted], Day));
    }

    [Fact]
    public void Today_prefers_the_newest_of_two()
    {
        var item = Case();
        var older = Round(item);
        older.CreatedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var newer = Round(item);
        newer.CreatedAt = DateTimeOffset.UtcNow;

        Assert.Same(newer, TreatmentRound.Today(item, [older, newer], Day));
    }

    [Fact]
    public void Start_returns_the_existing_round()
    {
        var item = Case();
        var round = Round(item);

        Assert.Same(round, TreatmentRound.Start(item, [round], Day, [], null));
    }

    [Fact]
    public void Start_makes_a_draft_with_the_last_recipe_and_an_empty_list()
    {
        var item = Case();
        var recipe = Recipe("Soap", "Soft soap", "Spray");
        var last = new PestTreatment { CaseId = item.Id, OccurredOn = Day.AddDays(-7), RecipeId = recipe.Id, What = "Soap" };

        var draft = TreatmentRound.Start(item, [last], Day, [recipe], null);

        Assert.Equal(recipe.Id, draft.RecipeId);
        Assert.Equal(Day, draft.OccurredOn);
        Assert.Empty(draft.TreatedPlantIds!);
        Assert.Null(draft.NextDueOn);
        Assert.NotSame(last, draft);
    }

    [Fact]
    public void Start_on_a_plan_step_sets_the_step_and_round()
    {
        var item = Case();
        var step = new PlanStep { What = "Neem oil" };
        item.PlanSteps = [step];

        var draft = TreatmentRound.Start(item, [], Day, [], step);

        Assert.Equal(step.Id, draft.PlanStepId);
        Assert.Equal(1, draft.PlanRound);
        Assert.Equal("Neem oil", draft.What);
        Assert.Empty(draft.TreatedPlantIds!);
    }

    [Fact]
    public void UseRecipe_switches_between_recipes_and_typed_text()
    {
        var soap = Recipe("Soap", "Soft soap", "Spray");
        var oil = Recipe("Oil", "Neem");
        var t = new PestTreatment();

        TreatmentRound.UseRecipe(t, soap, "ignored");
        Assert.Equal(soap.Id, t.RecipeId);
        Assert.Equal("Soap", t.What);
        Assert.Equal("Spray", t.Method);

        TreatmentRound.UseRecipe(t, oil, null);
        Assert.Equal(oil.Id, t.RecipeId);
        Assert.Equal(["Neem"], t.Ingredients.Select(i => i.Name));
        Assert.Null(t.Method);

        TreatmentRound.UseRecipe(t, null, "  Alcohol  ");
        Assert.Null(t.RecipeId);
        Assert.Empty(t.Ingredients);
        Assert.Null(t.Method);
        Assert.Equal("Alcohol", t.What);

        TreatmentRound.UseRecipe(t, null, " ");
        Assert.Null(t.What);
    }

    [Fact]
    public void TickAll_ticks_and_unticks_a_group_and_keeps_other_ticks()
    {
        var a = Plant("A");
        var b = Plant("B");
        var other = Guid.NewGuid();
        var t = new PestTreatment { TreatedPlantIds = [other, a.Id] };
        var group = Group(a, b);

        TreatmentRound.TickAll(t, group, true);
        Assert.Equal(3, t.TreatedPlantIds!.Count);
        Assert.Contains(other, t.TreatedPlantIds);
        Assert.Single(t.TreatedPlantIds, a.Id);

        TreatmentRound.TickAll(t, group, false);
        Assert.Equal([other], t.TreatedPlantIds);
    }

    [Fact]
    public void AllTicked_is_true_only_when_every_row_is()
    {
        var a = Plant("A");
        var b = Plant("B");

        Assert.False(TreatmentRound.AllTicked(Group(a, b)));
        Assert.True(TreatmentRound.AllTicked(new ChecklistGroup("R", false, [new(a, true), new(b, true)])));
    }

    [Fact]
    public void InProgress_is_null_when_finished_or_not_started()
    {
        var a = Plant("A");
        var b = Plant("B");
        var item = Case();

        Assert.Null(TreatmentRound.InProgress(item, [], [a, b], Day));
        Assert.Equal((1, 2), TreatmentRound.InProgress(item, [Round(item, ticked: [a.Id])], [a, b], Day));
        Assert.Null(TreatmentRound.InProgress(item, [Round(item, ticked: [a.Id, b.Id])], [a, b], Day));
        Assert.Null(TreatmentRound.InProgress(item, [Round(item, Day.AddDays(-1), [a.Id])], [a, b], Day));
    }
}
