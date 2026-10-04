using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SBuild.Compilation;

/// <summary>
/// A main class that compiling csharp sources to .dll with roslyn.
/// </summary>
public static class Compiler
{
    /// <summary>
    /// A main function for compiling sources to .dll.
    /// </summary>
    /// <param name="sourceCode">A source code to compile</param>
    /// <param name="outputPath">A path to save .dll and runtimeconfig.</param>
    /// <param name="createExe">Does it will executable.</param>
    /// <returns>Result of compilation, true if successfully, false if failed.</returns>
    public static bool Compile(string sourceCode, string outputPath, bool createExe = false)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        var globalUsingsTree = CSharpSyntaxTree.ParseText("""
                                                              global using System;
                                                              global using System.IO;
                                                              global using System.Linq;
                                                              global using System.Collections.Generic;
                                                              global using System.Threading;
                                                              global using System.Threading.Tasks;
                                                          """);

        var outputKind = createExe ? OutputKind.ConsoleApplication : OutputKind.DynamicallyLinkedLibrary;

        var options = new CSharpCompilationOptions(outputKind, optimizationLevel: OptimizationLevel.Release,
            allowUnsafe: false);

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToHashSet();

        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trustedAssemblies)
        {
            var paths = trustedAssemblies.Split(Path.PathSeparator);
            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    references.Add(MetadataReference.CreateFromFile(path));
                }
            }
        }

        var finalReferences = references.ToList();

        var assemblyName = Path.GetFileNameWithoutExtension(outputPath);
        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: [syntaxTree, globalUsingsTree],
            references: finalReferences,
            options: options
        );

        var result = compilation.Emit(outputPath);

        if (!result.Success)
        {
            Console.WriteLine("Ошибка компиляции:");
            foreach (var diagnostic in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
            {
                Console.WriteLine($"\t{diagnostic.Id}: {diagnostic.GetMessage()}");
            }

            return false;
        }

        Console.WriteLine($"Успешно скомпилировано в: {outputPath}");
        return true;
    }
}