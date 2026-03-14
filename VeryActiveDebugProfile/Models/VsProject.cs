namespace VeryActiveDebugProfile.Models;

public class VsProject
{
    public required string Name { get; set; }
    public required string Path { get; set; }
    public bool IsMaui { get; set; } = false;
}
