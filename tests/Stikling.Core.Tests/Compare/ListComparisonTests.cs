using Stikling.Core.Compare;
using Stikling.Core.Examples;
using Stikling.Core.Models;
using static Stikling.Core.Tests.CompareTestData;

namespace Stikling.Core.Tests;

public class ListComparisonTests
{
    private static ListComparisonResult Compare(FriendList friend, IEnumerable<Wish>? wishes = null,
        IEnumerable<Plant>? plants = null, IEnumerable<Propagation>? propagations = null) =>
        ListComparison.Compare(friend, wishes ?? [], plants ?? [], propagations ?? [], Dictionary);

    [Fact]
    public void Their_plant_on_my_wishlist_is_for_you()
    {
        var thai = Theirs("Thai", "Monstera", "deliciosa", "Thai Constellation");
        var wish = AWish("monstera", "Deliciosa", "thai constellation");

        var result = Compare(Friend([thai, Theirs("Other", "Hoya", "carnosa")]), [wish]);

        var match = Assert.Single(result.ForYou);
        Assert.Equal(MatchKind.Exact, match.Kind);
        Assert.Same(wish, match.Mine);
        Assert.Null(match.Difference);
        Assert.Equal(["Other"], result.More.Select(p => p.Name));
    }

    [Fact]
    public void Same_species_with_another_cultivar_is_close_and_says_what_theirs_is()
    {
        var result = Compare(Friend([Theirs("Albo", "Monstera", "deliciosa", "Albo")]), [AWish("Monstera", "deliciosa", "Thai Constellation")]);

        var match = Assert.Single(result.ForYou);
        Assert.Equal(MatchKind.Close, match.Kind);
        Assert.Equal("Theirs: 'Albo'", match.Difference);
    }

    [Fact]
    public void A_cultivar_on_one_side_only_is_close_and_says_no_cultivar()
    {
        var result = Compare(Friend([Theirs("M", "Monstera", "deliciosa")]), [AWish("Monstera", "deliciosa", "Albo")]);

        Assert.Equal("Theirs: no cultivar", Assert.Single(result.ForYou).Difference);
    }

    [Fact]
    public void A_wish_with_only_a_genus_matches_the_same_genus_only_when_the_species_is_too()
    {
        var result = Compare(Friend([Theirs("M", "Monstera", "deliciosa")]), [AWish("Monstera")]);

        Assert.Empty(result.ForYou);
    }

    [Fact]
    public void An_old_name_is_followed_to_the_one_it_goes_by_now()
    {
        var result = Compare(Friend([Theirs("M", "Monstera", "borsigiana")]), [AWish("Monstera", "deliciosa")]);

        Assert.Equal(MatchKind.Exact, Assert.Single(result.ForYou).Kind);
    }

    [Fact]
    public void Names_with_no_genus_match_only_the_same_name_typed()
    {
        var wish = AWish(name: " That Pink Syngonium ");
        var result = Compare(Friend([Theirs("that pink syngonium"), Theirs("pink syngonium")]), [wish]);

        var match = Assert.Single(result.ForYou);
        Assert.Equal(MatchKind.Exact, match.Kind);
        Assert.Equal("that pink syngonium", match.Theirs.Name);
        Assert.Equal(["pink syngonium"], result.More.Select(p => p.Name));
    }

    [Fact]
    public void Unnamed_plants_and_wishes_are_skipped()
    {
        var result = Compare(
            Friend([Theirs("Unnamed plant")], [TheirWish("Unnamed plant")]),
            [AWish()],
            [APlant()]);

        Assert.Empty(result.ForYou);
        Assert.Empty(result.ForThem);
    }

    [Fact]
    public void Wishes_that_are_got_or_deleted_do_not_count()
    {
        var got = AWish("Monstera", "deliciosa");
        got.GotOn = new DateOnly(2026, 10, 1);
        var deleted = AWish("Monstera", "deliciosa");
        deleted.DeletedAt = DateTimeOffset.UnixEpoch;

        Assert.Empty(Compare(Friend([Theirs("M", "Monstera", "deliciosa")]), [got, deleted]).ForYou);
    }

    [Fact]
    public void For_you_has_spare_first_then_exact_then_close()
    {
        var wishes = new[] { AWish("Monstera", "deliciosa", "Albo"), AWish("Hoya", "carnosa", "Krimson Queen"), AWish("Alocasia", "zebrina") };
        var friend = Friend([
            Theirs("close", "Hoya", "carnosa"),
            Theirs("exact", "Alocasia", "zebrina"),
            Theirs("spare", "Monstera", "deliciosa", "Albo", FriendSpare.UpForSwap),
        ]);

        Assert.Equal(["spare", "exact", "close"], Compare(friend, wishes).ForYou.Select(m => m.Theirs.Name));
    }

    [Fact]
    public void One_row_for_each_of_their_plants_with_exact_before_close()
    {
        var close = AWish("Monstera", "deliciosa", "Thai Constellation");
        var exact = AWish("Monstera", "deliciosa", "Albo");

        var match = Assert.Single(Compare(Friend([Theirs("A", "Monstera", "deliciosa", "Albo")]), [close, exact]).ForYou);

        Assert.Same(exact, match.Mine);
    }

    [Fact]
    public void My_plant_on_their_wishlist_is_for_them_and_says_what_mine_is_when_close()
    {
        var mine = APlant("Monstera", "deliciosa", "Albo", "Albo");
        var result = Compare(Friend(wishes: [TheirWish("Monstera deliciosa", "Monstera", "deliciosa")]), plants: [mine]);

        var match = Assert.Single(result.ForThem);
        Assert.Equal(mine.Id, match.Id);
        Assert.Equal(MatchKind.Close, match.Kind);
        Assert.Equal("Yours: 'Albo'", match.Difference);
    }

    [Fact]
    public void Mine_without_a_cultivar_says_so()
    {
        var result = Compare(Friend(wishes: [TheirWish("M", "Monstera", "deliciosa", "Albo")]), plants: [APlant("Monstera", "deliciosa")]);

        Assert.Equal("Yours: no cultivar", Assert.Single(result.ForThem).Difference);
    }

    [Fact]
    public void Only_plants_in_the_collection_and_cuttings_going_count_as_mine()
    {
        var gone = APlant("Monstera", "deliciosa");
        gone.Status = PlantStatus.Died;
        var deleted = APlant("Monstera", "deliciosa");
        deleted.DeletedAt = DateTimeOffset.UnixEpoch;
        var finished = new Propagation { Genus = "Monstera", Species = "deliciosa", Stage = PropagationStage.Done };
        var example = new Propagation { Id = ExampleFamily.CurrentCuttingId, Genus = "Monstera", Species = "deliciosa" };
        var friend = Friend(wishes: [TheirWish("M", "Monstera", "deliciosa")]);

        Assert.Empty(Compare(friend, plants: [gone, deleted], propagations: [finished, example]).ForThem);
        Assert.Single(Compare(friend, propagations: [new Propagation { Genus = "Monstera", Species = "deliciosa" }]).ForThem);
    }

    [Fact]
    public void One_row_for_each_of_their_wishes_and_my_spare_one_is_picked_first()
    {
        var plain = APlant("Monstera", "deliciosa");
        var offered = APlant("Monstera", "deliciosa");
        offered.Spare = SpareMark.UpForSwap;

        var match = Assert.Single(Compare(Friend(wishes: [TheirWish("M", "Monstera", "deliciosa")]), plants: [plain, offered]).ForThem);

        Assert.Equal(offered.Id, match.Id);
        Assert.True(match.IsSpare);
        Assert.False(match.CanMarkUpForSwap);
    }

    [Fact]
    public void A_cutting_with_some_to_spare_is_spare_and_can_not_be_marked()
    {
        var cutting = new Propagation { Genus = "Hoya", Species = "carnosa", InitialCount = 3, SpareCount = 2 };
        var match = Assert.Single(Compare(Friend(wishes: [TheirWish("H", "Hoya", "carnosa")]), propagations: [cutting]).ForThem);

        Assert.Equal(SubjectType.Propagation, match.Type);
        Assert.True(match.IsSpare);
        Assert.False(match.CanMarkUpForSwap);

        var notSpare = Assert.Single(Compare(Friend(wishes: [TheirWish("H", "Hoya", "carnosa")]),
            propagations: [new Propagation { Genus = "Hoya", Species = "carnosa", InitialCount = 3 }]).ForThem);
        Assert.False(notSpare.IsSpare);
        Assert.False(notSpare.CanMarkUpForSwap);
    }

    [Fact]
    public void A_plant_that_is_not_offered_can_be_marked_up_for_swap()
    {
        var match = Assert.Single(Compare(Friend(wishes: [TheirWish("A", "Alocasia", "zebrina")]), plants: [APlant("Alocasia", "zebrina")]).ForThem);

        Assert.True(match.CanMarkUpForSwap);
        Assert.False(match.IsSpare);
    }

    [Fact]
    public void For_them_has_my_spare_first_then_exact_then_close()
    {
        var spare = APlant("Hoya", "kerrii");
        spare.Spare = SpareMark.CuttingsToSpare;
        var exact = APlant("Alocasia", "zebrina");
        var close = APlant("Monstera", "deliciosa", "Albo");
        var friend = Friend(wishes: [
            TheirWish("close", "Monstera", "deliciosa"),
            TheirWish("exact", "Alocasia", "zebrina"),
            TheirWish("spare", "Hoya", "kerrii"),
        ]);

        var result = Compare(friend, plants: [close, exact, spare]);

        Assert.Equal(["spare", "exact", "close"], result.ForThem.Select(m => m.Theirs.Name));
    }

    [Fact]
    public void A_wish_with_no_genus_is_compared_with_my_plants_nickname()
    {
        var mine = APlant("Syngonium", "podophyllum", nickname: "Pink one");
        var match = Assert.Single(Compare(Friend(wishes: [TheirWish("pink one")]), plants: [mine, APlant("Hoya", "carnosa")]).ForThem);

        Assert.Equal(mine.Id, match.Id);
        Assert.Equal(MatchKind.Exact, match.Kind);
    }

    [Fact]
    public void More_leaves_out_plants_i_already_have_and_those_above_and_puts_spare_first()
    {
        var friend = Friend([
            Theirs("have", "Hoya", "carnosa", "Krimson Queen"),
            Theirs("wanted", "Alocasia", "zebrina"),
            Theirs("another cultivar", "Hoya", "carnosa"),
            Theirs("plain"),
            Theirs("spare", "Monstera", "deliciosa", "Albo", FriendSpare.Cuttings, 2, PropagationStage.Rooted),
        ]);

        var result = Compare(friend, [AWish("Alocasia", "zebrina")], [APlant("Hoya", "carnosa", "Krimson Queen")]);

        Assert.Equal(["wanted"], result.ForYou.Select(m => m.Theirs.Name));
        Assert.Equal(["spare", "another cultivar", "plain"], result.More.Select(p => p.Name));
    }

    [Fact]
    public void The_counts_line_says_how_many_each_way()
    {
        var friend = Friend([Theirs("a", "Alocasia", "zebrina"), Theirs("m", "Monstera", "deliciosa")], [TheirWish("h", "Hoya", "carnosa")]);
        var result = Compare(friend, [AWish("Alocasia", "zebrina"), AWish("Monstera", "deliciosa")], [APlant("Hoya", "carnosa")]);

        Assert.Equal("2 for you · 1 for them", ListComparison.CountsLine(result));
        Assert.Equal("2 til dig · 1 til dem", In("da", () => ListComparison.CountsLine(result)));
        Assert.Equal("0 for you · 0 for them", ListComparison.CountsLine(Compare(Friend())));
    }

    [Fact]
    public void Differences_are_in_Danish_too()
    {
        var result = Compare(Friend([Theirs("A", "Monstera", "deliciosa", "Albo")]), [AWish("Monstera", "deliciosa")]);
        var forThem = Compare(Friend(wishes: [TheirWish("M", "Monstera", "deliciosa")]), plants: [APlant("Monstera", "deliciosa")]);

        Assert.Equal("Hos dem: 'Albo'", In("da", () => Compare(Friend([Theirs("A", "Monstera", "deliciosa", "Albo")]), [AWish("Monstera", "deliciosa")]).ForYou[0].Difference));
        Assert.Null(Assert.Single(forThem.ForThem).Difference);
        Assert.Equal("Hos dig: ingen kultivar", In("da", () => Compare(Friend(wishes: [TheirWish("M", "Monstera", "deliciosa", "Albo")]), plants: [APlant("Monstera", "deliciosa")]).ForThem[0].Difference));
        Assert.Single(result.ForYou);
    }
}
