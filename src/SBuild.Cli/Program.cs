using System.CommandLine;
using SBuild.Cli;

var rootCommand = new RootCommand("SabakaBuild");

rootCommand.Subcommands.Add(AppCommands.BuildCommand);

return rootCommand.Parse(args).Invoke();