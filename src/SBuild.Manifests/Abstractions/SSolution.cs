using System.Xml.Linq;

namespace SBuild.Manifests.Abstractions;

/// <summary>
/// Base abstractions of solutions.
/// </summary>
public class SSolution
{
    /// <summary>
    /// File extension of solution manifests.
    /// </summary>
    public const string Extension = ".sbslnx";

    /// <summary>
    /// Solution name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// A list of solution's projects paths, relative to <see cref="RootPath"/>.
    /// Each entry is a <c>.sbproj</c> file or a directory that contains exactly one.
    /// </summary>
    public List<string> Projects { get; set; } = [];

    /// <summary>
    /// Absolute directory of the solution (set by <see cref="XmlParser.LoadSolution"/>).
    /// </summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>
    /// Absolute path of the manifest file this solution was loaded from, if any.
    /// </summary>
    public string ManifestPath { get; set; } = string.Empty;

    /// <summary>
    /// Resolves all projects of the solution to absolute <c>.sbproj</c> paths (duplicates are removed).
    /// </summary>
    public List<string> ResolveProjectFiles()
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in Projects)
        {
            var normalized = ManifestLocator.NormalizeSeparators(entry);
            var full = Path.IsPathRooted(normalized) ? normalized : Path.Combine(RootPath, normalized);
            var file = ManifestLocator.ResolveProjectFile(full);

            if (seen.Add(file))
            {
                result.Add(file);
            }
        }

        return result;
    }

    /// <summary>
    /// Serializes the solution to xml manifest.
    /// </summary>
    public string ToXml()
    {
        var xDoc = new XDocument(
            new XElement("Solution",
                new XAttribute("Name", Name),
                Projects.Select(path => new XElement("Project", new XAttribute("Path", path)))
            )
        );

        return xDoc.ToString();
    }
}
