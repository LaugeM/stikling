using Stikling.Core.Models;
using Stikling.Core.Pests;
using Stikling.Core.Rooms;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class TreatmentPlanTests
{
    private static readonly DateOnly Today = new(2026, 9, 20);
    private static readonly DateOnly Started = new(2026, 9, 10);

    private static TreatmentPlan Plan() => new()
    {
        Name = "Mites",
        Steps =
        [
            new PlanStep { What = "Alcohol spray" },
            new PlanStep { DaysAfter = 3, What = "Alcohol spray again" },
            new PlanStep { DaysAfter = 10, What = "Predatory mites" }
        ]
    };

    // A case that started the plan on 10 September
    private static PestCase Running(int interval = 4)
    {
        var item = new PestCase { IntervalDays = interval, StartedOn = Started.AddDays(-1) };
        item.StartPlan(Plan(), Started);
        return item;
    }

    private static PestTreatment ForStep(PestCase item, int step, DateOnly on, bool skipped = false) =>
        new() { CaseId = item.Id, OccurredOn = on, PlanStepId = item.PlanSteps[step].Id, Skipped = skipped };

    private static DateOnly? Due(PestCase item, IEnumerable<PestTreatment> treatments, PutOffs? putOffs = null) =>
        PestService.NextDue(item, treatments, putOffs);

    private static PestCaseView View(PestCase item, IEnumerable<PestTreatment> treatments, PutOffs? putOffs = null) =>
        PestService.Describe(item, [], treatments, Places.None, Today, putOffs: putOffs);

    // Due days

    [Fact]
    public void The_first_step_is_due_on_the_day_the_plan_starts()
    {
        var item = Running();

        Assert.Equal(Started, Due(item, []));
        var view = View(item, []);
        Assert.Equal((1, 3), (view.StepNumber, view.StepCount));
        Assert.Equal("Alcohol spray", view.Step?.What);
    }

    [Fact]
    public void The_next_step_counts_from_the_day_the_one_before_was_logged()
    {
        var item = Running();

        // Logged two days late, so the next one follows from then and not from the start day
        Assert.Equal(Started.AddDays(5), Due(item, [ForStep(item, 0, Started.AddDays(2))]));
    }

    [Fact]
    public void A_skipped_step_counts_as_done_and_the_next_one_follows_from_the_skip()
    {
        var item = Running();
        var skip = ForStep(item, 0, Started, skipped: true);

        Assert.Equal(Started.AddDays(3), Due(item, [skip]));
        Assert.Equal(2, View(item, [skip]).StepNumber);
    }

    [Fact]
    public void A_moved_step_is_due_on_the_day_it_was_moved_to()
    {
        var item = Running();
        var treatments = new[] { ForStep(item, 0, Started) };
        var putOffs = PutOffs.From([new PutOff { Key = PutOffs.PlanStep(item.Id, item.PlanSteps[1].Id), Until = Today.AddDays(4) }], Today);

        Assert.Equal(Today.AddDays(4), Due(item, treatments, putOffs));
        Assert.Equal(4, View(item, treatments, putOffs).DaysUntilDue);
    }

    [Fact]
    public void A_step_moved_to_a_day_that_has_passed_since_is_still_due_that_day()
    {
        var item = Running();
        var moved = Today.AddDays(-2);
        var putOffs = PutOffs.From([new PutOff { Key = PutOffs.PlanStep(item.Id, item.PlanSteps[0].Id), Until = moved }], Today);

        Assert.Equal(moved, Due(item, [], putOffs));
    }

    [Fact]
    public void An_extra_treatment_does_not_move_the_plan_on()
    {
        var item = Running();
        var extra = new PestTreatment { CaseId = item.Id, OccurredOn = Started.AddDays(1), What = "Soap" };
        var first = ForStep(item, 0, Started);

        Assert.Equal(Started, Due(item, [extra]));
        Assert.Equal(Started.AddDays(3), Due(item, [first, extra]));
    }

    [Fact]
    public void A_deleted_treatment_does_not_finish_a_step()
    {
        var item = Running();
        var gone = ForStep(item, 0, Started);
        gone.DeletedAt = DateTimeOffset.UtcNow;

        Assert.Equal(Started, Due(item, [gone]));
    }

    // The end of the plan

    private static List<PestTreatment> AllDone(PestCase item) =>
    [
        ForStep(item, 0, Started),
        ForStep(item, 1, Started.AddDays(3)),
        ForStep(item, 2, Started.AddDays(13))
    ];

    [Fact]
    public void Plan_done_is_due_an_interval_after_the_last_step()
    {
        var item = Running(interval: 5);

        var view = PestService.Describe(item, [], AllDone(item), Places.None, Started.AddDays(17));
        Assert.Equal(Started.AddDays(18), view.PlanDoneOn);
        Assert.False(view.PlanDone);

        view = PestService.Describe(item, [], AllDone(item), Places.None, Started.AddDays(18));
        Assert.True(view.PlanDone);
    }

    [Fact]
    public void A_finished_plan_is_not_a_treatment_due()
    {
        var item = Running();
        var view = PestService.Describe(item, [], AllDone(item), Places.None, Started.AddDays(30));

        Assert.Null(view.NextDue);
        Assert.True(view.PlanDone);
        Assert.Empty(PestService.Due([view]));
    }

    [Fact]
    public void Plan_done_waits_for_the_last_step_even_when_the_first_ones_are_old()
    {
        var item = Running();
        var treatments = AllDone(item).Take(2).ToList();

        var view = PestService.Describe(item, [], treatments, Places.None, Today);

        Assert.Null(view.PlanDoneOn);
        Assert.False(view.PlanDone);
        Assert.Equal(Started.AddDays(13), view.NextDue);
    }

    [Fact]
    public void Keep_treating_goes_on_at_the_interval_from_the_last_real_treatment()
    {
        var item = Running(interval: 4);
        var treatments = AllDone(item);
        item.PlanStoppedOn = Today;

        Assert.Equal(Started.AddDays(17), Due(item, treatments));
        Assert.False(item.PlanRunning);
    }

    // Skips are not treatments

    [Fact]
    public void A_skip_is_not_the_last_treatment_and_does_not_set_the_next_one()
    {
        var item = new PestCase { IntervalDays = 4, StartedOn = Started };
        var real = new PestTreatment { CaseId = item.Id, OccurredOn = Started.AddDays(2), What = "Soap" };
        var skip = new PestTreatment { CaseId = item.Id, OccurredOn = Started.AddDays(6), PlanStepId = Guid.NewGuid(), Skipped = true };

        var view = View(item, [real, skip]);

        Assert.Equal(real, view.Last);
        Assert.Equal(Started.AddDays(6), view.NextDue);
    }

    [Fact]
    public void A_skip_does_not_restart_the_clean_count()
    {
        var item = new PestCase { Status = PestCaseStatus.Monitoring, StartedOn = Started };
        var real = new PestTreatment { CaseId = item.Id, OccurredOn = Started.AddDays(2), What = "Soap" };
        var skip = new PestTreatment { CaseId = item.Id, OccurredOn = Started.AddDays(8), PlanStepId = Guid.NewGuid(), Skipped = true };

        Assert.Equal(Started.AddDays(2), PestService.CleanSince(item, [real, skip]));
    }

    // Starting

    [Fact]
    public void Starting_a_plan_copies_the_steps_with_fresh_ids()
    {
        var plan = Plan();
        var item = new PestCase();

        item.StartPlan(plan, Started);

        Assert.Equal(plan.Id, item.PlanId);
        Assert.Equal("Mites", item.PlanName);
        Assert.Equal(plan.Steps.Select(s => s.What), item.PlanSteps.Select(s => s.What));
        Assert.Equal(plan.Steps.Select(s => s.DaysAfter), item.PlanSteps.Select(s => s.DaysAfter));
        Assert.Empty(item.PlanSteps.Select(s => s.Id).Intersect(plan.Steps.Select(s => s.Id)));

        // Editing the plan afterwards leaves the case alone, and starting it again matches nothing old
        plan.Steps[0].What = "Changed";
        Assert.Equal("Alcohol spray", item.PlanSteps[0].What);
        var before = item.PlanSteps.Select(s => s.Id).ToList();
        item.StartPlan(plan, Today);
        Assert.Empty(before.Intersect(item.PlanSteps.Select(s => s.Id)));
    }

    [Fact]
    public void Copying_a_case_copies_the_steps()
    {
        var item = Running();
        var copy = item.Copy();

        copy.PlanSteps[0].What = "Other";

        Assert.Equal("Alcohol spray", item.PlanSteps[0].What);
    }

    [Fact]
    public void A_step_started_from_the_plan_uses_the_recipe_or_else_the_text()
    {
        var recipe = new TreatmentRecipe { Name = "Neem", Method = "Spray", Ingredients = [new RecipeIngredient { Name = "Neem oil" }] };
        var item = new PestCase();
        item.StartPlan(new TreatmentPlan { Name = "P", Steps = [new PlanStep { What = "Neem", RecipeId = recipe.Id }, new PlanStep { DaysAfter = 3, What = "Wipe" }] }, Started);

        var withRecipe = PlanProgress.StartStep(item, item.PlanSteps[0], Today, [recipe]);
        Assert.Equal((recipe.Id, "Neem", item.PlanSteps[0].Id), (withRecipe.RecipeId, withRecipe.What, withRecipe.PlanStepId));
        Assert.Single(withRecipe.Ingredients);

        // A deleted recipe falls back to the name the step kept
        recipe.DeletedAt = DateTimeOffset.UtcNow;
        var without = PlanProgress.StartStep(item, item.PlanSteps[0], Today, [recipe]);
        Assert.Null(without.RecipeId);
        Assert.Equal("Neem", without.What);
        Assert.Empty(without.Validate(Today));
    }

    // Stopping

    [Fact]
    public void Leaving_treatment_stops_a_running_plan()
    {
        var item = Running();
        item.Status = PestCaseStatus.Monitoring;

        PestService.StatusChanged(item, PestCaseStatus.Active, Today, []);

        Assert.Equal(Today, item.PlanStoppedOn);
    }

    [Fact]
    public void A_finished_plan_is_not_stopped_by_starting_to_watch_but_stops_running()
    {
        var item = Running();
        var treatments = AllDone(item);
        item.Status = PestCaseStatus.Monitoring;

        PestService.StatusChanged(item, PestCaseStatus.Active, Today, treatments);

        Assert.Null(item.PlanStoppedOn);
        Assert.False(item.PlanRunning);
        Assert.Null(View(item, treatments).PlanDoneOn);
    }

    [Fact]
    public void Treating_again_after_a_finished_plan_does_not_ask_again()
    {
        var item = Running();
        var treatments = AllDone(item);
        item.Status = PestCaseStatus.Monitoring;
        PestService.StatusChanged(item, PestCaseStatus.Active, Today.AddDays(-5), treatments);

        item.Status = PestCaseStatus.Active;
        PestService.StatusChanged(item, PestCaseStatus.Monitoring, Today, treatments);

        Assert.Equal(Today, item.PlanStoppedOn);
        Assert.False(View(item, treatments).PlanDone);
    }

    [Fact]
    public void A_stopped_plan_never_resumes()
    {
        var item = Running();
        item.PlanStoppedOn = Today;
        item.Status = PestCaseStatus.Monitoring;
        PestService.StatusChanged(item, PestCaseStatus.Active, Today.AddDays(1), []);
        item.Status = PestCaseStatus.Active;
        PestService.StatusChanged(item, PestCaseStatus.Monitoring, Today.AddDays(2), []);

        Assert.False(item.PlanRunning);
        Assert.Null(View(item, []).Step);
    }

    // Validation

    [Fact]
    public void A_good_plan_is_valid()
    {
        Assert.Empty(Plan().Validate());
    }

    [Fact]
    public void A_plan_needs_a_name_and_a_step()
    {
        var plan = new TreatmentPlan();

        Assert.Equal(["Give the plan a name.", "Add at least one step."], plan.Validate());
    }

    [Fact]
    public void Steps_after_the_first_come_at_least_a_day_later()
    {
        var plan = Plan();
        plan.Steps[2].DaysAfter = 0;
        Assert.Contains("A step after the first comes at least a day later.", plan.Validate());

        plan = Plan();
        plan.Steps[0].DaysAfter = 2;
        Assert.Contains("A step after the first comes at least a day later.", plan.Validate());
    }

    [Fact]
    public void Every_step_says_what_it_uses_by_text_or_recipe()
    {
        var plan = Plan();
        plan.Steps[1].What = "  ";
        Assert.Contains("Say what each step uses.", plan.Validate());

        plan.Steps[1].RecipeId = Guid.NewGuid();
        Assert.Empty(plan.Validate());
    }

    [Fact]
    public void Steps_move_up_and_down()
    {
        var plan = Plan();

        plan.MoveDown(0);
        Assert.Equal("Alcohol spray again", plan.Steps[0].What);
        plan.MoveUp(2);
        Assert.Equal("Predatory mites", plan.Steps[1].What);
        plan.MoveUp(0);
        plan.MoveDown(2);
        Assert.Equal(3, plan.Steps.Count);
    }

    [Fact]
    public void A_skipped_step_only_has_a_date_and_notes()
    {
        var stepId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var ok = new PestTreatment { CaseId = caseId, OccurredOn = Today, PlanStepId = stepId, Skipped = true, Notes = "No time" };
        Assert.Empty(ok.Validate(Today));

        const string error = "A skipped step only has a date and notes.";
        Assert.Contains(error, new PestTreatment { CaseId = caseId, OccurredOn = Today, Skipped = true }.Validate(Today));
        Assert.Contains(error, new PestTreatment { CaseId = caseId, OccurredOn = Today, PlanStepId = stepId, Skipped = true, What = "Spray" }.Validate(Today));
        Assert.Contains(error, new PestTreatment { CaseId = caseId, OccurredOn = Today, PlanStepId = stepId, Skipped = true, NextDueOn = Today.AddDays(2) }.Validate(Today));
        Assert.Contains(error, new PestTreatment { CaseId = caseId, OccurredOn = Today, PlanStepId = stepId, Skipped = true, Kind = PestTreatmentKind.Checked }.Validate(Today));
    }

    [Fact]
    public void A_plan_step_is_only_allowed_on_a_treatment()
    {
        var check = new PestTreatment { CaseId = Guid.NewGuid(), OccurredOn = Today, Kind = PestTreatmentKind.Checked, PlanStepId = Guid.NewGuid() };

        Assert.NotEmpty(check.Validate(Today));
    }
}
