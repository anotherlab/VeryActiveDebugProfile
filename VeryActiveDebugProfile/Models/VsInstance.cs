using System;
using System.Collections.Generic;
using System.Text;

namespace VeryActiveDebugProfile.Models;

public class VsInstance
{
    public int ProcessId { get; set; }
    public string Version { get; set; }
    public string SolutionPath { get; set; }
    public List<VsProject> Projects { get; set; } = new List<VsProject>();
}
