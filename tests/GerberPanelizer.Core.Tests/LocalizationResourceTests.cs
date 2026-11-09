using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace GerberPanelizer.Core.Tests;

public sealed class LocalizationResourceTests
{
    private static readonly string[] Languages = ["cs", "en", "de", "fr"];
    private static readonly string[] CzechLeakMarkers =
    [
        "Výchozí pracovní plocha",
        "souborů",
        "paketů",
        "výměn nástroje",
        "USB připojeno",
        "Souvislý ruční pohyb",
        "Povrch zatím nebyl změřen",
    ];

    [Fact]
    public void EveryLanguageContainsTheSameUniqueKeys()
    {
        var resources = Languages.ToDictionary(language => language, LoadResources);
        var expected = resources["en"].Keys.OrderBy(key => key).ToArray();

        foreach (var (language, values) in resources)
        {
            Assert.Equal(values.Count, values.Keys.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(expected, values.Keys.OrderBy(key => key).ToArray());
        }
    }

    [Fact]
    public void ForeignLanguageResourcesDoNotContainKnownCzechUiText()
    {
        foreach (var language in Languages.Where(language => language != "cs"))
        {
            var text = string.Join('\n', LoadResources(language).Values);
            foreach (var marker in CzechLeakMarkers) Assert.DoesNotContain(marker, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void EveryStaticallyReferencedLocalizationKeyExists()
    {
        var expected = LoadResources("en").Keys.ToHashSet(StringComparer.Ordinal);
        var appDirectory = Path.Combine(FindRepositoryRoot(), "src", "GerberPanelizer.App");
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        var bindingPattern = new Regex(@"Path=\[([^\]]+)\]", RegexOptions.CultureInvariant);
        var codePattern = new Regex(@"(?:LocalizationService\.Instance\[|L\()""([A-Za-z0-9]+)""", RegexOptions.CultureInvariant);

        foreach (var path in Directory.EnumerateFiles(appDirectory, "*.*", SearchOption.AllDirectories).Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
            var text = File.ReadAllText(path);
            foreach (Match match in bindingPattern.Matches(text)) referenced.Add(match.Groups[1].Value);
            foreach (Match match in codePattern.Matches(text)) referenced.Add(match.Groups[1].Value);
        }

        Assert.Empty(referenced.Where(key => !expected.Contains(key)).OrderBy(key => key));
    }

    private static Dictionary<string, string> LoadResources(string language)
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var path = Path.Combine(FindRepositoryRoot(), "src", "GerberPanelizer.App", "Resources", $"Strings.{language}.xaml");
        return XDocument.Load(path).Root!.Elements().ToDictionary(element => element.Attribute(x + "Key")!.Value, element => element.Value, StringComparer.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GerberPanelizer.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the Gerber Panelizer repository root.");
    }
}
