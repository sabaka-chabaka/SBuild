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
    public bool ImplicitUsings { get; set; }
    
    /// <summary>
    /// Does project allow nullable.
    /// </summary>
    public bool Nullable { get; set; }
    
    /// <summary>
    /// Project's references
    /// </summary>
    public List<SProject> References { get; set; } = [];
    
    /// <summary>
    /// Root path of project based from solution like:
    /// SBuild/src/SBuild.Manifests/
    /// </summary>
    public string RootPath { get; set; } = string.Empty;
}