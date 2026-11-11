using System.Globalization;
using Stikling.Core.Compare;
using Stikling.Core.Models;
using Stikling.Core.Names;

namespace Stikling.Core.Tests;

internal static class CompareTestData
{
    public static readonly PlantDictionary Dictionary = new(new PlantNameData
    {
        Genera =
        [
            new GenusNames
            {
                Name = "Monstera",
                Species = ["adansonii", "deliciosa"],
                Synonyms = new Dictionary<string, string> { ["borsigiana"] = "Monstera deliciosa" },
                Cultivars = [new("Thai Constellation", "deliciosa"), new("Albo", "deliciosa")]
            },
            new GenusNames { Name = "Hoya", Species = ["carnosa", "kerrii"], Cultivars = [new("Krimson Queen", "carnosa")] },
            new GenusNames { Name = "Alocasia", Species = ["zebrina"] },
        ]
    }, [new EverydayNameData { Language = "en", Plants = [new EverydayNames("Monstera", "deliciosa", null, ["Swiss cheese plant"])] }], "en");

    /// <summary>Runs with the text of another language, and puts English back.</summary>
    public static T In<T>(string language, Func<T> run)
    {
        var before = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
        try
        {
            return run();
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    public static Wish AWish(string? genus = null, string? species = null, string? cultivar = null, string? name = null) =>
        new() { Genus = genus, Species = species, Cultivar = cultivar, Name = name };

    public static Plant APlant(string? genus = null, string? species = null, string? cultivar = null, string? nickname = null) =>
        new() { Genus = genus, Species = species, Cultivar = cultivar, Nickname = nickname };

    public static FriendPlant Theirs(string name, string? genus = null, string? species = null, string? cultivar = null,
        FriendSpare spare = FriendSpare.None, int count = 0, PropagationStage? stage = null) =>
        new(Guid.NewGuid(), SubjectType.Plant, name, genus, species, cultivar, spare, count, stage, null);

    public static FriendWish TheirWish(string name, string? genus = null, string? species = null, string? cultivar = null) =>
        new(name, genus, species, cultivar);

    public static FriendList Friend(IEnumerable<FriendPlant>? plants = null, IEnumerable<FriendWish>? wishes = null) =>
        new(null, [.. plants ?? []], [.. wishes ?? []], true, null, []);
}
