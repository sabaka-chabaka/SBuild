using System.Reflection;
using Microsoft.CodeAnalysis;

namespace SBuild.Compilation;

/// <summary>
/// Metadata references to the .NET shared framework SBuild itself runs on.
/// Only <c>Microsoft.NETCore.App</c> is exposed: SBuild's own dependencies
/// (Roslyn, System.CommandLine, ...) must not leak into user code.
/// </summary>
internal static class ReferenceAssemblies
{
    private static readonly Lazy<IReadOnlyList<MetadataReference>> FrameworkLazy = new(LoadFramework);

    public static IReadOnlyList<MetadataReference> Framework => FrameworkLazy.Value;

    private static IReadOnlyList<MetadataReference> LoadFramework()
    {
        var references = new List<MetadataReference>();
        var directory = Path.GetDirectoryName(typeof(object).Assembly.Location);

        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return references;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.dll").OrderBy(f => f, StringComparer.Ordinal))
        {
            if (IsManagedAssembly(file))
            {
                references.Add(MetadataReference.CreateFromFile(file));
            }
        }

        return references;
    }

    private static bool IsManagedAssembly(string path)
    {
        try
        {
            AssemblyName.GetAssemblyName(path);
            return true;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
        catch (FileLoadException)
        {
            return false;
        }
    }
}
