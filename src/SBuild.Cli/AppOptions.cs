using System.CommandLine;

namespace SBuild.Cli;

/// <summary>
/// A class with basic command options for Cli.
/// </summary>
public static class AppOptions
{
    /// <summary>
    /// A path to C# file.
    /// </summary>
    public static readonly Option<string> FilePath = new("--path", "-p") {Description = "The path to the csharp file."};
    
    /// <summary>
    /// Does out .dll will executable.
    /// </summary>
    public static readonly Option<bool> IsExecutable = new ("--is-executable", "-ie") {Description = "Does the program will executable."};
}