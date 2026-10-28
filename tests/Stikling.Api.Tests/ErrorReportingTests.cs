using Sentry;
using Sentry.Protocol;
using Stikling.Api.Reports;

namespace Stikling.Api.Tests;

public class ErrorReportingTests
{
    [Fact]
    public void Reports_hold_no_user_headers_cookies_or_query()
    {
        var e = new SentryEvent
        {
            User = new SentryUser { Id = "person", Email = "a@example.com", IpAddress = "203.0.113.5" },
            Request = new SentryRequest
            {
                Url = "https://api.stikling.app/collections/1?token=secret#x",
                QueryString = "token=secret",
                Cookies = "session=abc",
            },
        };
        e.Request.Headers["Authorization"] = "Bearer secret";
        e.Request.Env["REMOTE_ADDR"] = "203.0.113.5";

        var scrubbed = ErrorReporting.Scrub(e, new SentryHint());

        Assert.NotNull(scrubbed);
        Assert.Null(scrubbed.User.Id);
        Assert.Null(scrubbed.User.Email);
        Assert.Null(scrubbed.User.IpAddress);
        Assert.Equal("https://api.stikling.app/collections/1", scrubbed.Request.Url);
        Assert.True(string.IsNullOrEmpty(scrubbed.Request.QueryString));
        Assert.True(string.IsNullOrEmpty(scrubbed.Request.Cookies));
        Assert.Empty(scrubbed.Request.Headers);
        Assert.False(scrubbed.Request.Env.ContainsKey("REMOTE_ADDR"));
    }

    [Fact]
    public void Reports_keep_the_kind_of_error_but_not_its_message()
    {
        var e = new SentryEvent
        {
            SentryExceptions = [new SentryException { Type = "System.InvalidOperationException", Value = "Mum's flat still has something in it." }],
            Message = new SentryMessage { Message = "Removing {Place} failed", Formatted = "Removing Mum's flat failed" },
        };

        var scrubbed = ErrorReporting.Scrub(e, new SentryHint());

        var exception = Assert.Single(scrubbed!.SentryExceptions!);
        Assert.Equal("System.InvalidOperationException", exception.Type);
        Assert.Null(exception.Value);
        Assert.Equal("Removing {Place} failed", scrubbed.Message!.Message);
        Assert.Null(scrubbed.Message.Formatted);
    }
}
