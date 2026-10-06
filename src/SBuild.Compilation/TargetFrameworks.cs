using System.Text.Json;
using System.Text.RegularExpressions;

namespace SBuild.Compilation;

/// <summary>
/// Helpers for target framework monikers (<c>net10.0</c>) and <c>*.runtimeconfig.json</c>.
/// </summary>
public static partial class TargetFrameworks
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [GeneratedRegex(@"^net(\d+)\.(\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex MonikerRegex();

    /// <summary>
    /// Parses a <c>netX.Y</c> moniker (X &gt;= 5).
    /// </summary>
    public static bool TryParse(string? moniker, out Version version)
    {
        version = new Version(0, 0);

        if (string.IsNullOrWhiteSpace(moniker)) return false;

        var match = MonikerRegex().Match(moniker.Trim());
        if (!match.Success) return false;

        if (!int.TryParse(match.Groups[1].Value, out var major) ||
            !int.TryParse(match.Groups[2].Value, out var minor) ||
            major < 5)
        {
            return false;
        }

        version = new Version(major, minor);
        return true;
    }

    /// <summary>
    /// Writes a <c>runtimeconfig.json</c> that lets <c>dotnet app.dll</c> run the assembly.
    /// </summary>
    public static void WriteRuntimeConfig(string path, Version version)
    {
        var config = new
        {
            runtimeOptions = new
            {
                tfm = $"net{version.Major}.{version.Minor}",
                framework = new
                {
                    name = "Microsoft.NETCore.App",
                    version = $"{version.Major}.{version.Minor}.0"
                }
            }
        };

        File.WriteAllText(path, JsonSerializer.Serialize(config, JsonOptions));
    }
}
