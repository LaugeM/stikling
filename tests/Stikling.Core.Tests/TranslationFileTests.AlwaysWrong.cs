using System.Text.RegularExpressions;

namespace Stikling.Core.Tests;

/// <summary>
/// docs/dev/always-wrong.&lt;language&gt;.txt lists phrases that are wrong in every sentence, like a misspelling
/// or a noun with the wrong gender. No translation in that language may have one.
/// </summary>
public partial class TranslationFileTests
{
    private sealed record WrongPhrase(string Phrase, string Instead, int Line);

    private static Dictionary<string, List<WrongPhrase>> ReadAlwaysWrong()
    {
        var lists = new Dictionary<string, List<WrongPhrase>>();
        foreach (var path in Directory.GetFiles(System.IO.Path.Combine(RepositoryRoot(), "docs", "dev"), "always-wrong.*.txt"))
        {
            // always-wrong.da.txt is the list for Danish
            var language = System.IO.Path.GetFileNameWithoutExtension(path).Split('.')[^1];
            var phrases = new List<WrongPhrase>();
            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;
                var parts = line.Split('|', StringSplitOptions.TrimEntries);
                Assert.True(parts.Length == 3 && parts.All(p => p.Length > 0),
                    $"{System.IO.Path.GetFileName(path)} line {i + 1} should be: phrase | write instead | why");
                phrases.Add(new WrongPhrase(parts[0], parts[1], i + 1));
            }
            lists[language] = phrases;
        }
        return lists;
    }

    // A hyphen counts as part of a word, so "et plante-navn" isn't read as "et plante"
    private static Regex PhrasePattern(string phrase) =>
        new($@"(?<![\w-]){Regex.Escape(phrase)}(?![\w-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [Fact]
    public void No_translation_has_a_phrase_from_its_always_wrong_list()
    {
        var lists = ReadAlwaysWrong();
        Assert.Contains("da", lists.Keys);

        var found = new List<string>();
        foreach (var file in ReadAll())
        {
            if (!lists.TryGetValue(file.Language, out var phrases))
                continue;
            foreach (var phrase in phrases)
            {
                var pattern = PhrasePattern(phrase.Phrase);
                foreach (var (key, text) in file.Texts)
                {
                    if (pattern.IsMatch(text))
                        found.Add($"{file.Area}.{file.Language} {key}: \"{phrase.Phrase}\", write \"{phrase.Instead}\"");
                }
            }
        }

        Assert.True(found.Count == 0, "Phrases from always-wrong lists:\n" + string.Join("\n", found));
    }

    [Fact]
    public void An_always_wrong_phrase_is_found_as_a_whole_phrase_only()
    {
        var pattern = PhrasePattern("et plante");

        Assert.Matches(pattern, "Du har et plante her");
        Assert.Matches(pattern, "Et plante.");
        Assert.DoesNotMatch(pattern, "Et plantenavn");
        Assert.DoesNotMatch(pattern, "Det plantebord");
        Assert.DoesNotMatch(pattern, "Et plante-navn");
        Assert.DoesNotMatch(pattern, "Sæt plante");
        Assert.Matches(PhrasePattern("en billede"), "Så kom en billede på");
    }
}
