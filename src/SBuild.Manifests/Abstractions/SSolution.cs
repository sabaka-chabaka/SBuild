using System.Xml.Linq;

namespace SBuild.Manifests.Abstractions;

/// <summary>
/// Base abstractions of solutions.
/// </summary>
public class SSolution
{
    /// <summary>
    /// Solution name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// A list of solution's projects paths.
    /// </summary>
    public List<string> Projects = [];

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