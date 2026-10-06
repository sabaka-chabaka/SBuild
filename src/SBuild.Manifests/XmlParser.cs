using System.Xml;
using System.Xml.Linq;
using SBuild.Manifests.Abstractions;
using SBuild.Manifests.Enums;

namespace SBuild.Manifests;

/// <summary>
/// Parser for xml manifests.
/// </summary>
public static class XmlParser
{
    /// <summary>
    /// Parsing xml to project.
    /// Simple properties may be written either as child elements or as attributes of the root element.
    /// </summary>
    /// <param name="xmlSrc">Xml text.</param>
    /// <returns>SProject with data from manifest.</returns>
    /// <exception cref="ManifestException">The manifest contains invalid values.</exception>
    public static SProject ParseProject(string xmlSrc)
    {
        var project = new SProject();

        if (string.IsNullOrWhiteSpace(xmlSrc)) return project;

        var root = XDocument.Parse(xmlSrc).Root;
        if (root == null) return project;

        project.Name = root.Attribute("Name")?.Value.Trim() ?? string.Empty;

        if (ReadValue(root, "TargetType") is { } targetType)
        {
            if (!Enum.TryParse<TargetType>(targetType, true, out var parsed))
            {
                throw new ManifestException(
                    $"Неизвестный TargetType '{targetType}'. Допустимо: {string.Join(", ", Enum.GetNames<TargetType>())}.");
            }

            project.TargetType = parsed;
        }

        if (ReadValue(root, "TargetFramework") is { Length: > 0 } tfm)
            project.TargetFramework = tfm;

        project.ImplicitUsings = ReadBool(root, "ImplicitUsings", project.ImplicitUsings);
        project.Nullable = ReadBool(root, "Nullable", project.Nullable);

        if (ReadValue(root, "OutputPath") is { Length: > 0 } output)
            project.OutputPath = output;

        if (ReadValue(root, "RootPath") is { Length: > 0 } rootPath)
            project.RootPath = rootPath;

        foreach (var reference in root.Element("References")?.Elements("Reference") ?? [])
        {
            var value = (reference.Attribute("Path")?.Value ?? reference.Value).Trim();
            if (value.Length > 0) project.References.Add(value);
        }

        foreach (var exclude in root.Element("Excludes")?.Elements("Exclude") ?? [])
        {
            var value = (exclude.Attribute("Path")?.Value ?? exclude.Value).Trim();
            if (value.Length > 0) project.Excludes.Add(value);
        }

        return project;
    }

    /// <summary>
    /// Parsing xml to solution.
    /// </summary>
    /// <param name="xmlSrc">Xml text.</param>
    /// <returns>SSolution with data from manifest.</returns>
    public static SSolution ParseSolution(string xmlSrc)
    {
        var solution = new SSolution();

        if (string.IsNullOrWhiteSpace(xmlSrc)) return solution;

        var root = XDocument.Parse(xmlSrc).Root;
        if (root == null) return solution;

        solution.Name = root.Attribute("Name")?.Value ?? string.Empty;

        foreach (var project in root.Elements("Project"))
        {
            var path = project.Attribute("Path")?.Value ?? project.Value;
            if (!string.IsNullOrWhiteSpace(path))
            {
                solution.Projects.Add(path.Trim());
            }
        }

        return solution;
    }

    /// <summary>
    /// Reads a project manifest from disk and fills <see cref="SProject.RootPath"/>
    /// and <see cref="SProject.ManifestPath"/>.
    /// </summary>
    /// <param name="path">Path to a <c>.sbproj</c> file.</param>
    /// <exception cref="ManifestException">File is missing or malformed.</exception>
    public static SProject LoadProject(string path)
    {
        var full = Path.GetFullPath(path);
        var project = Load(full, ParseProject);

        project.ManifestPath = full;
        project.RootPath = Path.GetDirectoryName(full)!;

        if (string.IsNullOrWhiteSpace(project.Name))
            project.Name = Path.GetFileNameWithoutExtension(full);

        return project;
    }

    /// <summary>
    /// Reads a solution manifest from disk and fills <see cref="SSolution.RootPath"/>
    /// and <see cref="SSolution.ManifestPath"/>.
    /// </summary>
    /// <param name="path">Path to a <c>.sbslnx</c> file.</param>
    /// <exception cref="ManifestException">File is missing or malformed.</exception>
    public static SSolution LoadSolution(string path)
    {
        var full = Path.GetFullPath(path);
        var solution = Load(full, ParseSolution);

        solution.ManifestPath = full;
        solution.RootPath = Path.GetDirectoryName(full)!;

        if (string.IsNullOrWhiteSpace(solution.Name))
            solution.Name = Path.GetFileNameWithoutExtension(full);

        return solution;
    }

    /// <summary>
    /// Writes the project manifest to disk.
    /// </summary>
    public static void SaveProject(SProject project, string path) =>
        File.WriteAllText(path, project.ToXml() + Environment.NewLine);

    /// <summary>
    /// Writes the solution manifest to disk.
    /// </summary>
    public static void SaveSolution(SSolution solution, string path) =>
        File.WriteAllText(path, solution.ToXml() + Environment.NewLine);

    private static T Load<T>(string path, Func<string, T> parse)
    {
        if (!File.Exists(path))
            throw new ManifestException($"Файл манифеста не найден: '{path}'.");

        try
        {
            return parse(File.ReadAllText(path));
        }
        catch (XmlException e)
        {
            throw new ManifestException($"Некорректный XML в '{path}': {e.Message}", e);
        }
        catch (ManifestException e)
        {
            throw new ManifestException($"{path}: {e.Message}", e);
        }
    }

    private static string? ReadValue(XElement root, string name) =>
        (root.Attribute(name)?.Value ?? root.Element(name)?.Value)?.Trim();

    private static bool ReadBool(XElement root, string name, bool fallback)
    {
        var value = ReadValue(root, name);
        if (string.IsNullOrEmpty(value)) return fallback;

        return bool.TryParse(value, out var result)
            ? result
            : throw new ManifestException($"Значение '{value}' параметра {name} не является true/false.");
    }
}
