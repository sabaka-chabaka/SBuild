using System.CommandLine;
using SBuild.Compilation;

namespace SBuild.Cli;

/// <summary>
/// Basic actions for app commands.
/// </summary>
public static class AppActions
{
    /// <summary>
    /// Build command
    /// </summary>
    /// <param name="path">Path to c# file.</param>
    /// <param name="isExecutable">Does .dll will executable.</param>
    /// <returns>Lambda</returns>
    public static Func<ParseResult, CancellationToken, Task<int>> ExecuteBuildAsync(Option<string> path, Option<bool> isExecutable)
    {
        return async (parseResult, cancellationToken) =>
        {
            var projectPath = parseResult.GetValue(path);
            
            if (string.IsNullOrEmpty(projectPath))
            {
                await Console.Error.WriteLineAsync("Ошибка: Путь к проекту не указан!");
                return 1;
            }

            try
            {
                var absoluteProjectPath = Path.GetFullPath(projectPath);
    
                var outputDirectory = Path.GetDirectoryName(absoluteProjectPath)!;
    
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(absoluteProjectPath);
    
                var assemblyOutputPath = Path.Combine(outputDirectory, $"{fileNameWithoutExtension}.dll");

                var source = await File.ReadAllTextAsync(absoluteProjectPath, cancellationToken);
    
                Compiler.Compile(source, assemblyOutputPath, parseResult.GetValue(isExecutable));

                if (parseResult.GetValue(isExecutable))
                {
                    await File.Create(Path.Combine(outputDirectory, $"{fileNameWithoutExtension}.runtimeconfig.json")).DisposeAsync();
                    await File.WriteAllTextAsync(Path.Combine(outputDirectory, $"{fileNameWithoutExtension}.runtimeconfig.json"), """
                        {
                            "runtimeOptions": {
                                 "tfm": "net10.0",
                                "framework": {
                                    "name": "Microsoft.NETCore.App",
                                    "version": "10.0.0"
                                }
                            }
                        }
                        """, cancellationToken);
                }
            }

            catch (Exception e)
            {
                Console.WriteLine(e);
                return 1;
            }

            Console.WriteLine($"Сборка {projectPath} успешно завершена.");
            return 0;
        };
    }
}