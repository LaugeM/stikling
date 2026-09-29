using System.Security.Cryptography;
using System.Text;

namespace Stikling.Core.Models;

/// <summary>
/// One thing on Today put off until a day, under the key that says what it is. Put-offs belong
/// to the collection, so putting something off hides it for everyone. Records are kept after the
/// day has passed and are just ignored.
/// </summary>
public sealed class PutOff : Entity
{
    public string Key { get; set; } = "";

    /// <summary>The day it shows again.</summary>
    public DateOnly Until { get; set; }

    /// <summary>
    /// The id for a key. It comes from the key, like a name-based UUID, so two devices putting off
    /// the same thing make the same record and the newest wins, instead of two records.
    /// </summary>
    public static Guid IdFor(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        var bytes = hash.AsSpan(0, 16).ToArray();
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50); // version 5
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC 4122 variant
        return new Guid(bytes, bigEndian: true);
    }
}
