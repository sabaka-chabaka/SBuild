using System.CommandLine;
using System.Diagnostics;
using SBuild.Compilation;
using SBuild.Manifests;
using SBuild.Manifests.Enums;
using SBuild.ProjectGeneration;

namespace SBuild.Cli;

/// <summary>
/// Basic actions for app commands.
/// </summary>
public static class AppActions
{
    /// <summary>
    /// Build command
    /// </summary>
    /// <param name="path">Path to a c# file, a project, a solution or a directory.</param>
    /// <param name="isExecutable">Does .dll will executable (single .cs file only).</param>
    /// <returns>Lambda</returns>
    public static Func<ParseResult, CancellationToken, Task<int>> ExecuteBuildAsync(Option<string> path, Option<bool> isExecutable)
    {
        return Sync((parseResult, cancellationToken) =>
        {
            var target = ManifestLocator.Locate(parseResult.GetValue(path));
            var builder = CreateBuilder(quiet: false);

            var summary = Build(builder, target, parseResult.GetValue(isExecutable), cancellationToken);

            ConsoleReporter.PrintSummary(summary);
            return summary.Success ? 0 : 1;
        });
    }

    /// <summary>
    /// Run command: builds the target and starts the resulting program with <c>dotnet</c>.
    /// </summary>
    /// <param name="path">Path to a c# file, a project, a solution or a directory.</param>
    /// <param name="programArguments">Arguments for the program.</param>
    public static Func<ParseResult, CancellationToken, Task<int>> ExecuteRunAsync(Option<string> path, Argument<string[]> programArguments)
    {
        return async (parseResult, cancellationToken) =>
        {
            try
            {
                var target = ManifestLocator.Locate(parseResult.GetValue(path));
                var builder = CreateBuilder(quiet: true);

                var summary = Build(builder, target, executable: true, cancellationToken);

                if (!summary.Success)
                {
                    ConsoleReporter.PrintSummary(summary);
                    return 1;
                }

                var program = PickProgram(target, summary);

                var startInfo = new ProcessStartInfo("dotnet") { UseShellExecute = false };
                startInfo.ArgumentList.Add(program.OutputPath);
                foreach (var argument in parseResult.GetValue(programArguments) ?? [])
                {
                    startInfo.ArgumentList.Add(argument);
                }

                using var process = Process.Start(startInfo)
                                    ?? throw new InvalidOperationException("Не удалось запустить dotnet.");

                await process.WaitForExitAsync(cancellationToken);
                return process.ExitCode;
            }
            catch (Exception e)
            {
                return Report(e);
            }
        };
    }

    /// <summary>
    /// Clean command: removes the build output of a solution or a project.
    /// </summary>
    /// <param name="path">Path to a project, a solution or a directory.</param>
    public static Func<ParseResult, CancellationToken, Task<int>> ExecuteCleanAsync(Option<string> path)
    {
        return Sync((parseResult, _) =>
        {
            var target = ManifestLocator.Locate(parseResult.GetValue(path));

            var removed = target.Kind switch
            {
                ManifestKind.Solution => ProjectCleaner.CleanSolution(target.Path),
                ManifestKind.Project => ProjectCleaner.CleanProject(target.Path),
                _ => throw new ManifestException("clean работает с проектами и решениями, а не с отдельными .cs файлами.")
            };

            foreach (var item in removed)
            {
                ConsoleReporter.Info($"  удалено: {item}");
            }

            ConsoleReporter.Success(removed.Count == 0 ? "Очищать нечего." : "Очистка завершена.");
            return 0;
        });
    }

    /// <summary>
    /// New solution command.
    /// </summary>
    /// <param name="name">Name of the solution.</param>
    /// <param name="output">Parent directory.</param>
    public static Func<ParseResult, CancellationToken, Task<int>> ExecuteNewSolutionAsync(Argument<string> name, Option<string> output)
    {
        return Sync((parseResult, _) =>
        {
            var directory = parseResult.GetValue(output) ?? Directory.GetCurrentDirectory();
            var file = ProjectGenerator.GenerateSolution(directory, parseResult.GetValue(name)!);

            ConsoleReporter.Success($"Решение создано: {file}");
            return 0;
        });
    }

    /// <summary>
    /// New project command. The project is added to the given solution,
    /// or to the one in the current directory if there is one.
    /// </summary>
    /// <param name="name">Name of the project.</param>
    /// <param name="type">Target type.</param>
    /// <param name="solution">Solution to add the project to.</param>
    /// <param name="output">Parent directory of the project directory.</param>
    public static Func<ParseResult, CancellationToken, Task<int>> ExecuteNewProjectAsync(
        Argument<string> name, Option<TargetType> type, Option<string> solution, Option<string> output)
    {
        return Sync((parseResult, _) =>
        {
            var explicitSolution = parseResult.GetValue(solution);
            var explicitOutput = parseResult.GetValue(output);

            var solutionFile = explicitSolution != null
                ? ManifestLocator.ResolveSolutionFile(explicitSolution)
                : explicitOutput == null
                    ? ManifestLocator.TryFindSolutionFile(Directory.GetCurrentDirectory())
                    : null;

            var parentDirectory = explicitOutput
                                  ?? (solutionFile != null ? Path.GetDirectoryName(solutionFile)! : Directory.GetCurrentDirectory());

            var projectFile = ProjectGenerator.GenerateProject(parentDirectory, parseResult.GetValue(name)!, parseResult.GetValue(type));
            ConsoleReporter.Success($"Проект создан: {projectFile}");

            if (solutionFile != null)
            {
                ProjectGenerator.AddProjectToSolution(solutionFile, projectFile);
                ConsoleReporter.Info($"Добавлен в решение: {solutionFile}");
            }

            return 0;
        });
    }

    /// <summary>
    /// Add project command: puts an existing project into a solution.
    /// </summary>
    /// <param name="project">Path to the project.</param>
    /// <param name="solution">Solution to add the project to.</param>
    public static Func<ParseResult, CancellationToken, Task<int>> ExecuteAddProjectAsync(Argument<string> project, Option<string> solution)
    {
        return Sync((parseResult, _) =>
        {
            var solutionFile = parseResult.GetValue(solution) is { } explicitSolution
                ? ManifestLocator.ResolveSolutionFile(explicitSolution)
                : ManifestLocator.TryFindSolutionFile(Directory.GetCurrentDirectory())
                  ?? throw new ManifestException("Решение не найдено в текущей директории. Укажите его через --solution.");

            var added = ProjectGenerator.AddProjectToSolution(solutionFile, parseResult.GetValue(project)!);

            if (added)
                ConsoleReporter.Success($"Проект добавлен в {solutionFile}");
            else
                ConsoleReporter.Info("Проект уже есть в решении.");

            return 0;
        });
    }

    /// <summary>
    /// Add reference command: makes a project reference another project or a .dll.
    /// </summary>
    /// <param name="reference">Path to the referenced project or .dll.</param>
    /// <param name="project">The project that gets the reference.</param>
    public static Func<ParseResult, CancellationToken, Task<int>> ExecuteAddReferenceAsync(Argument<string> reference, Option<string> project)
    {
        return Sync((parseResult, _) =>
        {
            var projectFile = ManifestLocator.ResolveProjectFile(parseResult.GetValue(project) ?? ".");
            var added = ProjectGenerator.AddReference(projectFile, parseResult.GetValue(reference)!);

            if (added)
                ConsoleReporter.Success($"Ссылка добавлена в {projectFile}");
            else
                ConsoleReporter.Info("Такая ссылка уже есть.");

            return 0;
        });
    }

    private static ProjectBuilder CreateBuilder(bool quiet)
    {
        var builder = new ProjectBuilder();
        builder.ProjectBuilt += result => ConsoleReporter.PrintResult(result, quiet);
        return builder;
    }

    private static BuildSummary Build(ProjectBuilder builder, ManifestTarget target, bool executable, CancellationToken cancellationToken) =>
        target.Kind switch
        {
            ManifestKind.Solution => builder.BuildSolution(target.Path, cancellationToken),
            ManifestKind.Project => builder.BuildProject(target.Path, cancellationToken),
            _ => builder.BuildSourceFile(target.Path, executable, cancellationToken)
        };

    private static ProjectBuildResult PickProgram(ManifestTarget target, BuildSummary summary)
    {
        if (target.Kind == ManifestKind.Solution)
        {
            var programs = summary.Results.Where(r => r.Project.TargetType == TargetType.Executable).ToList();

            return programs.Count switch
            {
                1 => programs[0],
                0 => throw new ManifestException("В решении нет исполняемых проектов."),
                _ => throw new ManifestException("В решении несколько исполняемых проектов (" +
                                                 string.Join(", ", programs.Select(p => p.Project.Name)) +
                                                 "). Укажите нужный через --path.")
            };
        }

        // A project is recorded after everything it references, so the target is always last.
        var result = summary.Results[^1];

        return result.Project.TargetType == TargetType.Executable
            ? result
            : throw new ManifestException($"'{result.Project.Name}' — библиотека, запускать нечего.");
    }

    private static Func<ParseResult, CancellationToken, Task<int>> Sync(Func<ParseResult, CancellationToken, int> body) =>
        (parseResult, cancellationToken) =>
        {
            try
            {
                return Task.FromResult(body(parseResult, cancellationToken));
            }
            catch (Exception e)
            {
                return Task.FromResult(Report(e));
            }
        };

    private static int Report(Exception exception)
    {
        switch (exception)
        {
            case OperationCanceledException:
                ConsoleReporter.Error("Операция отменена.");
                return 130;

            case ManifestException or InvalidOperationException or ArgumentException
                or IOException or UnauthorizedAccessException:
                ConsoleReporter.Error($"Ошибка: {exception.Message}");
                return 1;

            default:
                ConsoleReporter.Error(exception.ToString());
                return 1;
        }
    }
}
