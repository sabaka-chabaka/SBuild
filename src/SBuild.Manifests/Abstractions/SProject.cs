using System.Xml.Linq;
using SBuild.Manifests.Enums;

namespace SBuild.Manifests.Abstractions;

/// <summary>
/// Base abstractions for project.
/// </summary>
public class SProject
{
    /// <summary>
    /// Project name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Project target type.
    /// </summary>
    public TargetType TargetType { get; set; }
    
    /// <summary>
    /// Project target framework.
    /// </summary>
    public string TargetFramework { get; set; } = string.Empty;

    /// <summary>
    /// Does project use implicit usings.
    /// </summary>
    public bool ImplicitUsings { get; set; } = true;

    /// <summary>
    /// Does project allow nullable.
    /// </summary>
    public bool Nullable { get; set; } = true;
    
    /// <summary>
    /// Project's references paths.
    /// </summary>
    public List<string> References { get; set; } = [];
    
    /// <summary>
    /// Root path of project based from solution like:
    /// SBuild/src/SBuild.Manifests/
    /// </summary>
    public string RootPath { get; set; } = string.Empty;
    
    public string ToXml()
    {
        var xDoc = new XDocument(
            new XElement("Project",
                new XAttribute("Name", Name),
                new XAttribute("TargetType", TargetType.ToString()),
                new XAttribute("TargetFramework", TargetFramework),
                new XAttribute("ImplicitUsings", ImplicitUsings.ToString().ToLowerInvariant()),
                new XAttribute("Nullable", Nullable.ToString().ToLowerInvariant()),
                new XAttribute("RootPath", RootPath),
            
                References.Any() 
                    ? new XElement("References",
                        References.Select(refPath => new XElement("Reference", new XAttribute("Path", refPath)))
                    )
                    : null
            )
        );

        return xDoc.ToString();
    }
}