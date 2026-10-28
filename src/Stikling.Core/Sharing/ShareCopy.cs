using System.Text.Json.Serialization;
using Stikling.Core.Models;

namespace Stikling.Core.Sharing;

/// <summary>
/// A frozen copy of what a share page shows, for someone who was given the plant or cuttings to
/// keep in their own Stikling. It is built from the page model, so it never says more than the page.
/// </summary>
/// <param name="Version">The shape of the copy, 1.</param>
/// <param name="SubjectType">The giver's plant or propagation.</param>
/// <param name="SubjectId">The giver's id for it, which the receiving side makes its own ids from.</param>
/// <param name="Name">The link's name, else the card's.</param>
/// <param name="Propagation">Only when the subject is a propagation.</param>
/// <param name="Items">Oldest first, in the order of the page.</param>
/// <param name="NameQualifier">How sure the name is. Last and optional, so a copy without it still reads and an older receiver ignores it.</param>
/// <param name="NameNote">The locality or collector code that goes with the name.</param>
public sealed record ShareCopy(
    int Version,
    SubjectType SubjectType,
    Guid SubjectId,
    string Name,
    string? Genus,
    string? Species,
    string? Cultivar,
    ShareCopyPropagation? Propagation,
    Guid? CoverPhotoId,
    IReadOnlyList<ShareCopyItem> Items,
    [property: JsonConverter(typeof(TolerantNameQualifierConverter))] NameQualifier? NameQualifier = null,
    string? NameNote = null)
{
    public const int CurrentVersion = 1;

    public static ShareCopy ForPlant(SharePageModel page, Plant plant, TimeZoneInfo zone) =>
        new(CurrentVersion, SubjectType.Plant, plant.Id, page.Name, plant.Genus, plant.Species, plant.Cultivar, null,
            page.Cover?.Id, ItemsOf(page, zone), plant.NameQualifier, plant.NameNote);

    public static ShareCopy ForPropagation(SharePageModel page, Propagation propagation, TimeZoneInfo zone) =>
        new(CurrentVersion, SubjectType.Propagation, propagation.Id, page.Name, propagation.Genus, propagation.Species, propagation.Cultivar,
            new ShareCopyPropagation(propagation.Type, propagation.Medium, propagation.StartedOn, propagation.FirstRootOn,
                propagation.FirstLeafOn, propagation.FirstGerminatedOn, propagation.RootedOn),
            page.Cover?.Id, ItemsOf(page, zone), propagation.NameQualifier, propagation.NameNote);

    private static List<ShareCopyItem> ItemsOf(SharePageModel page, TimeZoneInfo zone) =>
        page.Items.Select(item => new ShareCopyItem(
            item.Day,
            item.Label,
            item.Kind,
            item.Text,
            item.Photos.Select(p => new ShareCopyPhoto(p.Id, p.Width, p.Height, p.Frame, NoonOn(item.Day, zone))).ToList()))
            .ToList();

    // The photos only keep the day they belong to on the page, so each is taken at noon that day in the link's zone
    private static DateTimeOffset NoonOn(DateOnly day, TimeZoneInfo zone)
    {
        var local = DateTime.SpecifyKind(day.ToDateTime(new TimeOnly(12, 0)), DateTimeKind.Unspecified);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }
}

/// <summary>The dates of a propagation, which its history items carry the milestones of.</summary>
public sealed record ShareCopyPropagation(
    PropagationType Type,
    GrowingMedium Medium,
    DateOnly StartedOn,
    DateOnly? FirstRootOn,
    DateOnly? FirstLeafOn,
    DateOnly? FirstGerminatedOn,
    DateOnly? RootedOn);

/// <param name="DayCount">The label after the date, frozen, like "day 24". Null when there is none.</param>
public sealed record ShareCopyItem(DateOnly Day, string? DayCount, ShareItemKind Kind, string? Text, IReadOnlyList<ShareCopyPhoto> Photos);

public sealed record ShareCopyPhoto(Guid Id, int Width, int Height, PhotoFrame? Frame, DateTimeOffset TakenAt);
