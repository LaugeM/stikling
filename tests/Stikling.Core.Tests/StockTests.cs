using Stikling.Core.Models;
using Stikling.Core.Propagations;
using Stikling.Core.SoilMixes;
using Stikling.Core.Stock;

namespace Stikling.Core.Tests;

public class StockTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 11);

    private static DateOnly DayOf(DateTimeOffset moment) => DateOnly.FromDateTime(moment.UtcDateTime);

    private static Supply Leca() => new() { Name = "LECA", Kind = SupplyKind.Mixing, Unit = SupplyUnit.Litres };

    private static SupplyEntry Entry(Supply supply, SupplyEntryKind? kind, int daysAgo, decimal? amount = null, int created = 0) => new()
    {
        SupplyId = supply.Id,
        Kind = kind,
        Amount = amount,
        At = Now.AddDays(-daysAgo),
        CreatedAt = Now.AddSeconds(created)
    };

    private static StockView View(
        IEnumerable<Supply> supplies, IEnumerable<SupplyEntry> entries,
        IEnumerable<ShoppingItem>? items = null, IEnumerable<Product>? products = null, IEnumerable<Pot>? pots = null) =>
        StockView.Build(supplies, entries, items ?? [], products ?? [], pots ?? [], DayOf, Today);

    private static SupplyStatus StatusOf(Supply supply, params SupplyEntry[] entries) =>
        Assert.Single(View([supply], entries).Supplies);

    private sealed record Setup(
        StockService Service, FakeSupplyRepository Supplies, FakeSupplyEntryRepository Entries,
        FakeShoppingItemRepository Items, FakeProductRepository Products, FakePotRepository Pots);

    private static Setup Create()
    {
        var supplies = new FakeSupplyRepository();
        var entries = new FakeSupplyEntryRepository();
        var items = new FakeShoppingItemRepository();
        var products = new FakeProductRepository();
        var pots = new FakePotRepository();
        return new Setup(new StockService(supplies, entries, items, products, pots, new FixedTime(Now)), supplies, entries, items, products, pots);
    }

    // Level

    [Fact]
    public void A_supply_with_no_entries_is_had()
    {
        var status = StatusOf(Leca());

        Assert.Equal(SupplyLevel.Have, status.Level);
        Assert.Null(status.Amount);
        Assert.Null(status.LastBought);
    }

    [Fact]
    public void The_latest_entry_decides_the_level_whatever_order_they_arrive_in()
    {
        var leca = Leca();
        var low = Entry(leca, SupplyEntryKind.RunningLow, 3);
        var bought = Entry(leca, SupplyEntryKind.Bought, 2);
        var gone = Entry(leca, SupplyEntryKind.Out, 1);

        Assert.Equal(SupplyLevel.Out, StatusOf(leca, low, bought, gone).Level);
        Assert.Equal(SupplyLevel.Out, StatusOf(leca, gone, bought, low).Level);
        var latest = Entry(leca, SupplyEntryKind.Bought, 0);
        Assert.Equal(SupplyLevel.Have, StatusOf(leca, gone, low, latest).Level);
    }

    [Fact]
    public void Entries_on_the_same_moment_are_told_apart_by_when_they_were_saved()
    {
        var leca = Leca();
        var first = Entry(leca, SupplyEntryKind.Out, 1, created: 1);
        var second = Entry(leca, SupplyEntryKind.Bought, 1, created: 2);

        Assert.Equal(SupplyLevel.Have, StatusOf(leca, first, second).Level);
        Assert.Equal(SupplyLevel.Have, StatusOf(leca, second, first).Level);
    }

    [Fact]
    public void A_purchase_entered_after_out_but_dated_before_it_still_means_had()
    {
        var leca = Leca();
        var gone = Entry(leca, SupplyEntryKind.Out, 0, created: 1);
        var bought = Entry(leca, SupplyEntryKind.Bought, 1, created: 2);

        Assert.Equal(SupplyLevel.Have, StatusOf(leca, gone, bought).Level);
    }

    [Fact]
    public void Counting_none_left_means_out_but_counting_some_means_had()
    {
        var leca = Leca();

        Assert.Equal(SupplyLevel.Out, StatusOf(leca, Entry(leca, SupplyEntryKind.Counted, 1, 0)).Level);
        Assert.Equal(SupplyLevel.Have, StatusOf(leca, Entry(leca, SupplyEntryKind.Counted, 1, 3)).Level);
    }

    [Fact]
    public void An_entry_of_a_kind_this_version_does_not_know_is_ignored()
    {
        var leca = Leca();
        var low = Entry(leca, SupplyEntryKind.RunningLow, 3);
        var unknown = Entry(leca, null, 1);

        Assert.Equal(SupplyLevel.Low, StatusOf(leca, low, unknown).Level);
    }

    [Fact]
    public void A_deleted_entry_is_ignored()
    {
        var leca = Leca();
        var low = Entry(leca, SupplyEntryKind.RunningLow, 3);
        var gone = Entry(leca, SupplyEntryKind.Out, 1);
        gone.DeletedAt = Now;

        Assert.Equal(SupplyLevel.Low, StatusOf(leca, low, gone).Level);
    }

    // Amount

    [Fact]
    public void The_amount_is_not_known_without_a_count()
    {
        var leca = Leca();

        Assert.Null(StatusOf(leca, Entry(leca, SupplyEntryKind.Bought, 2, 10), Entry(leca, SupplyEntryKind.RunningLow, 1)).Amount);
        Assert.Null(StatusOf(leca, Entry(leca, SupplyEntryKind.Counted, 2)).Amount);
    }

    [Fact]
    public void The_amount_is_the_count_plus_what_was_bought_after_it()
    {
        var leca = Leca();
        var before = Entry(leca, SupplyEntryKind.Bought, 5, 4);
        var count = Entry(leca, SupplyEntryKind.Counted, 3, 5);
        var after = Entry(leca, SupplyEntryKind.Bought, 1, 10);
        var noAmount = Entry(leca, SupplyEntryKind.Bought, 0);

        var status = StatusOf(leca, after, before, noAmount, count);

        Assert.Equal(15m, status.Amount);
        Assert.Equal(Today.AddDays(-3), status.CountedOn);
    }

    [Fact]
    public void A_later_count_replaces_the_earlier_one()
    {
        var leca = Leca();
        var first = Entry(leca, SupplyEntryKind.Counted, 5, 8);
        var bought = Entry(leca, SupplyEntryKind.Bought, 4, 2);
        var second = Entry(leca, SupplyEntryKind.Counted, 1, 6);

        Assert.Equal(6m, StatusOf(leca, first, bought, second).Amount);
    }

    [Fact]
    public void Out_or_a_count_without_an_amount_forgets_the_earlier_count()
    {
        var leca = Leca();
        var count = Entry(leca, SupplyEntryKind.Counted, 5, 8);
        var gone = Entry(leca, SupplyEntryKind.Out, 3);
        var haveIt = Entry(leca, SupplyEntryKind.Counted, 1);

        Assert.Null(StatusOf(leca, count, gone).Amount);
        Assert.Null(StatusOf(leca, count, gone, haveIt).Amount);
        Assert.Null(StatusOf(leca, count, haveIt).Amount);
    }

    [Fact]
    public void The_last_bought_line_comes_from_the_latest_bought_entry()
    {
        var leca = Leca();
        var seller = Guid.NewGuid();
        var old = Entry(leca, SupplyEntryKind.Bought, 20, 5);
        var recent = Entry(leca, SupplyEntryKind.Bought, 3, 10);
        recent.Price = 45;
        recent.SellerId = seller;

        var last = StatusOf(leca, recent, old).LastBought!;

        Assert.Equal((Today.AddDays(-3), 10m, 45m, seller), (last.Day, last.Amount, last.Price, last.SellerId));
    }

    // Names

    [Fact]
    public void A_supply_for_a_product_shows_the_products_name_while_the_product_exists()
    {
        var product = new Product { Name = "Dyna-Gro" };
        var supply = new Supply { Name = "Old name", ProductId = product.Id, Kind = SupplyKind.Water };

        Assert.Equal("Dyna-Gro", Assert.Single(View([supply], [], products: [product]).Supplies).Name);

        product.Name = "Dyna-Gro Grow";
        Assert.Equal("Dyna-Gro Grow", Assert.Single(View([supply], [], products: [product]).Supplies).Name);

        product.DeletedAt = Now;
        Assert.Equal("Old name", Assert.Single(View([supply], [], products: [product]).Supplies).Name);
        Assert.Equal("Old name", Assert.Single(View([supply], []).Supplies).Name);
    }

    [Fact]
    public void A_supply_needs_a_name_unless_it_is_a_product()
    {
        Assert.NotEmpty(new Supply().Validate());
        Assert.NotEmpty(new Supply { Name = "  " }.Validate());
        Assert.Empty(new Supply { Name = "LECA" }.Validate());
        Assert.Empty(new Supply { ProductId = Guid.NewGuid() }.Validate());
    }

    [Fact]
    public void A_missing_kind_reads_as_other_and_each_kind_has_a_default_unit()
    {
        Assert.Equal(SupplyKind.Other, new Supply().EffectiveKind);
        Assert.Equal(SupplyUnit.Litres, Supply.DefaultUnit(SupplyKind.Mixing));
        Assert.Equal(SupplyUnit.Millilitres, Supply.DefaultUnit(SupplyKind.Water));
        Assert.Equal(SupplyUnit.Pieces, Supply.DefaultUnit(SupplyKind.Pests));
        Assert.Equal(SupplyUnit.Pieces, Supply.DefaultUnit(SupplyKind.Other));
    }

    // The shopping list

    [Fact]
    public void The_list_has_what_is_out_before_what_is_low_then_loose_lines_newest_first()
    {
        var perlite = new Supply { Name = "Perlite" };
        var leca = Leca();
        var sphagnum = new Supply { Name = "Sphagnum" };
        var fine = new Supply { Name = "Bark" };
        var older = new ShoppingItem { Text = "Labels", CreatedAt = Now.AddDays(-2) };
        var newer = new ShoppingItem { Text = "Tape", CreatedAt = Now.AddDays(-1) };
        var entries = new[]
        {
            Entry(perlite, SupplyEntryKind.RunningLow, 1),
            Entry(leca, SupplyEntryKind.RunningLow, 1),
            Entry(sphagnum, SupplyEntryKind.Out, 1),
            Entry(fine, SupplyEntryKind.Bought, 1)
        };

        var view = View([perlite, leca, sphagnum, fine], entries, [older, newer]);

        Assert.Equal(["Sphagnum", "LECA", "Perlite", "Tape", "Labels"], view.ToBuy.Select(r => r.Name));
        Assert.Equal(3, view.RunningLow);
    }

    [Fact]
    public void A_deleted_supply_is_off_the_stock_and_the_list_and_entries_of_a_missing_supply_are_ignored()
    {
        var leca = Leca();
        leca.DeletedAt = Now;
        var stranger = Leca();

        var view = View([leca], [Entry(leca, SupplyEntryKind.Out, 1), Entry(stranger, SupplyEntryKind.Out, 1), Entry(stranger, SupplyEntryKind.Bought, 0)]);

        Assert.Empty(view.Supplies);
        Assert.Empty(view.ToBuy);
        Assert.Empty(view.BoughtToday);
    }

    [Fact]
    public void A_bought_line_shows_where_the_supply_was_last_bought()
    {
        var leca = Leca();
        var seller = Guid.NewGuid();
        var bought = Entry(leca, SupplyEntryKind.Bought, 10, 5);
        bought.SellerId = seller;
        var low = Entry(leca, SupplyEntryKind.RunningLow, 1);

        Assert.Equal(seller, Assert.Single(View([leca], [bought, low]).ToBuy).LastSellerId);
    }

    [Fact]
    public void A_pot_line_shows_the_pots_name_and_its_own_text_when_the_pot_is_gone()
    {
        var pot = new Pot { Name = "Clear nursery pot", TopCm = 9 };
        var line = new ShoppingItem { Text = pot.StoredName, PotId = pot.Id };

        Assert.Equal(pot.DisplayName, Assert.Single(View([], [], [line], pots: [pot]).ToBuy).Name);

        pot.Name = "Terracotta";
        Assert.Equal(pot.DisplayName, Assert.Single(View([], [], [line], pots: [pot]).ToBuy).Name);

        pot.DeletedAt = Now;
        Assert.Equal(line.Text, Assert.Single(View([], [], [line], pots: [pot]).ToBuy).Name);
        Assert.Equal(line.Text, Assert.Single(View([], [], [line]).ToBuy).Name);
    }

    [Fact]
    public void What_was_bought_today_is_its_own_part_and_older_days_are_not_shown()
    {
        var leca = Leca();
        var today = Entry(leca, SupplyEntryKind.Bought, 0, 5);
        var yesterday = Entry(leca, SupplyEntryKind.Bought, 1, 5);
        var boughtLine = new ShoppingItem { Text = "Tape", BoughtAt = Now };
        var oldLine = new ShoppingItem { Text = "Labels", BoughtAt = Now.AddDays(-1) };

        var view = View([leca], [today, yesterday], [boughtLine, oldLine]);

        Assert.Empty(view.ToBuy);
        Assert.Equal(2, view.BoughtToday.Count);
        Assert.Contains(view.BoughtToday, r => r.Entry?.Id == today.Id);
        Assert.Contains(view.BoughtToday, r => r.Item?.Id == boughtLine.Id);
    }

    [Fact]
    public async Task Ticking_a_supply_moves_it_to_bought_and_undo_puts_it_back()
    {
        var s = Create();
        var leca = Leca();
        await s.Supplies.SaveAsync(leca);
        await s.Service.MarkAsync(leca.Id, SupplyEntryKind.RunningLow, null, null, null, Now.AddHours(-3));

        Assert.Single((await s.Service.GetAsync()).ToBuy);

        var ticked = await s.Service.MarkAsync(leca.Id, SupplyEntryKind.Bought, null, null, null, Now);
        var view = await s.Service.GetAsync();
        Assert.Empty(view.ToBuy);
        Assert.Equal(ticked.Id, Assert.Single(view.BoughtToday).Entry?.Id);

        await s.Service.UndoAsync(ticked.Id);
        view = await s.Service.GetAsync();
        Assert.Equal(SupplyLevel.Low, Assert.Single(view.ToBuy).Level);
        Assert.Empty(view.BoughtToday);
        Assert.True(s.Entries.Entries[ticked.Id].IsDeleted);
    }

    [Fact]
    public async Task Price_and_seller_are_only_kept_on_a_bought_entry()
    {
        var s = Create();
        var leca = Leca();
        await s.Supplies.SaveAsync(leca);

        var low = await s.Service.MarkAsync(leca.Id, SupplyEntryKind.RunningLow, 3, 20, Guid.NewGuid(), Now);

        Assert.Equal((null, null, null), (low.Amount, low.Price, low.SellerId));
    }

    [Fact]
    public async Task Details_added_after_the_tick_are_saved_on_the_entry()
    {
        var s = Create();
        var leca = Leca();
        await s.Supplies.SaveAsync(leca);
        var ticked = await s.Service.MarkAsync(leca.Id, SupplyEntryKind.Bought, null, null, null, Now);
        var seller = Guid.NewGuid();

        await s.Service.UpdateBoughtAsync(ticked.Id, new BoughtDetails(Today.AddDays(-1), 10, 45, seller, false));

        var saved = s.Entries.Entries[ticked.Id];
        Assert.Equal((10m, 45m, seller, Today.AddDays(-1)), (saved.Amount, saved.Price, saved.SellerId, DayOf(saved.At)));
    }

    // Products

    [Fact]
    public async Task Marking_a_product_as_running_low_makes_its_supply_once_with_a_fixed_id()
    {
        var s = Create();
        var product = new Product { Name = "Dyna-Gro" };
        await s.Products.SaveAsync(product);

        await s.Service.RunningLowForProductAsync(product);
        await s.Service.RunningLowForProductAsync(product);

        var supply = Assert.Single(s.Supplies.Supplies.Values);
        Assert.Equal(Supply.IdForProduct(product.Id), supply.Id);
        Assert.Equal((product.Id, SupplyKind.Water, "Dyna-Gro"), (supply.ProductId, supply.Kind, supply.Name));
        Assert.Equal(2, s.Entries.Entries.Count);
        Assert.Equal(SupplyLevel.Low, Assert.Single((await s.Service.GetAsync()).Supplies).Level);
    }

    [Fact]
    public async Task Marking_a_product_as_running_low_brings_back_its_deleted_supply()
    {
        var s = Create();
        var product = new Product { Name = "Dyna-Gro" };
        await s.Products.SaveAsync(product);
        await s.Service.RunningLowForProductAsync(product);
        await s.Supplies.DeleteAsync(Supply.IdForProduct(product.Id));
        Assert.Empty((await s.Service.GetAsync()).Supplies);

        await s.Service.RunningLowForProductAsync(product);

        var supply = Assert.Single(s.Supplies.Supplies.Values);
        Assert.False(supply.IsDeleted);
        Assert.Equal(SupplyLevel.Low, Assert.Single((await s.Service.GetAsync()).Supplies).Level);
    }

    [Fact]
    public async Task Deleting_a_supply_deletes_its_entries_so_a_new_one_for_the_product_starts_fresh()
    {
        var s = Create();
        var product = new Product { Name = "Dyna-Gro" };
        await s.Products.SaveAsync(product);
        var id = Supply.IdForProduct(product.Id);
        await s.Service.RunningLowForProductAsync(product);
        await s.Service.MarkAsync(id, SupplyEntryKind.Counted, 500, null, null, Now);

        await s.Service.DeleteSupplyAsync(id);
        Assert.All(s.Entries.Entries.Values, e => Assert.True(e.IsDeleted));

        await s.Service.SaveSupplyAsync(new Supply { ProductId = product.Id });
        var status = Assert.Single((await s.Service.GetAsync()).Supplies);
        Assert.Equal((SupplyLevel.Have, (decimal?)null), (status.Level, status.Amount));
    }

    [Fact]
    public async Task Saving_a_supply_for_a_product_uses_the_fixed_id_and_the_products_name()
    {
        var s = Create();
        var product = new Product { Name = "  Dyna-Gro " };
        await s.Products.SaveAsync(product);

        await s.Service.SaveSupplyAsync(new Supply { ProductId = product.Id, Kind = SupplyKind.Pests, Unit = SupplyUnit.Millilitres });
        await s.Service.SaveSupplyAsync(new Supply { ProductId = product.Id, Unit = SupplyUnit.Millilitres });

        var supply = Assert.Single(s.Supplies.Supplies.Values);
        Assert.Equal((Supply.IdForProduct(product.Id), SupplyKind.Water, "Dyna-Gro"), (supply.Id, supply.Kind, supply.Name));
    }

    // Pots on the list

    [Fact]
    public async Task A_pot_goes_on_the_list_once_while_it_is_waiting_to_be_bought()
    {
        var s = Create();
        var pot = new Pot { Name = "Clear nursery pot", TopCm = 9 };
        await s.Pots.SaveAsync(pot);

        Assert.NotNull(await s.Service.AddPotAsync(pot));
        Assert.Null(await s.Service.AddPotAsync(pot));
        Assert.Single(s.Items.Items);

        await s.Service.TickAsync(Assert.Single(s.Items.Items.Values));
        Assert.NotNull(await s.Service.AddPotAsync(pot));
    }

    [Fact]
    public async Task Ticking_a_pot_line_with_an_amount_raises_owned_when_asked()
    {
        var s = Create();
        var pot = new Pot { Name = "Clear nursery pot", TopCm = 9, Owned = 2 };
        await s.Pots.SaveAsync(pot);
        var line = (await s.Service.AddPotAsync(pot))!;
        await s.Service.TickAsync(line);

        await s.Service.UpdateBoughtAsync(line, new BoughtDetails(Today, 4, 30, null, RaisePot: true));

        Assert.Equal(6, s.Pots.Pots[pot.Id].Owned);
        Assert.Equal(4m, s.Items.Items[line.Id].Amount);

        // Editing the details again doesn't count the same pots twice
        await s.Service.UpdateBoughtAsync(s.Items.Items[line.Id], new BoughtDetails(Today, 5, 30, null, RaisePot: true));
        Assert.Equal(6, s.Pots.Pots[pot.Id].Owned);
    }

    [Fact]
    public async Task Ticking_a_pot_line_leaves_owned_alone_when_not_asked_or_when_there_is_no_amount()
    {
        var s = Create();
        var pot = new Pot { Name = "Clear nursery pot", TopCm = 9, Owned = 2 };
        await s.Pots.SaveAsync(pot);
        var line = (await s.Service.AddPotAsync(pot))!;
        await s.Service.TickAsync(line);

        await s.Service.UpdateBoughtAsync(line, new BoughtDetails(Today, null, null, null, RaisePot: true));
        Assert.Equal(2, s.Pots.Pots[pot.Id].Owned);

        await s.Service.UpdateBoughtAsync(s.Items.Items[line.Id], new BoughtDetails(Today, 4, null, null, RaisePot: false));
        Assert.Equal(2, s.Pots.Pots[pot.Id].Owned);
    }

    [Fact]
    public async Task A_loose_line_is_ticked_and_unticked()
    {
        var s = Create();
        var line = (await s.Service.AddLineAsync("  Sticky traps "))!;
        Assert.Equal("Sticky traps", line.Text);
        Assert.Null(await s.Service.AddLineAsync("   "));

        await s.Service.TickAsync(line);
        var view = await s.Service.GetAsync();
        Assert.Empty(view.ToBuy);
        Assert.Single(view.BoughtToday);

        await s.Service.UntickAsync(s.Items.Items[line.Id]);
        view = await s.Service.GetAsync();
        Assert.Single(view.ToBuy);
        Assert.Empty(view.BoughtToday);
    }

    // Suggestions

    [Fact]
    public void Pest_suggestions_add_recipe_ingredients_without_repeats_and_leave_out_what_is_stocked()
    {
        var recipe = new TreatmentRecipe
        {
            Name = "Alcohol spray",
            Ingredients = [new RecipeIngredient { Name = "isopropyl alcohol" }, new RecipeIngredient { Name = "Dish soap" }]
        };

        var names = SupplySuggestions.For(SupplyKind.Pests, [], [recipe], ["Neem oil"]);

        Assert.Contains("Sticky traps", names);
        Assert.Contains("Dish soap", names);
        Assert.Single(names, n => n.Equals("Isopropyl alcohol", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("Neem oil", names);
        Assert.Empty(SupplySuggestions.For(SupplyKind.Other, [], [recipe], []));
        Assert.Contains("LECA", SupplySuggestions.For(SupplyKind.Mixing, [], [], []));
    }

    // Soil mixes

    [Fact]
    public void Cuttings_in_a_mix_are_counted_while_they_are_going_and_not_when_done_failed_or_deleted()
    {
        var mix = new SoilMix { Name = "Chunky soil" };
        var going = new Propagation { SoilMixId = mix.Id, Stage = PropagationStage.Rooting };
        var done = new Propagation { SoilMixId = mix.Id, Stage = PropagationStage.Done };
        var failed = new Propagation { SoilMixId = mix.Id, Stage = PropagationStage.Failed };
        var deleted = new Propagation { SoilMixId = mix.Id, Stage = PropagationStage.Started, DeletedAt = Now };
        var elsewhere = new Propagation { SoilMixId = Guid.NewGuid(), Stage = PropagationStage.Started };

        Assert.Equal(1, SoilMixService.CuttingsIn(mix.Id, [going, done, failed, deleted, elsewhere]));
        Assert.Equal(2, SoilMixService.CuttingsIn(mix.Id, [going, new Propagation { SoilMixId = mix.Id }, done]));
    }

    [Fact]
    public async Task The_mix_service_counts_cuttings_from_the_propagations()
    {
        var mix = new SoilMix { Name = "Chunky soil" };
        var propagations = new FakePropagationRepository();
        await propagations.SaveAsync(new Propagation { Nickname = "Pothos", SoilMixId = mix.Id });
        await propagations.SaveAsync(new Propagation { Nickname = "Old", SoilMixId = mix.Id, Stage = PropagationStage.Done });
        var service = new SoilMixService(new FakeSoilMixRepository(), new FakePlantRepository(), propagations);

        Assert.Equal(1, await service.CuttingsInAsync(mix.Id));
        Assert.Equal(0, await service.CuttingsInAsync(Guid.NewGuid()));
    }
}
