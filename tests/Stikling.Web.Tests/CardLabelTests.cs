using Bunit;
using Microsoft.AspNetCore.Components;
using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Today;
using Stikling.Web.Components;

namespace Stikling.Web.Tests;

public class CardLabelTests : BunitContext
{
    private static readonly DateOnly Today = new(2026, 11, 23);

    private static Plant Monstera => new() { Nickname = "Monstera" };

    public CardLabelTests() => this.AddTileServices();

    [Fact]
    public void GoRoundCardIsNamedBySentenceAndTakesFocus()
    {
        var cut = Render<RoundCardFrame>(p => p
            .Add(c => c.Name, "Monstera")
            .Add(c => c.Place, "Living room")
            .Add(c => c.Form, WateringForm.TopWatered)
            .Add(c => c.LastWateredDaysAgo, 6)
            .Add(c => c.ChildContent, "<h2>Monstera</h2>"));

        var card = cut.Find("article");
        Assert.Equal("Monstera, Living room, watered 6 days ago", card.GetAttribute("aria-label"));
        Assert.Equal("-1", card.GetAttribute("tabindex"));
    }

    [Fact]
    public void GoRoundCardSaysWhatIsLoggedToday()
    {
        var cut = Render<RoundCardFrame>(p => p
            .Add(c => c.Name, "Monstera")
            .Add(c => c.Form, WateringForm.TopWatered)
            .Add(c => c.LastWateredDaysAgo, 0)
            .Add(c => c.ReadingToday, 4)
            .Add(c => c.LookedOverToday, true));

        Assert.Equal("Monstera, watered today, reads 4, looked over today", cut.Find("article").GetAttribute("aria-label"));
    }

    [Fact]
    public void WaterRowGroupIsNamedBySentence()
    {
        var cut = Render<PassRowBody>(p => p
            .Add(c => c.Name, "Monstera")
            .Add(c => c.Lines, ["Dry, reads 2", "watered 3 days ago"])
            .Add(c => c.ChildContent, "<a href=\"plants/1\">Monstera</a>"));

        var group = cut.Find("[role=group]");
        Assert.Equal("Monstera, Dry, reads 2, watered 3 days ago", group.GetAttribute("aria-label"));
        Assert.Null(group.GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void WaterRowGroupSaysWhatWasLoggedToday()
    {
        var cut = Render<PassRowBody>(p => p
            .Add(c => c.Name, "Monstera")
            .Add(c => c.DoneText, "Watered with Feed"));

        Assert.Equal("Monstera, watered with Feed today", cut.Find("[role=group]").GetAttribute("aria-label"));
    }

    [Fact]
    public void TodayWaterRowLinkIsNamedBySentence()
    {
        var guess = new WateringGuess(WateringForm.TopWatered, WateringMode.Interval, Today.AddDays(-9), 9, 7, 4, 0, null, null, false, false);
        var plant = Monstera;
        var cut = Render<WaterDueRow>(p => p
            .Add(c => c.Due, new WateringDue(plant, guess, true, Today, false))
            .Add(c => c.Today, Today)
            .Add(c => c.OnDone, EventCallback.Factory.Create(this, () => { }))
            .Add(c => c.OnAnswer, EventCallback.Factory.Create<int?>(this, _ => { }))
            .Add(c => c.OnPutOff, EventCallback.Factory.Create<int>(this, _ => { })));

        var link = cut.Find("a.item-card");
        Assert.Equal($"Monstera, {cut.Find(".item-meta").TextContent.Trim()}", link.GetAttribute("aria-label"));
        Assert.Equal(3, cut.FindAll("button.row-btn").Count);
    }

    [Fact]
    public void CareRowLinkIsNamedBySentence()
    {
        var plant = Monstera;
        var interval = new CareInterval { Kind = CareKind.Fertilised };
        var item = new IntervalItem(interval, plant, Today.AddDays(-12), Today, Today, null, "Fertilise");
        var group = new IntervalGroup(CareKind.Fertilised, null, null, "Fertilise", interval, [item]);

        var cut = Render<CareIntervalCard>(p => p
            .Add(c => c.Group, group)
            .Add(c => c.Today, Today)
            .Add(c => c.Done, new Dictionary<(Guid, Guid), CareAction>()));

        Assert.Equal("Monstera, last 12 days ago", cut.Find("a.item-card").GetAttribute("aria-label"));
    }

    [Fact]
    public void CareRowOnAComingUpCardSaysWhenItIsDue()
    {
        var plant = Monstera;
        var interval = new CareInterval { Kind = CareKind.Fertilised };
        var item = new IntervalItem(interval, plant, null, Today.AddDays(1), Today, null, "Fertilise");
        var group = new IntervalGroup(CareKind.Fertilised, null, null, "Fertilise", interval, [item]);

        var cut = Render<CareIntervalCard>(p => p
            .Add(c => c.Group, group)
            .Add(c => c.Today, Today)
            .Add(c => c.Coming, true)
            .Add(c => c.Done, new Dictionary<(Guid, Guid), CareAction>()));

        Assert.Equal("Monstera, due tomorrow, first time", cut.Find("a.item-card").GetAttribute("aria-label"));
    }
}
