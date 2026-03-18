namespace VeryActiveDebugProfile.Models;

/// <summary>
/// Class representing a Visual Studio instance with its version, solution path, and associated projects.
/// As we may have multiple instances of Visual Studio running, we can use this class to track each instance separately.
/// </summary>
public class VsInstance
{
    public required string Version { get; set; }
    public required string SolutionPath { get; set; }
    public List<VsProject> Projects { get; set; } = [];
}
