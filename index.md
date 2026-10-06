# SBuild

A lightweight C# build system powered by Roslyn. It builds single files, projects and solutions without MSBuild.

```
dotnet tool install --global SBuild.Cli
```

## Commands

| Command | Description |
|---|---|
| `sbuild new solution <name> [-o dir]` | Creates a solution at `<name>/<name>.sbslnx` |
| `sbuild new project <name> [-t Executable\|Library] [-s solution] [-o dir]` | Creates a project and adds it to the solution (from `-s`, or the one in the current directory) |
| `sbuild add project <path> [-s solution]` | Adds an existing project to a solution |
| `sbuild add reference <path> [-p project]` | Adds a reference to another project or to a `.dll` |
| `sbuild build [-p path]` | Builds a solution, a project or a single `.cs` file (`-ie` builds a single file as an executable) |
| `sbuild run [-p path] [-- args]` | Builds and runs a program |
| `sbuild clean [-p path]` | Removes build output |

`path` can be a `.sbslnx` file, a `.sbproj` file, a `.cs` file or a directory. A directory resolves to the solution inside it if there is one, otherwise to the project.

## Project file (`.sbproj`)

```xml
<Project Name="App" TargetType="Executable">
  <TargetFramework>net10.0</TargetFramework>
  <ImplicitUsings>true</ImplicitUsings>
  <Nullable>true</Nullable>
  <OutputPath>bin</OutputPath>
  <References>
    <Reference>../Lib/Lib.sbproj</Reference>
    <Reference>libs/Other.dll</Reference>
  </References>
  <Excludes>
    <Exclude>Samples</Exclude>
  </Excludes>
</Project>
```

- **Sources:** every `*.cs` file under the project directory is compiled, recursively. `bin`, `obj`, hidden directories, entries from `Excludes` and nested projects (directories with their own `.sbproj`) are skipped.
- **References:** a reference is either a project (a `.sbproj` file or a directory containing one) or a `.dll`. Referenced projects are built automatically and before the projects that use them. Circular references are detected and reported as an error.
- **Output:** dependencies are copied to `OutputPath` together with their `.pdb` files. For an `Executable`, a `.runtimeconfig.json` is generated, so the program runs with `dotnet bin/App.dll`.
- **Implicit usings:** `ImplicitUsings` adds the same global usings as the .NET SDK: `System`, `System.Collections.Generic`, `System.IO`, `System.Linq`, `System.Net.Http`, `System.Threading` and `System.Threading.Tasks`.
- **Target framework:** use the `netX.Y` format (X ≥ 5). Compilation always runs against the .NET runtime SBuild itself is running on; a mismatch produces a warning.
- **Paths:** all paths are relative to the project directory, and both `/` and `\` are accepted.

## Solution file (`.sbslnx`)

```xml
<Solution Name="MySolution">
  <Project Path="App/App.sbproj" />
  <Project Path="Lib/Lib.sbproj" />
</Solution>
```

Paths are relative to the solution directory. An entry can point to a `.sbproj` file or to a directory that contains exactly one.

## Notes

- There is no incremental build yet: everything is recompiled on every run.
- `clean` removes the whole output directory only if it lies inside the project directory. Otherwise it deletes just the project's own `.dll`, `.pdb` and `.runtimeconfig.json`.