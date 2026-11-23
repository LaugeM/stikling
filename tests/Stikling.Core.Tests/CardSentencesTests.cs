using System.Globalization;
using Stikling.Core.Care;

namespace Stikling.Core.Tests;

public class CardSentencesTests
{
    private static void In(string culture, Action test)
    {
        var was = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            test();
        }
        finally
        {
            CultureInfo.CurrentUICulture = was;
        }
    }

    private static string Round(string name = "Monstera", string? place = null, WateringForm? form = WateringForm.TopWatered,
        int? watered = 6, int? reading = null, (int, int)? last = null, string? due = null, string? hand = null,
        bool dormant = false, bool looked = false, bool photo = false) =>
        CardSentences.GoRoundCard(name, place, form, watered, reading, last, due, hand, dormant, looked, photo);

    [Fact]
    public void GoRoundSaysNameRoomAndLastWatering()
    {
        Assert.Equal("Monstera, living room, watered 6 days ago", Round(place: "Living room").Replace("Living room", "living room"));
    }

    [Fact]
    public void GoRoundSaysTodayAndYesterday()
    {
        Assert.Equal("Monstera, watered today", Round(watered: 0));
        Assert.Equal("Monstera, watered yesterday", Round(watered: 1));
    }

    [Fact]
    public void GoRoundSaysNotWateredYet()
    {
        Assert.Equal("Monstera, not watered yet", Round(watered: null));
        Assert.Equal("Fern, not refilled yet", Round("Fern", form: WateringForm.Reservoir, watered: null));
        Assert.Equal("Fern, not topped up yet", Round("Fern", form: WateringForm.InWater, watered: null));
    }

    [Fact]
    public void GoRoundUsesTheKindOfWatering()
    {
        Assert.Equal("Fern, refilled 3 days ago", Round("Fern", form: WateringForm.Reservoir, watered: 3));
        Assert.Equal("Fern, topped up 3 days ago", Round("Fern", form: WateringForm.InWater, watered: 3));
    }

    [Fact]
    public void GoRoundLeavesOutTheWateringLineForACutting()
    {
        Assert.Equal("Cutting, Kitchen", Round("Cutting", "Kitchen", form: null, watered: null));
    }

    [Fact]
    public void GoRoundSaysTodaysReadingOrElseTheLastOne()
    {
        Assert.Equal("Monstera, watered 6 days ago, reads 4", Round(reading: 4, last: (7, 2)));
        Assert.Equal("Monstera, watered 6 days ago, last reading 7, 2 days ago", Round(last: (7, 2)));
    }

    [Fact]
    public void GoRoundSaysWhatIsLoggedToday()
    {
        Assert.Equal(
            "Monstera, watered today, due now, dormant, looked over today, photo added today",
            Round(watered: 0, due: "due now", dormant: true, looked: true, photo: true));
    }

    [Fact]
    public void GoRoundSkipsEmptyParts()
    {
        Assert.Equal("Monstera, needs a hand, watered 6 days ago", Round(place: " ", hand: "needs a hand", due: ""));
    }

    [Fact]
    public void GoRoundInDanish()
    {
        In("da", () =>
        {
            Assert.Equal("Monstera, Stue, vandet for 6 dage siden", Round(place: "Stue"));
            Assert.Equal("Monstera, vandet i dag, målt til 4", Round(watered: 0, reading: 4));
            Assert.Equal("Monstera, vandet i går, seneste måling 7, i går", Round(watered: 1, last: (7, 1)));
            Assert.Equal("Monstera, ikke vandet endnu", Round(watered: null));
            Assert.Equal("Bregne, fyldt på for 3 dage siden", Round("Bregne", form: WateringForm.Reservoir, watered: 3));
            Assert.Equal("Bregne, ikke fyldt op endnu", Round("Bregne", form: WateringForm.InWater, watered: null));
            Assert.Equal("Monstera, vandet i dag, hviler, tjekket i dag, billede tilføjet i dag",
                Round(watered: 0, dormant: true, looked: true, photo: true));
        });
    }

    [Fact]
    public void WaterRowSaysTheLines()
    {
        Assert.Equal("Monstera, dry, watered 3 days ago", CardSentences.WaterRow("Monstera", null, "dry", null, "watered 3 days ago"));
    }

    [Fact]
    public void WaterRowSaysWhatWasLoggedToday()
    {
        Assert.Equal("Monstera, watered with Feed today", CardSentences.WaterRow("Monstera", "Watered with Feed", "ignored"));
    }

    [Fact]
    public void WaterRowInDanish()
    {
        In("da", () =>
            Assert.Equal("Monstera, vandet i dag", CardSentences.WaterRow("Monstera", "Vandet")));
    }

    [Fact]
    public void TodayWaterRowSaysTheLine()
    {
        Assert.Equal("Monstera, every 7 days, 9 days ago", CardSentences.TodayWaterRow("Monstera", "every 7 days, 9 days ago"));
    }

    [Fact]
    public void CareRowSaysLastAndFirstTime()
    {
        Assert.Equal("Monstera, last 12 days ago", CardSentences.CareRow("Monstera", 12));
        Assert.Equal("Monstera, last yesterday", CardSentences.CareRow("Monstera", 1));
        Assert.Equal("Monstera, first time", CardSentences.CareRow("Monstera", null));
    }

    [Fact]
    public void CareRowSaysWhenItIsDueOnAComingUpCard()
    {
        Assert.Equal("Monstera, due today, last 12 days ago", CardSentences.CareRow("Monstera", 12, 0));
        Assert.Equal("Monstera, due tomorrow, first time", CardSentences.CareRow("Monstera", null, 1));
        Assert.Equal("Monstera, due in 4 days, last 3 days ago", CardSentences.CareRow("Monstera", 3, 4));
    }

    [Fact]
    public void CareRowInDanish()
    {
        In("da", () =>
        {
            Assert.Equal("Monstera, skal gøres om 4 dage, sidst for 3 dage siden", CardSentences.CareRow("Monstera", 3, 4));
            Assert.Equal("Monstera, første gang", CardSentences.CareRow("Monstera", null));
        });
    }
}
