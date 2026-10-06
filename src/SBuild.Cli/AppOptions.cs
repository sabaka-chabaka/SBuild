using System.CommandLine;
using SBuild.Manifests.Enums;

namespace SBuild.Cli;

/// <summary>
/// Factories for the options and arguments shared by Cli commands.
/// Every command gets its own instances.
/// </summary>
public static class AppOptions
{
    /// <summary>
    /// A path to a C# file, a project, a solution or a directory with one of them.
    /// </summary>
    public static Option<string> FilePath() => new("--path", "-p")
    {
        Description = "Path to a .cs file, a project (.sbproj), a solution (.sbslnx) or a directory containing one. " +
                      "Defaults to the current directory."
    };

    /// <summary>
    /// Does out .dll will executable (only matters for a single .cs file).
    /// </summary>
    public static Option<bool> IsExecutable() => new("--is-executable", "-ie")
    {
        Description = "Build a single .cs file as an executable program instead of a library."
    };

    /// <summary>
    /// Directory in which something is created.
    /// </summary>
    public static Option<string> OutputDirectory() => new("--output", "-o")
    {
        Description = "Directory to create the result in. Defaults to the current directory or the solution directory."
    };

    /// <summary>
    /// Target type of a new project.
    /// </summary>
    public static Option<TargetType> ProjectType() => new("--type", "-t")
    {
        Description = $"Project type: {string.Join(" or ", Enum.GetNames<TargetType>())}. Defaults to {TargetType.Executable}."
    };

    /// <summary>
    /// A solution to work with.
    /// </summary>
    public static Option<string> SolutionPath() => new("--solution", "-s")
    {
        Description = "Path to a solution (.sbslnx) or a directory containing one. " +
                      "Defaults to the solution in the current directory."
    };

    /// <summary>
    /// A project to work with.
    /// </summary>
    public static Option<string> ProjectPath() => new("--project", "-p")
    {
        Description = "Path to a project (.sbproj) or a directory containing one. Defaults to the current directory."
    };

    /// <summary>
    /// A name of a new solution or project.
    /// </summary>
    public static Argument<string> NameArgument(string what) => new("name")
    {
        Description = $"Name of the {what}."
    };

    /// <summary>
    /// A positional path.
    /// </summary>
    public static Argument<string> PathArgument(string description) => new("path")
    {
        Description = description
    };

    /// <summary>
    /// Arguments forwarded to the program started by <c>run</c>.
    /// </summary>
    public static Argument<string[]> ProgramArguments() => new("args")
    {
        Description = "Arguments passed to the program (put them after --).",
        Arity = ArgumentArity.ZeroOrMore
    };
}
