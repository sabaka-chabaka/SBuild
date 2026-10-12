using System.CommandLine;
using SBuild.Cli;

var rootCommand = new RootCommand("SabakaBuild root command");

foreach (var command in AppCommands.All())
{
    rootCommand.Subcommands.Add(command);
}

return rootCommand.Parse(args).Invoke();