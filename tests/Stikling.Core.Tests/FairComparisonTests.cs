using Stikling.Core.Models;
using Stikling.Core.Propagations;

namespace Stikling.Core.Tests;

public class FairComparisonTests
{
    private static readonly DateOnly Started = new(2026, 3, 10);
    private static readonly DateOnly Today = new(2026, 9, 20);

    private static Propagation Batch(GrowingMedium medium, DateOnly? started = null, string genus = "Monstera", Action<Propagation>? change = null)
    {
        var batch = new Propagation
        {
            Genus = genus,
            Experiment = "Test",
            Medium = medium,
            StartedOn = started ?? Started
        };
        change?.Invoke(batch);
        return batch;
    }

    private static ExperimentGroup Group(params Propagation[] batches) =>
        Experiments.Group(batches, Today).Single();

    [Fact]
    public void An_experiment_that_only_changes_the_medium_differs_in_one_thing()
    {
        var differing = FairComparison.InExperiment(Group(Batch(GrowingMedium.Water), Batch(GrowingMedium.Perlite)));

        Assert.Equal([ComparedThing.Medium], differing);
        Assert.False(FairComparison.IsMoreThanOne(differing));
    }

    [Fact]
    public void A_different_medium_and_month_is_more_than_one_thing()
    {
        var differing = FairComparison.InExperiment(Group(
            Batch(GrowingMedium.Water),
            Batch(GrowingMedium.Perlite, new DateOnly(2026, 6, 10))));

        Assert.Equal([ComparedThing.Medium, ComparedThing.StartMonth], differing);
        Assert.True(FairComparison.IsMoreThanOne(differing));
    }

    [Fact]
    public void Two_days_apart_over_a_month_end_is_not_a_different_month()
    {
        var differing = FairComparison.InExperiment(Group(
            Batch(GrowingMedium.Water, new DateOnly(2026, 3, 31)),
            Batch(GrowingMedium.Perlite, new DateOnly(2026, 4, 2))));

        Assert.Equal([ComparedThing.Medium], differing);
    }

    [Fact]
    public void The_same_month_a_year_later_is_a_different_start()
    {
        var differing = FairComparison.InExperiment(Group(
            Batch(GrowingMedium.Water, new DateOnly(2025, 3, 10)),
            Batch(GrowingMedium.Water, new DateOnly(2026, 3, 10))));

        Assert.Equal([ComparedThing.StartMonth], differing);
    }

    [Fact]
    public void A_field_left_empty_does_not_count_as_different()
    {
        var differing = FairComparison.InExperiment(Group(
            Batch(GrowingMedium.Water, change: p => { p.PlaceId = Guid.NewGuid(); p.Species = "deliciosa"; }),
            Batch(GrowingMedium.Perlite, genus: "", change: p => p.Nickname = "Unnamed")));

        Assert.Equal([ComparedThing.Medium], differing);
    }

    [Fact]
    public void Different_rooting_aids_drying_type_and_place_are_all_named()
    {
        var differing = FairComparison.InExperiment(Group(
            Batch(GrowingMedium.Water, change: p => { p.RootingAids = [RootingAid.HumidityDome]; p.DriedForHours = 30; p.PlaceId = Guid.NewGuid(); }),
            Batch(GrowingMedium.Water, change: p => { p.Type = PropagationType.Leaf; p.PlaceId = Guid.NewGuid(); })));

        Assert.Equal([ComparedThing.Type, ComparedThing.RootingAids, ComparedThing.Drying, ComparedThing.Place], differing);
    }

    [Fact]
    public void Different_species_of_a_genus_differ_but_a_missing_species_does_not()
    {
        var two = FairComparison.InExperiment(Group(
            Batch(GrowingMedium.Water, change: p => p.Species = "deliciosa"),
            Batch(GrowingMedium.Water, change: p => p.Species = "adansonii")));
        var one = FairComparison.InExperiment(Group(
            Batch(GrowingMedium.Water, change: p => p.Species = "deliciosa"),
            Batch(GrowingMedium.Water)));

        Assert.Equal([ComparedThing.Plant], two);
        Assert.Empty(one);
    }

    [Fact]
    public void A_deleted_batch_is_left_out_of_an_experiment()
    {
        var live = Batch(GrowingMedium.Water);
        var deleted = Batch(GrowingMedium.Perlite, new DateOnly(2026, 7, 1));
        deleted.DeletedAt = DateTimeOffset.UtcNow;
        var group = new ExperimentGroup("Test", Started,
        [
            new ExperimentEntry(live, 0, null),
            new ExperimentEntry(live.Copy(), 0, null),
            new ExperimentEntry(deleted, 0, null)
        ]);

        Assert.Empty(FairComparison.InExperiment(group));
    }

    [Fact]
    public void A_single_batch_has_nothing_to_differ_from()
    {
        Assert.Empty(FairComparison.InExperiment(Group(Batch(GrowingMedium.Water))));
    }

    [Fact]
    public void Mediums_that_are_otherwise_alike_say_nothing_on_the_results_page()
    {
        Assert.Empty(FairComparison.AcrossMediums([Batch(GrowingMedium.Water), Batch(GrowingMedium.Perlite)]));
    }

    [Fact]
    public void Mediums_used_at_different_times_of_year_are_named_on_the_results_page()
    {
        var differing = FairComparison.AcrossMediums(
        [
            Batch(GrowingMedium.Water, new DateOnly(2026, 1, 10)),
            Batch(GrowingMedium.Perlite, new DateOnly(2026, 7, 10))
        ]);

        Assert.Equal([ComparedThing.StartMonth], differing);
    }

    [Fact]
    public void The_next_month_over_a_new_year_counts_as_the_same_time_of_year()
    {
        Assert.Empty(FairComparison.AcrossMediums(
        [
            Batch(GrowingMedium.Water, new DateOnly(2025, 12, 10)),
            Batch(GrowingMedium.Perlite, new DateOnly(2026, 1, 10))
        ]));
    }

    [Fact]
    public void One_batch_out_of_line_does_not_make_mediums_differ()
    {
        Assert.Empty(FairComparison.AcrossMediums(
        [
            Batch(GrowingMedium.Water),
            Batch(GrowingMedium.Water),
            Batch(GrowingMedium.Water, genus: "Alocasia"),
            Batch(GrowingMedium.Perlite)
        ]));
    }

    [Fact]
    public void A_deleted_batch_does_not_count_on_the_results_page()
    {
        var deleted = Batch(GrowingMedium.Perlite, new DateOnly(2026, 7, 10));
        deleted.DeletedAt = DateTimeOffset.UtcNow;

        Assert.Empty(FairComparison.AcrossMediums([Batch(GrowingMedium.Water), deleted]));
    }

    [Fact]
    public void Batches_with_and_without_an_aid_can_differ_in_medium()
    {
        var differing = FairComparison.AcrossRootingAids(
        [
            Batch(GrowingMedium.Sphagnum, change: p => p.RootingAids = [RootingAid.HumidityDome]),
            Batch(GrowingMedium.Water)
        ]);

        Assert.Equal([ComparedThing.Medium], differing);
    }

    [Fact]
    public void Everyone_using_an_aid_has_nothing_to_compare_with()
    {
        Assert.Empty(FairComparison.AcrossRootingAids(
        [
            Batch(GrowingMedium.Sphagnum, change: p => p.RootingAids = [RootingAid.HumidityDome]),
            Batch(GrowingMedium.Water, change: p => p.RootingAids = [RootingAid.HumidityDome])
        ]));
    }

    [Fact]
    public void Dried_and_undried_batches_can_differ_in_type()
    {
        var differing = FairComparison.AcrossDrying(
        [
            Batch(GrowingMedium.Water, change: p => p.DriedForHours = 30),
            Batch(GrowingMedium.Water, change: p => p.Type = PropagationType.Leaf)
        ]);

        Assert.Equal([ComparedThing.Type], differing);
    }
}
