using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Stikling.Api.Data;
using Stikling.Core.Sync;

namespace Stikling.Api.People;

public static class SettingsEndpoints
{
    /// <summary>
    /// <c>/me/settings</c>: the caller's own settings record from the app. GET gives it, or 204
    /// when none has been sent yet. PUT sends one, and the newest version is kept and returned.
    /// </summary>
    public static IEndpointRouteBuilder MapSettings(this IEndpointRouteBuilder app)
    {
        var settings = app.MapGroup("/me/settings").RequireAuthorization();

        settings.MapGet("", async (CurrentPerson current, StiklingDbContext db) =>
        {
            var person = await current.FindOrCreateAsync();
            var data = await db.PersonSettings
                .Where(s => s.PersonId == person.Id)
                .Select(s => s.Data)
                .SingleOrDefaultAsync();

            return data is null ? Results.NoContent() : Results.Text(data, "application/json");
        });

        settings.MapPut("", async (JsonElement data, CurrentPerson current, StiklingDbContext db, TimeProvider clock) =>
        {
            if (SyncRules.Problem(data, clock.GetUtcNow()) is { } problem)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["settings"] = [problem] });

            RecordStamp.TryRead(data, out var stamp);
            var person = await current.FindOrCreateAsync();

            try
            {
                return Results.Text(await KeepNewestAsync(db, person.Id, stamp, data.GetRawText()), "application/json");
            }
            catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
            {
                // Two devices sent the first settings at the same moment. The other one is saved now
                db.ChangeTracker.Clear();
                return Results.Text(await KeepNewestAsync(db, person.Id, stamp, data.GetRawText()), "application/json");
            }
        });

        return app;
    }

    /// <summary>Saves the settings if they are newer than the ones here, and returns whichever is kept.</summary>
    private static async Task<string> KeepNewestAsync(StiklingDbContext db, Guid personId, RecordStamp stamp, string data)
    {
        var current = await db.PersonSettings.SingleOrDefaultAsync(s => s.PersonId == personId);
        if (current is null)
            db.PersonSettings.Add(current = new PersonSettings { PersonId = personId, UpdatedAt = stamp.UpdatedAt, Data = data });
        else if (SyncRules.Replaces(stamp, data, new RecordStamp(personId, current.UpdatedAt, null), current.Data))
        {
            current.UpdatedAt = stamp.UpdatedAt;
            current.Data = data;
        }

        await db.SaveChangesAsync();
        return current.Data;
    }
}
