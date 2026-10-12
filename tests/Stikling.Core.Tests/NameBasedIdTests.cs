using Stikling.Core.Models;

namespace Stikling.Core.Tests;

public class NameBasedIdTests
{
    // The DNS namespace from RFC 4122
    private static readonly Guid Dns = new("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

    [Fact]
    public void It_matches_the_known_version_5_uuid()
    {
        Assert.Equal(new Guid("2ed6657d-e927-568b-95e1-2665a8aea6a2"), NameBasedId.For(Dns, "www.example.com"));
    }

    [Fact]
    public void The_same_name_gives_the_same_id_and_another_name_another()
    {
        Assert.Equal(NameBasedId.For("received/Plant/abc"), NameBasedId.For("received/Plant/abc"));
        Assert.NotEqual(NameBasedId.For("received/Plant/abc"), NameBasedId.For("received/Plant/abd"));
    }

    [Fact]
    public void The_fixed_namespace_gives_ids_that_never_change()
    {
        // Pinned, since a different namespace would make a plant received twice come out as two plants
        Assert.Equal(new Guid("8543e345-2526-597b-9960-96ee48cfcc23"),
            NameBasedId.For("received/Plant/00000000-0000-0000-0000-000000000001"));
    }
}
