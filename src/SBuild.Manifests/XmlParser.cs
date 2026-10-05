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
    /// </summary>
    /// <param name="xmlSrc">Xml text.</param>
    /// <returns>SProject with data from manifest.</returns>
    public static SProject ParseProject(string xmlSrc)
    {
        var project = new SProject();
        
        if (string.IsNullOrWhiteSpace(xmlSrc)) return project;
        
        var xDoc = XDocument.Parse(xmlSrc);
        var root = xDoc.Root;

        if (root == null) return project;

        project.Name = root.Attribute("Name")!.Value;
        project.TargetType = Enum.Parse<TargetType>(root.Attribute("TargetType")!.Value);
        project.TargetFramework = root.Element("TargetFramework")!.Value;
        project.ImplicitUsings = bool.Parse(root.Element("ImplicitUsings")!.Value);
        project.Nullable = bool.Parse(root.Element("Nullable")!.Value);
        project.RootPath = root.Attribute("RootPath")!.Value;
        
        var references = root.Element("References")!.Elements("Reference");

        foreach (var reference in references)
        {
            project.References.Add(reference.Value);
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
        
        var xDoc = XDocument.Parse(xmlSrc);
        var root = xDoc.Root;

        if (root == null) return solution;
        
        solution.Name = root.Attribute("Name")!.Value;
            
        var projects = root.Elements("Project");
        foreach (var project in projects)
        {
            solution.Projects.Add(project.Attribute("Path")!.Value);
        }

        return solution;
    }
}