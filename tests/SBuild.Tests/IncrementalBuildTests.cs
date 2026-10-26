using SBuild.Compilation;
using SBuild.ProjectGeneration;
using Xunit;

namespace SBuild.Tests;

public sealed class IncrementalBuildTests : IDisposable
{
    private readonly string _tempDir;

    public IncrementalBuildTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sbuild_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public void BuildProject_SecondTimeWithoutChanges_IsUpToDate()
    {
        var projFile = ProjectGenerator.GenerateProject(_tempDir, "App", SBuild.Manifests.Enums.TargetType.Executable);
        var projDir = Path.GetDirectoryName(projFile)!;
        File.WriteAllText(Path.Combine(projDir, "Program.cs"), "System.Console.WriteLine(\"Hello\");");

        var builder1 = new ProjectBuilder();
        var summary1 = builder1.BuildProject(projFile);

        Assert.True(summary1.Success);
        Assert.Single(summary1.Results);
        Assert.False(summary1.Results[0].IsUpToDate);

        var builder2 = new ProjectBuilder();
        var summary2 = builder2.BuildProject(projFile);

        Assert.True(summary2.Success);
        Assert.Single(summary2.Results);
        Assert.True(summary2.Results[0].IsUpToDate);
    }

    [Fact]
    public void BuildProject_SourceCodeModified_TriggersRebuild()
    {
        var projFile = ProjectGenerator.GenerateProject(_tempDir, "App", SBuild.Manifests.Enums.TargetType.Executable);
        var projDir = Path.GetDirectoryName(projFile)!;
        var sourceFile = Path.Combine(projDir, "Program.cs");
        File.WriteAllText(sourceFile, "System.Console.WriteLine(\"Version 1\");");

        var builder1 = new ProjectBuilder();
        var summary1 = builder1.BuildProject(projFile);
        Assert.True(summary1.Success);
        Assert.False(summary1.Results[0].IsUpToDate);

        // Modify source
        File.WriteAllText(sourceFile, "System.Console.WriteLine(\"Version 2\");");

        var builder2 = new ProjectBuilder();
        var summary2 = builder2.BuildProject(projFile);
        Assert.True(summary2.Success);
        Assert.False(summary2.Results[0].IsUpToDate);

        // Unchanged rebuild
        var builder3 = new ProjectBuilder();
        var summary3 = builder3.BuildProject(projFile);
        Assert.True(summary3.Success);
        Assert.True(summary3.Results[0].IsUpToDate);
    }

    [Fact]
    public void BuildProject_ManifestModified_TriggersRebuild()
    {
        var projFile = ProjectGenerator.GenerateProject(_tempDir, "App", SBuild.Manifests.Enums.TargetType.Executable);
        var projDir = Path.GetDirectoryName(projFile)!;
        File.WriteAllText(Path.Combine(projDir, "Program.cs"), "System.Console.WriteLine(\"Test\");");

        var builder1 = new ProjectBuilder();
        var summary1 = builder1.BuildProject(projFile);
        Assert.True(summary1.Success);
        Assert.False(summary1.Results[0].IsUpToDate);

        // Modify manifest
        var manifestText = File.ReadAllText(projFile);
        File.WriteAllText(projFile, manifestText.Replace("<Nullable>true</Nullable>", "<Nullable>false</Nullable>"));

        var builder2 = new ProjectBuilder();
        var summary2 = builder2.BuildProject(projFile);
        Assert.True(summary2.Success);
        Assert.False(summary2.Results[0].IsUpToDate);
    }

    [Fact]
    public void BuildProject_OutputDllDeleted_TriggersRebuild()
    {
        var projFile = ProjectGenerator.GenerateProject(_tempDir, "App", SBuild.Manifests.Enums.TargetType.Executable);
        var projDir = Path.GetDirectoryName(projFile)!;
        File.WriteAllText(Path.Combine(projDir, "Program.cs"), "System.Console.WriteLine(\"Hello\");");

        var builder1 = new ProjectBuilder();
        var summary1 = builder1.BuildProject(projFile);
        Assert.True(summary1.Success);
        Assert.False(summary1.Results[0].IsUpToDate);

        // Delete output assembly
        File.Delete(summary1.Results[0].OutputPath);

        var builder2 = new ProjectBuilder();
        var summary2 = builder2.BuildProject(projFile);
        Assert.True(summary2.Success);
        Assert.False(summary2.Results[0].IsUpToDate);
    }

    [Fact]
    public void BuildProject_ForceRebuild_IgnoresCache()
    {
        var projFile = ProjectGenerator.GenerateProject(_tempDir, "App", SBuild.Manifests.Enums.TargetType.Executable);
        var projDir = Path.GetDirectoryName(projFile)!;
        File.WriteAllText(Path.Combine(projDir, "Program.cs"), "System.Console.WriteLine(\"Hello\");");

        var builder1 = new ProjectBuilder();
        var summary1 = builder1.BuildProject(projFile);
        Assert.True(summary1.Success);
        Assert.False(summary1.Results[0].IsUpToDate);

        var builder2 = new ProjectBuilder { Incremental = false };
        var summary2 = builder2.BuildProject(projFile);
        Assert.True(summary2.Success);
        Assert.False(summary2.Results[0].IsUpToDate);
    }

    [Fact]
    public void BuildSolution_ReferencedProjectModified_TriggersCascadeRebuild()
    {
        var slnFile = ProjectGenerator.GenerateSolution(_tempDir, "TestSln");
        var slnDir = Path.GetDirectoryName(slnFile)!;

        var coreProj = ProjectGenerator.GenerateProject(slnDir, "Core", SBuild.Manifests.Enums.TargetType.Library);
        var appProj = ProjectGenerator.GenerateProject(slnDir, "App", SBuild.Manifests.Enums.TargetType.Executable);

        var coreDir = Path.GetDirectoryName(coreProj)!;
        var appDir = Path.GetDirectoryName(appProj)!;

        ProjectGenerator.AddProjectToSolution(slnFile, coreProj);
        ProjectGenerator.AddProjectToSolution(slnFile, appProj);
        ProjectGenerator.AddReference(appProj, coreProj);

        var coreSource = Path.Combine(coreDir, "Class1.cs");
        File.WriteAllText(coreSource, "public class Class1 { public static string GetText() => \"Hello\"; }");
        File.WriteAllText(Path.Combine(appDir, "Program.cs"), "System.Console.WriteLine(Class1.GetText());");

        // 1. Initial build: both dirty
        var builder1 = new ProjectBuilder();
        var summary1 = builder1.BuildSolution(slnFile);
        Assert.True(summary1.Success);
        Assert.Equal(2, summary1.Results.Count);
        Assert.All(summary1.Results, r => Assert.False(r.IsUpToDate));

        // 2. Second build: both cached / up to date
        var builder2 = new ProjectBuilder();
        var summary2 = builder2.BuildSolution(slnFile);
        Assert.True(summary2.Success);
        Assert.Equal(2, summary2.Results.Count);
        Assert.All(summary2.Results, r => Assert.True(r.IsUpToDate));

        // 3. Modify Core library source
        File.WriteAllText(coreSource, "public class Class1 { public static string GetText() => \"Hello 2\"; }");

        var builder3 = new ProjectBuilder();
        var summary3 = builder3.BuildSolution(slnFile);
        Assert.True(summary3.Success);
        Assert.Equal(2, summary3.Results.Count);
        // Both Core and App must rebuild
        var coreResult = summary3.Results.Single(r => r.Project.Name == "Core");
        var appResult = summary3.Results.Single(r => r.Project.Name == "App");
        Assert.False(coreResult.IsUpToDate);
        Assert.False(appResult.IsUpToDate);

        // 4. Build again: both up to date
        var builder4 = new ProjectBuilder();
        var summary4 = builder4.BuildSolution(slnFile);
        Assert.True(summary4.Success);
        Assert.All(summary4.Results, r => Assert.True(r.IsUpToDate));
    }

    [Fact]
    public void CleanProject_RemovesCacheFile()
    {
        var projFile = ProjectGenerator.GenerateProject(_tempDir, "App", SBuild.Manifests.Enums.TargetType.Executable);
        var projDir = Path.GetDirectoryName(projFile)!;
        File.WriteAllText(Path.Combine(projDir, "Program.cs"), "System.Console.WriteLine(\"Hello\");");

        var builder = new ProjectBuilder();
        var summary = builder.BuildProject(projFile);
        Assert.True(summary.Success);

        var outputDir = Path.GetDirectoryName(summary.Results[0].OutputPath)!;
        var cacheFile = IncrementalBuildCache.GetCacheFilePath(outputDir, "App");
        Assert.True(File.Exists(cacheFile));

        var removed = ProjectCleaner.CleanProject(projFile);
        Assert.NotEmpty(removed);
        Assert.False(File.Exists(cacheFile));
    }
}
