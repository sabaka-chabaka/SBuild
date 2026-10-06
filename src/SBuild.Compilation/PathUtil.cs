namespace SBuild.Compilation;

internal static class PathUtil
{
    public static StringComparison Comparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    /// True when <paramref name="path"/> is <paramref name="directory"/> itself or lies inside it.
    /// </summary>
    public static bool IsSameOrUnder(string path, string directory)
    {
        var p = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var d = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));

        return p.Equals(d, Comparison) || p.StartsWith(d + Path.DirectorySeparatorChar, Comparison);
    }

    /// <summary>
    /// True when <paramref name="path"/> lies inside <paramref name="directory"/> but is not the directory itself.
    /// </summary>
    public static bool IsStrictlyUnder(string path, string directory)
    {
        var p = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var d = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));

        return p.StartsWith(d + Path.DirectorySeparatorChar, Comparison);
    }
}
