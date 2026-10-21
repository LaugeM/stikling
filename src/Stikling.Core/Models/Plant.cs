using System.Text.Json.Serialization;

namespace Stikling.Core.Models;

public sealed class Plant : Entity
{
    /// <summary>Your own name for the plant, e.g. "Big Monstera" or "Kitchen basil".</summary>
    public string? Nickname { get; set; }

    /// <summary>
    /// The short number written on the pot, like "117", and for a cutting from it "117a". Null when
    /// it has none. Optional, and not unique: a merge can make two with the same number. See
    /// <see cref="Plants.CollectionNumbers"/>.
    /// </summary>
    public string? CollectionNumber { get; set; }

    public string? Genus { get; set; }
    public string? Species { get; set; }

    /// <summary>Named variety, e.g. "Thai Constellation".</summary>
    public string? Cultivar { get; set; }

    /// <summary>How sure the name is: cf., aff., sp. or lost. Null when it is sure.</summary>
    [JsonConverter(typeof(TolerantNameQualifierConverter))]
    public NameQualifier? NameQualifier { get; set; }

    /// <summary>A locality or collector code that goes with the name, like "Sumatra EPC 123".</summary>
    public string? NameNote { get; set; }

    /// <summary>The species that counts: none while sp. or a lost name hides it. The stored value stays.</summary>
    [JsonIgnore]
    public string? KnownSpecies => PlantNames.SpeciesToLookUp(Species, NameQualifier);

    /// <summary>The room or spot it stands in. See <see cref="Place"/>.</summary>
    public Guid? PlaceId { get; set; }

    /// <summary>How much light it gets where it stands. Null when not set.</summary>
    public LightLevel? Light { get; set; }

    /// <summary>The light it likes, which can differ from <see cref="Light"/>, where it stands. Null when not set.</summary>
    public LightLevel? LikesLight { get; set; }

    /// <summary>How it likes its water. Null when not set.</summary>
    public WaterNeed? LikesWater { get; set; }

    /// <summary>How much humidity it likes. Null when not set.</summary>
    public HumidityNeed? LikesHumidity { get; set; }

    /// <summary>When it grows and whether it rests. Null when not set.</summary>
    public GrowingSeason? GrowingSeason { get; set; }

    /// <summary>How it rests. Only meaningful when <see cref="GrowingSeason"/> is Summer or Winter.</summary>
    public RestKind? RestHow { get; set; }

    public PlantOrigin Origin { get; set; } = PlantOrigin.Purchased;

    /// <summary>
    /// How it was raised, for a plant that was bought, given, swapped or of unknown origin. Null when
    /// not said. The stored value stays when the origin changes, so use <see cref="Raised"/> to read it.
    /// </summary>
    [JsonConverter(typeof(TolerantNullableEnumConverter<RaisedAs>))]
    public RaisedAs? RaisedAs { get; set; }

    /// <summary>
    /// How it was raised as far as the app can tell: seed for a plant grown from seed and cutting
    /// for a propagated one, otherwise what <see cref="RaisedAs"/> says.
    /// </summary>
    [JsonIgnore]
    public RaisedAs? Raised => Origin switch
    {
        PlantOrigin.GrownFromSeed => Models.RaisedAs.Seed,
        PlantOrigin.Propagated => Models.RaisedAs.Cutting,
        _ => RaisedAs
    };

    /// <summary>True when the plant form asks how it was raised: for a bought, given or swapped plant, or one of unknown origin.</summary>
    [JsonIgnore]
    public bool AsksHowRaised => Origin is PlantOrigin.Purchased or PlantOrigin.Gift or PlantOrigin.Swap or PlantOrigin.Unknown;

    /// <summary>When it was got, to whatever precision is remembered: a day, a month or a year.</summary>
    public LooseDate? AcquiredOn { get; set; }

    /// <summary>Where it came from: a shop, a friend, a swap event...</summary>
    public string? Source { get; set; }

    /// <summary>
    /// The seller it came from. <see cref="Source"/> keeps the seller's name as text beside it, so
    /// older versions and the CSV still show it. Null for no seller; one that was deleted or isn't
    /// known here counts as none and the Source text shows instead.
    /// </summary>
    public Guid? SellerId { get; set; }

    /// <summary>How it was when it arrived. Null when not said.</summary>
    [JsonConverter(typeof(TolerantNullableEnumConverter<ArrivalRating>))]
    public ArrivalRating? ArrivedIn { get; set; }

    /// <summary>What was wrong when it arrived.</summary>
    [JsonConverter(typeof(TolerantEnumListConverter<ArrivalProblem>))]
    public List<ArrivalProblem> ArrivalProblems { get; set; } = [];

    public string? ArrivalNote { get; set; }

    /// <summary>
    /// What was paid, as a number with no currency, so totals per year can be added up later.
    /// Null when not entered.
    /// </summary>
    public decimal? PricePaid { get; set; }

    public PlantStatus Status { get; set; } = PlantStatus.Active;

    /// <summary>
    /// How many are in the pot, e.g. five aloe pups kept as one plant. Null means one, which is what
    /// every older plant reads as. One is never stored.
    /// </summary>
    public int? Count { get; set; }

    [JsonIgnore]
    public int HowMany => Count ?? 1;

    /// <summary>
    /// The plant whose pot this one went into. Null when it has a pot of its own. The plant keeps
    /// Status Active, since older versions can't read a new status; <see cref="InCollection"/> says
    /// whether it counts as a plant of its own.
    /// </summary>
    public Guid? PottedWithPlantId { get; set; }

    /// <summary>The day it went into the other plant's pot.</summary>
    public DateOnly? PottedWithSince { get; set; }

    /// <summary>
    /// In the collection as a plant of its own, which is what lists, counts, Today and pots go by:
    /// not died or gone, and not potted in with another plant.
    /// </summary>
    [JsonIgnore]
    public bool InCollection => Status == PlantStatus.Active && PottedWithPlantId is null;

    /// <summary>
    /// The day the status last changed: when it died, was given away or sold, or came back into the
    /// collection. Null for a plant whose status hasn't changed since this field was added; for those,
    /// <see cref="Year.PlantYear"/> reads the day from the plant's history instead.
    /// </summary>
    public DateOnly? StatusSince { get; set; }

    /// <summary>An optional note on what went wrong, kept while the status is Died and cleared if it comes back.</summary>
    public string? CauseOfDeath { get; set; }

    /// <summary>Who got it, kept while the status is GivenAway or Sold and cleared if it comes back.</summary>
    public string? LeftTo { get; set; }

    /// <summary>What it was sold or swapped for, in the person's own words ("150 kr", "a Hoya carnosa"). Kept while the status is GivenAway or Sold.</summary>
    public string? LeftFor { get; set; }

    public GrowingMedium Medium { get; set; } = GrowingMedium.Soil;

    /// <summary>The pot the roots are in.</summary>
    public Guid? InnerPotId { get; set; }

    /// <summary>What it stands in, when there is one.</summary>
    public Guid? OuterPotId { get; set; }

    /// <summary>
    /// Water kept in the outer pot, wicking up through the medium. An ordinary ceramic run as a
    /// self-watering pot, which is a different thing from a pot built to water itself.
    /// </summary>
    public bool WaterInOuterPot { get; set; }

    /// <summary>
    /// Whether Today reminds you to water it, once the app has learned its pace from the care log.
    /// On unless turned off, and a plant saved before this existed reads as on.
    /// </summary>
    public bool WateringReminder { get; set; } = true;

    /// <summary>
    /// The moisture meter reading, 1 to 10, it is watered at. Null means <see cref="DefaultWaterAt"/>.
    /// Only used for a plant watered from the top.
    /// </summary>
    public int? WaterAt { get; set; }

    /// <summary>The top of the dry part of a 1 to 10 meter.</summary>
    public const int DefaultWaterAt = 3;

    /// <summary>
    /// The day its current way of getting water started, e.g. when it moved from soil to Leca.
    /// What was logged before it says nothing about the pace now, so the reminder skips it. Null
    /// means all of the history counts.
    /// </summary>
    public DateOnly? WateringSince { get; set; }

    /// <summary>
    /// Which pots went with it when it was given away or sold. The pot ids stay on the plant, so
    /// its history still says what it lived in; this says whether they left the house with it.
    /// </summary>
    public PotsTaken PotsTaken { get; set; } = PotsTaken.None;

    /// <summary>The soil mix it was potted in, when it is one you have saved.</summary>
    public Guid? SoilMixId { get; set; }

    /// <summary>
    /// The saved feed given with the water, so ticking Watered logs it too. Null means off, which
    /// is what every plant from before this existed reads as. A feed that is deleted counts as off.
    /// </summary>
    public Guid? WateringFeedId { get; set; }

    /// <summary>Feed on every 1st, 2nd or 3rd watering. Null reads as every watering.</summary>
    public int? WateringFeedEvery { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// The values of the person's own fields (<see cref="OwnField"/>), by field id, as the stored text.
    /// A top-level field so an older version saving the plant keeps it. The values of a removed field
    /// stay, so they come back if a field with that name is added again. A missing key means not
    /// set, and an empty string is never stored.
    /// </summary>
    public Dictionary<Guid, string> OwnFields { get; set; } = [];

    /// <summary>How it is watered, for the plant-sitter sheet. Null when not said, and older plants read as null.</summary>
    public WaterMethod? WaterBy { get; set; }

    /// <summary>A note for whoever looks after it while you are away. Not shared. Older plants read as null.</summary>
    public string? SitterNote { get; set; }

    /// <summary>Your own labels, e.g. "variegated", "rare" or "for swap". See <see cref="Plants.PlantTags"/>.</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// The day it went into quarantine, kept apart from the other plants. Null when it isn't in
    /// quarantine, so there is no flag that can disagree with the date.
    /// </summary>
    public DateOnly? QuarantinedSince { get; set; }

    [JsonIgnore]
    public bool InQuarantine => QuarantinedSince is not null;

    /// <summary>How many days the quarantine lasts. Null when it isn't in quarantine.</summary>
    public int? QuarantineDays { get; set; }

    /// <summary>The length given to a quarantine when it starts.</summary>
    public const int DefaultQuarantineDays = 14;

    /// <summary>The day the quarantine is up, or null when it isn't in quarantine.</summary>
    [JsonIgnore]
    public DateOnly? QuarantineEnds =>
        QuarantinedSince?.AddDays(QuarantineDays ?? DefaultQuarantineDays);

    /// <summary>
    /// The day it went dormant, e.g. an alocasia that dropped its leaves for winter. Null while
    /// it's growing, the same way as quarantine.
    /// </summary>
    public DateOnly? DormantSince { get; set; }

    [JsonIgnore]
    public bool IsDormant => DormantSince is not null;

    /// <summary>
    /// The day the move from soil or water to LECA or PON started. Null when it isn't being tracked,
    /// which is what every older plant reads as. The fields are left on the plant when it settles.
    /// </summary>
    public DateOnly? TransitionSince { get; set; }

    /// <summary>The medium it is moving into: Leca or Pon.</summary>
    public GrowingMedium? TransitionTo { get; set; }

    /// <summary>The day the soil was washed off the roots.</summary>
    public DateOnly? RootsWashedOn { get; set; }

    /// <summary>The day the first new water root showed.</summary>
    public DateOnly? FirstWaterRootOn { get; set; }

    /// <summary>The day the first new leaf showed.</summary>
    public DateOnly? FirstNewLeafOn { get; set; }

    /// <summary>Weeks without news before Today asks for a look. Null reads as <see cref="DefaultTransitionCheckWeeks"/>.</summary>
    public int? TransitionCheckWeeks { get; set; }

    public const int DefaultTransitionCheckWeeks = 6;

    /// <summary>It has both a water root and a new leaf, so the move is over.</summary>
    [JsonIgnore]
    public bool TransitionSettled =>
        TransitionSince is not null && FirstWaterRootOn is not null && FirstNewLeafOn is not null;

    /// <summary>Tracked and not settled yet.</summary>
    [JsonIgnore]
    public bool InTransition => TransitionSince is not null && !TransitionSettled;

    /// <summary>Days after settling that the card stays on the plant's page, so a wrong tick can be undone.</summary>
    public const int TransitionCardDaysAfterSettling = 7;

    /// <summary>The checklist card shows while the move is going on, and for a week after it settled.</summary>
    public bool ShowsTransitionCard(DateOnly today) =>
        InCollection && (InTransition
            || TransitionSettledOn is { } settled && settled.AddDays(TransitionCardDaysAfterSettling) >= today);

    /// <summary>The latest of the day it started and the steps ticked.</summary>
    [JsonIgnore]
    public DateOnly? TransitionLastNews
    {
        get
        {
            if (TransitionSince is not { } since)
                return null;
            var last = since;
            foreach (var day in new[] { RootsWashedOn, FirstWaterRootOn, FirstNewLeafOn })
                if (day is { } d && d > last)
                    last = d;
            return last;
        }
    }

    /// <summary>The day it settled in: the later of the first water root and the first new leaf.</summary>
    [JsonIgnore]
    public DateOnly? TransitionSettledOn =>
        TransitionSettled ? (FirstWaterRootOn > FirstNewLeafOn ? FirstWaterRootOn : FirstNewLeafOn) : null;

    /// <summary>
    /// What it is offered for, shown on the Available for swap page. Null when it isn't. Only
    /// counts while the plant is still here, and is left as it is when the plant leaves.
    /// </summary>
    public SpareMark? Spare { get; set; }

    /// <summary>Marked as something to swap, and still here to swap.</summary>
    [JsonIgnore]
    public bool IsOffered => Spare is not null && !IsDeleted && InCollection;

    /// <summary>Pinned to the top of the plant list.</summary>
    public bool Favourite { get; set; }

    /// <summary>Something it needs doing, which keeps it on Today until cleared. Null when nothing is.</summary>
    public Attention? Attention { get; set; }

    /// <summary>
    /// When the flag starts to show on Today, if it was set for later. Only counts while it was set
    /// for the current flag. At the top of the record, so an older version saving it keeps it.
    /// </summary>
    public AttentionLater? AttentionLater { get; set; }

    /// <summary>Given away or sold: gone to someone else, and possibly with its pots.</summary>
    [JsonIgnore]
    public bool HasLeft => Status is PlantStatus.GivenAway or PlantStatus.Sold;

    public Guid? CoverPhotoId { get; set; }

    /// <summary>The plant this one was propagated from, if known.</summary>
    public Guid? ParentPlantId { get; set; }

    /// <summary>The propagation this plant was promoted from, if any.</summary>
    public Guid? FromPropagationId { get; set; }

    /// <summary>"Monstera deliciosa 'Thai Constellation'", or null when no botanical name is set.</summary>
    [JsonIgnore]
    public string? BotanicalName => PlantNames.Botanical(Genus, Species, Cultivar, NameQualifier, NameNote);

    /// <summary>The name to show in lists: the nickname, else the botanical name.</summary>
    [JsonIgnore]
    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Nickname) ? Nickname.Trim() : BotanicalName ?? (NameQualifier == Models.NameQualifier.Unknown ? "Name unknown" : "Unnamed plant");

    /// <summary>
    /// Who a plant went to and what it went for, as one line: "Given to Anna · swapped for a Hoya carnosa"
    /// or "Sold to Anna for 150 kr". Only says what is filled in, and is null when nothing is.
    /// </summary>
    public string? DescribeDeparture()
    {
        var to = LeftTo?.Trim();
        var forWhat = LeftFor?.Trim();
        var hasTo = !string.IsNullOrEmpty(to);
        var hasFor = !string.IsNullOrEmpty(forWhat);
        if (!hasTo && !hasFor) return null;
        if (Status == PlantStatus.Sold)
            return "Sold" + (hasTo ? $" to {to}" : "") + (hasFor ? $" for {forWhat}" : "");
        if (Status == PlantStatus.GivenAway)
        {
            if (!hasTo) return $"Swapped for {forWhat}";
            return $"Given to {to}" + (hasFor ? $" · swapped for {forWhat}" : "");
        }
        return null;
    }

    /// <summary>
    /// Where a bought plant came from and what it cost, as one line: "Bought from Plantorama for 150"
    /// or "Paid 150". Null unless it was purchased and a price is set.
    /// </summary>
    public string? DescribePurchase()
    {
        if (PaidText is not { } amount)
            return null;
        return string.IsNullOrWhiteSpace(Source) ? $"Paid {amount}" : $"Bought from {Source.Trim()} for {amount}";
    }

    /// <summary>The price as written in a line, "150" or "79.5". Null unless it was purchased and a price is set.</summary>
    [JsonIgnore]
    public string? PaidText =>
        Origin == PlantOrigin.Purchased && PricePaid is { } price
            ? price.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
            : null;

    /// <summary>A copy to compare with after editing. The tags get their own list.</summary>
    public Plant Copy()
    {
        var copy = (Plant)MemberwiseClone();
        copy.Tags = [.. Tags];
        copy.ArrivalProblems = [.. ArrivalProblems];
        copy.OwnFields = new(OwnFields);
        return copy;
    }

    /// <summary>
    /// A new plant to add alongside this one, e.g. a second basil pot. It gets the names, room,
    /// light, origin, how it was raised, source, price paid, medium, soil mix, feed with the watering, how it is watered, tags and the values of its own fields. The nickname, the date it was got, pots, notes,
    /// photos and anything about how this plant is doing right now stay behind.
    /// </summary>
    public Plant Duplicate() => new()
    {
        Genus = Genus,
        Species = Species,
        Cultivar = Cultivar,
        NameQualifier = NameQualifier,
        NameNote = NameNote,
        PlaceId = PlaceId,
        Light = Light,
        LikesLight = LikesLight,
        LikesWater = LikesWater,
        LikesHumidity = LikesHumidity,
        GrowingSeason = GrowingSeason,
        RestHow = RestHow,
        Origin = Origin,
        RaisedAs = RaisedAs,
        Source = Source,
        SellerId = SellerId,
        PricePaid = PricePaid,
        Medium = Medium,
        SoilMixId = SoilMixId,
        WateringFeedId = WateringFeedId,
        WateringFeedEvery = WateringFeedEvery,
        WaterBy = WaterBy,
        Tags = [.. Tags],
        OwnFields = new(OwnFields),
        ParentPlantId = ParentPlantId
    };

    /// <summary>
    /// True while the plant takes up this pot. Only a plant in the collection does: a pot whose
    /// plant died or left is either free again or went with it.
    /// </summary>
    public bool Uses(Guid potId) =>
        InCollection && (InnerPotId == potId || OuterPotId == potId);

    /// <summary>True when this pot left the house with the plant.</summary>
    public bool TookAway(Guid potId) => HasLeft && PotsTaken switch
    {
        PotsTaken.Inner => InnerPotId == potId,
        PotsTaken.All => InnerPotId == potId || OuterPotId == potId,
        _ => false
    };

    /// <summary>
    /// The choice as far as this plant's pots allow, so several plants given away together can
    /// share one answer: a plant without an outer pot can't take one, and one without pots takes none.
    /// </summary>
    public PotsTaken Fit(PotsTaken choice) => choice switch
    {
        PotsTaken.All when OuterPotId is not null => PotsTaken.All,
        PotsTaken.All or PotsTaken.Inner when InnerPotId is not null => PotsTaken.Inner,
        _ => PotsTaken.None
    };

    /// <summary>Days in quarantine, counting the day it went in as day 0.</summary>
    public int? DaysInQuarantine(DateOnly today) =>
        QuarantinedSince is { } since ? Math.Max(0, today.DayNumber - since.DayNumber) : null;

    /// <summary>Days dormant, counting the day it went dormant as day 0.</summary>
    public int? DaysDormant(DateOnly today) =>
        DormantSince is { } since ? Math.Max(0, today.DayNumber - since.DayNumber) : null;

    /// <summary>Validation rules shared by every place a plant can be saved from.</summary>
    public IReadOnlyList<string> Validate(DateOnly today)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Nickname) && string.IsNullOrWhiteSpace(Genus) && NameQualifier != Models.NameQualifier.Unknown)
            errors.Add("Give the plant a nickname or a genus.");
        errors.AddRange(PlantNames.QualifierErrors(Genus, Species, NameQualifier));
        if (ParentPlantId is not null && ParentPlantId == Id)
            errors.Add("A plant can't be its own parent.");
        if (Plants.CollectionNumbers.Normalize(CollectionNumber) is { } code && !Plants.CollectionNumbers.IsValid(code))
            errors.Add(Plants.CollectionNumbers.NotValidText);
        if (PricePaid < 0)
            errors.Add("The price paid can't be negative.");
        if (AcquiredOn is { } acquired && acquired.Start > today)
            errors.Add("The date you got it can't be in the future.");
        if (InnerPotId is not null && InnerPotId == OuterPotId)
            errors.Add("A plant can't have the same pot inside and outside.");
        if (QuarantinedSince > today)
            errors.Add("The quarantine can't start in the future.");
        if (QuarantineDays < 1)
            errors.Add("A quarantine has to last at least a day.");
        if (DormantSince > today)
            errors.Add("It can't go dormant in the future.");
        if (TransitionSince > today)
            errors.Add("The move to semi-hydro can't start in the future.");
        if (RootsWashedOn > today || FirstWaterRootOn > today || FirstNewLeafOn > today)
            errors.Add("A step can't be in the future.");
        if (TransitionSince is { } started && (RootsWashedOn < started || FirstWaterRootOn < started || FirstNewLeafOn < started))
            errors.Add("A step can't come before the move started.");
        if (TransitionCheckWeeks is < 1 or > 52)
            errors.Add("Check after 1 to 52 weeks.");
        if (WaterAt is < 1 or > 10)
            errors.Add("The watering level goes from 1 to 10.");
        if (Count < 1)
            errors.Add("There's at least one plant in a pot.");
        if (PottedWithPlantId is not null && PottedWithPlantId == Id)
            errors.Add("A plant can't be potted with itself.");
        if (PotsTaken != PotsTaken.None && !HasLeft)
            errors.Add("Only a plant that was given away or sold can take its pots with it.");
        return errors;
    }
}
