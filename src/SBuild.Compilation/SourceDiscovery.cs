using SBuild.Manifests.Abstractions;

namespace SBuild.Compilation;

/// <summary>
/// Finds the C# files that belong to a project.
/// </summary>
public static class SourceDiscovery
{
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules"
    };

    /// <summary>
    /// Recursively collects <c>*.cs</c> files under <paramref name="root"/>.
    /// Skips <c>bin</c>, <c>obj</c>, hidden directories, directories of nested projects
    /// (those containing their own <c>.sbproj</c>) and everything in <paramref name="excludedPaths"/>.
    /// </summary>
    /// <param name="root">Project directory.</param>
    /// <param name="excludedPaths">Absolute files or directories to skip.</param>
    /// <returns>Absolute paths in a stable (ordinal) order.</returns>
    public static IReadOnlyList<string> FindSources(string root, IEnumerable<string>? excludedPaths = null)
    {
        var excluded = (excludedPaths ?? []).Select(Path.GetFullPath).ToList();
        var result = new List<string>();

        bool IsExcluded(string path) => excluded.Any(e => PathUtil.IsSameOrUnder(path, e));

        void Walk(string directory)
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.cs"))
            {
                if (!IsExcluded(file))
                {
                    result.Add(Path.GetFullPath(file));
                }
            }

            foreach (var sub in Directory.EnumerateDirectories(directory))
            {
                var name = Path.GetFileName(sub);

                if (name.StartsWith('.') || IgnoredDirectories.Contains(name) || IsExcluded(sub))
                {
                    continue;
                }

                if (Directory.EnumerateFiles(sub, "*" + SProject.Extension).Any())
                {
                    continue; // nested project owns this directory
                }

                Walk(sub);
            }
        }

        Walk(Path.GetFullPath(root));

        result.Sort(StringComparer.Ordinal);
        return result;
    }
}
