using Stikling.Core.Feedback;

namespace Stikling.Core.Tests;

public class FeedbackLinksTests
{
    private const string Line = "Firefox 133 on Android · Stikling 2026.11.22 (abc1234) · da";

    [Fact]
    public void Without_a_question_the_forms_get_the_version_line_and_no_title()
    {
        var links = FeedbackLinks.Create("2026.11.22 (abc1234)", "Firefox 133 on Android", "da");

        Assert.StartsWith("https://github.com/LaugeM/stikling/issues/new?template=bug_report.yml", links.Bug);
        Assert.StartsWith("https://github.com/LaugeM/stikling/issues/new?template=feature_request.yml", links.Idea);
        Assert.DoesNotContain("title=", links.Bug);
        Assert.DoesNotContain("title=", links.Idea);
        Assert.Contains("&device=" + Uri.EscapeDataString(Line), links.Bug);
        Assert.Contains("&what=" + Uri.EscapeDataString("\n\n" + Line), links.Idea);
    }

    [Fact]
    public void A_question_goes_into_the_title_and_the_what_field()
    {
        var links = FeedbackLinks.Create("2026.11.22 (abc1234)", "Firefox 133 on Android", "da", "Can I move a plant?");

        Assert.Contains("&title=" + Uri.EscapeDataString("Can I move a plant?"), links.Bug);
        Assert.Contains("&what=" + Uri.EscapeDataString("About \"Can I move a plant?\": ") + "&device=", links.Bug);
        Assert.Contains("&title=" + Uri.EscapeDataString("Can I move a plant?"), links.Idea);
        Assert.Contains("&what=" + Uri.EscapeDataString("About \"Can I move a plant?\": \n\n" + Line), links.Idea);
    }

    [Fact]
    public void The_mail_has_a_subject_and_two_blank_lines_before_the_version_line()
    {
        var plain = FeedbackLinks.Create("v", "B", "en");
        var asked = FeedbackLinks.Create("v", "B", "en", "What is this?");

        Assert.StartsWith("mailto:support@stikling.app?subject=Stikling&body=", plain.Mail);
        Assert.EndsWith("&body=" + Uri.EscapeDataString("\n\nB · Stikling v · en"), plain.Mail);
        Assert.Contains("subject=" + Uri.EscapeDataString("What is this?"), asked.Mail);
    }

    [Fact]
    public void Everything_is_escaped_so_a_question_cannot_add_parameters()
    {
        var links = FeedbackLinks.Create("v", "B", "en", "Why & how? #1 =x");

        foreach (var url in new[] { links.Bug, links.Idea, links.Mail })
        {
            Assert.DoesNotContain("Why & how", url);
            Assert.DoesNotContain(" ", url);
            Assert.DoesNotContain("#", url);
        }
        Assert.Contains(Uri.EscapeDataString("Why & how? #1 =x"), links.Bug);
    }

    [Fact]
    public void Only_the_known_parameters_are_used()
    {
        var links = FeedbackLinks.Create("v", "B", "en", "Q");

        Assert.Equal(["template", "title", "what", "device"], Names(links.Bug));
        Assert.Equal(["template", "title", "what"], Names(links.Idea));
        Assert.Equal(["subject", "body"], Names(links.Mail));
    }

    private static string[] Names(string url) =>
        url[(url.IndexOf('?') + 1)..].Split('&').Select(part => part[..part.IndexOf('=')]).ToArray();
}
