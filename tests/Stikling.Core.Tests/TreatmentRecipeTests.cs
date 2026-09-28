using Stikling.Core.Models;
using Stikling.Core.Pests;

namespace Stikling.Core.Tests;

public class TreatmentRecipeTests
{
    private static readonly DateOnly Today = new(2026, 9, 28);

    private static TreatmentRecipe AlcoholSpray() => new()
    {
        Name = "Alcohol spray",
        Ingredients =
        [
            new RecipeIngredient { Name = "Isopropyl alcohol", Amount = 250 },
            new RecipeIngredient { Name = "Dish soap", Amount = 2, Unit = DoseUnit.Drops, Per = 500, PerUnit = WaterUnit.Millilitres }
        ],
        Method = "Both sides of the leaves"
    };

    private static PestTreatment Treated(PestCase item, DateOnly on, TreatmentRecipe? recipe = null)
    {
        var treatment = new PestTreatment { CaseId = item.Id, OccurredOn = on };
        if (recipe is not null)
            treatment.Use(recipe);
        return treatment;
    }

    // Measuring out a batch

    [Fact]
    public void An_amount_is_worked_out_for_the_size_of_the_bottle()
    {
        var recipe = AlcoholSpray();

        Assert.Equal(125m, recipe.Ingredients[0].For(0.5m));
        Assert.Equal(1m, recipe.Ingredients[1].For(0.25m));
    }

    [Fact]
    public void An_ingredient_without_an_amount_has_nothing_to_measure()
    {
        var soap = new RecipeIngredient { Name = "Dish soap" };

        Assert.Null(soap.For(1));
        Assert.Equal("Dish soap", soap.ToString());
    }

    [Fact]
    public void An_ingredient_reads_the_way_it_was_typed()
    {
        var recipe = AlcoholSpray();

        Assert.Equal("Isopropyl alcohol, 250 ml/L", recipe.Ingredients[0].ToString());
        Assert.Equal("Dish soap, 2 drops per 500 ml", recipe.Ingredients[1].ToString());
    }

    // Validation

    [Fact]
    public void A_recipe_needs_a_name()
    {
        Assert.Contains("Give the recipe a name.", new TreatmentRecipe().Validate());
    }

    [Fact]
    public void A_recipe_with_only_a_name_is_fine()
    {
        Assert.Empty(new TreatmentRecipe { Name = "Shower" }.Validate());
    }

    [Fact]
    public void Every_ingredient_needs_a_name()
    {
        var recipe = AlcoholSpray();
        recipe.Ingredients.Add(new RecipeIngredient { Amount = 5 });

        Assert.Contains("Every ingredient needs a name.", recipe.Validate());
    }

    [Fact]
    public void An_amount_cannot_be_negative_or_for_no_mix_at_all()
    {
        var recipe = AlcoholSpray();
        recipe.Ingredients[0].Amount = -1;
        recipe.Ingredients[1].Per = 0;

        var errors = recipe.Validate();

        Assert.Contains("An amount can't be less than 0.", errors);
        Assert.Contains("What an amount is for has to be more than 0.", errors);
    }

    [Fact]
    public void Ingredients_move_up_and_down_and_stop_at_the_ends()
    {
        var recipe = AlcoholSpray();

        recipe.MoveUp(0);
        Assert.Equal("Isopropyl alcohol", recipe.Ingredients[0].Name);

        recipe.MoveDown(0);
        Assert.Equal(["Dish soap", "Isopropyl alcohol"], recipe.Ingredients.Select(i => i.Name));
    }

    [Fact]
    public void Editing_a_copy_leaves_the_recipe_alone()
    {
        var recipe = AlcoholSpray();

        var copy = recipe.Copy();
        copy.Ingredients[0].Amount = 300;
        copy.Ingredients.RemoveAt(1);

        Assert.Equal(250m, recipe.Ingredients[0].Amount);
        Assert.Equal(2, recipe.Ingredients.Count);
    }

    // Using a recipe on a treatment

    [Fact]
    public void A_treatment_keeps_the_recipe_as_it_was_that_day()
    {
        var recipe = AlcoholSpray();
        var treatment = Treated(new PestCase(), Today, recipe);

        recipe.Name = "Stronger alcohol spray";
        recipe.Ingredients[0].Amount = 400;
        recipe.Method = "Soak it";

        Assert.Equal(recipe.Id, treatment.RecipeId);
        Assert.Equal("Alcohol spray", treatment.What);
        Assert.Equal(250m, treatment.Ingredients[0].Amount);
        Assert.Equal("Both sides of the leaves", treatment.Method);
    }

    [Fact]
    public void A_new_treatment_offers_the_last_recipe_as_it_is_now()
    {
        var item = new PestCase { StartedOn = Today.AddDays(-10) };
        var recipe = AlcoholSpray();
        var last = Treated(item, Today.AddDays(-4), recipe);
        recipe.Ingredients[0].Amount = 300;

        var next = PestService.StartTreatment(item, [last], Today, [recipe]);

        Assert.Equal(recipe.Id, next.RecipeId);
        Assert.Equal(300m, next.Ingredients[0].Amount);
    }

    [Fact]
    public void A_deleted_recipe_is_not_offered_again()
    {
        var item = new PestCase { StartedOn = Today.AddDays(-10) };
        var recipe = AlcoholSpray();
        var last = Treated(item, Today.AddDays(-4), recipe);
        recipe.DeletedAt = DateTimeOffset.UtcNow;

        var next = PestService.StartTreatment(item, [last], Today, [recipe]);

        Assert.Null(next.RecipeId);
        Assert.Null(next.What);
        Assert.Empty(next.Ingredients);
    }

    [Fact]
    public void A_check_cannot_carry_a_recipe()
    {
        var check = PestService.StartCheck(new PestCase(), Today);
        check.Use(AlcoholSpray());

        Assert.Contains("A check only has a date and notes.", check.Validate(Today));
    }

    [Fact]
    public void Copying_a_treatment_does_not_share_its_ingredients()
    {
        var treatment = Treated(new PestCase { Id = Guid.NewGuid() }, Today, AlcoholSpray());

        var copy = treatment.Copy();
        copy.Ingredients.Clear();

        Assert.Equal(2, treatment.Ingredients.Count);
    }

    // How much each recipe is used

    [Fact]
    public void Use_counts_the_treatments_made_with_each_recipe()
    {
        var item = new PestCase();
        var spray = AlcoholSpray();
        var neem = new TreatmentRecipe { Name = "neem drench" };
        var unused = new TreatmentRecipe { Name = "Insecticidal soap" };
        var deleted = Treated(item, Today, spray);
        deleted.DeletedAt = DateTimeOffset.UtcNow;

        var uses = RecipeUse.List(
            [spray, neem, unused],
            [Treated(item, Today.AddDays(-8), spray), Treated(item, Today.AddDays(-4), spray), Treated(item, Today.AddDays(-2), neem), deleted]);

        Assert.Equal(["Alcohol spray", "Insecticidal soap", "neem drench"], uses.Select(u => u.Recipe.Name));
        Assert.Equal(2, uses[0].Times);
        Assert.Equal(Today.AddDays(-4), uses[0].LastUsed);
        Assert.Equal(0, uses[1].Times);
        Assert.Null(uses[1].LastUsed);
    }

    [Fact]
    public void Suggestions_add_the_ingredients_already_used_without_repeats()
    {
        var recipe = AlcoholSpray();
        recipe.Ingredients.Add(new RecipeIngredient { Name = "Rain water" });
        recipe.Ingredients.Add(new RecipeIngredient { Name = "dish soap" });

        var suggestions = RecipeIngredients.Suggestions([recipe]);

        Assert.Contains("Rain water", suggestions);
        Assert.Single(suggestions, s => s.Equals("Dish soap", StringComparison.OrdinalIgnoreCase));
    }
}
