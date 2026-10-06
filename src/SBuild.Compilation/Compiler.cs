using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SBuild.Compilation;

/// <summary>
/// A main class that compiling csharp sources to .dll with roslyn.
/// </summary>
public static class Compiler
{
    /// <summary>
    /// Compiles a set of source files into a single assembly.
    /// Files are written to disk only when compilation succeeds.
    /// </summary>
    /// <param name="request">Sources, references and options.</param>
    /// <param name="cancellationToken">Cancels compilation.</param>
    /// <returns>Result with compiler diagnostics.</returns>
    public static CompilationResult Compile(CompilationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Sources.Count == 0)
        {
            return Failure("Нет исходных файлов для компиляции.");
        }

        if (ReferenceAssemblies.Framework.Count == 0)
        {
            return Failure("Не удалось найти базовые сборки .NET рядом с SBuild.");
        }

        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);

        var syntaxTrees = new List<SyntaxTree>(request.Sources.Count + 1);
        foreach (var source in request.Sources)
        {
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(source.Text, parseOptions, source.Path, Encoding.UTF8));
        }

        if (request.ImplicitUsings)
        {
            syntaxTrees.Add(ImplicitUsingsGenerator.CreateSyntaxTree(parseOptions));
        }

        var references = new List<MetadataReference>(ReferenceAssemblies.Framework);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var reference in request.References)
        {
            var full = Path.GetFullPath(reference);

            if (!File.Exists(full))
            {
                return Failure($"Сборка '{full}' не найдена.");
            }

            if (seen.Add(full))
            {
                references.Add(MetadataReference.CreateFromFile(full));
            }
        }

        var options = new CSharpCompilationOptions(
            request.IsExecutable ? OutputKind.ConsoleApplication : OutputKind.DynamicallyLinkedLibrary,
            optimizationLevel: OptimizationLevel.Release,
            allowUnsafe: false,
            nullableContextOptions: request.Nullable ? NullableContextOptions.Enable : NullableContextOptions.Disable,
            deterministic: true);

        var outputPath = Path.GetFullPath(request.OutputPath);
        var compilation = CSharpCompilation.Create(
            Path.GetFileNameWithoutExtension(outputPath),
            syntaxTrees,
            references,
            options);

        using var peStream = new MemoryStream();
        using var pdbStream = new MemoryStream();

        var emitResult = compilation.Emit(peStream, pdbStream, cancellationToken: cancellationToken);

        var diagnostics = emitResult.Diagnostics
            .Where(d => d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToList();

        if (!emitResult.Success)
        {
            return new CompilationResult { Success = false, Diagnostics = diagnostics };
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllBytes(outputPath, peStream.ToArray());
        File.WriteAllBytes(Path.ChangeExtension(outputPath, ".pdb"), pdbStream.ToArray());

        return new CompilationResult { Success = true, OutputPath = outputPath, Diagnostics = diagnostics };

        static CompilationResult Failure(string message) => new() { Success = false, Error = message };
    }

    /// <summary>
    /// A shortcut for compiling a single source string to .dll.
    /// </summary>
    /// <param name="sourceCode">A source code to compile</param>
    /// <param name="outputPath">A path to save .dll.</param>
    /// <param name="createExe">Does it will executable.</param>
    /// <returns>Result of compilation, true if successfully, false if failed.</returns>
    public static bool Compile(string sourceCode, string outputPath, bool createExe = false)
    {
        var result = Compile(new CompilationRequest
        {
            OutputPath = outputPath,
            Sources = [new SourceFile("Program.cs", sourceCode)],
            IsExecutable = createExe
        });

        if (!result.Success)
        {
            Console.WriteLine("Ошибка компиляции:");

            if (result.Error != null)
            {
                Console.WriteLine($"\t{result.Error}");
            }

            foreach (var diagnostic in result.Errors)
            {
                Console.WriteLine($"\t{diagnostic}");
            }

            return false;
        }

        Console.WriteLine($"Успешно скомпилировано в: {result.OutputPath}");
        return true;
    }
}
