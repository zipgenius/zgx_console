using System.Globalization;

namespace ZipGenius_X.Utils;

// Resource lookup for the shared SFX writer in the standalone console.
internal static class StringUtils
{
    public static string GetResString(string id) =>
        zg.Properties.Resources.ResourceManager.GetString(id, CultureInfo.CurrentUICulture) ?? id;
}
