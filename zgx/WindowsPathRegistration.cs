using System;
using System.IO;
using System.Linq;

namespace zg;

internal static class WindowsPathRegistration
{
    // Keep other entries verbatim: they may contain variables, quotes or empty
    // segments. Only our own directory is removed/repositioned.
    internal static string Prepend(string? path, string directory)
    {
        string remainder = Remove(path, directory);
        return string.IsNullOrEmpty(remainder) ? directory : directory + ";" + remainder;
    }

    internal static string Remove(string? path, string directory)
    {
        string normalizedDirectory = Normalize(directory);
        return string.Join(";", (path ?? string.Empty).Split(';')
            .Where(entry => !string.Equals(Normalize(entry), normalizedDirectory, StringComparison.OrdinalIgnoreCase)));
    }

    private static string Normalize(string path)
    {
        string value = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // An unrelated invalid PATH entry must not prevent registration.
            return value.TrimEnd('\\', '/');
        }
    }
}
