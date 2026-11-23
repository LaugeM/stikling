using Stikling.Core.Models;
using Stikling.Core.Plants;

namespace Stikling.Core.Tests;

public class PlantDescriptionTests
{
    private static readonly DateOnly Today = new(2026, 11, 23);

    private static Problem Open(DateOnly startedOn, string? note = null, params ProblemSign[] signs) => new()
    {
        StartedOn = startedOn,
        Note = note,
        Signs = [.. signs.Select(s => s.ToString())]
    };

    private static ProblemUpdate Update(Problem problem, DateOnly on, ProblemTrend? trend = null, string? note = null, params ProblemAction[] did) => new()
    {
        ProblemId = problem.Id,
        OccurredOn = on,
        Trend = trend,
        Note = note,
        Did = [.. did.Select(a => a.ToString())]
    };

    private static ILookup<Guid, ProblemUpdate> Lookup(params ProblemUpdate[] updates) => updates.ToLookup(u => u.ProblemId);

    [Fact]
    public void Room_where_it_came_from_price_and_tags_start_unticked()
    {
        foreach (var part in Enum.GetValues<DescribePart>())
        {
            var off = part is DescribePart.Room or DescribePart.CameFrom or DescribePart.Price or DescribePart.Tags;
            Assert.Equal(!off, PlantDescription.OnAtFirst(part));
        }
    }

    [Fact]
    public void Write_puts_the_parts_in_order_whatever_order_they_come_in()
    {
        var sections = new[]
        {
            new DescribeSection(DescribePart.Care, ["Watered 3 days ago"]),
            new DescribeSection(DescribePart.Name, ["Monstera deliciosa"]),
            new DescribeSection(DescribePart.Pot, ["Grows in: Soil", "second line"])
        };

        var text = PlantDescription.Write(sections, new HashSet<DescribePart>(Enum.GetValues<DescribePart>()));

        Assert.Equal("Monstera deliciosa\nGrows in: Soil\nsecond line\nWatered 3 days ago", text);
    }

    [Fact]
    public void Write_leaves_out_parts_that_are_not_ticked_or_have_no_lines()
    {
        var sections = new[]
        {
            new DescribeSection(DescribePart.Name, ["Monstera"]),
            new DescribeSection(DescribePart.Room, ["Room: Hall"]),
            new DescribeSection(DescribePart.Pot, [])
        };

        var text = PlantDescription.Write(sections, new HashSet<DescribePart> { DescribePart.Name, DescribePart.Pot });

        Assert.Equal("Monstera", text);
        Assert.DoesNotContain("\n", text);
    }

    [Fact]
    public void Write_gives_nothing_when_nothing_is_ticked()
    {
        var sections = new[] { new DescribeSection(DescribePart.Name, ["Monstera"]) };

        Assert.Equal("", PlantDescription.Write(sections, new HashSet<DescribePart>()));
        Assert.Equal("", PlantDescription.Write([], new HashSet<DescribePart> { DescribePart.Name }));
    }

    [Fact]
    public void A_problem_line_has_the_signs_the_start_day_the_duration_and_the_note()
    {
        var problem = Open(new DateOnly(2026, 11, 10), "Started after the move", ProblemSign.YellowLeaves, ProblemSign.Drooping);

        var lines = PlantDescription.ProblemLines([problem], Lookup(), Today);

        Assert.Equal(["Problem: Yellow leaves, drooping, since 10 Nov (13 days). Started after the move"], lines);
    }

    [Fact]
    public void A_problem_without_a_note_has_no_trailing_text()
    {
        var problem = Open(new DateOnly(2026, 11, 10), "  ", ProblemSign.BrownTips);

        Assert.Equal(["Problem: Brown or crispy tips, since 10 Nov (13 days)"], PlantDescription.ProblemLines([problem], Lookup(), Today));
    }

    [Fact]
    public void Updates_come_oldest_first_with_trend_actions_and_note()
    {
        var problem = Open(new DateOnly(2026, 11, 10), null, ProblemSign.Drooping);
        var later = Update(problem, new DateOnly(2026, 11, 14), ProblemTrend.Worse, " Two leaves dropped ", ProblemAction.MovedIt, ProblemAction.WateredLess);
        var earlier = Update(problem, new DateOnly(2026, 11, 12), ProblemTrend.Same);

        var lines = PlantDescription.ProblemLines([problem], Lookup(later, earlier), Today);

        Assert.Equal(
        [
            "Problem: Drooping, since 10 Nov (13 days)",
            "  12 Nov: About the same",
            "  14 Nov: Worse. Moved it, watered less. Two leaves dropped"
        ], lines);
    }

    [Fact]
    public void An_update_with_nothing_set_and_a_deleted_update_are_left_out()
    {
        var problem = Open(new DateOnly(2026, 11, 10), null, ProblemSign.Spots);
        var empty = Update(problem, new DateOnly(2026, 11, 12));
        var gone = Update(problem, new DateOnly(2026, 11, 13), ProblemTrend.Better);
        gone.DeletedAt = DateTimeOffset.UtcNow;
        var kept = Update(problem, new DateOnly(2026, 11, 15), note: "Looks better");

        var lines = PlantDescription.ProblemLines([problem], Lookup(empty, gone, kept), Today);

        Assert.Equal(["Problem: Spots on leaves, since 10 Nov (13 days)", "  15 Nov: Looks better"], lines);
    }

    [Fact]
    public void A_problem_with_no_known_signs_uses_the_fallback_title()
    {
        var problem = Open(new DateOnly(2026, 11, 22));
        problem.Signs = ["SomethingFromANewerVersion"];

        var lines = PlantDescription.ProblemLines([problem], Lookup(), Today);

        Assert.Equal(["Problem: A problem, since 22 Nov (1 day)"], lines);
    }

    [Fact]
    public void Oldest_problem_comes_first_and_deleted_problems_are_skipped()
    {
        var newer = Open(new DateOnly(2026, 11, 20), null, ProblemSign.Leggy);
        var older = Open(new DateOnly(2026, 11, 1), null, ProblemSign.Sticky);
        var gone = Open(new DateOnly(2026, 10, 1), null, ProblemSign.Curled);
        gone.DeletedAt = DateTimeOffset.UtcNow;

        var lines = PlantDescription.ProblemLines([newer, older, gone], Lookup(), Today);

        Assert.Equal(2, lines.Count);
        Assert.StartsWith("Problem: Sticky", lines[0]);
        Assert.StartsWith("Problem: Leggy", lines[1]);
    }
}
