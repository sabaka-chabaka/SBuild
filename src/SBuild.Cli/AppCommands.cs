using System.CommandLine;

namespace SBuild.Cli;

/// <summary>
/// A class with basic commands for Cli.
/// </summary>
public static class AppCommands
{
    /// <summary>
    /// Command for build .cs file to .dll.
    /// </summary>
    public static readonly Command BuildCommand = new("build", "Builds the csharp file") {AppOptions.FilePath, AppOptions.IsExecutable};
    
    static AppCommands()
    {
        BuildCommand.SetAction(AppActions.ExecuteBuildAsync(AppOptions.FilePath, AppOptions.IsExecutable));
    }
}