using System.Text.RegularExpressions;
using System.Xml.Linq;
using Stikling.Core.Settings;

namespace Stikling.Core.Tests;

/// <summary>
/// Checks the translation files in src/*/Resources. English is X.resx and a translation is X.da.resx.
/// docs/dev/translating.md explains the rules these tests keep.
/// </summary>
public partial class TranslationFileTests
{
    /// <summary>A language is ready to offer to everyone once this share of the English text is translated.</summary>
    private const double ReadyShare = 0.9;

    /// <summary>
    /// False while the screens are still being moved into the resource files. The share translated is
    /// then worked out on a text that keeps growing, so a language can be 100 percent translated of what has been
    /// moved so far without being ready. The last of those pull requests sets this to true, and from then
    /// on any language that reaches the share has to be listed as ready.
    /// </summary>
    private static readonly bool EveryScreenMoved = true;

    private sealed record Resx(string Area, string Language, string Path, Dictionary<string, string> Texts);

    private static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(System.IO.Path.Combine(folder.FullName, "Stikling.slnx")))
            folder = folder.Parent;
        return folder?.FullName ?? throw new InvalidOperationException("Stikling.slnx not found above the test folder.");
    }

    private static List<Resx> ReadAll()
    {
        var files = new List<Resx>();
        foreach (var path in Directory.GetFiles(System.IO.Path.Combine(RepositoryRoot(), "src"), "*.resx", SearchOption.AllDirectories))
        {
            var folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path));
            if (folder != "Resources" || path.Contains($"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}")
                || path.Contains($"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            // Settings.da.resx is the area Settings in Danish, Settings.resx the same in English
            var parts = System.IO.Path.GetFileNameWithoutExtension(path).Split('.');
            var texts = XDocument.Load(path).Root!.Elements("data")
                .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "");
            var project = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(path)));
            files.Add(new Resx($"{project}/{parts[0]}", parts.Length > 1 ? parts[^1] : AppLanguages.English, path, texts));
        }
        return files;
    }

    [GeneratedRegex(@"\{(\d+)(?:[,:][^}]*)?\}")]
    private static partial Regex Placeholder();

    private static string Placeholders(string text) =>
        string.Join(",", Placeholder().Matches(text).Select(m => m.Groups[1].Value).Distinct().Order());

    [Fact]
    public void The_resource_files_are_found()
    {
        var files = ReadAll();
        Assert.Contains(files, f => f.Language == "en" && f.Area == "Stikling.Web/Settings");
        Assert.Contains(files, f => f.Language == "da" && f.Area == "Stikling.Web/Settings");
    }

    [Fact]
    public void Every_translated_key_exists_in_English()
    {
        var files = ReadAll();
        var english = files.Where(f => f.Language == "en").ToDictionary(f => f.Area);

        foreach (var file in files.Where(f => f.Language != "en"))
        {
            Assert.True(english.ContainsKey(file.Area), $"{file.Path} has no English file ({file.Area}.resx) next to it.");
            var missing = file.Texts.Keys.Except(english[file.Area].Texts.Keys).ToList();
            Assert.True(missing.Count == 0, $"{file.Path} has keys that English doesn't: {string.Join(", ", missing)}");
        }
    }

    [Fact]
    public void Placeholders_in_a_translation_match_the_English_ones()
    {
        var files = ReadAll();
        var english = files.Where(f => f.Language == "en").ToDictionary(f => f.Area);

        foreach (var file in files.Where(f => f.Language != "en" && english.ContainsKey(f.Area)))
        {
            foreach (var (key, text) in file.Texts.Where(t => t.Value.Length > 0))
            {
                if (!english[file.Area].Texts.TryGetValue(key, out var original))
                    continue;
                // The singular may leave the number out, as in "Hver dag" for "Every {0} day"
                if (key.EndsWith("_One", StringComparison.Ordinal)
                    && Placeholders(original).Split(',').Where(p => p != "0").SequenceEqual(Placeholders(text).Split(',', StringSplitOptions.RemoveEmptyEntries).Where(p => p != "0")))
                    continue;
                Assert.True(Placeholders(original) == Placeholders(text),
                    $"{file.Path}, {key}: has placeholders [{Placeholders(text)}] where English has [{Placeholders(original)}].");
            }
        }
    }

    [Fact]
    public void Plural_texts_come_in_pairs()
    {
        foreach (var file in ReadAll().Where(f => f.Language == "en"))
        {
            foreach (var key in file.Texts.Keys.Where(k => k.EndsWith("_One", StringComparison.Ordinal)))
                Assert.True(file.Texts.ContainsKey(key[..^4] + "_Other"), $"{file.Path}: {key} has no _Other next to it.");
            foreach (var key in file.Texts.Keys.Where(k => k.EndsWith("_Other", StringComparison.Ordinal)))
                Assert.True(file.Texts.ContainsKey(key[..^6] + "_One"), $"{file.Path}: {key} has no _One next to it.");
        }
    }

    /// <summary>
    /// How much of the English text a language has, counting all areas together. A text that is the same
    /// as the English one counts, since some words are the same in Danish. An empty one doesn't.
    /// </summary>
    private static double TranslatedShare(List<Resx> files, string language)
    {
        var total = 0;
        var done = 0;
        foreach (var english in files.Where(f => f.Language == "en"))
        {
            total += english.Texts.Count;
            if (files.FirstOrDefault(f => f.Language == language && f.Area == english.Area) is { } translation)
                done += english.Texts.Keys.Count(k => translation.Texts.TryGetValue(k, out var text) && text.Length > 0);
        }
        return total == 0 ? 0 : (double)done / total;
    }

    [Fact]
    public void A_language_listed_as_ready_has_enough_translated()
    {
        var files = ReadAll();
        foreach (var language in AppLanguages.All.Where(l => l.Ready && l.Code != AppLanguages.English))
        {
            var share = TranslatedShare(files, language.Code);
            Assert.True(share >= ReadyShare,
                $"{language.NativeName} ({language.Code}) is listed as ready in AppLanguages but only {share:P0} of the text is translated, and it needs {ReadyShare:P0}.");
        }
    }

    [Fact]
    public void A_language_with_enough_translated_is_listed_as_ready()
    {
        if (!EveryScreenMoved)
            return;

        var files = ReadAll();
        foreach (var code in files.Where(f => f.Language != AppLanguages.English).Select(f => f.Language).Distinct())
        {
            var share = TranslatedShare(files, code);
            var listed = AppLanguages.All.FirstOrDefault(l => l.Code == code);
            Assert.True(share < ReadyShare || listed is { Ready: true },
                $"{listed?.NativeName ?? code} ({code}) has {share:P0} of the text translated, which is enough, but isn't listed as ready in AppLanguages.");
        }
    }

    [Fact]
    public void Every_language_with_files_is_listed_in_AppLanguages()
    {
        foreach (var code in ReadAll().Select(f => f.Language).Distinct())
            Assert.Contains(AppLanguages.All, l => l.Code == code);
    }
}
