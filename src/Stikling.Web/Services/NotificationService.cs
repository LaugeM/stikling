using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Stikling.Core.Models;
using Stikling.Core.Notifications;
using Stikling.Core.Settings;
using Stikling.Core.Today;

namespace Stikling.Web.Services;

/// <summary>What turning notifications on came to.</summary>
public enum TurnOnResult
{
    Done,

    /// <summary>The key wasn't loaded yet. It is now, and the next tap asks for permission straight away.</summary>
    Ready,

    /// <summary>The person said no to the browser's question, or it is blocked for the site.</summary>
    Denied,

    /// <summary>The server has no keys to send with.</summary>
    NotSetUp,

    /// <summary>The browser or the server didn't take it, e.g. offline.</summary>
    Failed
}

/// <summary>What asking the server about notifications found.</summary>
public enum ServerCheck
{
    Available,
    NotSetUp,
    Unreachable
}

/// <param name="Sent">True when the test went out.</param>
/// <param name="Message">What to tell the person.</param>
public sealed record TestOutcome(bool Sent, string Message);

/// <summary>
/// The daily notification on this device: the browser's subscription (through notifications.js,
/// which only this class calls), keeping the server's copy of it, and uploading the text for the
/// coming days. Whether this device is on is device state. The time and the days belong to the
/// person and are in <c>UserSettings</c>. Nothing here throws at a page: a failed upload is logged
/// and tried again at the next trigger.
/// </summary>
public sealed class NotificationService(
    IJSRuntime js,
    DeviceFiles files,
    StiklingApi api,
    AccountService account,
    SettingsService settings,
    TodayService today,
    IndexedDb db,
    TimeProvider time,
    ILogger<NotificationService> logger) : IAsyncDisposable
{
    internal const string ModulePath = "./js/notifications.js";
    internal const string OnKey = "notifications-on";
    internal const string EndpointKey = "notifications-endpoint";
    internal const string UploadedKey = "notifications-uploaded";

    /// <summary>How long to wait after a change, so a few changes in a row go in one upload.</summary>
    private static readonly TimeSpan AfterChange = TimeSpan.FromSeconds(10);

    private readonly SemaphoreSlim uploading = new(1, 1);
    private Task<IJSObjectReference>? module;
    private CancellationTokenSource? waiting;
    private string? key;
    private bool started;

    private Task<IJSObjectReference> Module =>
        module ??= js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();

    /// <summary>"ok", "needs-home-screen" or "unsupported".</summary>
    public async Task<string> SupportAsync()
    {
        try
        {
            return await (await Module).InvokeAsync<string>("support");
        }
        catch (JSException)
        {
            return "unsupported";
        }
    }

    /// <summary>The browser's permission for this site: "granted", "denied" or "default".</summary>
    public async Task<string> PermissionAsync()
    {
        try
        {
            return await (await Module).InvokeAsync<string>("permission");
        }
        catch (JSException)
        {
            return "denied";
        }
    }

    public async Task<bool> IsOnAsync() => await files.GetAsync(OnKey) is not null;

    /// <summary>
    /// Asks the server whether it can send notifications, and keeps its key, so Turn on can ask for
    /// permission straight from the tap without waiting for the server first.
    /// </summary>
    public async Task<ServerCheck> CheckServerAsync()
    {
        try
        {
            key = await api.GetNotificationKeyAsync();
            return key is null ? ServerCheck.NotSetUp : ServerCheck.Available;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return ServerCheck.Unreachable;
        }
    }

    /// <summary>
    /// Asks for permission, subscribes this device and tells the server. Only to be called from the
    /// tap on Turn on.
    /// </summary>
    public async Task<TurnOnResult> TurnOnAsync()
    {
        try
        {
            if (key is null)
            {
                // Not followed by the prompt, since the wait can use up the browser's allowance for a tap
                var check = await CheckServerAsync();
                if (check != ServerCheck.Available)
                    return check == ServerCheck.NotSetUp ? TurnOnResult.NotSetUp : TurnOnResult.Failed;
                return TurnOnResult.Ready;
            }

            var subscription = await SubscribeAsync(key!);
            if (subscription is { Reason: "denied" })
                return TurnOnResult.Denied;
            if (subscription is not { Endpoint: { Length: > 0 } endpoint })
                return TurnOnResult.Failed;

            try
            {
                await api.PutSubscriptionAsync(new SubscriptionRequest(endpoint, subscription.P256dh, subscription.Auth));
            }
            catch (Exception e) when (e is HttpRequestException or AccountUnavailableException or JSException or TaskCanceledException)
            {
                logger.LogWarning(e, "The server didn't take this device's notification subscription");
                await UnsubscribeAsync();
                return TurnOnResult.Failed;
            }

            await settings.EnsureNotifyAtAsync();
            await files.SetAsync(OnKey, "yes");
            await files.SetAsync(EndpointKey, endpoint);
            await files.SetAsync(UploadedKey, null);
            await UploadAsync(force: true);
            return TurnOnResult.Done;
        }
        catch (JSException e)
        {
            logger.LogWarning(e, "Turning notifications on failed");
            return TurnOnResult.Failed;
        }
    }

    /// <summary>
    /// Turns notifications off on this device: removes its subscription from the server and from the
    /// browser. The time stays in the settings and the schedule on the server is left alone, since
    /// the person's other devices may still be on. When the server can't be reached the device is
    /// still turned off, and the server drops the subscription the first time the push service
    /// says it is gone.
    /// </summary>
    /// <param name="fromServer">False when there is no sign-in to ask the server with.</param>
    public async Task TurnOffAsync(bool fromServer = true)
    {
        var endpoint = await files.GetAsync(EndpointKey);
        if (fromServer && endpoint is not null && await account.WasSignedInAsync())
        {
            try
            {
                await api.RemoveSubscriptionAsync(endpoint);
            }
            catch (Exception e) when (e is HttpRequestException or AccountUnavailableException or JSException or TaskCanceledException)
            {
                logger.LogWarning(e, "The server didn't remove this device's notification subscription");
            }
        }

        await UnsubscribeAsync();
        await files.SetAsync(OnKey, null);
        await files.SetAsync(EndpointKey, null);
        await files.SetAsync(UploadedKey, null);
    }

    /// <summary>Sends a test notification to this device. If the server has lost the subscription, it is made again once.</summary>
    public async Task<TestOutcome> SendTestAsync()
    {
        const string TryLater = "Couldn't send a test right now. Try again in a moment.";
        const string TurnOffAndOn = "Couldn't send a test. Turn notifications off and on again.";
        var sent = new TestOutcome(true, "Sent. It should arrive in a moment.");

        try
        {
            var endpoint = await files.GetAsync(EndpointKey);
            var status = endpoint is null ? HttpStatusCode.NotFound : await api.SendNotificationTestAsync(endpoint);

            if (status is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                // Permission is already given, so this asks nothing. A fresh subscription, since the old one may be dead
                if (await ResubscribeAsync() is not { } renewed)
                    return new TestOutcome(false, TurnOffAndOn);

                return await api.SendNotificationTestAsync(renewed) == HttpStatusCode.NoContent
                    ? sent
                    : new TestOutcome(false, TurnOffAndOn);
            }

            return status == HttpStatusCode.NoContent ? sent : new TestOutcome(false, TryLater);
        }
        catch (Exception e) when (e is HttpRequestException or AccountUnavailableException or JSException or TaskCanceledException)
        {
            logger.LogWarning(e, "Sending a test notification failed");
            return new TestOutcome(false, TryLater);
        }
    }

    /// <summary>
    /// Works out the text for the coming days and sends it to the server, when this device is on and
    /// someone is signed in. A payload the same as the last one sent today is skipped unless
    /// <paramref name="force"/>. Never throws.
    /// </summary>
    public async Task UploadAsync(bool force = false)
    {
        await uploading.WaitAsync();
        try
        {
            if (!await IsOnAsync() || !await account.WasSignedInAsync())
                return;

            var current = await settings.GetAsync();
            if (current.NotifyAt is not { } notifyAt)
                return;

            var data = await today.LoadAsync(TodayNotice.DaysAhead);
            var days = NoticeUpload.Days(TodayNotice.Days(data, time), current);
            var request = new DaysRequest(
                await TimeZoneAsync(), notifyAt.ToString("HH:mm", CultureInfo.InvariantCulture), [.. days]);

            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
            var stamp = $"{time.Today():yyyy-MM-dd}|{hash}";
            if (!force && stamp == await files.GetAsync(UploadedKey))
                return;

            await api.PutNotificationDaysAsync(request);
            await files.SetAsync(UploadedKey, stamp);
        }
        catch (Exception e) when (e is HttpRequestException or AccountUnavailableException or JSException or TaskCanceledException)
        {
            logger.LogWarning(e, "Uploading the notification text failed, it is tried again later");
        }
        catch (Exception e)
        {
            // Nothing waits for this, so it mustn't stop the app
            logger.LogError(e, "Uploading the notification text failed");
        }
        finally
        {
            uploading.Release();
        }
    }

    /// <summary>
    /// Called once the app is on screen. Uploads after changes made here, and sends the server the
    /// new address if the browser has swapped this device's (Firefox can).
    /// </summary>
    public async Task StartAsync()
    {
        if (started)
            return;

        started = true;
        db.Changed += OnLocalChange;

        try
        {
            if (!await IsOnAsync() || !await account.WasSignedInAsync())
                return;

            var stored = await files.GetAsync(EndpointKey);
            var now = await (await Module).InvokeAsync<SubscriptionInfo?>("currentSubscription");
            if (now is null)
            {
                // The browser dropped or expired it (Firefox does). With permission still given this asks nothing
                if (await PermissionAsync() == "granted")
                    await ResubscribeAsync();
            }
            else if (now is { Endpoint: { Length: > 0 } endpoint } && endpoint != stored)
            {
                await api.PutSubscriptionAsync(new SubscriptionRequest(endpoint, now.P256dh, now.Auth));
                await files.SetAsync(EndpointKey, endpoint);
                if (stored is not null)
                    await api.RemoveSubscriptionAsync(stored);
            }
        }
        catch (Exception e) when (e is HttpRequestException or AccountUnavailableException or JSException or TaskCanceledException)
        {
            logger.LogWarning(e, "Checking this device's notification subscription failed");
        }

        await UploadAsync();
    }

    private async void OnLocalChange()
    {
        waiting?.Cancel();
        var mine = waiting = new CancellationTokenSource();
        try
        {
            await Task.Delay(AfterChange, mine.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        await UploadAsync();
    }

    private async Task<string> TimeZoneAsync() =>
        await (await Module).InvokeAsync<string>("timeZone");

    private async Task<SubscriptionInfo?> SubscribeAsync(string publicKey) =>
        await (await Module).InvokeAsync<SubscriptionInfo?>("subscribe", publicKey);

    private async Task UnsubscribeAsync()
    {
        try
        {
            await (await Module).InvokeAsync<bool>("unsubscribe");
        }
        catch (JSException e)
        {
            logger.LogWarning(e, "The browser didn't drop the notification subscription");
        }
    }

    // A new subscription for this device, saved on the server and here. The address, or null if it didn't work
    private async Task<string?> ResubscribeAsync()
    {
        if (key is null)
            await CheckServerAsync();
        if (key is null)
            return null;

        await UnsubscribeAsync();
        if (await SubscribeAsync(key) is not { Endpoint: { Length: > 0 } endpoint } subscription)
            return null;

        await api.PutSubscriptionAsync(new SubscriptionRequest(endpoint, subscription.P256dh, subscription.Auth));
        await files.SetAsync(EndpointKey, endpoint);
        return endpoint;
    }

    public async ValueTask DisposeAsync()
    {
        db.Changed -= OnLocalChange;
        waiting?.Cancel();
        if (module is { IsCompletedSuccessfully: true })
            await module.Result.DisposeAsync();
    }

    private sealed record SubscriptionInfo(string? Endpoint, string? P256dh, string? Auth, string? Reason);
}
