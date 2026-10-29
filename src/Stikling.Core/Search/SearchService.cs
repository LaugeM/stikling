using System.Globalization;
using Stikling.Core.Care;
using Stikling.Core.Feeds;
using Stikling.Core.Fields;
using Stikling.Core.Models;
using Stikling.Core.Pests;
using Stikling.Core.Plants;
using Stikling.Core.Pots;
using Stikling.Core.Problems;
using Stikling.Core.Products;
using Stikling.Core.Propagations;
using Stikling.Core.Rooms;
using Stikling.Core.SoilMixes;
using Stikling.Core.Text;
using Stikling.Core.Timeline;
using Stikling.Core.Wishlist;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Search;

/// <summary>
/// Search across the whole app. <see cref="LoadAsync"/> reads every record once and the index it
/// returns does the searching, so a page loads it when it opens and searches on each keystroke.
/// </summary>
public sealed class SearchService(
    IPlantRepository plants,
    IPropagationRepository propagations,
    ITimelineRepository timeline,
    ICareLogRepository careLogs,
    IPropagationCareLogRepository propagationCareLogs,
    IProblemRepository problems,
    IProblemUpdateRepository problemUpdates,
    IPestCaseRepository pestCases,
    IPestTreatmentRepository pestTreatments,
    ITreatmentRecipeRepository recipes,
    IWishRepository wishes,
    IPotRepository pots,
    ISoilMixRepository mixes,
    IFeedRepository feeds,
    IProductRepository products,
    IPlaceRepository placeRepository,
    IOwnCareKindRepository ownCareKinds,
    IOwnFieldRepository ownFields,
    TimeProvider time)
{
    /// <param name="label">Names the enums the way the app shows them (care kinds and pests).</param>
    /// <param name="englishLabel">Names the same enums in English, so a history line saved before lines had a key can be read back and shown in the app's language.</param>
    /// <param name="everyday">The everyday name for a genus, species and cultivar, like "Snake plant". Null for none.</param>
    public async Task<SearchIndex> LoadAsync(Func<Enum, string> label, Func<Enum, string> englishLabel, Func<string?, string?, string?, string?>? everyday = null)
    {
        var allPlants = (await plants.GetAllAsync()).Where(p => !p.IsDeleted).ToList();
        var allPropagations = (await propagations.GetAllAsync()).Where(p => !p.IsDeleted).ToList();
        var places = await placeRepository.GetPlacesAsync();
        var textFields = (await ownFields.GetAllAsync()).Where(f => f.Kind is OwnFieldKind.Text or OwnFieldKind.Choice).ToList();

        var docs = new Dictionary<SearchKind, IReadOnlyList<SearchIndex.Doc>>
        {
            [SearchKind.Plant] = [.. allPlants.Select(p => PlantDoc(p, everyday, textFields))],
            [SearchKind.Propagation] = [.. allPropagations.Select(p => PropagationDoc(p, everyday))],
            [SearchKind.Wish] = [.. (await wishes.GetAllAsync()).Where(w => !w.IsDeleted).Select(WishDoc)],
            [SearchKind.Pot] = [.. (await pots.GetAllAsync()).Where(p => !p.IsDeleted).Select(PotDoc)],
            [SearchKind.Mix] = [.. (await mixes.GetAllAsync()).Where(m => !m.IsDeleted).Select(MixDoc)],
            [SearchKind.Feed] = [.. (await feeds.GetAllAsync()).Where(f => !f.IsDeleted).Select(FeedDoc)],
            [SearchKind.Product] = [.. (await products.GetAllAsync()).Where(p => !p.IsDeleted).Select(ProductDoc)],
            [SearchKind.TreatmentRecipe] = [.. (await recipes.GetAllAsync()).Where(r => !r.IsDeleted).Select(RecipeDoc)],
            [SearchKind.Place] = PlaceDocs(places),
            [SearchKind.Tag] = [.. PlantTags.InUse(allPlants).Select(TagDoc)]
        };

        return new SearchIndex(docs, await NotesAsync(allPlants, allPropagations, label, englishLabel));
    }

    private static string Names(params string?[] parts) =>
        SearchText.Fold(string.Join(' ', parts.Where(p => !string.IsNullOrWhiteSpace(p))));

    private static SearchIndex.Extra InNotes(string? notes) => new(Has(notes), Strings.Shared.SearchInNotes, IsNotes: true);

    private static Func<string, bool> Has(string? text)
    {
        var folded = SearchText.Fold(text);
        return word => folded.Contains(word, StringComparison.Ordinal);
    }

    private static List<SearchIndex.Extra> Extras(string? notes, params SearchIndex.Extra[] before)
    {
        List<SearchIndex.Extra> extras = [.. before];
        if (!string.IsNullOrWhiteSpace(notes))
            extras.Add(InNotes(notes));
        return extras;
    }

    private static string? Subtitle(string? nickname, string? botanical) =>
        !string.IsNullOrWhiteSpace(nickname) && botanical is not null ? botanical : null;

    private static SearchIndex.Doc PlantDoc(Plant plant, Func<string?, string?, string?, string?>? everyday, IReadOnlyList<OwnField> textFields)
    {
        var item = new SearchItem(SearchKind.Plant, plant.Id, plant.DisplayName, Subtitle(plant.Nickname, plant.BotanicalName),
            plant.LivesElsewhere ? (plant.LivesWithName is { } whose ? string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Shared.SearchLivesWith, whose) : Strings.Shared.SearchNotYours) : null,
            !plant.InCollection && !plant.LivesElsewhere, $"plants/{plant.Id}");
        var names = Names(plant.DisplayName, plant.Nickname, plant.Genus, plant.Species, plant.Cultivar, plant.NameNote, plant.BotanicalName,
            everyday?.Invoke(plant.Genus, PlantNames.SpeciesToLookUp(plant.Species, plant.NameQualifier), plant.Cultivar));

        List<SearchIndex.Extra> before =
            [.. PlantTags.Normalize(plant.Tags).Select(tag => new SearchIndex.Extra(Has(tag), string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Shared.SearchTag, tag)))];
        if (CollectionNumbers.Normalize(plant.CollectionNumber) is { } code)
            before.Add(new SearchIndex.Extra(word => CollectionNumbers.MatchesSearch(code, word), string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Shared.SearchNumber, code)));
        foreach (var field in textFields)
            if (plant.OwnFields.TryGetValue(field.Id, out var value) && !string.IsNullOrWhiteSpace(value))
                before.Add(new SearchIndex.Extra(Has(value), $"{field.Name} {value}"));
        return new(item, names, Extras(plant.Notes, [.. before]));
    }

    private static SearchIndex.Doc PropagationDoc(Propagation propagation, Func<string?, string?, string?, string?>? everyday)
    {
        var item = new SearchItem(SearchKind.Propagation, propagation.Id, propagation.DisplayName,
            Subtitle(propagation.Nickname, propagation.BotanicalName), null, !propagation.IsActive,
            $"propagations/{propagation.Id}");
        var names = Names(propagation.DisplayName, propagation.Nickname, propagation.Genus, propagation.Species,
            propagation.Cultivar, propagation.NameNote, propagation.BotanicalName, propagation.Source,
            everyday?.Invoke(propagation.Genus, PlantNames.SpeciesToLookUp(propagation.Species, propagation.NameQualifier), propagation.Cultivar));

        List<SearchIndex.Extra> before = [];
        if (CollectionNumbers.Normalize(propagation.CollectionNumber) is { } code)
            before.Add(new SearchIndex.Extra(word => CollectionNumbers.MatchesSearch(code, word), string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Shared.SearchNumber, code)));
        return new(item, names, Extras(propagation.Notes, [.. before]));
    }

    private static SearchIndex.Doc WishDoc(Wish wish) =>
        new(new SearchItem(SearchKind.Wish, wish.Id, wish.DisplayName, null, null, wish.IsGot, $"wishlist/{wish.Id}/edit"),
            Names(wish.DisplayName, wish.Name, wish.Genus, wish.Species, wish.Cultivar, wish.NameNote, wish.BotanicalName, wish.FoundAt),
            Extras(wish.Notes));

    private static SearchIndex.Doc PotDoc(Pot pot) =>
        new(new SearchItem(SearchKind.Pot, pot.Id, pot.DisplayName, null, null, false, $"pots?show={pot.Id}"),
            Names(pot.DisplayName, pot.Name),
            Extras(pot.Notes));

    private static SearchIndex.Doc MixDoc(SoilMix mix)
    {
        var recipe = string.IsNullOrWhiteSpace(mix.Recipe) ? null : mix.Recipe;
        var title = string.IsNullOrWhiteSpace(mix.Name) ? Strings.Shared.SearchUnnamedMix : mix.Name.Trim();
        return new(new SearchItem(SearchKind.Mix, mix.Id, title, null, recipe, false, $"soil-mixes?show={mix.Id}"),
            Names(title, recipe),
            Extras(mix.Notes));
    }

    private static SearchIndex.Doc FeedDoc(Feed feed)
    {
        var title = string.IsNullOrWhiteSpace(feed.Name) ? Strings.Shared.SearchUnnamedFeed : feed.Name.Trim();
        return new(new SearchItem(SearchKind.Feed, feed.Id, title, null, null, false, $"feeds?show={feed.Id}"),
            Names(title),
            Extras(feed.Notes));
    }

    private static SearchIndex.Doc ProductDoc(Product product)
    {
        var title = string.IsNullOrWhiteSpace(product.Name) ? Strings.Shared.SearchUnnamedProduct : product.Name.Trim();
        return new(new SearchItem(SearchKind.Product, product.Id, title, null, null, false, $"products?show={product.Id}"),
            Names(title),
            Extras(product.Notes));
    }

    private static SearchIndex.Doc RecipeDoc(TreatmentRecipe recipe)
    {
        var title = string.IsNullOrWhiteSpace(recipe.Name) ? Strings.Shared.SearchUnnamedRecipe : recipe.Name.Trim();
        return new(new SearchItem(SearchKind.TreatmentRecipe, recipe.Id, title, null, null, false, $"treatment-recipes?show={recipe.Id}"),
            Names(title, recipe.Method),
            []);
    }

    private static SearchIndex.Doc TagDoc(TagCount tag) =>
        new(new SearchItem(SearchKind.Tag, Guid.Empty, tag.Name, null, Plural.Of(tag.Plants, Strings.Shared.SearchTagPlants_One, Strings.Shared.SearchTagPlants_Other),
                false, $"plants?tag={Uri.EscapeDataString(tag.Name)}"),
            Names(tag.Name),
            []);

    // Rooms and the spots in them. Places merged into another aren't in either list.
    private static List<SearchIndex.Doc> PlaceDocs(Places places)
    {
        var docs = new List<SearchIndex.Doc>();
        foreach (var room in places.Rooms)
        {
            docs.Add(PlaceDoc(room, room.IsSpot ? Strings.Shared.SearchSpot : Strings.Shared.SearchRoom));
            foreach (var spot in places.SpotsIn(room.Id))
                docs.Add(PlaceDoc(spot, string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Shared.SearchSpotIn, room.Name)));
        }

        return docs;
    }

    private static SearchIndex.Doc PlaceDoc(Place place, string meta) =>
        new(new SearchItem(SearchKind.Place, place.Id, place.Name, null, meta, false, $"plants?room={place.Id}"),
            Names(place.Name),
            []);

    // Notes

    private async Task<List<SearchIndex.NoteDoc>> NotesAsync(
        List<Plant> allPlants, List<Propagation> allPropagations, Func<Enum, string> label, Func<Enum, string> englishLabel)
    {
        var plantsById = allPlants.ToDictionary(p => p.Id);
        var propagationsById = allPropagations.ToDictionary(p => p.Id);
        var notes = new List<SearchIndex.NoteDoc>();

        void Add(Guid id, string href, NoteSource source, DateTimeOffset date, SubjectType? ownerType, Guid ownerId,
            string ownerName, string kind, params string?[] text)
        {
            var joined = SearchIndex.Collapse(string.Join(' ', text.Where(t => !string.IsNullOrWhiteSpace(t))));
            if (joined.Length > 0)
                notes.Add(new(id, href, source, date, ownerType, ownerId, ownerName, kind, joined));
        }

        // What owns a subject, if it's there and not deleted
        (SubjectType Type, string Name)? Owner(SubjectType type, Guid id) => type switch
        {
            SubjectType.Plant when plantsById.TryGetValue(id, out var plant) => (type, plant.DisplayName),
            SubjectType.Propagation when propagationsById.TryGetValue(id, out var cutting) => (type, cutting.DisplayName),
            _ => null
        };

        var plantCareLogs = (await careLogs.GetAllAsync()).Where(l => !l.IsDeleted).ToList();

        // History lines are searched as the reader sees them, in the language of the app
        string Shown(TimelineEntry entry) => HistoryText.Show(entry, label, englishLabel, CultureInfo.CurrentUICulture);

        // Repotting and the like write a history line with the care note in it, which would be
        // listed next to the care log itself
        bool RepeatsCareNote(TimelineEntry entry)
        {
            if (entry.Kind != TimelineKind.Change || entry.SubjectType != SubjectType.Plant)
                return false;
            var text = SearchText.Fold(SearchIndex.Collapse(Shown(entry)));
            var day = time.LocalDay(entry.OccurredAt);
            return plantCareLogs.Any(l => l.PlantId == entry.SubjectId && l.OccurredOn == day
                && SearchText.Fold(SearchIndex.Collapse(l.Notes ?? "")) is { Length: > 0 } notes && text.Contains(notes, StringComparison.Ordinal));
        }

        // The older versions of a corrected note (Edits) aren't searched
        foreach (var entry in await timeline.GetSinceAsync(DateTimeOffset.MinValue))
        {
            // A problem has its own records below, so its history line isn't listed twice
            if (entry.IsDeleted || entry.Kind is not (TimelineKind.Note or TimelineKind.Change)
                || TimelineEvents.Of(entry) == TimelineEvent.Problem
                || entry.SubjectType == SubjectType.Pot
                || RepeatsCareNote(entry)
                || Owner(entry.SubjectType, entry.SubjectId) is not { } owner)
                continue;

            var route = owner.Type == SubjectType.Plant ? "plants" : "propagations";
            Add(entry.Id, $"{route}/{entry.SubjectId}?entry={entry.Id}", NoteSource.Timeline, entry.OccurredAt, owner.Type,
                entry.SubjectId, owner.Name, entry.Kind == TimelineKind.Note ? Strings.Shared.SearchLabelNote : Strings.Shared.SearchLabelUpdate, Shown(entry));
        }

        var ownKinds = CareLabels.Lookup(await ownCareKinds.GetAllAsync());
        foreach (var log in plantCareLogs)
        {
            if (plantsById.TryGetValue(log.PlantId, out var plant))
                Add(log.Id, $"plants/{plant.Id}?tab=care&entry={log.Id}", NoteSource.PlantCare, time.MomentOn(log.OccurredOn),
                    SubjectType.Plant, plant.Id, plant.DisplayName, CareLabels.For(log, ownKinds, label), log.Notes);
        }

        // A finished propagation has no care sheet, so its care notes can't be shown and are left out
        foreach (var log in await propagationCareLogs.GetAllAsync())
        {
            if (!log.IsDeleted && propagationsById.TryGetValue(log.PropagationId, out var cutting) && cutting.IsActive)
                Add(log.Id, $"propagations/{cutting.Id}?entry={log.Id}", NoteSource.PropagationCare, time.MomentOn(log.OccurredOn),
                    SubjectType.Propagation, cutting.Id, cutting.DisplayName, label(log.Kind), log.Notes);
        }

        var liveProblems = new Dictionary<Guid, (Problem Problem, string Name, SubjectType Type)>();
        foreach (var problem in await problems.GetAllAsync())
        {
            if (problem.IsDeleted || Owner(problem.SubjectType, problem.SubjectId) is not { } owner)
                continue;
            liveProblems[problem.Id] = (problem, owner.Name, owner.Type);
            Add(problem.Id, $"problems/{problem.Id}", NoteSource.Problem, time.MomentOn(problem.StartedOn),
                owner.Type, problem.SubjectId, owner.Name, Strings.Shared.SearchLabelProblem, problem.Note);
        }

        foreach (var update in await problemUpdates.GetAllAsync())
        {
            if (!update.IsDeleted && liveProblems.TryGetValue(update.ProblemId, out var found))
                Add(update.Id, $"problems/{update.ProblemId}?entry={update.Id}", NoteSource.ProblemUpdate,
                    time.MomentOn(update.OccurredOn), found.Type, found.Problem.SubjectId, found.Name, Strings.Shared.SearchLabelProblem, update.Note);
        }

        // A case with one plant is named for the plant, else for the pest
        var liveCases = new Dictionary<Guid, (PestCase Case, SubjectType? Type, Guid OwnerId, string Name)>();
        foreach (var item in await pestCases.GetAllAsync())
        {
            if (item.IsDeleted)
                continue;

            SubjectType? type = null;
            var ownerId = item.Id;
            var name = label(item.Pest);
            if (item.PlantIds.Count == 1)
            {
                // The case is left out when its one plant is gone
                if (!plantsById.TryGetValue(item.PlantIds[0], out var only))
                    continue;
                type = SubjectType.Plant;
                ownerId = only.Id;
                name = only.DisplayName;
            }

            liveCases[item.Id] = (item, type, ownerId, name);
            Add(item.Id, $"pests/{item.Id}", NoteSource.PestCase, time.MomentOn(item.StartedOn), type, ownerId, name, Strings.Shared.SearchLabelPests, item.Notes);
        }

        foreach (var treatment in await pestTreatments.GetAllAsync())
        {
            if (!treatment.IsDeleted && liveCases.TryGetValue(treatment.CaseId, out var found))
                Add(treatment.Id, $"pests/{treatment.CaseId}?entry={treatment.Id}", NoteSource.PestTreatment,
                    time.MomentOn(treatment.OccurredOn), found.Type, found.OwnerId, found.Name, Strings.Shared.SearchLabelPests,
                    treatment.Notes, treatment.What, treatment.Method);
        }

        return notes;
    }
}
