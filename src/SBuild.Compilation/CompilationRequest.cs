namespace SBuild.Compilation;

/// <summary>
/// A single C# source file.
/// </summary>
/// <param name="Path">Path of the file (used in diagnostics and debug info).</param>
/// <param name="Text">Content of the file.</param>
public sealed record SourceFile(string Path, string Text);

/// <summary>
/// Everything <see cref="Compiler"/> needs to produce one assembly.
/// </summary>
public sealed class CompilationRequest
{
    /// <summary>
    /// Full path of the output <c>.dll</c>. The file name (without extension) becomes the assembly name.
    /// A portable <c>.pdb</c> is written next to it.
    /// </summary>
    public required string OutputPath { get; init; }

    /// <summary>
    /// Source files to compile together as one assembly.
    /// </summary>
    public required IReadOnlyList<SourceFile> Sources { get; init; }

    /// <summary>
    /// Paths to additional assemblies to compile against (the .NET shared framework is always referenced).
    /// </summary>
    public IReadOnlyList<string> References { get; init; } = [];

    /// <summary>
    /// Console application when true, class library otherwise.
    /// </summary>
    public bool IsExecutable { get; init; }

    /// <summary>
    /// Add the default global usings (<see cref="ImplicitUsingsGenerator.Namespaces"/>).
    /// </summary>
    public bool ImplicitUsings { get; init; } = true;

    /// <summary>
    /// Enable the nullable annotation and warning context.
    /// </summary>
    public bool Nullable { get; init; } = true;
}
