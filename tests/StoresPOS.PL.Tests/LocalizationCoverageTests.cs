using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace StoresPOS.PL.Tests;

/// <summary>
/// Phase 17 safety net: every user-facing English key must have an Arabic entry in
/// <c>src/StoresPOS.PL/Resources/SharedResource.ar.resx</c>. The English text is the key itself.
/// Sources scanned (static text only — interpolated strings cannot be keys):
///   • <c>L["..."]</c> / <c>_l["..."]</c> / <c>Localizer["..."]</c> in PL views and C# files
///   • <c>[Display(Name = "...")]</c> and validation <c>ErrorMessage = "..."</c> in PL C# files
///   • BLL message constants (<c>const string Msg... = "..."</c>) and literal <c>new BusinessException("...")</c> (BLL and PL)
/// When this fails, add the listed keys to the Arabic resx (same commit as the UI text).
/// </summary>
public class LocalizationCoverageTests
{
    // A C# regular string literal body: no unescaped quote; escapes allowed.
    private const string Lit = "\"((?:[^\"\\\\]|\\\\.)*)\"";

    private static readonly Regex IndexerKey = new(@"\b(?:L|_l|_localizer|Localizer|localizer|T)\s*\[\s*" + Lit + @"\s*\]", RegexOptions.Compiled);
    private static readonly Regex DisplayName = new(@"\bDisplay\s*\([^)]*?\bName\s*=\s*" + Lit, RegexOptions.Compiled);
    private static readonly Regex ErrorMessage = new(@"\bErrorMessage\s*=\s*" + Lit, RegexOptions.Compiled);
    private static readonly Regex MessageConst = new(@"\bconst\s+string\s+Msg\w*\s*=\s*" + Lit, RegexOptions.Compiled);
    private static readonly Regex BusinessLiteral = new(@"\bnew\s+BusinessException\s*\(\s*" + Lit, RegexOptions.Compiled);

    [Fact]
    public void Every_user_facing_key_has_an_arabic_translation()
    {
        var root = RepoRoot();
        var pl = Path.Combine(root, "src", "StoresPOS.PL");
        var bll = Path.Combine(root, "src", "StoresPOS.BLL");
        var arabic = LoadResxKeys(Path.Combine(pl, "Resources", "SharedResource.ar.resx"));

        var used = new Dictionary<string, string>(StringComparer.Ordinal); // key -> first place it was seen

        foreach (var file in SourceFiles(pl, "*.cshtml").Concat(SourceFiles(pl, "*.cs")))
        {
            var text = File.ReadAllText(file);
            Collect(used, IndexerKey, text, file, root);
            if (file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                Collect(used, DisplayName, text, file, root);
                Collect(used, ErrorMessage, text, file, root);
                Collect(used, BusinessLiteral, text, file, root);
            }
        }

        foreach (var file in SourceFiles(bll, "*.cs"))
        {
            var text = File.ReadAllText(file);
            Collect(used, MessageConst, text, file, root);
            Collect(used, BusinessLiteral, text, file, root);
        }

        Assert.True(used.Count > 500, $"Expected to find hundreds of keys, found {used.Count} — is the scan looking at the right folders?");

        var missing = used
            .Where(kv => !arabic.Contains(kv.Key))
            .OrderBy(kv => kv.Value, StringComparer.Ordinal)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"  \"{kv.Key}\"   ({kv.Value})")
            .ToList();

        Assert.True(missing.Count == 0,
            $"{missing.Count} key(s) have no Arabic entry in SharedResource.ar.resx:{Environment.NewLine}"
            + string.Join(Environment.NewLine, missing));
    }

    [Fact]
    public void Arabic_resource_file_is_well_formed_and_has_no_empty_values()
    {
        var path = Path.Combine(RepoRoot(), "src", "StoresPOS.PL", "Resources", "SharedResource.ar.resx");
        var doc = XDocument.Load(path);
        var empty = doc.Root!.Elements("data")
            .Where(d => string.IsNullOrWhiteSpace((string?)d.Element("value")))
            .Select(d => (string?)d.Attribute("name"))
            .ToList();

        Assert.True(empty.Count == 0, "Arabic entries with an empty value: " + string.Join(", ", empty));
    }

    // ------------------------------------------------------------------ helpers

    private static void Collect(Dictionary<string, string> used, Regex pattern, string text, string file, string root)
    {
        foreach (Match m in pattern.Matches(text))
        {
            var key = Unescape(m.Groups[1].Value);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (!used.ContainsKey(key))
            {
                var line = text.Take(m.Index).Count(c => c == '\n') + 1;
                used[key] = $"{Path.GetRelativePath(root, file).Replace('\\', '/')}:{line}";
            }
        }
    }

    private static string Unescape(string s) =>
        Regex.Replace(s, @"\\(u[0-9a-fA-F]{4}|.)", m =>
        {
            var e = m.Groups[1].Value;
            return e[0] switch
            {
                'u' when e.Length == 5 => ((char)Convert.ToInt32(e[1..], 16)).ToString(),
                'n' => "\n",
                't' => "\t",
                'r' => "\r",
                '0' => "\0",
                _ => e
            };
        });

    private static HashSet<string> LoadResxKeys(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .Select(d => (string?)d.Attribute("name"))
            .Where(n => n is not null)
            .Select(n => n!)
            .ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<string> SourceFiles(string folder, string pattern) =>
        Directory.EnumerateFiles(folder, pattern, SearchOption.AllDirectories)
            .Where(f =>
            {
                var rel = f.Replace('\\', '/');
                return !rel.Contains("/bin/") && !rel.Contains("/obj/") && !rel.Contains("/wwwroot/lib/")
                    && !rel.Contains("/Migrations/");
            });

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "StoresPOS.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("StoresPOS.sln not found above " + AppContext.BaseDirectory);
    }
}
