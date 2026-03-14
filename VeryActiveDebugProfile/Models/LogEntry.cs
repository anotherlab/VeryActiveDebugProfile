using System;
using System.Collections.Generic;
using System.Text;

namespace VeryActiveDebugProfile.Models;

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public required string Message { get; set; }
}