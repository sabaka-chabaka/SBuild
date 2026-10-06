using System.Xml.Linq;
using SBuild.Manifests.Enums;

namespace SBuild.Manifests.Abstractions;

/// <summary>
/// Base abstractions for project.
/// </summary>
public class SProject
{
    /// <summary>
    /// File extension of project manifests.
    /// </summary>
    public const string Extension = ".sbproj";

    /// <summary>
    /// Target framework moniker matching the runtime SBuild is currently running on, e.g. <c>net10.0</c>.
    /// </summary>
    public static string DefaultTargetFramework => $"net{Environment.Version.Major}.{Environment.Version.Minor}";

    /// <summary>
    /// Project name. Also used as the assembly name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Project target type.
    /// </summary>
    public TargetType TargetType { get; set; }

    /// <summary>
    /// Project target framework, e.g. <c>net10.0</c>.
    /// </summary>
    public string TargetFramework { get; set; } = DefaultTargetFramework;

    /// <summary>
    /// Does project use implicit usings.
    /// </summary>
    public bool ImplicitUsings { get; set; } = true;

    /// <summary>
    /// Does project allow nullable.
    /// </summary>
    public bool Nullable { get; set; } = true;

    /// <summary>
    /// Output directory, relative to <see cref="RootPath"/>.
    /// </summary>
    public string OutputPath { get; set; } = "bin";

    /// <summary>
    /// Project's references. Each entry is either a path to another project
    /// (a <c>.sbproj</c> file or a directory that contains one) or a path to a ready <c>.dll</c>.
    /// Paths are relative to <see cref="RootPath"/>.
    /// </summary>
    public List<string> References { get; set; } = [];

    /// <summary>
    /// Files or directories (relative to <see cref="RootPath"/>) that must not be compiled.
    /// <c>bin</c>, <c>obj</c>, hidden directories and nested projects are always skipped.
    /// </summary>
    public List<string> Excludes { get; set; } = [];

    /// <summary>
    /// Absolute directory of the project. It is not stored in the manifest:
    /// it is always the directory the manifest file lives in (set by <see cref="XmlParser.LoadProject"/>).
    /// </summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>
    /// Absolute path of the manifest file this project was loaded from, if any.
    /// </summary>
    public string ManifestPath { get; set; } = string.Empty;

    /// <summary>
    /// Resolves a path from the manifest (relative to the project root) to an absolute one.
    /// Both <c>/</c> and <c>\</c> are accepted as separators.
    /// </summary>
    public string ResolvePath(string relativePath)
    {
        var normalized = ManifestLocator.NormalizeSeparators(relativePath);
        return Path.GetFullPath(Path.IsPathRooted(normalized) ? normalized : Path.Combine(RootPath, normalized));
    }

    /// <summary>
    /// Serializes the project to xml manifest.
    /// </summary>
    public string ToXml()
    {
        var xDoc = new XDocument(
            new XElement("Project",
                new XAttribute("Name", Name),
                new XAttribute("TargetType", TargetType.ToString()),
                new XElement("TargetFramework", TargetFramework),
                new XElement("ImplicitUsings", ImplicitUsings.ToString().ToLowerInvariant()),
                new XElement("Nullable", Nullable.ToString().ToLowerInvariant()),
                new XElement("OutputPath", OutputPath),
                References.Count > 0
                    ? new XElement("References", References.Select(r => new XElement("Reference", r)))
                    : null,
                Excludes.Count > 0
                    ? new XElement("Excludes", Excludes.Select(e => new XElement("Exclude", e)))
                    : null
            )
        );

        return xDoc.ToString();
    }
}
