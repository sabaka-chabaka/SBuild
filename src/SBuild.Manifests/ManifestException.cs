namespace SBuild.Manifests;

/// <summary>
/// Thrown when a manifest (project or solution) is missing, malformed or ambiguous.
/// </summary>
public class ManifestException : Exception
{
    public ManifestException(string message) : base(message)
    {
    }

    public ManifestException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
