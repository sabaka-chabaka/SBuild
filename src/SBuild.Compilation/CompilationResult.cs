using Microsoft.CodeAnalysis;

namespace SBuild.Compilation;

/// <summary>
/// Outcome of <see cref="Compiler.Compile(CompilationRequest, CancellationToken)"/>.
/// </summary>
public sealed class CompilationResult
{
    /// <summary>
    /// True when the assembly was emitted.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Path of the emitted <c>.dll</c> (empty if compilation failed).
    /// </summary>
    public string OutputPath { get; init; } = string.Empty;

    /// <summary>
    /// Errors and warnings reported by the compiler.
    /// </summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];

    /// <summary>
    /// Failure reason when compilation could not even start (no sources, missing reference, ...).
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Only the errors from <see cref="Diagnostics"/>.
    /// </summary>
    public IEnumerable<Diagnostic> Errors => Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);

    /// <summary>
    /// Only the warnings from <see cref="Diagnostics"/>.
    /// </summary>
    public IEnumerable<Diagnostic> Warnings => Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning);
}
