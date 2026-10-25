using Stikling.Core.Models;
using Stikling.Core.Pots;
using Stikling.Core.Rooms;

namespace Stikling.Core.Propagations;

/// <summary>How the units being potted up are put in pots.</summary>
public enum PotUpKind { Each, OnePot, Into }

/// <summary>
/// The rules of the Pot up sheet, kept apart from the page so they can be tested. The page binds to the
/// properties and calls <see cref="SaveAsync"/>. A save that failed before anything was counted can be
/// run again: the plants it made are written over, not made twice.
/// </summary>
public sealed class PotUpForm
{
    private readonly UnitForms deps;
    private readonly Func<Enum, string> label;
    private int count = 1;
    private PotUpKind mode;
    private bool mediumChosen;
    private List<Guid>? plantIds;
    // The mode the plant ids were made for
    private PotUpKind triedMode;

    internal PotUpForm(UnitForms deps, Propagation propagation, Places places, Func<Enum, string> label)
    {
        this.deps = deps;
        this.label = label;
        Propagation = propagation;

        // A propagation named only by nickname passes it on, since a plant needs a name
        Nickname = string.IsNullOrWhiteSpace(propagation.Genus) ? propagation.Nickname : null;
        Date = deps.Time.Today();
        Location = places.NameOf(propagation.PlaceId);
        // Soil is the usual place for a potted up plant. Only a default, it's a select.
        Medium = propagation.InPlantMedium ? propagation.Medium : GrowingMedium.Soil;
        SoilMixId = propagation.Medium == GrowingMedium.Soil ? propagation.SoilMixId : null;
        // Already in a plant medium, it stays there whatever pot is picked, unless the person says otherwise
        mediumChosen = propagation.InPlantMedium;
    }

    /// <summary>
    /// The propagation it records on. The page sets the one it reloaded after a sync, so a save never
    /// writes the copy the sheet was opened with over a newer one.
    /// </summary>
    public Propagation Propagation { get; set; }

    /// <summary>
    /// How many to pot up. Once a save has failed after saving some of the plants it keeps its value,
    /// since those plants depend on it.
    /// </summary>
    public int Count
    {
        get => count;
        set
        {
            if (!Retrying)
                count = value;
        }
    }

    public string? Nickname { get; set; }

    public DateOnly Date { get; set; }

    /// <summary>As picked, "Kitchen / Window". A room is made from it when saving.</summary>
    public string? Location { get; set; }

    public Guid? SoilMixId { get; set; }

    /// <summary>
    /// Locked like <see cref="Count"/> once a save has failed with nothing counted.
    /// </summary>
    public PotUpKind Mode
    {
        get => mode;
        set
        {
            if (!Retrying)
                mode = value;
        }
    }

    public Guid? IntoPlantId { get; set; }

    /// <summary>Shows Grows in and the mix for a propagation already in a plant medium, which hides them.</summary>
    public bool ChangeMedium { get; set; }

    public GrowingMedium Medium { get; private set; }

    public Guid? PotId { get; private set; }

    /// <summary>
    /// True after a save that failed with nothing counted but some of its plants stored. Saving again
    /// carries on over the same plants. A save that failed before storing any plant leaves the form free.
    /// </summary>
    public bool Retrying { get; private set; }

    /// <summary>
    /// The ids of the plants a save makes, empty until the first save. When only a later step failed the
    /// plants exist, so the page can find them by these.
    /// </summary>
    public IReadOnlyList<Guid> PlantIds => plantIds ?? [];

    /// <summary>How many plants a save makes.</summary>
    public int PlantCount =>
        EffectiveMode == PotUpKind.OnePot || (EffectiveMode == PotUpKind.Into && IntoPlantId is not null) ? 1 : Count;

    /// <summary>
    /// One left can't be split into pots, so it is only a pot of its own or in with a plant. While
    /// <see cref="Retrying"/> it stays the mode the stored plants were made for.
    /// </summary>
    public PotUpKind EffectiveMode =>
        Retrying ? triedMode
        : Mode == PotUpKind.OnePot && Propagation.RemainingCount < 2 ? PotUpKind.Each : Mode;

    public bool CountIsValid => Count >= 1 && Count <= Propagation.RemainingCount;

    public bool CanSave => CountIsValid && !(EffectiveMode == PotUpKind.Into && IntoPlantId is null);

    /// <summary>The person picked a medium, so a pot no longer suggests one.</summary>
    public void PickMedium(GrowingMedium medium)
    {
        Medium = medium;
        mediumChosen = true;
    }

    /// <summary>A pot that suggests a medium sets it, until the person has picked a medium in this form.</summary>
    /// <param name="pot">The pot with this id, if the page has it.</param>
    public void PickPot(Guid? id, Pot? pot)
    {
        PotId = id;
        Medium = PotKinds.MediumAfterPicking(pot, Medium, mediumChosen);
    }

    /// <summary>
    /// Pots the units up. An exception means nothing was counted: <see cref="Retrying"/> is set and saving
    /// again carries on. When the count was stored and a later step failed, that comes back as
    /// <see cref="UnitResult.LaterError"/> and the form is done.
    /// </summary>
    public async Task<UnitResult> SaveAsync()
    {
        // New ids unless a failed save stored some of its plants, so a retry writes over those
        if (!Retrying)
        {
            triedMode = EffectiveMode;
            plantIds = [.. Enumerable.Range(0, PlantCount).Select(_ => Guid.NewGuid())];
        }

        try
        {
            return await UnitForms.RunAsync(
                () => Propagation.PottedUpCount,
                async () =>
                {
                    var placeId = await deps.RoomService.PlaceIdAsync(Location);
                    return await deps.PropagationService.PotUpAsync(Propagation, new PotUpRequest(
                        Count, Date, Nickname, placeId, Medium, PotId, Medium == GrowingMedium.Soil ? SoilMixId : null,
                        OnePot: EffectiveMode == PotUpKind.OnePot,
                        IntoPlantId: EffectiveMode == PotUpKind.Into ? IntoPlantId : null,
                        PlantIds: plantIds), label);
                },
                counted =>
                {
                    if (counted)
                        Retrying = false;
                });
        }
        catch
        {
            // A check that failed before any plant was stored leaves the form free to change
            bool stored;
            try
            {
                stored = await deps.AnyStoredAsync(PlantIds);
            }
            catch
            {
                stored = true;
            }
            Retrying = stored;
            throw;
        }
    }
}
