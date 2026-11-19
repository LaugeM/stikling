using System.Security.Cryptography;
using System.Text;

namespace Stikling.Api.Collections;

/// <summary>
/// How an invite's token is kept. Its SHA-256 finds the invite, and the token itself is encrypted with
/// AES-GCM under <c>Invites:TokenKey</c>, so people who manage the collection can be shown the link again
/// while a copy of the database alone gives nobody a way in. The invite's id is bound into the encryption,
/// so a token can't be moved to another row.
/// </summary>
public sealed class InviteTokens
{
    public const string KeySetting = "Invites:TokenKey";

    /// <summary>The first byte of what is stored, so a later way of encrypting can be told apart.</summary>
    private const byte Version = 1;

    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public InviteTokens(IConfiguration configuration)
    {
        var setting = configuration[KeySetting];
        if (string.IsNullOrWhiteSpace(setting))
            throw new InvalidOperationException($"{KeySetting} is not set. The API needs it to keep invite links, see docs/ops/hosting.md.");

        byte[] key;
        try
        {
            key = Convert.FromBase64String(setting);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException($"{KeySetting} is not base64. It should be 32 random bytes as base64.");
        }

        if (key.Length != 32)
            throw new InvalidOperationException($"{KeySetting} is {key.Length} bytes. It should be 32 random bytes as base64.");
        _key = key;
    }

    /// <summary>SHA-256 of the token, as 64 lowercase hex characters. The migration computes the same in SQL.</summary>
    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(token)));

    /// <summary>Whether a hash read from the database is the one asked for, compared in constant time.</summary>
    public static bool HashMatches(string stored, string computed) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(stored), Encoding.ASCII.GetBytes(computed));

    /// <summary>The version byte, the nonce, the tag and the ciphertext, in that order.</summary>
    public byte[] Encrypt(Guid inviteId, string token)
    {
        var plain = Encoding.ASCII.GetBytes(token);
        var stored = new byte[1 + NonceSize + TagSize + plain.Length];
        stored[0] = Version;
        var nonce = stored.AsSpan(1, NonceSize);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, stored.AsSpan(1 + NonceSize + TagSize), stored.AsSpan(1 + NonceSize, TagSize), inviteId.ToByteArray());
        return stored;
    }

    /// <summary>
    /// The token again. Throws when it was encrypted with another key or for another invite, so a wrong
    /// key is an error and never a link that quietly goes missing.
    /// </summary>
    public string Decrypt(Guid inviteId, byte[] stored)
    {
        if (stored.Length <= 1 + NonceSize + TagSize || stored[0] != Version)
            throw new CryptographicException($"Invite {inviteId} has a token stored in a form this version can't read.");

        var plain = new byte[stored.Length - 1 - NonceSize - TagSize];
        using var aes = new AesGcm(_key, TagSize);
        try
        {
            aes.Decrypt(
                stored.AsSpan(1, NonceSize), stored.AsSpan(1 + NonceSize + TagSize), stored.AsSpan(1 + NonceSize, TagSize), plain,
                inviteId.ToByteArray());
        }
        catch (AuthenticationTagMismatchException e)
        {
            throw new CryptographicException(
                $"The token of invite {inviteId} can't be read with {KeySetting}. The key isn't the one the invites were encrypted with.", e);
        }

        return Encoding.ASCII.GetString(plain);
    }
}
