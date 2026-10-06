using System.CommandLine;

namespace SBuild.Cli;

/// <summary>
/// A class with basic commands for Cli.
/// </summary>
public static class AppCommands
{
    /// <summary>
    /// All top level commands of the tool.
    /// </summary>
    public static IReadOnlyList<Command> All() =>
    [
        CreateBuildCommand(),
        CreateRunCommand(),
        CreateCleanCommand(),
        CreateNewCommand(),
        CreateAddCommand()
    ];

    /// <summary>
    /// Command for building a solution, a project or a .cs file to .dll.
    /// </summary>
    public static Command CreateBuildCommand()
    {
        var path = AppOptions.FilePath();
        var isExecutable = AppOptions.IsExecutable();

        var command = new Command("build", "Builds a solution, a project or a single csharp file")
        {
            path, isExecutable
        };
        command.SetAction(AppActions.ExecuteBuildAsync(path, isExecutable));
        return command;
    }

    /// <summary>
    /// Command for building and running a program.
    /// </summary>
    public static Command CreateRunCommand()
    {
        var path = AppOptions.FilePath();
        var args = AppOptions.ProgramArguments();

        var command = new Command("run", "Builds and runs an executable project or csharp file")
        {
            path, args
        };
        command.SetAction(AppActions.ExecuteRunAsync(path, args));
        return command;
    }

    /// <summary>
    /// Command for removing build output.
    /// </summary>
    public static Command CreateCleanCommand()
    {
        var path = AppOptions.FilePath();

        var command = new Command("clean", "Removes the build output of a solution or a project")
        {
            path
        };
        command.SetAction(AppActions.ExecuteCleanAsync(path));
        return command;
    }

    /// <summary>
    /// Commands for creating solutions and projects.
    /// </summary>
    public static Command CreateNewCommand()
    {
        var command = new Command("new", "Creates a new solution or project");

        var solutionName = AppOptions.NameArgument("solution");
        var solutionOutput = AppOptions.OutputDirectory();
        var solution = new Command("solution", "Creates a new solution in its own directory")
        {
            solutionName, solutionOutput
        };
        solution.SetAction(AppActions.ExecuteNewSolutionAsync(solutionName, solutionOutput));
        command.Subcommands.Add(solution);

        var projectName = AppOptions.NameArgument("project");
        var projectType = AppOptions.ProjectType();
        var projectSolution = AppOptions.SolutionPath();
        var projectOutput = AppOptions.OutputDirectory();
        var project = new Command("project", "Creates a new project and adds it to the solution if there is one")
        {
            projectName, projectType, projectSolution, projectOutput
        };
        project.SetAction(AppActions.ExecuteNewProjectAsync(projectName, projectType, projectSolution, projectOutput));
        command.Subcommands.Add(project);

        return command;
    }

    /// <summary>
    /// Commands for adding projects to a solution and references to a project.
    /// </summary>
    public static Command CreateAddCommand()
    {
        var command = new Command("add", "Adds a project to a solution or a reference to a project");

        var projectArgument = AppOptions.PathArgument("Path to the project (.sbproj) or its directory.");
        var solution = AppOptions.SolutionPath();
        var addProject = new Command("project", "Adds a project to a solution")
        {
            projectArgument, solution
        };
        addProject.SetAction(AppActions.ExecuteAddProjectAsync(projectArgument, solution));
        command.Subcommands.Add(addProject);

        var referenceArgument = AppOptions.PathArgument("Path to a project (.sbproj), its directory or a .dll.");
        var project = AppOptions.ProjectPath();
        var addReference = new Command("reference", "Adds a reference to a project")
        {
            referenceArgument, project
        };
        addReference.SetAction(AppActions.ExecuteAddReferenceAsync(referenceArgument, project));
        command.Subcommands.Add(addReference);

        return command;
    }
}
