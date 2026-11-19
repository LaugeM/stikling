using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Stikling.Api.Collections;

namespace Stikling.Api.Data;

/// <summary>
/// Brings the database up to date on start, and checks that the invite key fits the invites that are there.
/// Most of it is EF Core's migrations. Invite tokens were kept in plain text until EncryptInviteTokens, and
/// encrypting them needs the key, which SQL doesn't have. So the migrations stop after EncryptInviteTokens,
/// the tokens are encrypted here, and then DropInvitePlainTokens drops the plain column, which it refuses
/// to do while a token is left in it.
/// </summary>
public static class DatabaseStartup
{
    private const string EncryptMigration = "EncryptInviteTokens";
    private const string DropMigration = "DropInvitePlainTokens";

    public static async Task MigrateAsync(StiklingDbContext db, InviteTokens tokens, TimeProvider clock)
    {
        var applied = await db.Database.GetAppliedMigrationsAsync();
        if (!applied.Any(id => id.EndsWith($"_{DropMigration}", StringComparison.Ordinal)))
        {
            await db.GetService<IMigrator>().MigrateAsync(EncryptMigration);
            await EncryptPlainTokensAsync(db, tokens);
        }

        await db.Database.MigrateAsync();
        await CheckKeyAsync(db, tokens, clock);
    }

    private sealed record PlainToken(Guid Id, string Token);

    /// <summary>
    /// Encrypts the tokens still in the plain column and empties it, row by row in one transaction. Only runs
    /// between the two migrations, while the column is there. The hash is set again from C#, though the
    /// migration computed the same in SQL.
    /// </summary>
    public static async Task EncryptPlainTokensAsync(StiklingDbContext db, InviteTokens tokens)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var plain = await db.Database
            .SqlQuery<PlainToken>($"SELECT Id, Token FROM Invites WHERE Token IS NOT NULL")
            .ToListAsync();

        foreach (var row in plain)
        {
            var hash = InviteTokens.Hash(row.Token);
            var ciphertext = tokens.Encrypt(row.Id, row.Token);
            await db.Database.ExecuteSqlAsync(
                $"UPDATE Invites SET TokenHash = {hash}, TokenCiphertext = {ciphertext}, Token = NULL WHERE Id = {row.Id}");
        }

        await transaction.CommitAsync();
    }

    /// <summary>
    /// Reads the newest open invite's token with the key, so a key that was changed or mistyped stops the API
    /// from starting, instead of every Show link failing later. Closed invites are never shown, so they aren't
    /// checked: withdrawing the open ones is the way out when the key is lost (docs/ops/hosting.md).
    /// </summary>
    public static async Task CheckKeyAsync(StiklingDbContext db, InviteTokens tokens, TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        var newest = await db.Invites.AsNoTracking()
            .Where(i => i.UsedAt == null && i.RevokedAt == null && i.ExpiresAt > now)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new { i.Id, i.TokenHash, i.TokenCiphertext })
            .FirstOrDefaultAsync();
        if (newest is null)
            return;

        string token;
        try
        {
            token = tokens.Decrypt(newest.Id, newest.TokenCiphertext);
        }
        catch (CryptographicException e)
        {
            throw new InvalidOperationException(
                $"{InviteTokens.KeySetting} doesn't fit the open invites in the database. It must be the key they were made with. See docs/ops/hosting.md.", e);
        }

        if (!InviteTokens.HashMatches(newest.TokenHash, InviteTokens.Hash(token)))
            throw new InvalidOperationException($"Invite {newest.Id} has a token that doesn't match its hash.");
    }
}
