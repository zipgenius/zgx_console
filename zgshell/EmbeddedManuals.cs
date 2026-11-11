using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text;

namespace zg;

internal static class EmbeddedManuals
{
    private const string Prefix = "zg.Manuals.";
    private static readonly Assembly Assembly = typeof(EmbeddedManuals).Assembly;
    // RecursiveDir uses the build host's separators; normalize for cross-platform publishes.
    private static readonly Dictionary<string, string> Resources = Assembly.GetManifestResourceNames()
        .Where(name => name.StartsWith(Prefix, StringComparison.Ordinal))
        .ToDictionary(name => name.Replace('\\', '/'), name => name, StringComparer.Ordinal);

    internal static IEnumerable<string> ReadLines(string filename, CultureInfo culture)
    {
        var folders = new List<string>();
        for (var current = culture; !string.IsNullOrEmpty(current.Name); current = current.Parent)
            folders.Add(current.Name + "/");

        // A neutral language, or another regional variant, can use an available
        // regional translation (e.g. it / it-CH -> the existing it-IT manuals).
        string language = culture.TwoLetterISOLanguageName;
        folders.AddRange(Resources.Keys.Select(name => name[Prefix.Length..])
            .Where(path => path.Contains('/'))
            .Select(path => path[..(path.IndexOf('/') + 1)])
            .Where(folder => folder.StartsWith(language + "-", StringComparison.OrdinalIgnoreCase))
            .Distinct().OrderBy(folder => folder, StringComparer.Ordinal));
        folders.Add(""); // Root man/*.txt is the English fallback.

        foreach (string folder in folders.Distinct())
        {
            if (!Resources.TryGetValue(Prefix + folder + filename, out string? resource)) continue;
            using Stream stream = Assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            while (reader.ReadLine() is string line) yield return line;
            yield break;
        }
        throw new MissingManifestResourceException("Embedded manual not found: " + filename);
    }
}
