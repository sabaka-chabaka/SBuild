using SBuild.Manifests.Abstractions;
using SBuild.Manifests.Enums;

namespace SBuild.ProjectGeneration;

/// <summary>
/// This class generates template projects/solutions.
/// </summary>
public static class ProjectGenerator
{
    /// <summary>
    /// Generates a solution template.
    /// </summary>
    /// <param name="workDir">Parent directory where the solution folder will be created.</param>
    /// <param name="name">The name of the solution.</param>
    public static void GenerateSolution(string workDir, string name)
    {
        var solutionDir = Path.Combine(workDir, name);
        
        if (!Directory.Exists(solutionDir))
        {
            Directory.CreateDirectory(solutionDir);
        }
        
        var sbslnxPath = Path.Combine(solutionDir, name + ".sbslnx");
        
        File.WriteAllText(sbslnxPath, $@"<Solution Name=""{name}"">
</Solution>");
    }

    /// <summary>
    /// Creating project template
    /// </summary>
    /// <param name="solutionDir">Directory of solution.</param>
    /// <param name="name">Name of project.</param>
    /// <param name="targetType">Target type of project.</param>
    public static void GenerateProject(string solutionDir, string name, TargetType targetType)
    {
        var projectDir = Path.Combine(solutionDir, name);
        if (!Directory.Exists(projectDir))
        {
            Directory.CreateDirectory(projectDir);
        }

        var project = new SProject
        {
            Name = name,
            TargetFramework = AppContext.TargetFrameworkName!,
            TargetType = targetType,
            RootPath = projectDir
        };

        File.WriteAllText(projectDir, project.ToXml());
    }
}