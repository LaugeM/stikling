using System.Globalization;
using Stikling.Core.Models;
using Stikling.Core.Text;
using Stikling.Core.Plants;
using Stikling.Core.Pests;
using Stikling.Core.Pots;
using Stikling.Core.SoilMixes;
using Strings = Stikling.Web.Resources;

namespace Stikling.Web.Services;

/// <summary>Human-readable text for enum values and dates shown in the UI.</summary>
public static class Labels
{
    public static string For(PlantStatus status) => status switch
    {
        PlantStatus.Active => Strings.Labels.StatusActive,
        PlantStatus.Died => Strings.Labels.StatusDied,
        PlantStatus.GivenAway => Strings.Labels.StatusGivenAway,
        PlantStatus.Sold => Strings.Labels.StatusSold,
        _ => status.ToString()
    };

    /// <summary>
    /// What to say about a plant that isn't in the collection: its status, or that it is potted with
    /// another plant, by name when the name is known.
    /// </summary>
    public static string StatusOf(Plant plant, string? pottedWith = null) =>
        plant.LivesElsewhere
            ? (plant.LivesWithName is { } owner ? string.Format(CultureInfo.CurrentCulture, Strings.Labels.StatusLivesWith, owner) : Strings.Labels.StatusNotYours)
            : plant.Status == PlantStatus.Active && plant.PottedWithPlantId is not null
            ? (string.IsNullOrWhiteSpace(pottedWith) ? Strings.Labels.StatusPottedWithOther : string.Format(CultureInfo.CurrentCulture, Strings.Labels.StatusPottedWith, pottedWith))
            : For(plant.Status);

    public static string For(PlantOrigin origin) => origin switch
    {
        PlantOrigin.Purchased => Strings.Labels.OriginPurchased,
        PlantOrigin.Propagated => Strings.Labels.OriginPropagated,
        PlantOrigin.GrownFromSeed => Strings.Labels.OriginGrownFromSeed,
        PlantOrigin.Gift => Strings.Labels.OriginGift,
        PlantOrigin.Swap => Strings.Labels.OriginSwap,
        PlantOrigin.Unknown => Strings.Labels.OriginUnknown,
        _ => origin.ToString()
    };

    public static string For(RaisedAs raised) => raised switch
    {
        RaisedAs.NurseryGrown => Strings.Labels.RaisedNurseryGrown,
        RaisedAs.TissueCulture => Strings.Labels.RaisedTissueCulture,
        RaisedAs.Seed => Strings.Labels.RaisedSeed,
        RaisedAs.Cutting => Strings.Labels.RaisedCutting,
        RaisedAs.WildCollected => Strings.Labels.RaisedWildCollected,
        _ => raised.ToString()
    };

    public static string For(SellerKind kind) => kind switch
    {
        SellerKind.Shop => Strings.Labels.SellerShop,
        SellerKind.OnlineShop => Strings.Labels.SellerOnlineShop,
        SellerKind.Person => Strings.Labels.SellerPerson,
        SellerKind.SwapEvent => Strings.Labels.SellerSwapEvent,
        SellerKind.Other => Strings.Labels.SellerOther,
        _ => kind.ToString()
    };

    public static string For(ArrivalRating rating) => rating switch
    {
        ArrivalRating.Good => Strings.Labels.ArrivalGood,
        ArrivalRating.Ok => Strings.Labels.ArrivalOk,
        ArrivalRating.Poor => Strings.Labels.ArrivalPoor,
        _ => rating.ToString()
    };

    public static string For(ArrivalProblem problem) => problem switch
    {
        ArrivalProblem.Pests => Strings.Labels.ArrivalPests,
        ArrivalProblem.Damaged => Strings.Labels.ArrivalDamaged,
        ArrivalProblem.RootRot => Strings.Labels.ArrivalRootRot,
        ArrivalProblem.WrongPlant => Strings.Labels.ArrivalWrongPlant,
        ArrivalProblem.SmallerThanShown => Strings.Labels.ArrivalSmallerThanShown,
        _ => problem.ToString()
    };

    public static string For(LightLevel light) => light switch
    {
        LightLevel.Low => Strings.Labels.LightLow,
        LightLevel.Medium => Strings.Labels.LightMedium,
        LightLevel.BrightIndirect => Strings.Labels.LightBrightIndirect,
        LightLevel.DirectSun => Strings.Labels.LightDirectSun,
        _ => light.ToString()
    };

    public static string For(WaterNeed need) => need switch
    {
        WaterNeed.DryOut => Strings.Labels.WaterDryOut,
        WaterNeed.DryPartly => Strings.Labels.WaterDryPartly,
        WaterNeed.EvenlyMoist => Strings.Labels.WaterEvenlyMoist,
        _ => need.ToString()
    };

    public static string For(HumidityNeed need) => need switch
    {
        HumidityNeed.Low => Strings.Labels.HumidityLow,
        HumidityNeed.Medium => Strings.Labels.HumidityMedium,
        HumidityNeed.High => Strings.Labels.HumidityHigh,
        _ => need.ToString()
    };

    public static string For(GrowingSeason season) => season switch
    {
        GrowingSeason.Summer => Strings.Labels.SeasonRestsInWinter,
        GrowingSeason.Winter => Strings.Labels.SeasonRestsInSummer,
        GrowingSeason.NoRest => Strings.Labels.SeasonGrowsAllYear,
        _ => season.ToString()
    };

    public static string For(RestKind rest) => rest switch
    {
        RestKind.Cooler => Strings.Labels.RestCooler,
        RestKind.Drier => Strings.Labels.RestDrier,
        RestKind.CoolerAndDrier => Strings.Labels.RestCoolerAndDrier,
        RestKind.DiesBack => Strings.Labels.RestDiesBack,
        _ => rest.ToString()
    };

    public static string For(GrowingMedium medium) => medium switch
    {
        GrowingMedium.OrchidBark => Strings.Labels.MediumOrchidBark,
        GrowingMedium.Leca => Strings.Labels.MediumLeca,
        GrowingMedium.Pon => Strings.Labels.MediumPon,
        GrowingMedium.Sphagnum => Strings.Labels.MediumSphagnum,
        GrowingMedium.CormRiser => Strings.Labels.MediumCormRiser,
        GrowingMedium.Soil => Strings.Labels.MediumSoil,
        GrowingMedium.Perlite => Strings.Labels.MediumPerlite,
        GrowingMedium.Water => Strings.Labels.MediumWater,
        _ => medium.ToString()
    };

    /// <summary>The start of a longer text on one line, for a form section's summary.</summary>
    public static string Short(string text, int max = 50)
    {
        var line = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return line.Length <= max ? line : line[..max].TrimEnd() + "…";
    }

    /// <summary>A line on what the less obvious media are, or null for the ones that need none.</summary>
    public static string? Explain(GrowingMedium medium) => medium switch
    {
        GrowingMedium.Leca => Strings.Labels.ExplainLeca,
        GrowingMedium.Pon => Strings.Labels.ExplainPon,
        GrowingMedium.Sphagnum => Strings.Labels.ExplainSphagnum,
        GrowingMedium.CormRiser => Strings.Labels.ExplainCormRiser,
        _ => null
    };

    /// <summary>A line on what a propagation type means, or null for the ones that need none.</summary>
    public static string? Explain(PropagationType type) => type switch
    {
        PropagationType.AirLayer => Strings.Labels.ExplainAirLayer,
        _ => null
    };

    public static string For(RootingAid aid) => aid switch
    {
        RootingAid.RootingPowder => Strings.Labels.AidRootingPowder,
        RootingAid.RootingGel => Strings.Labels.AidRootingGel,
        RootingAid.WillowWater => Strings.Labels.AidWillowWater,
        RootingAid.HumidityDome => Strings.Labels.AidHumidityDome,
        RootingAid.HeatMat => Strings.Labels.AidHeatMat,
        _ => aid.ToString()
    };

    public static string For(CareKind kind) => kind switch
    {
        CareKind.Watered => Strings.Labels.CareWatered,
        CareKind.Fertilised => Strings.Labels.CareFertilised,
        CareKind.ToppedUp => Strings.Labels.CareToppedUp,
        CareKind.Flushed => Strings.Labels.CareFlushed,
        CareKind.Repotted => Strings.Labels.CareRepotted,
        CareKind.Pruned => Strings.Labels.CarePruned,
        CareKind.Rotated => Strings.Labels.CareRotated,
        CareKind.LeavesCleaned => Strings.Labels.CareLeavesCleaned,
        CareKind.Harvested => Strings.Labels.CareHarvested,
        CareKind.MoistureReading => Strings.Labels.CareMoistureReading,
        _ => kind.ToString()
    };

    public static string For(PropagationType type) => type switch
    {
        PropagationType.Cutting => Strings.Labels.TypeCutting,
        PropagationType.Corm => Strings.Labels.TypeCorm,
        PropagationType.Offset => Strings.Labels.TypeOffset,
        PropagationType.Seed => Strings.Labels.TypeSeed,
        PropagationType.Division => Strings.Labels.TypeDivision,
        PropagationType.AirLayer => Strings.Labels.TypeAirLayer,
        PropagationType.Leaf => Strings.Labels.TypeLeaf,
        PropagationType.Other => Strings.Labels.TypeOther,
        _ => type.ToString()
    };

    public static string For(Pest pest) => pest switch
    {
        Pest.SpiderMites => Strings.Labels.PestSpiderMites,
        Pest.Thrips => Strings.Labels.PestThrips,
        Pest.FungusGnats => Strings.Labels.PestFungusGnats,
        Pest.Mealybugs => Strings.Labels.PestMealybugs,
        Pest.Scale => Strings.Labels.PestScale,
        Pest.Aphids => Strings.Labels.PestAphids,
        Pest.Whitefly => Strings.Labels.PestWhitefly,
        Pest.Other => Strings.Labels.PestOther,
        _ => pest.ToString()
    };

    public static string For(PestCaseStatus status) => status switch
    {
        PestCaseStatus.Active => Strings.Labels.CaseTreating,
        PestCaseStatus.Monitoring => Strings.Labels.CaseWatching,
        PestCaseStatus.Resolved => Strings.Labels.CaseResolved,
        _ => status.ToString()
    };

    /// <summary>"All plants", "Living room" or "4 plants", for a case's one-line summary.</summary>
    public static string Scope(PestCaseView view) => view.Case.Scope switch
    {
        PestScope.Everywhere => Strings.Labels.ScopeAllPlants,
        PestScope.Room => view.Room ?? Strings.Labels.ScopeARoom,
        _ => Plants(view.Plants.Count)
    };

    /// <summary>"Living room · 4 plants", or just "2 plants" when the case covers picked plants.</summary>
    public static string ScopeAndCount(PestCaseView view) =>
        view.Case.Scope == PestScope.PickedPlants
            ? Plants(view.Plants.Count)
            : $"{Scope(view)} · {Plants(view.Plants.Count)}";

    /// <summary>
    /// When a case wants its next treatment or check, or "no treatment yet" when it's being treated
    /// and nothing is logged, rather than counting days overdue since the pests were found.
    /// </summary>
    public static string Due(PestCaseView view) =>
        view.Untreated && view.IsDue && view.StepNumber <= 1 ? Strings.Labels.DueNoTreatmentYet : Due(view.DaysUntilDue ?? 0, view.IsCheck);

    /// <summary>"due today", "2 days overdue", "next in 3 days", or "check due today" and so on for a check.</summary>
    public static string Due(int daysUntil, bool check = false) => daysUntil switch
    {
        0 => check ? Strings.Labels.CheckDueToday : Strings.Labels.DueToday,
        < 0 => check
            ? Plural.Of(-daysUntil, Strings.Labels.CheckOverdue_One, Strings.Labels.CheckOverdue_Other)
            : Plural.Of(-daysUntil, Strings.Labels.DueOverdue_One, Strings.Labels.DueOverdue_Other),
        1 => check ? Strings.Labels.CheckDueTomorrow : Strings.Labels.DueTomorrow,
        _ => string.Format(CultureInfo.CurrentCulture, check ? Strings.Labels.CheckDueInDays : Strings.Labels.DueInDays, daysUntil)
    };

    /// <summary>Stage names. Seeds get their own words: sown, germinating, sprouted.</summary>
    public static string Stage(PropagationStage stage, PropagationType type = PropagationType.Cutting) =>
        (stage, type == PropagationType.Seed) switch
        {
            (PropagationStage.Started, true) => Strings.Labels.StageSown,
            (PropagationStage.Rooting, true) => Strings.Labels.StageGerminating,
            (PropagationStage.Rooted, true) => Strings.Labels.StageSprouted,
            (PropagationStage.Started, _) => Strings.Labels.StageStarted,
            (PropagationStage.Rooting, _) => Strings.Labels.StageRooting,
            (PropagationStage.Rooted, _) => Strings.Labels.StageRooted,
            (PropagationStage.Done, _) => Strings.Labels.StageDone,
            (PropagationStage.Failed, _) => Strings.Labels.StageFailed,
            _ => stage.ToString()
        };

    public static string For(PropagationCareKind kind) => kind switch
    {
        PropagationCareKind.Watered => Strings.Labels.CareWatered,
        PropagationCareKind.ChangedWater => Strings.Labels.CareChangedWater,
        PropagationCareKind.MoistureReading => Strings.Labels.CareMoistureReading,
        _ => kind.ToString()
    };

    /// <summary>
    /// What "Died" reads as in the "Status: Died (was Active)" lines saved in a plant's history: the English word,
    /// since the lines have been saved in English, and the one for the current language, for lines saved in it.
    /// </summary>
    public static string[] DiedInSavedLines()
    {
        var english = Strings.Labels.ResourceManager.GetString(nameof(Strings.Labels.StatusDied), CultureInfo.InvariantCulture)!;
        var current = For(PlantStatus.Died);
        return english == current ? [english] : [english, current];
    }

    /// <summary>Any of the app's enums, for code that works with several (e.g. history text).</summary>
    public static string For(Enum value) => value switch
    {
        PlantStatus s => For(s),
        PlantOrigin o => For(o),
        GrowingMedium m => For(m),
        LightLevel l => For(l),
        RootingAid a => For(a),
        CareKind c => For(c),
        PropagationCareKind c => For(c),
        PropagationType t => For(t),
        PropagationStage s => Stage(s),
        Pest p => For(p),
        PestCaseStatus s => For(s),
        PotKind g => For(g),
        PotMaterial m => For(m),
        PotWatering w => For(w),
        MixUnit u => For(u),
        DoseUnit u => For(u),
        ProductKind k => For(k),
        _ => value.ToString()
    };

    /// <summary>
    /// <see cref="For(Enum)"/> in English, whatever language the app is in. The History text saved with an entry
    /// and read back by <c>HistoryText.Parse</c> is in English. The app runs on one thread, so the language
    /// can be switched around the call.
    /// </summary>
    public static string English(Enum value)
    {
        var ui = CultureInfo.CurrentUICulture;
        var culture = CultureInfo.CurrentCulture;
        var english = CultureInfo.GetCultureInfo("en-GB");
        CultureInfo.CurrentUICulture = english;
        CultureInfo.CurrentCulture = english;
        try
        {
            return For(value);
        }
        finally
        {
            CultureInfo.CurrentUICulture = ui;
            CultureInfo.CurrentCulture = culture;
        }
    }

    /// <summary>Like <see cref="For(Enum)"/>, with stage names that fit the propagation's type.</summary>
    public static Func<Enum, string> For(Propagation propagation) =>
        value => value is PropagationStage stage ? Stage(stage, propagation.Type) : For(value);

    /// <summary>"3× corm · LECA".</summary>
    public static string Summary(Propagation propagation) =>
        string.Format(CultureInfo.CurrentCulture, Strings.Labels.PropagationSummary,
            propagation.InitialCount, For(propagation.Type).ToLowerInvariant(), For(propagation.Medium));

    /// <summary>"started today" / "day 24".</summary>
    public static string Age(Propagation propagation, DateOnly today) =>
        propagation.DaysSinceStart(today) switch
        {
            0 => Strings.Labels.AgeStartedToday,
            var days => string.Format(CultureInfo.CurrentCulture, Strings.Labels.AgeDay, days)
        };

    /// <summary>"12.4 MB", for the storage line in Settings.</summary>
    public static string Bytes(long bytes) => bytes switch
    {
        < 1024 => string.Format(CultureInfo.CurrentCulture, "{0} B", bytes),
        < 1024 * 1024 => string.Format(CultureInfo.CurrentCulture, "{0:0.#} KB", bytes / 1024d),
        < 1024L * 1024 * 1024 => string.Format(CultureInfo.CurrentCulture, "{0:0.#} MB", bytes / (1024d * 1024)),
        _ => string.Format(CultureInfo.CurrentCulture, "{0:0.##} GB", bytes / (1024d * 1024 * 1024))
    };

    /// <summary>"soil" in the middle of a line, but LECA and PON stay in capitals.</summary>
    public static string MediumWord(GrowingMedium medium) =>
        For(medium) is var label && label == label.ToUpperInvariant() ? label : label.ToLowerInvariant();

    /// <summary>"today", "yesterday", "12 days ago".</summary>
    public static string DaysAgo(int days) => days switch
    {
        <= 0 => Strings.Labels.DaysAgoToday,
        1 => Strings.Labels.DaysAgoYesterday,
        _ => string.Format(CultureInfo.CurrentCulture, Strings.Labels.DaysAgoMany, days)
    };

    /// <summary>"from March" for a month, "from Sat 17 Oct" for a day, for a flag set for later.</summary>
    public static string FlagFrom(LooseDate from) => from.Precision == DatePrecision.Day
        ? string.Format(CultureInfo.CurrentCulture, Strings.Labels.FlagFromDay, Format(from.Start, Strings.Labels.DayFormat))
        : string.Format(CultureInfo.CurrentCulture, Strings.Labels.FlagFromMonth, Format(from.Start, Strings.Labels.MonthFormat));

    /// <summary>
    /// "Usually likes bright indirect · dry out fully · rests in winter, kept drier": the needs the care file
    /// would fill in, in the order light, water, humidity, season with how it rests. The source comes after it.
    /// </summary>
    public static string Offer(IReadOnlyList<LikesField> fills, PlantCare care)
    {
        var parts = new List<string>();
        if (fills.Contains(LikesField.Light) && care.Light is { } light) parts.Add(Lower(For(light)));
        if (fills.Contains(LikesField.Water) && care.Water is { } water) parts.Add(Lower(For(water)));
        if (fills.Contains(LikesField.Humidity) && care.Humidity is { } humidity) parts.Add(Lower(For(humidity)));

        // A way of resting is only offered for the season the plant already has or gets, so name it with the rest
        var season = (fills.Contains(LikesField.Season) || fills.Contains(LikesField.Rest)) && care.Season is { } s ? Lower(For(s)) : null;
        var rest = fills.Contains(LikesField.Rest) && care.Rest is { } r ? For(r) : null;
        if (season is not null || rest is not null)
            parts.Add(string.Join(", ", new[] { season, rest }.OfType<string>()));
        return string.Format(CultureInfo.CurrentCulture, Strings.Labels.UsuallyLikes, string.Join(" · ", parts));
    }

    private static string Lower(string text) => char.ToLowerInvariant(text[0]) + text[1..];

    /// <summary>"1 plant" / "3 plants".</summary>
    public static string Plants(int count) => Plural.Of(count, Strings.Common.Plant_One, Strings.Common.Plant_Other);

    public static string Time(DateTimeOffset moment) =>
        moment.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

    // English reads "19 Sep 2026" (en-GB would write "Sept"), see TextCulture
    public static string Date(DateOnly date) => Format(date, Strings.Labels.DateFormat);

    /// <summary>"2024", "June 2024" or "12 Jun 2024", depending on how much is known.</summary>
    public static string Date(LooseDate date) => date.Precision switch
    {
        DatePrecision.Year => date.Text(),
        DatePrecision.Month => Format(date.Start, Strings.Labels.MonthYearFormat),
        _ => Date(date.Start)
    };

    /// <summary>"Mon", for the buttons that pick weekdays.</summary>
    public static string DayShort(Weekday day) =>
        TextCulture.ForDates.DateTimeFormat.GetAbbreviatedDayName(Enum.Parse<DayOfWeek>(day.ToString()));

    private static string Format(DateOnly date, string pattern) => date.ToString(pattern, TextCulture.ForDates);

    public static string For(PotKind group) => group switch
    {
        PotKind.Inner => Strings.Labels.PotNursery,
        PotKind.Outer => Strings.Labels.PotOuter,
        PotKind.Propagation => Strings.Labels.PotPropagation,
        _ => Strings.Labels.PotStandalone
    };

    /// <summary>The short name on the switch between kinds at the top of the pot library.</summary>
    public static string Tab(PotKind group) => group switch
    {
        PotKind.Inner => Strings.Labels.TabNursery,
        PotKind.Outer => Strings.Labels.TabOuter,
        PotKind.Propagation => Strings.Labels.TabPropagation,
        _ => Strings.Labels.TabStandalone
    };

    /// <summary>The heading over a group of pots in the library.</summary>
    public static string Heading(PotKind group) => group switch
    {
        PotKind.Inner => Strings.Labels.HeadingNursery,
        PotKind.Outer => Strings.Labels.HeadingOuter,
        PotKind.Propagation => Strings.Labels.HeadingPropagation,
        _ => Strings.Labels.HeadingStandalone
    };

    public static string For(PotMaterial material) => material switch
    {
        PotMaterial.Terracotta => Strings.Labels.MaterialTerracotta,
        PotMaterial.Ceramic => Strings.Labels.MaterialCeramic,
        PotMaterial.Glass => Strings.Labels.MaterialGlass,
        PotMaterial.Metal => Strings.Labels.MaterialMetal,
        PotMaterial.Other => Strings.Labels.MaterialOther,
        _ => Strings.Labels.MaterialPlastic
    };

    public static string For(PotWatering watering) =>
        watering == PotWatering.Wick ? Strings.Labels.WateringWick : Strings.Labels.WateringSubmerged;

    /// <summary>"2 of 3 in use, 1 free", so the library says what is still available.</summary>
    public static string Use(PotUse use)
    {
        var here = use switch
        {
            { Here: 0 } => null,
            { InUse: 0 } => use.Here == 1 ? Strings.Labels.UseFree : string.Format(CultureInfo.CurrentCulture, Strings.Labels.UseAllFree, use.Here),
            { Free: 0 } => use.Here == 1 ? Strings.Labels.UseInUse : string.Format(CultureInfo.CurrentCulture, Strings.Labels.UseAllInUse, use.Here),
            _ => string.Format(CultureInfo.CurrentCulture, Strings.Labels.UseSome, use.InUse, use.Here, use.Free)
        };
        var gone = use.Gone == 0 ? null : Plural.Of(use.Gone, Strings.Labels.UseGone_One, Strings.Labels.UseGone_Other);
        return string.Join(", ", new[] { here, gone }.OfType<string>());
    }

    /// <summary>
    /// The answers to "what went with it" that fit the plants' pots: always just the plant, then
    /// its pot, then both when there is an outer pot too. Empty when none of them has a pot.
    /// </summary>
    public static IReadOnlyList<(PotsTaken Value, string Label)> PotsTakenChoices(IReadOnlyCollection<Plant> plants)
    {
        var inner = plants.Any(p => p.InnerPotId is not null);
        var outer = plants.Any(p => p.OuterPotId is not null);
        var one = plants.Count == 1;

        if (!inner && !outer)
            return [];

        var choices = new List<(PotsTaken, string)> { (PotsTaken.None, one ? Strings.Labels.TakenPlantOnly_One : Strings.Labels.TakenPlantOnly_Other) };
        if (inner)
            choices.Add((PotsTaken.Inner, one ? Strings.Labels.TakenWithPot_One : Strings.Labels.TakenWithPot_Other));
        if (outer)
            choices.Add((PotsTaken.All, (one, inner) switch
            {
                (true, true) => Strings.Labels.TakenWithBothPots,
                (true, false) => Strings.Labels.TakenWithOuterPot,
                _ => Strings.Labels.TakenWithOuterPots
            }));
        return choices;
    }

    public static string For(MixUnit unit) => unit switch
    {
        MixUnit.Parts => Strings.Labels.MixParts,
        MixUnit.Percent => Strings.Labels.MixPercent,
        _ => Strings.Labels.MixNoAmounts
    };

    public static string For(DoseUnit unit) => Doses.Symbol(unit);

    public static string For(ProductKind kind) => kind switch
    {
        ProductKind.Fertiliser => Strings.Labels.ProductFertiliser,
        ProductKind.Stimulant => Strings.Labels.ProductStimulant,
        ProductKind.Silica => Strings.Labels.ProductSilica,
        ProductKind.CalMag => Strings.Labels.ProductCalMag,
        ProductKind.Ph => Strings.Labels.ProductPh,
        ProductKind.Other => Strings.Labels.ProductOther,
        _ => kind.ToString()
    };

    public static string For(Nutrient nutrient) => nutrient switch
    {
        Nutrient.Nitrogen => Strings.Labels.NutrientNitrogen,
        Nutrient.Phosphorus => Strings.Labels.NutrientPhosphorus,
        Nutrient.Potassium => Strings.Labels.NutrientPotassium,
        Nutrient.Calcium => Strings.Labels.NutrientCalcium,
        Nutrient.Magnesium => Strings.Labels.NutrientMagnesium,
        Nutrient.Sulphur => Strings.Labels.NutrientSulphur,
        Nutrient.Silicon => Strings.Labels.NutrientSilicon,
        Nutrient.Iron => Strings.Labels.NutrientIron,
        Nutrient.Manganese => Strings.Labels.NutrientManganese,
        Nutrient.Zinc => Strings.Labels.NutrientZinc,
        Nutrient.Boron => Strings.Labels.NutrientBoron,
        Nutrient.Copper => Strings.Labels.NutrientCopper,
        Nutrient.Molybdenum => Strings.Labels.NutrientMolybdenum,
        _ => nutrient.ToString()
    };

    /// <summary>Said under the ingredients when the percentages do not reach 100. Never blocks a save.</summary>
    public static string PercentTotal(decimal total) =>
        string.Format(CultureInfo.CurrentCulture, Strings.Labels.PercentTotal, SoilMix.Number(total));
}
