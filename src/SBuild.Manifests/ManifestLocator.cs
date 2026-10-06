using SBuild.Manifests.Abstractions;

namespace SBuild.Manifests;

/// <summary>
/// Kind of a build target.
/// </summary>
public enum ManifestKind
{
    /// <summary>A <c>.sbproj</c> file.</summary>
    Project,

    /// <summary>A <c>.sbslnx</c> file.</summary>
    Solution,

    /// <summary>A loose <c>.cs</c> file.</summary>
    SourceFile
}

/// <summary>
/// A resolved build target.
/// </summary>
/// <param name="Kind">What the path points to.</param>
/// <param name="Path">Absolute path to the file.</param>
public readonly record struct ManifestTarget(ManifestKind Kind, string Path);

/// <summary>
/// Finds project and solution manifests on disk.
/// </summary>
public static class ManifestLocator
{
    /// <summary>
    /// Converts <c>/</c> and <c>\</c> to the separator of the current platform.
    /// </summary>
    public static string NormalizeSeparators(string path) =>
        path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

    /// <summary>
    /// Resolves a file or directory to a single <c>.sbproj</c> file.
    /// </summary>
    /// <exception cref="ManifestException">Nothing found, or several candidates.</exception>
    public static string ResolveProjectFile(string pathOrDirectory) =>
        ResolveSingle(pathOrDirectory, SProject.Extension, "проект");

    /// <summary>
    /// Resolves a file or directory to a single <c>.sbslnx</c> file.
    /// </summary>
    /// <exception cref="ManifestException">Nothing found, or several candidates.</exception>
    public static string ResolveSolutionFile(string pathOrDirectory) =>
        ResolveSingle(pathOrDirectory, SSolution.Extension, "решение");

    /// <summary>
    /// Finds a solution file in a directory, or returns <c>null</c> when there is none.
    /// </summary>
    /// <exception cref="ManifestException">Several solutions in one directory.</exception>
    public static string? TryFindSolutionFile(string directory)
    {
        if (!Directory.Exists(directory)) return null;

        var files = Directory.GetFiles(directory, "*" + SSolution.Extension);
        return files.Length switch
        {
            0 => null,
            1 => Path.GetFullPath(files[0]),
            _ => throw new ManifestException($"В '{directory}' найдено несколько файлов решения: " +
                                             string.Join(", ", files.Select(Path.GetFileName)))
        };
    }

    /// <summary>
    /// Works out what a user-supplied path means: a solution, a project or a source file.
    /// A directory resolves to its solution first, then to its project.
    /// </summary>
    /// <exception cref="ManifestException">The path does not lead to anything buildable.</exception>
    public static ManifestTarget Locate(string? path)
    {
        var full = Path.GetFullPath(NormalizeSeparators(string.IsNullOrWhiteSpace(path) ? "." : path));

        if (File.Exists(full))
        {
            var ext = Path.GetExtension(full);
            if (ext.Equals(SSolution.Extension, StringComparison.OrdinalIgnoreCase))
                return new ManifestTarget(ManifestKind.Solution, full);
            if (ext.Equals(SProject.Extension, StringComparison.OrdinalIgnoreCase))
                return new ManifestTarget(ManifestKind.Project, full);
            if (ext.Equals(".cs", StringComparison.OrdinalIgnoreCase))
                return new ManifestTarget(ManifestKind.SourceFile, full);

            throw new ManifestException(
                $"Неподдерживаемый тип файла '{full}'. Ожидается {SSolution.Extension}, {SProject.Extension} или .cs.");
        }

        if (!Directory.Exists(full))
            throw new ManifestException($"Путь '{full}' не существует.");

        var solution = TryFindSolutionFile(full);
        if (solution != null)
            return new ManifestTarget(ManifestKind.Solution, solution);

        return new ManifestTarget(ManifestKind.Project, ResolveProjectFile(full));
    }

    private static string ResolveSingle(string pathOrDirectory, string extension, string what)
    {
        var full = Path.GetFullPath(NormalizeSeparators(pathOrDirectory));

        if (File.Exists(full))
        {
            if (!Path.GetExtension(full).Equals(extension, StringComparison.OrdinalIgnoreCase))
                throw new ManifestException($"'{full}' не является файлом {what} ({extension}).");
            return full;
        }

        if (!Directory.Exists(full))
            throw new ManifestException($"Не найден {what}: '{full}'.");

        var files = Directory.GetFiles(full, "*" + extension);
        return files.Length switch
        {
            0 => throw new ManifestException($"В '{full}' нет файла {extension}."),
            1 => Path.GetFullPath(files[0]),
            _ => throw new ManifestException($"В '{full}' несколько файлов {extension}: " +
                                             string.Join(", ", files.Select(Path.GetFileName)) +
                                             ". Укажите нужный явно.")
        };
    }
}
