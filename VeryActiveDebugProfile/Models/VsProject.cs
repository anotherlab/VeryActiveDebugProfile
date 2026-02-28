using System;
using System.Collections.Generic;
using System.Text;

namespace VeryActiveDebugProfile.Models;

public class VsProject
{
    public string Name { get; set; }
    public string Path { get; set; }

    public bool IsMaui { get; set; } = false;
}
