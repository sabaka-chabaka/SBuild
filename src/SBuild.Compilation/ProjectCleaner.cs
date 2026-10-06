using SBuild.Manifests;

namespace SBuild.Compilation;

/// <summary>
/// Removes build output.
/// </summary>
public static class ProjectCleaner
{
    /// <summary>
    /// Cleans one project.
    /// If its output directory lies inside the project directory the whole output directory is removed,
    /// otherwise only the files SBuild itself produces for the project (<c>.dll</c>, <c>.pdb</c>,
    /// <c>.runtimeconfig.json</c>) are deleted, so a custom <c>OutputPath</c> can never wipe source files.
    /// </summary>
    /// <param name="projectFile">A <c>.sbproj</c> file or a directory containing exactly one.</param>
    /// <returns>Deleted files and directories.</returns>
    public static IReadOnlyList<string> CleanProject(string projectFile)
    {
        var project = XmlParser.LoadProject(ManifestLocator.ResolveProjectFile(projectFile));
        var outputDirectory = project.ResolvePath(project.OutputPath);
        var removed = new List<string>();

        if (!Directory.Exists(outputDirectory))
        {
            return removed;
        }

        if (PathUtil.IsStrictlyUnder(outputDirectory, project.RootPath))
        {
            Directory.Delete(outputDirectory, recursive: true);
            removed.Add(outputDirectory);
            return removed;
        }

        var basePath = Path.Combine(outputDirectory, project.Name);
        foreach (var file in new[] { basePath + ".dll", basePath + ".pdb", basePath + ".runtimeconfig.json" })
        {
            if (File.Exists(file))
            {
                File.Delete(file);
                removed.Add(file);
            }
        }

        return removed;
    }

    /// <summary>
    /// Cleans every project of a solution.
    /// </summary>
    /// <param name="solutionFile">A <c>.sbslnx</c> file or a directory containing exactly one.</param>
    /// <returns>Deleted files and directories.</returns>
    public static IReadOnlyList<string> CleanSolution(string solutionFile)
    {
        var solution = XmlParser.LoadSolution(ManifestLocator.ResolveSolutionFile(solutionFile));
        var removed = new List<string>();

        foreach (var projectFile in solution.ResolveProjectFiles())
        {
            removed.AddRange(CleanProject(projectFile));
        }

        return removed;
    }
}
