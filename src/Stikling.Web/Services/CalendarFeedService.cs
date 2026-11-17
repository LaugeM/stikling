using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Stikling.Core.Calendar;
using Stikling.Core.Models;
using Stikling.Core.Settings;
using Stikling.Core.Today;

namespace Stikling.Web.Services;

/// <summary>What asking the server about the calendar link found.</summary>
public enum CalendarStatus
{
    Off,
    On,
    Unreachable
}

/// <summary>
/// The person's calendar link as this device sees it: making and turning it off, and uploading the
/// days for it. The server only shows the link when it is made, so the device that made it keeps
/// the address in <see cref="DeviceFiles"/> to show it again. Whether the link is on belongs to the
/// person and is in <c>UserSettings.CalendarFeed</c>, so each of their devices uploads. Nothing here
/// throws at a page: a failed upload is logged and tried again at the next trigger.
/// </summary>
public sealed class CalendarFeedService(
    DeviceFiles files,
    StiklingApi api,
    AccountService account,
    SettingsService settings,
    TodayService today,
    IndexedDbSyncStore store,
    NavigationManager navigation,
    TimeProvider time,
    ILogger<CalendarFeedService> logger)
{
    internal const string LinkKey = "calendar-link";
    internal const string UploadedKey = "calendar-uploaded";

    private readonly SemaphoreSlim uploading = new(1, 1);

    /// <summary>Asks the server whether the person has a link on.</summary>
    public async Task<CalendarStatus> StatusAsync()
    {
        try
        {
            return await api.GetCalendarStatusAsync() ? CalendarStatus.On : CalendarStatus.Off;
        }
        catch (Exception e) when (e is HttpRequestException or AccountUnavailableException or JSException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(e, "Asking the server about the calendar link failed");
            return CalendarStatus.Unreachable;
        }
    }

    /// <summary>The address of the link, when this device is the one that made it.</summary>
    public Task<string?> LinkOnThisDeviceAsync() => files.GetAsync(LinkKey);

    /// <summary>
    /// Makes a new link (the old one stops working), keeps its address on this device, turns the
    /// calendar on for the person and uploads the days. The address, or null when it didn't work.
    /// </summary>
    public async Task<string?> MakeLinkAsync()
    {
        try
        {
            var link = await api.NewCalendarLinkAsync();
            await files.SetAsync(LinkKey, link);
            await settings.SetCalendarFeedAsync(true);
            await files.SetAsync(UploadedKey, null);
            await UploadAsync(force: true);
            return link;
        }
        catch (Exception e) when (e is HttpRequestException or AccountUnavailableException or JSException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(e, "Making a calendar link failed");
            return null;
        }
    }

    /// <summary>Turns the link off on the server, so it answers 404 at once. False when the server couldn't be reached.</summary>
    public async Task<bool> TurnOffAsync()
    {
        try
        {
            await api.TurnOffCalendarAsync();
        }
        catch (Exception e) when (e is HttpRequestException or AccountUnavailableException or JSException or TaskCanceledException)
        {
            logger.LogWarning(e, "Turning off the calendar link failed");
            return false;
        }

        await ForgetDeviceAsync();
        await settings.SetCalendarFeedAsync(false);
        return true;
    }

    /// <summary>
    /// Works out the days and sends them to the server, when someone is signed in and the person has
    /// a link on. A payload the same as the last one sent today is skipped unless
    /// <paramref name="force"/>. Never throws.
    /// </summary>
    public async Task UploadAsync(bool force = false)
    {
        await uploading.WaitAsync();
        try
        {
            if (!await account.WasSignedInAsync())
                return;

            var current = await settings.GetAsync();
            if (!current.CalendarFeed || (await store.GetStateAsync())?.CollectionId is not { } collectionId)
                return;

            var data = await today.LoadAsync(TodayNotice.DaysAhead);
            var days = CalendarFeed.Days(data, time, navigation.BaseUri, current.CalendarLeaveOut);
            var request = new CalendarDaysRequest([.. days]);

            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
            var stamp = $"{time.Today():yyyy-MM-dd}|{collectionId}|{hash}";
            if (!force && stamp == await files.GetAsync(UploadedKey))
                return;

            // False when the link was turned off on another device. The next status check shows it
            if (await api.PutCalendarDaysAsync(collectionId, request))
                await files.SetAsync(UploadedKey, stamp);
        }
        catch (Exception e) when (e is HttpRequestException or AccountUnavailableException or JSException or TaskCanceledException)
        {
            logger.LogWarning(e, "Uploading the calendar days failed, it is tried again later");
        }
        catch (Exception e)
        {
            // Nothing waits for this, so it mustn't stop the app
            logger.LogError(e, "Uploading the calendar days failed");
        }
        finally
        {
            uploading.Release();
        }
    }

    /// <summary>
    /// Forgets what this device kept about the link: its address and the last upload. The server is
    /// left alone, since the link is the person's and signing out here doesn't turn it off.
    /// </summary>
    public async Task ForgetDeviceAsync()
    {
        await files.SetAsync(LinkKey, null);
        await files.SetAsync(UploadedKey, null);
    }
}
