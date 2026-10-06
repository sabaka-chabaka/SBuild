using Microsoft.CodeAnalysis;
using SBuild.Manifests;
using SBuild.Manifests.Abstractions;
using SBuild.Manifests.Enums;

namespace SBuild.Compilation;

/// <summary>
/// Outcome of building one project.
/// </summary>
public sealed class ProjectBuildResult
{
    /// <summary>
    /// The project that was built.
    /// </summary>
    public required SProject Project { get; init; }

    /// <summary>
    /// True when the project (and all of its references) built successfully.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Path of the produced <c>.dll</c> (empty on failure).
    /// </summary>
    public string OutputPath { get; init; } = string.Empty;

    /// <summary>
    /// Compiler errors and warnings.
    /// </summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];

    /// <summary>
    /// Non-compiler failure reason (missing reference, no sources, I/O error, ...).
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Non-fatal remarks about the build.
    /// </summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    /// <summary>
    /// Number of source files that were compiled.
    /// </summary>
    public int SourceCount { get; init; }

    /// <summary>
    /// Assemblies needed next to this project's output at runtime (all transitive references).
    /// </summary>
    internal IReadOnlyList<string> RuntimeDependencies { get; init; } = [];

    /// <summary>
    /// Only the errors from <see cref="Diagnostics"/>.
    /// </summary>
    public IEnumerable<Diagnostic> Errors => Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);

    /// <summary>
    /// Only the warnings from <see cref="Diagnostics"/>.
    /// </summary>
    public IEnumerable<Diagnostic> Warnings => Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning);
}

/// <summary>
/// Results of a whole build (a project with its references, or a solution).
/// </summary>
/// <param name="Results">Per-project results in build order: references come before their dependants.</param>
public sealed record BuildSummary(IReadOnlyList<ProjectBuildResult> Results)
{
    /// <summary>
    /// True when every project built successfully.
    /// </summary>
    public bool Success => Results.All(r => r.Success);
}

/// <summary>
/// Builds projects, solutions and loose source files.
/// One instance remembers what it has already built, so a project shared by several
/// dependants is compiled once. Use a new instance for every build.
/// </summary>
public sealed class ProjectBuilder
{
    private readonly Dictionary<string, ProjectBuildResult> _cache = new(StringComparer.Ordinal);
    private readonly List<ProjectBuildResult> _results = [];
    private readonly List<string> _stack = [];

    /// <summary>
    /// Raised right after each project has been built (successfully or not), in build order.
    /// </summary>
    public event Action<ProjectBuildResult>? ProjectBuilt;

    /// <summary>
    /// Builds a project and, first, everything it references.
    /// </summary>
    /// <param name="projectFile">A <c>.sbproj</c> file or a directory containing exactly one.</param>
    /// <exception cref="ManifestException">A manifest is missing, malformed, or references form a cycle.</exception>
    public BuildSummary BuildProject(string projectFile, CancellationToken cancellationToken = default)
    {
        BuildManifest(ManifestLocator.ResolveProjectFile(projectFile), cancellationToken);
        return new BuildSummary(_results.ToArray());
    }

    /// <summary>
    /// Builds every project of a solution (references are built before the projects that use them).
    /// </summary>
    /// <param name="solutionFile">A <c>.sbslnx</c> file or a directory containing exactly one.</param>
    /// <exception cref="ManifestException">A manifest is missing, malformed, or references form a cycle.</exception>
    public BuildSummary BuildSolution(string solutionFile, CancellationToken cancellationToken = default)
    {
        var solution = XmlParser.LoadSolution(ManifestLocator.ResolveSolutionFile(solutionFile));
        var projectFiles = solution.ResolveProjectFiles();

        if (projectFiles.Count == 0)
        {
            throw new ManifestException($"В решении '{solution.Name}' нет проектов.");
        }

        foreach (var projectFile in projectFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BuildManifest(projectFile, cancellationToken);
        }

        return new BuildSummary(_results.ToArray());
    }

    /// <summary>
    /// Compiles one loose <c>.cs</c> file. The output is placed next to it.
    /// </summary>
    /// <param name="sourceFile">Path to the C# file.</param>
    /// <param name="executable">Console application when true, class library otherwise.</param>
    public BuildSummary BuildSourceFile(string sourceFile, bool executable, CancellationToken cancellationToken = default)
    {
        var full = Path.GetFullPath(sourceFile);

        if (!File.Exists(full))
        {
            throw new ManifestException($"Файл '{full}' не найден.");
        }

        var project = new SProject
        {
            Name = Path.GetFileNameWithoutExtension(full),
            TargetType = executable ? TargetType.Executable : TargetType.Library,
            RootPath = Path.GetDirectoryName(full)!,
            ManifestPath = full,
            OutputPath = "."
        };

        Record(full, BuildCore(project, [full], cancellationToken));
        return new BuildSummary(_results.ToArray());
    }

    private ProjectBuildResult BuildManifest(string manifestFile, CancellationToken cancellationToken)
    {
        var key = Path.GetFullPath(manifestFile);

        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (_stack.Contains(key, StringComparer.Ordinal))
        {
            var cycle = _stack.SkipWhile(s => !s.Equals(key, StringComparison.Ordinal)).Append(key);
            throw new ManifestException("Циклическая ссылка между проектами: " +
                                        string.Join(" -> ", cycle.Select(Path.GetFileNameWithoutExtension)));
        }

        _stack.Add(key);
        try
        {
            var project = XmlParser.LoadProject(key);
            return Record(key, BuildCore(project, null, cancellationToken));
        }
        finally
        {
            _stack.RemoveAt(_stack.Count - 1);
        }
    }

    private ProjectBuildResult Record(string key, ProjectBuildResult result)
    {
        _cache[key] = result;
        _results.Add(result);
        ProjectBuilt?.Invoke(result);
        return result;
    }

    private ProjectBuildResult BuildCore(SProject project, IReadOnlyList<string>? explicitSources,
        CancellationToken cancellationToken)
    {
        if (!TargetFrameworks.TryParse(project.TargetFramework, out var framework))
        {
            return Fail(project,
                $"Неподдерживаемый TargetFramework '{project.TargetFramework}'. Ожидается формат netX.Y, например net10.0.");
        }

        var notes = new List<string>();
        if (framework.Major != Environment.Version.Major)
        {
            notes.Add($"Целевой фреймворк net{framework.Major}.{framework.Minor} отличается от среды SBuild " +
                      $"(net{Environment.Version.Major}.{Environment.Version.Minor}): " +
                      "компиляция идёт против библиотек среды SBuild.");
        }

        // References: other projects are built first, plain .dll files are used as they are.
        var compileReferences = new List<string>();
        var runtimeDependencies = new List<string>();
        var referenceErrors = new List<string>();

        foreach (var reference in project.References)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var path = project.ResolvePath(reference);

            if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(path))
                {
                    AddUnique(compileReferences, path);
                    AddUnique(runtimeDependencies, path);
                }
                else
                {
                    referenceErrors.Add($"Сборка '{reference}' не найдена ({path}).");
                }

                continue;
            }

            string referencedProjectFile;
            try
            {
                referencedProjectFile = ManifestLocator.ResolveProjectFile(path);
            }
            catch (ManifestException e)
            {
                referenceErrors.Add($"Некорректная ссылка '{reference}': {e.Message}");
                continue;
            }

            var dependency = BuildManifest(referencedProjectFile, cancellationToken);

            if (!dependency.Success)
            {
                referenceErrors.Add($"Не удалось собрать зависимость '{dependency.Project.Name}'.");
                continue;
            }

            AddUnique(compileReferences, dependency.OutputPath);
            AddUnique(runtimeDependencies, dependency.OutputPath);

            foreach (var transitive in dependency.RuntimeDependencies)
            {
                AddUnique(compileReferences, transitive);
                AddUnique(runtimeDependencies, transitive);
            }
        }

        if (referenceErrors.Count > 0)
        {
            return Fail(project, string.Join(Environment.NewLine, referenceErrors), notes);
        }

        var outputDirectory = project.ResolvePath(project.OutputPath);

        IReadOnlyList<string> sources;
        if (explicitSources != null)
        {
            sources = explicitSources;
        }
        else
        {
            var excluded = project.Excludes.Select(project.ResolvePath).ToList();
            if (PathUtil.IsStrictlyUnder(outputDirectory, project.RootPath))
            {
                excluded.Add(outputDirectory);
            }

            sources = SourceDiscovery.FindSources(project.RootPath, excluded);
        }

        if (sources.Count == 0)
        {
            return Fail(project, $"В '{project.RootPath}' не найдено ни одного .cs файла.", notes);
        }

        var outputPath = Path.Combine(outputDirectory, project.Name + ".dll");

        try
        {
            var request = new CompilationRequest
            {
                OutputPath = outputPath,
                Sources = sources.Select(p => new SourceFile(p, File.ReadAllText(p))).ToList(),
                References = compileReferences,
                IsExecutable = project.TargetType == TargetType.Executable,
                ImplicitUsings = project.ImplicitUsings,
                Nullable = project.Nullable
            };

            var compilation = Compiler.Compile(request, cancellationToken);

            if (!compilation.Success)
            {
                return new ProjectBuildResult
                {
                    Project = project,
                    Success = false,
                    Diagnostics = compilation.Diagnostics,
                    Error = compilation.Error,
                    Notes = notes,
                    SourceCount = sources.Count
                };
            }

            if (project.TargetType == TargetType.Executable)
            {
                TargetFrameworks.WriteRuntimeConfig(Path.ChangeExtension(outputPath, ".runtimeconfig.json"), framework);
            }

            foreach (var dependency in runtimeDependencies)
            {
                CopyToOutput(dependency, outputDirectory);
            }

            return new ProjectBuildResult
            {
                Project = project,
                Success = true,
                OutputPath = compilation.OutputPath,
                Diagnostics = compilation.Diagnostics,
                Notes = notes,
                SourceCount = sources.Count,
                RuntimeDependencies = runtimeDependencies
            };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Fail(project, $"Ошибка ввода-вывода: {e.Message}", notes);
        }
    }

    private static ProjectBuildResult Fail(SProject project, string error, IReadOnlyList<string>? notes = null) => new()
    {
        Project = project,
        Success = false,
        Error = error,
        Notes = notes ?? []
    };

    private static void AddUnique(List<string> list, string path)
    {
        if (!list.Contains(path, StringComparer.Ordinal))
        {
            list.Add(path);
        }
    }

    private static void CopyToOutput(string assemblyPath, string outputDirectory)
    {
        var destination = Path.Combine(outputDirectory, Path.GetFileName(assemblyPath));

        if (Path.GetFullPath(destination).Equals(Path.GetFullPath(assemblyPath), PathUtil.Comparison))
        {
            return;
        }

        File.Copy(assemblyPath, destination, overwrite: true);

        var pdb = Path.ChangeExtension(assemblyPath, ".pdb");
        if (File.Exists(pdb))
        {
            File.Copy(pdb, Path.ChangeExtension(destination, ".pdb"), overwrite: true);
        }
    }
}
