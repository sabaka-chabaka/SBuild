using System.Security.Cryptography;
using System.Text.Json;
using SBuild.Manifests.Abstractions;

namespace SBuild.Compilation;

/// <summary>
/// Persistent state of a project build used to determine whether a project needs recompilation.
/// </summary>
public sealed class ProjectBuildState
{
    public int FormatVersion { get; set; } = 1;
    public string TargetFramework { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public bool Nullable { get; set; }
    public bool ImplicitUsings { get; set; }
    public string? ManifestHash { get; set; }
    public Dictionary<string, string> Sources { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> References { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Outputs { get; set; } = new(StringComparer.Ordinal);
    public List<string> RuntimeDependencies { get; set; } = [];
}

/// <summary>
/// Manages loading, saving and validating the incremental build cache.
/// </summary>
public static class IncrementalBuildCache
{
    private const string CacheFileExtension = ".sbuildcache";

    public static string GetCacheFilePath(string outputDirectory, string projectName) =>
        Path.Combine(outputDirectory, projectName + CacheFileExtension);

    public static string ComputeFileHash(string path)
    {
        using var stream = File.OpenRead(path);
        var hashBytes = SHA256.HashData(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static bool TryLoadState(string cacheFilePath, out ProjectBuildState? state)
    {
        state = null;
        if (!File.Exists(cacheFilePath))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(cacheFilePath);
            state = JsonSerializer.Deserialize<ProjectBuildState>(json);
            return state != null && state.FormatVersion == 1;
        }
        catch
        {
            return false;
        }
    }

    public static void SaveState(string cacheFilePath, ProjectBuildState state)
    {
        try
        {
            var directory = Path.GetDirectoryName(cacheFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(cacheFilePath, json);
        }
        catch
        {
            // Non-fatal if cache saving fails
        }
    }

    public static bool CheckUpToDate(
        SProject project,
        IReadOnlyList<string> sources,
        IReadOnlyList<string> compileReferences,
        IReadOnlyList<string> runtimeDependencies,
        string outputPath,
        bool anyDependencyRebuilt)
    {
        if (anyDependencyRebuilt)
        {
            return false;
        }

        var outputDirectory = Path.GetDirectoryName(outputPath) ?? project.ResolvePath(project.OutputPath);
        var cacheFilePath = GetCacheFilePath(outputDirectory, project.Name);

        if (!TryLoadState(cacheFilePath, out var state) || state == null)
        {
            return false;
        }

        // Compare project configuration
        if (!string.Equals(state.TargetFramework, project.TargetFramework, StringComparison.Ordinal) ||
            !string.Equals(state.TargetType, project.TargetType.ToString(), StringComparison.Ordinal) ||
            state.Nullable != project.Nullable ||
            state.ImplicitUsings != project.ImplicitUsings)
        {
            return false;
        }

        // Compare manifest hash if it exists
        if (File.Exists(project.ManifestPath))
        {
            if (string.IsNullOrEmpty(state.ManifestHash) ||
                !string.Equals(state.ManifestHash, ComputeFileHash(project.ManifestPath), StringComparison.Ordinal))
            {
                return false;
            }
        }

        // Check source files
        if (state.Sources.Count != sources.Count)
        {
            return false;
        }

        foreach (var source in sources)
        {
            var fullPath = Path.GetFullPath(source);
            if (!File.Exists(fullPath) ||
                !state.Sources.TryGetValue(fullPath, out var savedHash) ||
                !string.Equals(savedHash, ComputeFileHash(fullPath), StringComparison.Ordinal))
            {
                return false;
            }
        }

        // Check references
        if (state.References.Count != compileReferences.Count)
        {
            return false;
        }

        foreach (var reference in compileReferences)
        {
            var fullPath = Path.GetFullPath(reference);
            if (!File.Exists(fullPath) ||
                !state.References.TryGetValue(fullPath, out var savedHash) ||
                !string.Equals(savedHash, ComputeFileHash(fullPath), StringComparison.Ordinal))
            {
                return false;
            }
        }

        // Check outputs
        if (!File.Exists(outputPath))
        {
            return false;
        }

        if (state.Outputs.TryGetValue(outputPath, out var expectedDllHash))
        {
            if (!string.Equals(expectedDllHash, ComputeFileHash(outputPath), StringComparison.Ordinal))
            {
                return false;
            }
        }

        var pdbPath = Path.ChangeExtension(outputPath, ".pdb");
        if (!File.Exists(pdbPath))
        {
            return false;
        }

        if (project.TargetType == Manifests.Enums.TargetType.Executable)
        {
            var runtimeConfigPath = Path.ChangeExtension(outputPath, ".runtimeconfig.json");
            if (!File.Exists(runtimeConfigPath))
            {
                return false;
            }
        }

        // Check copied runtime dependencies
        foreach (var dependency in runtimeDependencies)
        {
            var destination = Path.Combine(outputDirectory, Path.GetFileName(dependency));
            if (!File.Exists(destination))
            {
                return false;
            }
        }

        return true;
    }

    public static void RecordState(
        SProject project,
        IReadOnlyList<string> sources,
        IReadOnlyList<string> compileReferences,
        IReadOnlyList<string> runtimeDependencies,
        string outputPath)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath) ?? project.ResolvePath(project.OutputPath);
        var cacheFilePath = GetCacheFilePath(outputDirectory, project.Name);

        var state = new ProjectBuildState
        {
            FormatVersion = 1,
            TargetFramework = project.TargetFramework,
            TargetType = project.TargetType.ToString(),
            Nullable = project.Nullable,
            ImplicitUsings = project.ImplicitUsings,
            RuntimeDependencies = runtimeDependencies.ToList()
        };

        if (File.Exists(project.ManifestPath))
        {
            state.ManifestHash = ComputeFileHash(project.ManifestPath);
        }

        foreach (var source in sources)
        {
            var fullPath = Path.GetFullPath(source);
            if (File.Exists(fullPath))
            {
                state.Sources[fullPath] = ComputeFileHash(fullPath);
            }
        }

        foreach (var reference in compileReferences)
        {
            var fullPath = Path.GetFullPath(reference);
            if (File.Exists(fullPath))
            {
                state.References[fullPath] = ComputeFileHash(fullPath);
            }
        }

        if (File.Exists(outputPath))
        {
            state.Outputs[outputPath] = ComputeFileHash(outputPath);
        }

        var pdbPath = Path.ChangeExtension(outputPath, ".pdb");
        if (File.Exists(pdbPath))
        {
            state.Outputs[pdbPath] = ComputeFileHash(pdbPath);
        }

        SaveState(cacheFilePath, state);
    }
}
