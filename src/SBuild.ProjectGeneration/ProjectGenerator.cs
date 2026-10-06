using System.Text;
using SBuild.Manifests;
using SBuild.Manifests.Abstractions;
using SBuild.Manifests.Enums;

namespace SBuild.ProjectGeneration;

/// <summary>
/// This class generates template projects/solutions and edits existing manifests.
/// </summary>
public static class ProjectGenerator
{
    /// <summary>
    /// Generates a solution template.
    /// </summary>
    /// <param name="workDir">Parent directory where the solution folder will be created.</param>
    /// <param name="name">The name of the solution.</param>
    /// <returns>Path of the created <c>.sbslnx</c> file.</returns>
    /// <exception cref="InvalidOperationException">The solution already exists.</exception>
    public static string GenerateSolution(string workDir, string name)
    {
        ValidateName(name);

        var solutionDir = Path.GetFullPath(Path.Combine(workDir, name));
        Directory.CreateDirectory(solutionDir);

        var solutionPath = Path.Combine(solutionDir, name + SSolution.Extension);
        if (File.Exists(solutionPath))
        {
            throw new InvalidOperationException($"Решение '{solutionPath}' уже существует.");
        }

        XmlParser.SaveSolution(new SSolution { Name = name }, solutionPath);
        return solutionPath;
    }

    /// <summary>
    /// Creating project template: <c>solutionDir/name/name.sbproj</c> plus a starter source file.
    /// </summary>
    /// <param name="solutionDir">Directory of solution.</param>
    /// <param name="name">Name of project.</param>
    /// <param name="targetType">Target type of project.</param>
    /// <returns>Path of the created <c>.sbproj</c> file.</returns>
    /// <exception cref="InvalidOperationException">The project already exists.</exception>
    public static string GenerateProject(string solutionDir, string name, TargetType targetType)
    {
        ValidateName(name);

        var projectDir = Path.GetFullPath(Path.Combine(solutionDir, name));
        Directory.CreateDirectory(projectDir);

        var projectPath = Path.Combine(projectDir, name + SProject.Extension);
        if (File.Exists(projectPath))
        {
            throw new InvalidOperationException($"Проект '{projectPath}' уже существует.");
        }

        var project = new SProject
        {
            Name = name,
            TargetFramework = SProject.DefaultTargetFramework,
            TargetType = targetType
        };

        XmlParser.SaveProject(project, projectPath);

        var (fileName, content) = targetType == TargetType.Executable
            ? ("Program.cs", "Console.WriteLine(\"Hello, World!\");" + Environment.NewLine)
            : ("Class1.cs", LibraryTemplate(name));

        var sourcePath = Path.Combine(projectDir, fileName);
        if (!File.Exists(sourcePath))
        {
            File.WriteAllText(sourcePath, content);
        }

        return projectPath;
    }

    /// <summary>
    /// Adds a project to a solution manifest.
    /// </summary>
    /// <param name="solutionFile">A <c>.sbslnx</c> file or a directory containing exactly one.</param>
    /// <param name="projectFile">A <c>.sbproj</c> file or a directory containing exactly one.</param>
    /// <returns>True when added, false when the solution already contained the project.</returns>
    public static bool AddProjectToSolution(string solutionFile, string projectFile)
    {
        var solutionPath = ManifestLocator.ResolveSolutionFile(solutionFile);
        var projectPath = ManifestLocator.ResolveProjectFile(projectFile);

        var solution = XmlParser.LoadSolution(solutionPath);

        var alreadyAdded = solution.ResolveProjectFiles()
            .Any(p => p.Equals(projectPath, StringComparison.Ordinal));
        if (alreadyAdded)
        {
            return false;
        }

        solution.Projects.Add(RelativePath(solution.RootPath, projectPath));
        XmlParser.SaveSolution(solution, solutionPath);
        return true;
    }

    /// <summary>
    /// Adds a reference (another project or a <c>.dll</c>) to a project manifest.
    /// </summary>
    /// <param name="projectFile">A <c>.sbproj</c> file or a directory containing exactly one.</param>
    /// <param name="reference">A <c>.sbproj</c> file, a project directory or a <c>.dll</c> file.</param>
    /// <returns>True when added, false when the reference was already there.</returns>
    /// <exception cref="ManifestException">The reference does not exist or points to the project itself.</exception>
    public static bool AddReference(string projectFile, string reference)
    {
        var projectPath = ManifestLocator.ResolveProjectFile(projectFile);
        var project = XmlParser.LoadProject(projectPath);

        var referencePath = Path.GetFullPath(ManifestLocator.NormalizeSeparators(reference));

        if (referencePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            if (!File.Exists(referencePath))
            {
                throw new ManifestException($"Сборка '{referencePath}' не найдена.");
            }
        }
        else
        {
            referencePath = ManifestLocator.ResolveProjectFile(referencePath);

            if (referencePath.Equals(projectPath, StringComparison.Ordinal))
            {
                throw new ManifestException("Проект не может ссылаться на самого себя.");
            }
        }

        var relative = RelativePath(project.RootPath, referencePath);

        var alreadyAdded = project.References.Any(r => project.ResolvePath(r).Equals(referencePath, StringComparison.Ordinal));
        if (alreadyAdded)
        {
            return false;
        }

        project.References.Add(relative);
        XmlParser.SaveProject(project, projectPath);
        return true;
    }

    private static string RelativePath(string fromDirectory, string toPath) =>
        Path.GetRelativePath(fromDirectory, toPath).Replace('\\', '/');

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            name is "." or "..")
        {
            throw new ArgumentException($"Недопустимое имя '{name}'.", nameof(name));
        }
    }

    private static string LibraryTemplate(string name) =>
        $$"""
          namespace {{ToNamespace(name)}};

          public class Class1
          {
          }

          """;

    private static string ToNamespace(string name)
    {
        var sb = new StringBuilder(name.Length);

        foreach (var part in name.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (sb.Length > 0) sb.Append('.');

            if (!char.IsLetter(part[0]) && part[0] != '_') sb.Append('_');

            foreach (var c in part)
            {
                sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            }
        }

        return sb.Length > 0 ? sb.ToString() : "Library";
    }
}
