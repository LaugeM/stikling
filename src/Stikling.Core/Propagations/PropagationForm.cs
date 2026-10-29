using Stikling.Core.Text;
using Strings = Stikling.Core.Resources;
using Stikling.Core.Models;
using Stikling.Core.Names;
using Stikling.Core.Plants;
using Stikling.Core.Pots;
using Stikling.Core.Rooms;
using Stikling.Core.Sellers;
using Stikling.Core.Wishlist;

namespace Stikling.Core.Propagations;

/// <summary>What saving the start or edit form came to.</summary>
/// <param name="Errors">What is wrong with the form. Empty means it was saved, and nothing was written otherwise.</param>
/// <param name="Created">This save created the propagation, so the photos taken for it belong to it now.</param>
/// <param name="LaterError">A step after the propagation was saved failed, like moving the wish to Got or a line in its history. Logged, not a failed save.</param>
public sealed record PropagationFormResult(
    IReadOnlyList<string> Errors,
    bool Created,
    Exception? LaterError)
{
    /// <summary>The form was wrong, so nothing was written.</summary>
    public PropagationFormResult(IReadOnlyList<string> errors) : this(errors, false, null)
    {
    }
}

/// <summary>
/// The rules of filling in and saving a propagation on the start and edit form, kept apart from the
/// page so they can be tested. The page binds to <see cref="Propagation"/> and calls the methods here.
/// A form that failed part of the way can be saved again: what already ran isn't done twice.
/// </summary>
public sealed class PropagationForm
{
    /// <summary>Shown when the cut end is ticked as dried but the time isn't a number.</summary>
    public static string DryMessage => Strings.Propagations.DryMessage;

    private readonly PropagationForms deps;
    private readonly Func<Propagation, Func<Enum, string>> label;
    private readonly IReadOnlyList<Wish> wishes;
    private readonly PlantDictionary? dictionary;
    private string? sellerTextStart;
    private string? containerTextStart;

    // The number filled in from the parent, so a change of parent can replace it
    private string? numberSuggested;

    // The drying is kept as hours on the propagation; these hold what is typed
    private bool dryFirst;
    private int? dryAmount = 1;
    private bool dryInDays;

    internal PropagationForm(
        PropagationForms deps,
        PropagationFormData data,
        Func<Propagation, Func<Enum, string>> label,
        Propagation propagation,
        Propagation? original,
        Wish? wish)
    {
        this.deps = deps;
        this.label = label;
        Propagation = propagation;
        Original = original;
        IsNew = original is null;
        Stored = !IsNew;
        Wish = wish;
        plantList = [.. data.Plants];
        Propagations = data.Propagations;
        Places = data.Places;
        Mixes = data.Mixes;
        PotUses = data.PotUses;
        ContainerList = data.ContainerList;
        SellerList = data.Sellers;
        wishes = data.Wishes;
        dictionary = data.Dictionary;

        Location = data.Places.NameOf(propagation.PlaceId);

        SellerText = data.Sellers.For(propagation.SellerId, propagation.Source)?.Name ?? propagation.Source;
        // A Source that came with the draft, like a wish's "found at", is made into a seller on save
        sellerTextStart = IsNew && propagation.SellerId is null ? null : SellerText;

        ContainerText = data.ContainerList.For(propagation.ContainerPotId, propagation.Container)?.Name ?? propagation.Container;
        // A container that came with the draft, like Like last time without an id, is looked up on save
        containerTextStart = IsNew && propagation.ContainerPotId is null ? null : ContainerText;

        LoadDrying();
    }

    private DateOnly Today => deps.Time.Today();

    /// <summary>The draft the page binds to.</summary>
    public Propagation Propagation { get; }

    /// <summary>The propagation as it was stored, to record what changed. Null while it is new.</summary>
    public Propagation? Original { get; private set; }

    /// <summary>True until the propagation has been created. A form that failed after creating it saves as an edit.</summary>
    public bool IsNew { get; private set; }

    /// <summary>
    /// The propagation is stored, even when the create that stored it failed partway and <see cref="IsNew"/>
    /// is still true. The photos taken for it belong to it then, so leaving the page keeps them.
    /// </summary>
    public bool Stored { get; private set; }

    /// <summary>The wish the form was started from, if it is still open.</summary>
    public Wish? Wish { get; }

    public bool MarkWishGot { get; set; } = true;

    /// <summary>The wish this cutting is for: the one it was started from, else an open wish with the same names.</summary>
    public Wish? WishHere =>
        !IsNew ? null
        : Wish is { IsGot: false } chosen ? chosen
        : WishMatches.OpenFor(Propagation.Genus, Propagation.Species, Propagation.Cultivar, Propagation.Nickname, wishes, dictionary).FirstOrDefault();

    /// <summary>What the Where from box says. The seller is only looked up again when it was changed.</summary>
    public string? SellerText { get; set; }

    /// <summary>What the Container box says. Looked up again only when it was changed.</summary>
    public string? ContainerText { get; set; }

    /// <summary>As picked, "Kitchen / Window". A room is made from it when saving.</summary>
    public string? Location { get; set; }

    /// <summary>Sorted by name.</summary>
    public IReadOnlyList<Plant> Plants => plantList;

    private readonly List<Plant> plantList;

    /// <summary>
    /// Makes a plant saved while the form is open, like a parent that lives with someone else,
    /// known to the form so the parent picker can list it.
    /// </summary>
    public void AddKnownPlant(Plant plant)
    {
        if (plantList.All(p => p.Id != plant.Id))
            plantList.Add(plant);
    }

    public IReadOnlyList<Propagation> Propagations { get; }

    public Places Places { get; }

    public IReadOnlyList<SoilMix> Mixes { get; }

    public IReadOnlyList<PotUse> PotUses { get; }

    public ContainerList ContainerList { get; }

    public SellerList SellerList { get; }

    // Plants potted up from it that still exist. The potted up count can't be corrected below them
    public int PlantsFromIt => Plants.Count(p => p.FromPropagationId == Propagation.Id);

    // Units already potted up, given away or failed can't be counted away
    public int MinCount => Math.Max(1, Propagation.PottedUpCount + Propagation.FailedCount + Propagation.GivenAwayCount);

    // A new propagation from a plant with a number gets the next letter after it, until the person
    // has typed a number of their own
    internal void SuggestNumber()
    {
        if (!IsNew)
            return;
        var untouched = string.IsNullOrWhiteSpace(Propagation.CollectionNumber) || Propagation.CollectionNumber == numberSuggested;
        if (!untouched)
            return;
        var parent = Propagation.ParentPlantId is { } id ? Plants.FirstOrDefault(p => p.Id == id) : null;
        numberSuggested = parent is null ? null : CollectionNumbers.NextLetter(parent, Propagations, Plants);
        Propagation.CollectionNumber = numberSuggested;
    }

    /// <summary>Picking a parent suggests its next number and fills in empty name fields and place from it.</summary>
    public void ParentChanged()
    {
        SuggestNumber();
        if (Propagation.ParentPlantId is not { } parentId || Plants.FirstOrDefault(p => p.Id == parentId) is not { } parent)
            return;
        if (string.IsNullOrWhiteSpace(Propagation.Nickname) && string.IsNullOrWhiteSpace(Propagation.Genus))
        {
            var filled = deps.PropagationService.StartFrom(parent);
            Propagation.Nickname = filled.Nickname;
            Propagation.Genus = filled.Genus;
            Propagation.Species = filled.Species;
            Propagation.Cultivar = filled.Cultivar;
            Propagation.NameQualifier = filled.NameQualifier;
            Propagation.NameNote = filled.NameNote;
        }
        if (string.IsNullOrWhiteSpace(Location))
            Location = Places.NameOf(parent.PlaceId);
    }

    private void LoadDrying()
    {
        if (Propagation.DriedForHours is { } hours)
        {
            (var amount, dryInDays) = Drying.Split(hours);
            dryAmount = amount;
            dryFirst = true;
        }
        else
        {
            dryFirst = false;
            dryAmount = 1;
            dryInDays = false;
        }
    }

    private void SaveDrying() =>
        Propagation.DriedForHours = dryFirst ? Drying.ToHours(dryAmount, dryInDays) : null;

    /// <summary>Ticked when the cut end dried first. Setting it writes the hours on the propagation.</summary>
    public bool DryFirst
    {
        get => dryFirst;
        set
        {
            dryFirst = value;
            SaveDrying();
        }
    }

    public int? DryAmount
    {
        get => dryAmount;
        set
        {
            dryAmount = value;
            SaveDrying();
        }
    }

    public bool DryInDays
    {
        get => dryInDays;
        set
        {
            dryInDays = value;
            SaveDrying();
        }
    }

    public bool DryInvalid => dryFirst && dryAmount is not (>= 1 and <= Drying.MaxAmount);

    // The newest earlier propagation from the same parent, else of the same genus, offered once there is something to match on
    public Propagation? LastTimeMatch =>
        IsNew
        && LastTime.Find(Propagations, Propagation.ParentPlantId, Propagation.Genus) is { } found
        && !LastTime.SameSetup(found, Propagation)
            ? found
            : null;

    public void UseLastTime(Propagation from)
    {
        LastTime.Apply(from, Propagation);
        ContainerText = ContainerList.For(from.ContainerPotId, from.Container)?.Name ?? from.Container;
        LoadDrying();
    }

    /// <summary>
    /// Checks the form and saves it. Nothing is written when something is wrong. Exceptions from the
    /// repositories are not caught. After one, saving again carries on from where it stopped.
    /// </summary>
    public async Task<PropagationFormResult> SaveAsync(IReadOnlyList<Photo> photos)
    {
        if (Propagation.Type != PropagationType.Seed)
        {
            Propagation.SeedsGerminated = null;
            Propagation.FirstGerminatedOn = null;
        }
        if (Propagation.Type != PropagationType.Corm)
            Propagation.CormSizeMm = null;

        Propagation.ClearMixUnlessSoil();
        Propagation.CollectionNumber = CollectionNumbers.Normalize(Propagation.CollectionNumber);

        var errors = Propagation.Validate(Today).ToList();
        if (DryInvalid)
            errors.Add(DryMessage);
        if (!IsNew && Propagation.PottedUpCount < PlantsFromIt)
            errors.Add(Plural.Of(PlantsFromIt, Strings.Propagations.PottedUpBelowPlants_One, Strings.Propagations.PottedUpBelowPlants_Other));
        if (errors.Count > 0)
            return new PropagationFormResult(errors);

        if (Propagation.ParentPlantId is not null)
        {
            Propagation.Source = null;
            Propagation.SellerId = null;
        }
        else if (SellerText != sellerTextStart || (Propagation.SellerId is null && SellerList.For(null, Propagation.Source) is not null))
        {
            var choice = await deps.SellerService.ChooseAsync(SellerText, SellerKind.Other, Propagation.SellerId);
            Propagation.SellerId = choice.SellerId;
            Propagation.Source = choice.Source;
            sellerTextStart = SellerText;
        }

        if (ContainerText != containerTextStart || (Propagation.ContainerPotId is null && ContainerList.For(null, Propagation.Container) is not null))
        {
            var choice = await deps.PotService.ChooseContainerAsync(ContainerText, Propagation.ContainerPotId);
            Propagation.ContainerPotId = choice.PotId;
            Propagation.Container = choice.Name;
            containerTextStart = ContainerText;
        }

        Propagation.PlaceId = await deps.RoomService.PlaceIdAsync(Location);

        var created = false;
        Exception? laterError = null;
        if (IsNew)
        {
            var wish = MarkWishGot ? WishHere : null;
            try
            {
                await deps.PropagationService.CreateAsync(Propagation, label(Propagation), photos);
            }
            catch
            {
                Stored = await deps.IsStoredAsync(Propagation.Id);
                throw;
            }
            created = true;
            IsNew = false;
            Stored = true;
            Original = Propagation.Copy();
            if (wish is not null)
            {
                // The cutting is saved by now, so a wish that can't be moved is reported apart from the save
                try
                {
                    await deps.WishService.MarkGotAsync(wish.Id, null, Propagation.Id);
                }
                catch (Exception ex)
                {
                    laterError = ex;
                }
            }
        }
        else
        {
            var places = await deps.RoomService.GetPlacesAsync();
            try
            {
                await deps.PropagationService.UpdateAsync(Original!, Propagation, label(Propagation), id => places.NameOf(id));
            }
            catch (PartlySavedException ex)
            {
                // The propagation is saved, so saving again mustn't record the change a second time
                laterError = ex.InnerException ?? ex;
            }
            Original = Propagation.Copy();
        }

        return new PropagationFormResult([], created, laterError);
    }
}
