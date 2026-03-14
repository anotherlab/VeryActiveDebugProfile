using System;
using System.Collections.Generic;
using System.Text;

namespace VeryActiveDebugProfile.Models;

public class VsInstance
{
    public required string Version { get; set; }
    public required string SolutionPath { get; set; }
    public List<VsProject> Projects { get; set; } = [];
}
