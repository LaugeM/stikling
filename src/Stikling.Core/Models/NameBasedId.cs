using System.Security.Cryptography;
using System.Text;

namespace Stikling.Core.Models;

/// <summary>
/// Ids made from a name (RFC 4122 version 5), so two devices that receive the same copy of a plant
/// make the same records instead of two sets.
/// </summary>
public static class NameBasedId
{
    // Must never change. Every id made from a name depends on it, and changing it would make a
    // plant that was received before come out as a new one when it is received again.
    private static readonly Guid Namespace = new("6f3c1f0e-8a52-4d6b-9b8e-2d4a7c1e5f93");

    public static Guid For(string name) => For(Namespace, name);

    public static Guid For(Guid ns, string name)
    {
        var nsBytes = ns.ToByteArray();
        SwapByteOrder(nsBytes);

        var nameBytes = Encoding.UTF8.GetBytes(name);
        var input = new byte[nsBytes.Length + nameBytes.Length];
        nsBytes.CopyTo(input, 0);
        nameBytes.CopyTo(input, nsBytes.Length);

        var hash = SHA1.HashData(input);
        var bytes = new byte[16];
        Array.Copy(hash, bytes, 16);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        SwapByteOrder(bytes);
        return new Guid(bytes);
    }

    // A Guid keeps its first three groups in the machine's byte order, the RFC wants network order
    private static void SwapByteOrder(byte[] guid)
    {
        (guid[0], guid[3]) = (guid[3], guid[0]);
        (guid[1], guid[2]) = (guid[2], guid[1]);
        (guid[4], guid[5]) = (guid[5], guid[4]);
        (guid[6], guid[7]) = (guid[7], guid[6]);
    }
}
