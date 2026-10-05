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
}