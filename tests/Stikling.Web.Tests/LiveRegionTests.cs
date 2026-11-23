using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Stikling.Web.Components;
using Stikling.Web.Services;

namespace Stikling.Web.Tests;

public class LiveRegionTests : BunitContext
{
    private readonly Announcer announcer = new();

    public LiveRegionTests() => Services.AddSingleton(announcer);

    [Fact]
    public void IsAPoliteStatusRegionThatIsEmptyAtFirst()
    {
        var cut = Render<LiveRegion>();

        var region = cut.Find("[role=status]");
        Assert.Equal("polite", region.GetAttribute("aria-live"));
        Assert.Contains("visually-hidden", region.ClassName);
        Assert.Equal("", region.TextContent);
    }

    [Fact]
    public void ReadsOutWhatAnnouncerSays()
    {
        var cut = Render<LiveRegion>();

        announcer.Say("Monstera watered");

        cut.WaitForAssertion(() => Assert.Equal("Monstera watered", cut.Find("[role=status]").TextContent));
    }

    [Fact]
    public void ReadsOutTheSameTextAgainByClearingItFirst()
    {
        var cut = Render<LiveRegion>();
        var seen = new List<string>();
        cut.OnMarkupUpdated += (_, _) => seen.Add(cut.Find("[role=status]").TextContent);

        announcer.Say("Monstera watered");
        cut.WaitForAssertion(() => Assert.Equal("Monstera watered", cut.Find("[role=status]").TextContent));
        // The second time the text goes empty and back, which is what makes a screen reader read it again
        announcer.Say("Monstera watered");
        cut.WaitForAssertion(() => Assert.Equal(["Monstera watered", "", "Monstera watered"], seen));
    }

    [Fact]
    public void IgnoresEmptyText()
    {
        var cut = Render<LiveRegion>();

        announcer.Say(null);
        announcer.Say("  ");

        Assert.Equal("", cut.Find("[role=status]").TextContent);
    }

    [Fact]
    public void StopsListeningWhenItIsRemoved()
    {
        var cut = Render<LiveRegion>();
        cut.Dispose();

        announcer.Say("Too late");
    }
}
