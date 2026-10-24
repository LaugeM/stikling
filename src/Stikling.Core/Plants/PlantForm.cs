using Stikling.Core.Fields;
using Stikling.Core.Models;
using Stikling.Core.Names;
using Stikling.Core.Pests;
using Stikling.Core.Pots;
using Stikling.Core.Rooms;
using Stikling.Core.Sellers;
using Stikling.Core.Wishlist;

namespace Stikling.Core.Plants;

/// <summary>What saving the add or edit form came to.</summary>
/// <param name="Errors">What is wrong with the form. Empty means it was saved, and nothing was written otherwise.</param>
/// <param name="Created">This save created the plant, so the photos taken for it belong to it now.</param>
/// <param name="LikesOffer">Other plants of the same species that lack the needs just set.</param>
/// <param name="LikesFields">Which needs the offer is about.</param>
/// <param name="WishError">Moving the wish to Got failed after the plant was saved. Logged, not a failed save.</param>
public sealed record PlantFormResult(
    IReadOnlyList<string> Errors,
    bool Created,
    IReadOnlyList<Plant> LikesOffer,
    IReadOnlyList<LikesField> LikesFields,
    Exception? WishError)
{
    /// <summary>The form was wrong, so nothing was written.</summary>
    public PlantFormResult(IReadOnlyList<string> errors) : this(errors, false, [], [], null)
    {
    }
}

/// <summary>
/// The rules of filling in and saving a plant on the add and edit form, kept apart from the page so
/// they can be tested. The page binds to <see cref="Plant"/> and calls the methods here. A form that
/// failed part of the way can be saved again: what already ran isn't done twice.
/// </summary>
public sealed class PlantForm
{
    private readonly PlantForms deps;
    private readonly Func<Enum, string> label;
    private readonly Dictionary<Guid, string?> fieldInputs = [];
    private readonly List<Pot> pots;
    private List<OwnField> ownFields;
    private Plant? startedWith;
    private readonly PlantDictionary? dictionary;
    private string? sellerTextStart;
    private bool quarantineTouched;
    private bool mediumPicked;

    // What a save has done, so saving again after a failure carries on instead of starting over
    private bool written;
    private bool cameInsideAtSave;
    private bool startMoveAtSave;
    private Plant? likesBefore;
    private bool transitionStarted;
    private bool checkSaved;

    internal PlantForm(
        PlantForms deps,
        PlantFormData data,
        Func<Enum, string> label,
        Plant plant,
        Plant? original,
        bool fromWish,
        LooseDate? copiedDate,
        string? copiedFrom,
        Wish? wish)
    {
        this.deps = deps;
        this.label = label;
        Plant = plant;
        Original = original;
        IsNew = original is null;
        CopiedDate = copiedDate;
        CopiedFrom = copiedFrom;
        Wish = wish;
        Plants = data.Plants;
        Propagations = data.Propagations;
        Places = data.Places;
        pots = [.. data.Pots];
        Mixes = data.Mixes;
        Feeds = data.Feeds;
        SellerList = data.Sellers;
        Settings = data.Settings;
        ownFields = [.. data.OwnFields];
        wishes = data.Wishes;
        dictionary = data.Dictionary;

        Location = data.Places.NameOf(plant.PlaceId);

        // A feed deleted since it was picked reads as off, so saving the plant clears it the same way
        if (plant.WateringFeedId is { } feedId && !Feeds.Any(f => f.Id == feedId))
            SetWateringFeed(null);

        SellerText = data.Sellers.For(plant.SellerId, plant.Source)?.Name ?? plant.Source;
        // A wish's "found at" is made into a seller on save, like anything typed
        sellerTextStart = fromWish ? null : SellerText;

        foreach (var field in ownFields)
            fieldInputs[field.Id] = plant.OwnFields.GetValueOrDefault(field.Id);
    }

    private readonly IReadOnlyList<Wish> wishes;

    // A new plant as the form started, e.g. a duplicate, to tell what was set here
    internal void RememberStart() => startedWith = Plant.Copy();

    private DateOnly Today => deps.Time.Today();

    /// <summary>The draft the page binds to.</summary>
    public Plant Plant { get; }

    /// <summary>The plant as it was stored, to record what changed. Null while it is new.</summary>
    public Plant? Original { get; private set; }

    /// <summary>True until the plant has been created. A form that failed after creating it saves as an edit.</summary>
    public bool IsNew { get; private set; }

    /// <summary>When the plant this one is copied from was got, to offer the same date.</summary>
    public LooseDate? CopiedDate { get; }

    public string? CopiedFrom { get; }

    /// <summary>The wish the form was started from, if any.</summary>
    public Wish? Wish { get; }

    public bool MarkWishGot { get; set; } = true;

    /// <summary>The wish this plant is for: the one it was started from, else an open wish with the same names.</summary>
    public Wish? WishHere =>
        !IsNew ? null
        : Wish is { IsGot: false } chosen ? chosen
        : WishMatches.OpenFor(Plant.Genus, Plant.Species, Plant.Cultivar, Plant.Nickname, wishes, dictionary).FirstOrDefault();

    /// <summary>What the Where from box says. A seller is only looked up again when it was changed.</summary>
    public string? SellerText { get; set; }

    /// <summary>As picked, "Living room / Windowsill". A room is made from it when saving.</summary>
    public string? Location { get; set; }

    public IReadOnlyList<Plant> Plants { get; }

    public IReadOnlyList<Propagation> Propagations { get; }

    public Places Places { get; }

    public IReadOnlyList<Pot> Pots => pots;

    public IReadOnlyList<SoilMix> Mixes { get; }

    /// <summary>The feeds by name.</summary>
    public IReadOnlyList<Feed> Feeds { get; }

    public SellerList SellerList { get; }

    public IReadOnlyList<OwnField> OwnFields => ownFields;

    public UserSettings Settings { get; }

    public void AddPot(Pot pot) => pots.Add(pot);

    /// <summary>Takes a field just added into the form with an empty input, leaving the other inputs alone.</summary>
    public async Task AddOwnFieldAsync(OwnField field)
    {
        ownFields = [.. await deps.OwnFieldBook.GetAllAsync()];
        fieldInputs.TryAdd(field.Id, null);
    }

    public string? FieldInput(Guid fieldId) => fieldInputs.GetValueOrDefault(fieldId);

    public void SetFieldInput(Guid fieldId, string? value) => fieldInputs[fieldId] = value;

    public string? PotName(Guid id) => pots.FirstOrDefault(p => p.Id == id)?.DisplayName;

    public string? MixName(Guid id) => Mixes.FirstOrDefault(m => m.Id == id)?.Name;

    public string? FeedName(Guid id) => Feeds.FirstOrDefault(f => f.Id == id)?.Name;

    public Pot? FindPot(Guid id) => pots.FirstOrDefault(p => p.Id == id);

    /// <summary>The feed given with the water, or null when it is off or the feed is gone.</summary>
    public Guid? FeedChoice => Plant.WateringFeedId is { } id && Feeds.Any(f => f.Id == id) ? id : null;

    public void SetWateringFeed(Guid? id)
    {
        if (id is { } feed)
        {
            Plant.WateringFeedId = feed;
            Plant.WateringFeedEvery ??= 1;
        }
        else
        {
            Plant.WateringFeedId = null;
            Plant.WateringFeedEvery = null;
        }
    }

    /// <summary>The setting for how long a plant is kept apart.</summary>
    public int QuarantineLength => Settings.QuarantineDays ?? Plant.DefaultQuarantineDays;

    /// <summary>
    /// A new plant starts in quarantine if it was bought, swapped or gifted, until the person has
    /// ticked or unticked the box themselves. The page calls this when the origin changes.
    /// </summary>
    public void OriginChanged()
    {
        if (!IsNew || !Settings.QuarantineNewPlants || quarantineTouched)
            return;

        var on = PlantService.StartsInQuarantine(Plant.Origin, true);
        Plant.QuarantinedSince = on ? Today : null;
        Plant.QuarantineDays = on ? QuarantineLength : null;
    }

    /// <summary>Ticking it again after unticking keeps the date it had, so a slip doesn't restart the count.</summary>
    public void SetQuarantine(bool on)
    {
        quarantineTouched = true;
        Plant.QuarantinedSince = on ? Original?.QuarantinedSince ?? Today : null;
        Plant.QuarantineDays = on ? Original?.QuarantineDays ?? QuarantineLength : null;
    }

    /// <summary>Back from outside: a saved plant's place was changed from an outdoor one to an indoor one.</summary>
    public bool CameInside
    {
        get
        {
            if (IsNew || Original is null || Original.InQuarantine || string.IsNullOrWhiteSpace(Location))
                return false;
            if (!Places.IsOutdoors(Original.PlaceId))
                return false;

            // A place that doesn't exist yet is indoors, unless it's a new spot in an outdoor room
            if (Places.Named(Location) is { } place)
                return !Places.IsOutdoors(place.Id);
            return Places.RoomNamed(RoomName.Split(Location).Item1)?.Outdoors != true;
        }
    }

    public bool BackQuarantine { get; set; }

    public bool BackChecked { get; set; }

    /// <summary>Soil or water into LECA or PON on a saved plant that isn't already being tracked.</summary>
    public bool OffersTransition =>
        !IsNew && Original is not null && Plant.InCollection && !Plant.InTransition
        && PlantService.OffersTransition(Original.Medium, Plant.Medium);

    public bool TrackTransition { get; set; } = true;

    // A pot that suggests a medium sets it on a new plant, until the person has picked a medium
    // themselves. A saved plant's medium was already chosen, so a pot never changes it
    private bool MediumChosen => !IsNew || mediumPicked;

    public void SetMedium(GrowingMedium medium)
    {
        var offered = OffersTransition;
        Plant.Medium = medium;
        mediumPicked = true;
        if (!offered)
            TrackTransition = true;
    }

    public void SetInnerPot(Guid? id)
    {
        var offered = OffersTransition;
        Plant.InnerPotId = id;
        Plant.Medium = PotKinds.MediumAfterPicking(pots.FirstOrDefault(p => p.Id == id), Plant.Medium, MediumChosen);
        if (!offered)
            TrackTransition = true;
    }

    /// <summary>Clearing the outer pot clears what would be kept in it.</summary>
    public void SetOuterPot(Guid? id)
    {
        Plant.OuterPotId = id;
        if (id is null)
            Plant.WaterInOuterPot = false;
    }

    /// <summary>
    /// Checks the form and saves it. Nothing is written when something is wrong. Exceptions from the
    /// repositories are not caught. After one, saving again carries on from where it stopped.
    /// </summary>
    public async Task<PlantFormResult> SaveAsync(IReadOnlyList<Photo> photos)
    {
        var errors = Plant.Validate(Today).ToList();
        var fieldValues = new List<(Guid Id, string? Value)>();
        foreach (var field in ownFields)
        {
            // A stored value left as it was is kept, even one that no longer fits after the field's
            // kind changed, so it never stops the plant from being saved
            if (Plant.OwnFields.TryGetValue(field.Id, out var before) && FieldInput(field.Id) == before)
                continue;
            var stored = OwnFieldValues.Normalize(field.Kind, FieldInput(field.Id), out var fieldError);
            if (fieldError is not null)
                errors.Add($"{field.Name}: {fieldError}");
            fieldValues.Add((field.Id, stored));
        }
        if (errors.Count > 0)
            return new PlantFormResult(errors);

        // Only the fields that are there are set. Values of a removed field are left on the plant.
        foreach (var (fieldId, value) in fieldValues)
        {
            if (value is null)
                Plant.OwnFields.Remove(fieldId);
            else
                Plant.OwnFields[fieldId] = value;
        }

        // Saving also links a plant that only has its seller written in Source
        if (SellerText != sellerTextStart || (Plant.SellerId is null && SellerList.For(null, Plant.Source) is not null))
        {
            var choice = await deps.SellerService.ChooseAsync(SellerText, SellerName.Guess(Plant.Origin), Plant.SellerId);
            Plant.SellerId = choice.SellerId;
            Plant.Source = choice.Source;
            sellerTextStart = SellerText;
        }

        Plant.PlaceId = await deps.RoomService.PlaceIdAsync(Location);

        // What the change is worked out from before the write, and kept once it's written
        if (!written)
        {
            cameInsideAtSave = CameInside;
            startMoveAtSave = OffersTransition && TrackTransition;
            // A new plant counts from what the form started with, like a duplicate's values
            likesBefore = Original ?? startedWith ?? new Plant();
        }
        if (cameInsideAtSave && BackQuarantine)
        {
            // Set before the save, so it's one write and the history says it went into quarantine
            Plant.QuarantinedSince = Today;
            Plant.QuarantineDays = QuarantineLength;
        }

        var created = false;
        Exception? wishError = null;
        if (IsNew)
        {
            var wish = MarkWishGot ? WishHere : null;
            await deps.PlantService.CreateAsync(Plant, photos);
            created = true;
            written = true;
            IsNew = false;
            Original = Plant.Copy();
            if (wish is not null)
            {
                // The plant is saved by now, so a wish that can't be moved is reported apart from the save
                try
                {
                    await deps.WishService.MarkGotAsync(wish.Id, Plant.Id, null);
                }
                catch (Exception ex)
                {
                    wishError = ex;
                }
            }
        }
        else
        {
            var places = await deps.RoomService.GetPlacesAsync();
            await deps.PlantService.UpdateAsync(Original!, Plant, label, PotName, MixName, id => places.NameOf(id), FindPot, FeedName);
            written = true;
            Original = Plant.Copy();
            if (startMoveAtSave && !transitionStarted)
            {
                await deps.PlantService.StartTransitionAsync(Plant, Today, Plant.Medium, null, label);
                transitionStarted = true;
                Original = Plant.Copy();
            }
        }

        if (cameInsideAtSave && BackChecked && !checkSaved)
        {
            await deps.PestTreatments.SaveAsync(PestService.StartPlantCheck(Plant, Today));
            checkSaved = true;
        }

        // Others of the same species that don't have what was just set are asked before leaving
        var changed = CareLikes.Changed(likesBefore!, Plant);
        var others = changed.Count == 0 ? [] : CareLikes.WouldChange(Plant, changed, CareLikes.SameSpecies(Plant, Plants));
        return others.Count > 0
            ? new PlantFormResult([], created, others, changed, wishError)
            : new PlantFormResult([], created, [], [], wishError);
    }
}
