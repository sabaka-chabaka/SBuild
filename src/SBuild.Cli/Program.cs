using System.CommandLine;
using SBuild.Cli;

var rootCommand = new RootCommand("SabakaBuild");

foreach (var command in AppCommands.All())
{
    rootCommand.Subcommands.Add(command);
}

return rootCommand.Parse(args).Invoke();