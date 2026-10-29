using Stikling.Core.Settings;

namespace Stikling.Core.Tests;

public class AppLanguageTests
{
    [Fact]
    public void A_chosen_language_that_is_offered_wins_over_the_browser()
    {
        Assert.Equal("da", AppLanguages.Resolve("da", ["en-GB"], includeInProgress: true));
        Assert.Equal("en", AppLanguages.Resolve("en", ["da-DK"], includeInProgress: true));
    }

    [Fact]
    public void The_first_browser_language_that_is_offered_is_used()
    {
        Assert.Equal("da", AppLanguages.Resolve(null, ["de-DE", "da-DK", "en"], includeInProgress: true));
        Assert.Equal("en", AppLanguages.Resolve(null, ["en-US", "da"], includeInProgress: true));
    }

    [Fact]
    public void A_region_is_ignored_when_matching_the_browser()
    {
        Assert.Equal("da", AppLanguages.Resolve(null, ["da-DK"], includeInProgress: true));
        Assert.Equal("da", AppLanguages.Resolve(null, ["DA_dk"], includeInProgress: true));
    }

    [Fact]
    public void English_is_used_when_nothing_matches()
    {
        Assert.Equal("en", AppLanguages.Resolve(null, [], includeInProgress: true));
        Assert.Equal("en", AppLanguages.Resolve("xx", ["de", "fr-CA"], includeInProgress: true));
        Assert.Equal("en", AppLanguages.Resolve("", [""], includeInProgress: true));
    }

    [Fact]
    public void A_language_still_being_translated_is_only_used_when_asked_for()
    {
        var progress = AppLanguages.All.Where(l => !l.Ready).Select(l => l.Code).ToList();

        // Empty once every language is ready, until a new one is added
        foreach (var code in progress)
        {
            Assert.Equal("en", AppLanguages.Resolve(code, [code + "-XX"], includeInProgress: false));
            Assert.DoesNotContain(AppLanguages.Offered(includeInProgress: false), l => l.Code == code);
            Assert.Contains(AppLanguages.Offered(includeInProgress: true), l => l.Code == code);
        }
    }

    [Fact]
    public void A_ready_language_is_offered_to_everyone()
    {
        Assert.Contains(AppLanguages.Offered(includeInProgress: false), l => l.Code == "en");
        Assert.Contains(AppLanguages.Offered(includeInProgress: false), l => l.Code == "da");
        Assert.All(AppLanguages.Offered(includeInProgress: false), l => Assert.True(l.Ready));
    }

    [Fact]
    public void English_stays_en_GB_and_Danish_is_da_DK()
    {
        Assert.Equal("en-GB", AppLanguages.CultureOf("en"));
        Assert.Equal("da-DK", AppLanguages.CultureOf("da"));
        Assert.Equal("en-GB", AppLanguages.CultureOf("unknown"));
    }

    [Fact]
    public void Every_language_is_named_in_its_own_language()
    {
        Assert.Equal("Dansk", AppLanguages.All.Single(l => l.Code == "da").NativeName);
        Assert.Equal("English", AppLanguages.All.Single(l => l.Code == "en").NativeName);
        Assert.True(AppLanguages.All.Single(l => l.Code == "en").Ready);
    }
}
