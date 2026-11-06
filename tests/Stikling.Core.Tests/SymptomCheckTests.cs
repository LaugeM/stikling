using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Problems;

namespace Stikling.Core.Tests;

public class SymptomCheckTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 6);

    private static SymptomResult Rank(
        ProblemSign[] signs, SymptomFacts? facts = null, WhichLeaves leaves = WhichLeaves.NotSure,
        HowFast speed = HowFast.NotSure, SoilNow soil = SoilNow.NotSure) =>
        SymptomCheck.Rank(signs, leaves, speed, soil, facts ?? SymptomFacts.None);

    private static SymptomCauseKind[] Kinds(SymptomResult result) => [.. result.Causes.Select(c => c.Kind)];

    private static RecentChange Change(RecentChangeKind kind, int daysAgo = 5) => new(kind, Today.AddDays(-daysAgo));

    // Ranking per cause: each one can be reached with its signs and a fact or answer that moves it up

    public static IEnumerable<object[]> Reachable() =>
    [
        [SymptomCauseKind.TooWet, new[] { ProblemSign.YellowLeaves }, WhichLeaves.NotSure, HowFast.NotSure, SoilNow.Wet, SymptomFacts.None],
        [SymptomCauseKind.TooDry, new[] { ProblemSign.Drooping }, WhichLeaves.NotSure, HowFast.NotSure, SoilNow.Dry, SymptomFacts.None],
        [SymptomCauseKind.TooLittleLight, new[] { ProblemSign.Leggy }, WhichLeaves.NotSure, HowFast.NotSure, SoilNow.NotSure, new SymptomFacts { Light = LightFit.Darker }],
        [SymptomCauseKind.TooMuchSun, new[] { ProblemSign.Sunburn }, WhichLeaves.NotSure, HowFast.NotSure, SoilNow.NotSure, new SymptomFacts { Light = LightFit.Brighter }],
        [SymptomCauseKind.RecentChange, new[] { ProblemSign.Drooping }, WhichLeaves.NotSure, HowFast.NotSure, SoilNow.NotSure, new SymptomFacts { Changes = [Change(RecentChangeKind.Moved)] }],
        [SymptomCauseKind.Cold, new[] { ProblemSign.LeafDrop }, WhichLeaves.NotSure, HowFast.FewDays, SoilNow.NotSure, SymptomFacts.None],
        [SymptomCauseKind.DryAir, new[] { ProblemSign.BrownTips }, WhichLeaves.NotSure, HowFast.NotSure, SoilNow.NotSure, new SymptomFacts { LikesHighHumidity = true }],
        [SymptomCauseKind.Salts, new[] { ProblemSign.BrownTips }, WhichLeaves.NotSure, HowFast.NotSure, SoilNow.NotSure, new SymptomFacts { FeedingsLast3Months = 4 }],
        [SymptomCauseKind.Hungry, new[] { ProblemSign.PaleNewGrowth }, WhichLeaves.NotSure, HowFast.NotSure, SoilNow.NotSure, new SymptomFacts { FeedingsLast3Months = 0 }],
        [SymptomCauseKind.RootsFillPot, new[] { ProblemSign.Drooping }, WhichLeaves.NotSure, HowFast.NotSure, SoilNow.NotSure, new SymptomFacts { NotRepottedInTwoYears = true }],
        [SymptomCauseKind.Pests, new[] { ProblemSign.Sticky }, WhichLeaves.NotSure, HowFast.NotSure, SoilNow.NotSure, new SymptomFacts { OpenPestCase = true }],
        [SymptomCauseKind.LeafSpot, new[] { ProblemSign.Spots }, WhichLeaves.NotSure, HowFast.NotSure, SoilNow.Wet, SymptomFacts.None],
        [SymptomCauseKind.Ageing, new[] { ProblemSign.YellowLeaves }, WhichLeaves.Older, HowFast.NotSure, SoilNow.NotSure, SymptomFacts.None],
        [SymptomCauseKind.Rest, new[] { ProblemSign.YellowLeaves, ProblemSign.Drooping }, WhichLeaves.NotSure, HowFast.NotSure, SoilNow.NotSure, new SymptomFacts { Resting = true }]
    ];

    [Theory]
    [MemberData(nameof(Reachable))]
    public void Every_cause_can_be_reached(
        SymptomCauseKind cause, ProblemSign[] signs, WhichLeaves leaves, HowFast speed, SoilNow soil, SymptomFacts facts)
    {
        var result = SymptomCheck.Rank(signs, leaves, speed, soil, facts);

        Assert.Contains(cause, Kinds(result));
    }

    [Fact]
    public void Every_cause_has_a_name_and_checks_and_every_source_a_title_and_link()
    {
        foreach (var cause in Enum.GetValues<SymptomCauseKind>())
        {
            Assert.False(string.IsNullOrWhiteSpace(SymptomText.Name(cause)));
            Assert.NotEmpty(SymptomText.Checks(cause));
        }
        foreach (var source in Enum.GetValues<SymptomSource>())
        {
            Assert.False(string.IsNullOrWhiteSpace(SymptomText.SourceTitle(source)));
            Assert.StartsWith("https://", SymptomSources.Url(source));
        }
        Assert.Equal(4, SymptomText.GeneralChecks.Count);
    }

    [Fact]
    public void Every_sign_has_its_own_name()
    {
        var names = Enum.GetValues<ProblemSign>().Select(ProblemText.Sign).ToList();

        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.Equal("Sticky leaves", ProblemText.Sign(ProblemSign.Sticky));
        Assert.Equal(ProblemSign.Other, Enum.GetValues<ProblemSign>()[^1]);
    }

    // Only listed when

    [Fact]
    public void A_recent_change_is_only_listed_when_a_change_is_known_or_it_came_over_a_few_days()
    {
        // Leaf drop and drooping are 4 points for it, which would be enough without the rule
        ProblemSign[] signs = [ProblemSign.LeafDrop, ProblemSign.Drooping];

        Assert.DoesNotContain(SymptomCauseKind.RecentChange, Kinds(Rank(signs)));
        Assert.DoesNotContain(SymptomCauseKind.RecentChange, Kinds(Rank(signs, speed: HowFast.Weeks)));
        Assert.Contains(SymptomCauseKind.RecentChange, Kinds(Rank(signs, speed: HowFast.FewDays)));
        Assert.Contains(SymptomCauseKind.RecentChange,
            Kinds(Rank(signs, new SymptomFacts { Changes = [Change(RecentChangeKind.Arrived)] })));
    }

    [Fact]
    public void Natural_ageing_is_only_listed_when_the_person_says_older_leaves()
    {
        ProblemSign[] signs = [ProblemSign.YellowLeaves];

        Assert.DoesNotContain(SymptomCauseKind.Ageing, Kinds(Rank(signs)));
        Assert.DoesNotContain(SymptomCauseKind.Ageing, Kinds(Rank(signs, leaves: WhichLeaves.Newest)));
        Assert.DoesNotContain(SymptomCauseKind.Ageing, Kinds(Rank(signs, leaves: WhichLeaves.AllOver)));
        Assert.Contains(SymptomCauseKind.Ageing, Kinds(Rank(signs, leaves: WhichLeaves.Older)));
    }

    [Fact]
    public void Going_into_rest_is_only_listed_when_it_rests_now_or_is_dormant()
    {
        ProblemSign[] signs = [ProblemSign.YellowLeaves, ProblemSign.Drooping];

        Assert.DoesNotContain(SymptomCauseKind.Rest, Kinds(Rank(signs)));
        Assert.Contains(SymptomCauseKind.Rest, Kinds(Rank(signs, new SymptomFacts { Resting = true })));
    }

    // A source has to name the cause for the sign

    [Fact]
    public void A_cause_no_source_gives_for_the_signs_is_never_listed_however_much_the_facts_say()
    {
        // Every fact pushes every cause up. Only a sign picked can bring one in.
        var everything = new SymptomFacts
        {
            Changes = [Change(RecentChangeKind.Arrived), Change(RecentChangeKind.Repotted), Change(RecentChangeKind.Moved)],
            Light = LightFit.Darker,
            LikesDryOut = true,
            LikesHighHumidity = true,
            Meter = new MoistureReading(9, Today, 0),
            LongerThanUsual = true,
            FeedingsLast3Months = 5,
            NotRepottedInTwoYears = true,
            OpenPestCase = true,
            Resting = true,
            DarkerHalf = true,
            HeatingHalf = true
        };

        var result = Rank([ProblemSign.Speckled], everything, WhichLeaves.Older, HowFast.FewDays, SoilNow.Wet);

        Assert.Equal([SymptomCauseKind.Pests], Kinds(result));
        Assert.Empty(Rank([ProblemSign.Other], everything, WhichLeaves.Older, HowFast.FewDays, SoilNow.Wet).Causes);
    }

    [Fact]
    public void A_sign_that_needs_an_answer_only_counts_with_it()
    {
        var dark = new SymptomFacts { Light = LightFit.Darker, Changes = [Change(RecentChangeKind.Moved)] };

        // Yellow leaves explain too little light only on the older leaves
        Assert.DoesNotContain(SymptomCauseKind.TooLittleLight, Kinds(Rank([ProblemSign.YellowLeaves], dark)));
        Assert.DoesNotContain(SymptomCauseKind.TooLittleLight, Kinds(Rank([ProblemSign.YellowLeaves], dark, WhichLeaves.AllOver)));
        Assert.Contains(SymptomCauseKind.TooLittleLight, Kinds(Rank([ProblemSign.YellowLeaves], dark, WhichLeaves.Older)));

        // and too much sun only when yellow all over
        var bright = new SymptomFacts { Light = LightFit.Brighter, Changes = [Change(RecentChangeKind.Moved)] };
        Assert.DoesNotContain(SymptomCauseKind.TooMuchSun, Kinds(Rank([ProblemSign.YellowLeaves], bright, WhichLeaves.Older)));
        Assert.Contains(SymptomCauseKind.TooMuchSun, Kinds(Rank([ProblemSign.YellowLeaves], bright, WhichLeaves.AllOver)));
    }

    // Facts and answers move the order

    [Fact]
    public void Facts_and_answers_move_a_cause_up_and_down()
    {
        ProblemSign[] signs = [ProblemSign.Drooping];

        Assert.Empty(Rank(signs).Causes);
        Assert.Equal([SymptomCauseKind.TooWet], Kinds(Rank(signs, soil: SoilNow.Wet)));
        Assert.Equal([SymptomCauseKind.TooDry], Kinds(Rank(signs, soil: SoilNow.Dry)));

        // Wet soil is a point down for too dry, and water culture a point down for too wet
        Assert.Empty(Rank(signs, new SymptomFacts { WaterCulture = true }, soil: SoilNow.Wet).Causes);
        Assert.Empty(Rank(signs, new SymptomFacts { WateredRecently = true }, soil: SoilNow.Dry).Causes);
    }

    [Fact]
    public void Brighter_than_it_likes_moves_too_little_light_down_and_too_much_sun_up()
    {
        var bright = new SymptomFacts { Light = LightFit.Brighter, Changes = [Change(RecentChangeKind.Moved)] };

        // Leggy: too little light has 2 + moved 1 - brighter 1
        Assert.Empty(Rank([ProblemSign.Leggy], bright).Causes);
        Assert.Equal([SymptomCauseKind.TooMuchSun], Kinds(Rank([ProblemSign.Sunburn], bright)));
    }

    [Fact]
    public void The_meter_reading_counts_in_the_wet_and_dry_parts_only()
    {
        ProblemSign[] signs = [ProblemSign.Drooping];
        MoistureReading Reading(int value) => new(value, Today, 0);

        Assert.Empty(Rank(signs, new SymptomFacts { Meter = Reading(6) }).Causes);
        Assert.Equal([SymptomCauseKind.TooWet], Kinds(Rank(signs, new SymptomFacts { Meter = Reading(7) })));

        Assert.Empty(Rank(signs, new SymptomFacts { Meter = Reading(4) }).Causes);
        Assert.Equal([SymptomCauseKind.TooDry], Kinds(Rank(signs, new SymptomFacts { Meter = Reading(3) })));

        // The plant's own watering level moves the dry part
        Assert.Equal([SymptomCauseKind.TooDry], Kinds(Rank(signs, new SymptomFacts { Meter = Reading(5), DryAt = 5 })));
    }

    [Fact]
    public void A_cause_lists_the_facts_that_moved_it_up_and_not_the_answers()
    {
        var facts = new SymptomFacts { LikesDryOut = true, Meter = new MoistureReading(8, Today.AddDays(-1), 1), WaterCulture = false };

        var result = Rank([ProblemSign.YellowLeaves], facts, soil: SoilNow.Wet);

        var wet = Assert.Single(result.Causes, c => c.Kind == SymptomCauseKind.TooWet);
        Assert.Equal([SymptomFactKind.MeterReading, SymptomFactKind.LikesDryOut], wet.Because);
        Assert.Equal(2 + 3, wet.Points);
    }

    [Fact]
    public void A_cause_lists_the_sources_that_name_it_for_the_signs_picked()
    {
        var rot = Rank([ProblemSign.RootRot], soil: SoilNow.Wet).Causes.Single();
        Assert.Equal([SymptomSource.UcIpm], rot.Sources);

        var both = Rank([ProblemSign.RootRot, ProblemSign.Edema]).Causes.Single(c => c.Kind == SymptomCauseKind.TooWet);
        Assert.Equal([SymptomSource.UcIpm, SymptomSource.Rhs], both.Sources);
    }

    // The list and the fallback

    [Fact]
    public void At_most_three_are_listed_and_ties_keep_the_order_of_the_causes()
    {
        // Drooping and leaf drop: too wet 4, too dry 4, cold 4, roots fill the pot 4. Wet soil breaks the tie.
        var result = Rank([ProblemSign.Drooping, ProblemSign.LeafDrop], soil: SoilNow.Wet);

        Assert.Equal(
            [SymptomCauseKind.TooWet, SymptomCauseKind.Cold, SymptomCauseKind.RootsFillPot],
            Kinds(result));
        Assert.Equal([5, 4, 4], result.Causes.Select(c => c.Points));
    }

    [Fact]
    public void Nothing_clear_when_no_cause_reaches_three()
    {
        var result = Rank([ProblemSign.Drooping]);

        Assert.True(result.NoClearCause);
        Assert.Empty(result.Causes);
    }

    [Fact]
    public void Nothing_clear_when_more_than_three_tie_at_the_top()
    {
        var result = Rank([ProblemSign.Drooping, ProblemSign.LeafDrop]);

        Assert.True(result.NoClearCause);
    }

    [Fact]
    public void Three_tied_at_the_top_are_still_listed()
    {
        // Watered lately takes too dry down to 3. The other three stay at 4.
        var result = Rank([ProblemSign.Drooping, ProblemSign.LeafDrop], new SymptomFacts { WateredRecently = true });

        Assert.Equal(
            [SymptomCauseKind.TooWet, SymptomCauseKind.Cold, SymptomCauseKind.RootsFillPot],
            Kinds(result));
    }

    // The soil answer from a meter reading

    [Theory]
    [InlineData(10, SoilNow.Wet)]
    [InlineData(7, SoilNow.Wet)]
    [InlineData(6, SoilNow.Damp)]
    [InlineData(4, SoilNow.Damp)]
    [InlineData(3, SoilNow.Dry)]
    [InlineData(0, SoilNow.Dry)]
    public void A_meter_reading_fills_in_the_soil_answer(int reading, SoilNow expected) =>
        Assert.Equal(expected, SymptomCheck.SoilFromMeter(reading));

    [Fact]
    public void A_plant_with_its_own_watering_level_is_dry_up_to_it()
    {
        Assert.Equal(SoilNow.Dry, SymptomCheck.SoilFromMeter(5, 5));
        Assert.Equal(SoilNow.Damp, SymptomCheck.SoilFromMeter(6, 5));
    }

    // Unticking a fact

    [Fact]
    public void Taking_a_fact_out_leaves_the_others_and_changes_the_ranking()
    {
        var facts = new SymptomFacts
        {
            Changes = [Change(RecentChangeKind.Arrived), Change(RecentChangeKind.Moved)],
            Light = LightFit.Darker,
            FeedingsLast3Months = 6
        };

        var without = facts.Without(SymptomFactKind.Moved);
        Assert.False(without.Has(SymptomFactKind.Moved));
        Assert.True(without.Has(SymptomFactKind.Arrived));
        Assert.True(without.Has(SymptomFactKind.DarkerThanItLikes));
        Assert.True(without.Has(SymptomFactKind.FedOften));

        Assert.False(facts.Without([SymptomFactKind.DarkerThanItLikes, SymptomFactKind.FedOften]).Has(SymptomFactKind.FedOften));
        Assert.Equal(6, facts.Without(SymptomFactKind.NotFed).FeedingsLast3Months);

        // Too little light on leggy: 2 + darker + moved is listed, and unticking both takes it out
        Assert.Contains(SymptomCauseKind.TooLittleLight, Kinds(Rank([ProblemSign.Leggy], facts)));
        Assert.DoesNotContain(SymptomCauseKind.TooLittleLight,
            Kinds(Rank([ProblemSign.Leggy], facts.Without([SymptomFactKind.DarkerThanItLikes, SymptomFactKind.Moved]))));
    }

    // Gathering the facts in the service

    private sealed class Setup
    {
        public FakePlantRepository Plants { get; } = new();
        public FakeCareLogRepository Care { get; } = new();
        public FakeTimelineRepository Timeline { get; } = new();
        public FakePotRepository Pots { get; } = new();
        public FakeGrowLightRepository Lights { get; } = new();
        public FakePlaceRepository Places { get; } = new();
        public FakeAwayStretchRepository Away { get; } = new();
        public FakePestCaseRepository Cases { get; } = new();
        public Plant Plant { get; }

        public Setup(Action<Plant>? configure = null)
        {
            Plant = new Plant { Nickname = "Hoya", AcquiredOn = LooseDate.Of(Today.AddYears(-5)) };
            configure?.Invoke(Plant);
            Plants.Plants[Plant.Id] = Plant;
        }

        public SymptomCheckService Service =>
            new(Plants, Care, Timeline, Pots, Lights, Places, Away, Cases, new FixedTime(Now));

        public Task<SymptomContext> Gather() => Service.ForAsync(Plant);

        public void Log(CareKind kind, int daysAgo, int? moisture = null, DateTimeOffset? createdAt = null) =>
            Care.Logs.Add(new CareLog
            {
                PlantId = Plant.Id, Kind = kind, OccurredOn = Today.AddDays(-daysAgo), Moisture = moisture,
                CreatedAt = createdAt ?? Now
            });
    }

    [Fact]
    public async Task A_meter_reading_from_the_last_two_days_fills_in_the_soil_and_says_when()
    {
        var s = new Setup();
        s.Log(CareKind.MoistureReading, 2, 8);

        var context = await s.Gather();

        Assert.Equal(SoilNow.Wet, context.SoilFromMeter);
        Assert.Equal(Today.AddDays(-2), context.MeterDay);
        Assert.Contains(context.Lines, l => l.Kind == SymptomFactKind.MeterReading && l.Text == "Last meter reading 8, 2 days ago");
        Assert.Equal(8, context.Facts.Meter!.Value);
    }

    [Fact]
    public async Task An_older_reading_is_a_fact_but_does_not_fill_in_the_soil_and_a_stale_one_is_left_out()
    {
        var s = new Setup();
        s.Log(CareKind.MoistureReading, 5, 2);

        var context = await s.Gather();

        Assert.Null(context.SoilFromMeter);
        Assert.Equal(2, context.Facts.Meter!.Value);

        s.Log(CareKind.MoistureReading, 9, 9);
        s.Care.Logs.RemoveAt(0);
        Assert.Null((await s.Gather()).Facts.Meter);
    }

    [Fact]
    public async Task A_reading_from_before_the_last_watering_says_nothing_about_the_soil_now()
    {
        var s = new Setup();
        s.Log(CareKind.MoistureReading, 3, 1);
        s.Log(CareKind.Watered, 2);

        var context = await s.Gather();

        Assert.Null(context.Facts.Meter);
        Assert.Null(context.SoilFromMeter);
        Assert.True(context.Facts.WateredRecently);
        Assert.Contains(context.Lines, l => l.Kind == SymptomFactKind.WateredRecently && l.Text == "Watered 2 days ago");
    }

    [Fact]
    public async Task A_plants_own_watering_level_decides_which_readings_are_dry()
    {
        var s = new Setup(p => p.WaterAt = 5);
        s.Log(CareKind.MoistureReading, 1, 5);

        var context = await s.Gather();

        Assert.Equal(SoilNow.Dry, context.SoilFromMeter);
        Assert.Equal(5, context.Facts.DryAt);
    }

    [Fact]
    public async Task Arrival_repot_and_move_in_the_last_four_weeks_are_facts_with_lines()
    {
        var s = new Setup(p =>
        {
            p.AcquiredOn = LooseDate.Of(Today.AddDays(-20));
            p.PlaceId = Guid.NewGuid();
        });
        s.Log(CareKind.Repotted, 12);
        s.Timeline.Entries.Add(new TimelineEntry
        {
            SubjectType = SubjectType.Plant, SubjectId = s.Plant.Id, Kind = TimelineKind.Change,
            OccurredAt = Now.AddDays(-5), Text = "Moved to Kitchen"
        });

        var context = await s.Gather();

        Assert.Equal(
            ["Arrived 2 weeks ago", "Repotted 12 days ago", "Moved 5 days ago"],
            context.Lines.Where(l => l.Kind is SymptomFactKind.Arrived or SymptomFactKind.Repotted or SymptomFactKind.Moved).Select(l => l.Text));
    }

    [Fact]
    public async Task A_change_older_than_four_weeks_is_not_a_fact()
    {
        var s = new Setup(p => p.AcquiredOn = LooseDate.Of(Today.AddDays(-29)));

        var context = await s.Gather();

        Assert.Empty(context.Facts.Changes);
    }

    [Fact]
    public async Task Light_and_what_it_likes_come_from_the_plant()
    {
        var s = new Setup(p =>
        {
            p.Light = LightLevel.Low;
            p.LikesLight = LightLevel.BrightIndirect;
            p.LikesWater = WaterNeed.DryOut;
            p.LikesHumidity = HumidityNeed.High;
        });

        var context = await s.Gather();

        Assert.Equal(LightFit.Darker, context.Facts.Light);
        Assert.True(context.Facts.LikesDryOut);
        Assert.True(context.Facts.LikesHighHumidity);
        var texts = context.Lines.Select(l => l.Text).ToList();
        Assert.Contains("Stands darker than it likes", texts);
        Assert.Contains("Likes to dry out between waterings", texts);
        Assert.Contains("Likes high humidity", texts);
    }

    [Fact]
    public async Task Light_that_fits_or_is_not_set_is_not_a_fact()
    {
        var fits = new Setup(p => { p.Light = LightLevel.Medium; p.LikesLight = LightLevel.Medium; });
        Assert.Null((await fits.Gather()).Facts.Light);

        var unset = new Setup(p => p.LikesLight = LightLevel.Medium);
        Assert.Null((await unset.Gather()).Facts.Light);
    }

    [Fact]
    public async Task Longer_since_watering_than_its_usual_gap_only_counts_once_the_plant_is_learned()
    {
        var s = new Setup();
        s.Log(CareKind.Watered, 20);
        Assert.False((await s.Gather()).Facts.LongerThanUsual);

        // Every 7 days, the last 20 days ago
        foreach (var ago in new[] { 27, 34, 41 })
            s.Log(CareKind.Watered, ago);

        var context = await s.Gather();
        Assert.True(context.Facts.LongerThanUsual);
        Assert.Contains(context.Lines, l => l.Kind == SymptomFactKind.LongerThanUsual && l.Text.StartsWith("2 weeks since it was watered"));
    }

    [Fact]
    public async Task Feeding_is_counted_over_three_months_and_the_last_month()
    {
        var s = new Setup();
        s.Log(CareKind.Fertilised, 10);
        s.Log(CareKind.Fertilised, 40);
        s.Log(CareKind.Fertilised, 41);
        s.Log(CareKind.Fertilised, 60);
        s.Log(CareKind.Fertilised, 120);

        var context = await s.Gather();

        Assert.True(context.Facts.FedLastMonth);
        Assert.Equal(4, context.Facts.FeedingsLast3Months);
        Assert.Contains(context.Lines, l => l.Kind == SymptomFactKind.FedOften && l.Text == "Fed on 4 days in the last 3 months");
        Assert.Contains(context.Lines, l => l.Kind == SymptomFactKind.FedLastMonth);
    }

    [Fact]
    public async Task Never_fed_in_three_months_is_a_fact_unless_the_plant_rests()
    {
        var growing = new Setup();
        var context = await growing.Gather();
        Assert.Equal(0, context.Facts.FeedingsLast3Months);
        Assert.Contains(context.Lines, l => l.Kind == SymptomFactKind.NotFed);

        var resting = new Setup(p => p.GrowingSeason = GrowingSeason.Summer);
        var restingContext = await resting.Gather();
        Assert.True(restingContext.Facts.Resting);
        Assert.Null(restingContext.Facts.FeedingsLast3Months);
        Assert.DoesNotContain(restingContext.Lines, l => l.Kind == SymptomFactKind.NotFed);
    }

    [Fact]
    public async Task Not_repotted_in_two_years_uses_the_repot_log_and_falls_back_to_the_day_it_was_got()
    {
        var old = new Setup();
        Assert.True((await old.Gather()).Facts.NotRepottedInTwoYears);

        var repotted = new Setup();
        repotted.Log(CareKind.Repotted, 400);
        Assert.False((await repotted.Gather()).Facts.NotRepottedInTwoYears);

        var longAgo = new Setup();
        longAgo.Log(CareKind.Repotted, 800);
        Assert.True((await longAgo.Gather()).Facts.NotRepottedInTwoYears);

        var newcomer = new Setup(p => p.AcquiredOn = LooseDate.Of(Today.AddYears(-1)));
        Assert.False((await newcomer.Gather()).Facts.NotRepottedInTwoYears);

        var unknown = new Setup(p => p.AcquiredOn = null);
        Assert.True((await unknown.Gather()).Facts.NotRepottedInTwoYears);
    }

    [Fact]
    public async Task An_open_pest_case_covering_the_plant_is_a_fact_and_a_resolved_one_is_not()
    {
        var s = new Setup();
        var item = new PestCase { Scope = PestScope.PickedPlants, PlantIds = [s.Plant.Id], StartedOn = Today.AddDays(-3) };
        s.Cases.Cases[item.Id] = item;

        var context = await s.Gather();
        Assert.True(context.Facts.OpenPestCase);
        Assert.Contains(context.Lines, l => l.Kind == SymptomFactKind.PestCase);

        item.Status = PestCaseStatus.Resolved;
        item.ResolvedOn = Today;
        Assert.False((await s.Gather()).Facts.OpenPestCase);
    }

    [Fact]
    public async Task Resting_comes_from_the_growing_season_or_from_being_dormant()
    {
        // 6 November is in the winter rest of a plant that grows in summer
        var summer = new Setup(p => p.GrowingSeason = GrowingSeason.Summer);
        Assert.True((await summer.Gather()).Facts.Resting);

        var winter = new Setup(p => p.GrowingSeason = GrowingSeason.Winter);
        Assert.False((await winter.Gather()).Facts.Resting);

        var noRest = new Setup(p => p.GrowingSeason = GrowingSeason.NoRest);
        Assert.False((await noRest.Gather()).Facts.Resting);

        var dormant = new Setup(p => p.DormantSince = Today.AddDays(-10));
        var context = await dormant.Gather();
        Assert.True(context.Facts.Resting);
        Assert.Contains(context.Lines, l => l.Text == "Marked as dormant");
    }

    [Fact]
    public async Task Water_culture_and_the_time_of_year_are_facts()
    {
        var leca = new Setup(p => p.Medium = GrowingMedium.Leca);
        var context = await leca.Gather();
        Assert.True(context.Facts.WaterCulture);

        var soil = await new Setup().Gather();
        Assert.False(soil.Facts.WaterCulture);

        // 6 November is in the darker, colder half in the north
        Assert.True(soil.Facts.DarkerHalf);
        Assert.True(soil.Facts.HeatingHalf);
    }

    [Fact]
    public async Task A_fact_that_is_taken_out_leaves_the_ranking_without_it()
    {
        var s = new Setup(p => { p.Light = LightLevel.Low; p.LikesLight = LightLevel.BrightIndirect; });
        var context = await s.Gather();
        ProblemSign[] signs = [ProblemSign.Leggy];

        // Darker 1 + darker half of the year 1 on top of 2
        var all = SymptomCheck.Rank(signs, WhichLeaves.NotSure, HowFast.NotSure, SoilNow.NotSure, context.Facts);
        Assert.Contains(SymptomCauseKind.TooLittleLight, Kinds(all));

        var ticked = context.Facts.Without([SymptomFactKind.DarkerThanItLikes, SymptomFactKind.DarkerHalf]);
        var rest = SymptomCheck.Rank(signs, WhichLeaves.NotSure, HowFast.NotSure, SoilNow.NotSure, ticked);
        Assert.DoesNotContain(SymptomCauseKind.TooLittleLight, Kinds(rest));
    }
}
