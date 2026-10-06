using SBuild.Compilation;

namespace SBuild.Cli;

/// <summary>
/// Prints build results to the console.
/// </summary>
internal static class ConsoleReporter
{
    public static void Info(string message) => Console.WriteLine(message);

    public static void Success(string message) => Write(message, ConsoleColor.Green, Console.Out);

    public static void Warning(string message) => Write(message, ConsoleColor.Yellow, Console.Out);

    public static void Error(string message) => Write(message, ConsoleColor.Red, Console.Error);

    /// <summary>
    /// Prints the outcome of a single project.
    /// </summary>
    /// <param name="result">The result to print.</param>
    /// <param name="quiet">Do not print the line for a successful project.</param>
    public static void PrintResult(ProjectBuildResult result, bool quiet = false)
    {
        foreach (var note in result.Notes)
        {
            Warning($"  {result.Project.Name}: {note}");
        }

        if (result.Success)
        {
            foreach (var warning in result.Warnings)
            {
                Warning($"  {warning}");
            }

            if (!quiet)
            {
                Success($"  {result.Project.Name} -> {result.OutputPath}");
            }

            return;
        }

        Error($"  Error building project {result.Project.Name}:");

        if (!string.IsNullOrEmpty(result.Error))
        {
            foreach (var line in result.Error.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
            {
                Error($"    {line}");
            }
        }

        foreach (var error in result.Errors)
        {
            Error($"    {error}");
        }

        foreach (var warning in result.Warnings)
        {
            Warning($"    {warning}");
        }
    }

    /// <summary>
    /// Prints the final line of a build.
    /// </summary>
    public static void PrintSummary(BuildSummary summary)
    {
        var failed = summary.Results.Count(r => !r.Success);
        var warnings = summary.Results.Sum(r => r.Warnings.Count());

        if (failed == 0)
        {
            Success($"Сборка успешно завершена. Projects: {summary.Results.Count}, warns: {warnings}.");
        }
        else
        {
            Error($"Build ended with errors. Not builded projects: {failed} out of {summary.Results.Count}.");
        }
    }

    private static void Write(string message, ConsoleColor color, TextWriter writer)
    {
        var useColor = !Console.IsOutputRedirected && !Console.IsErrorRedirected;

        if (useColor) Console.ForegroundColor = color;

        try
        {
            writer.WriteLine(message);
        }
        finally
        {
            if (useColor) Console.ResetColor();
        }
    }
}
