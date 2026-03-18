namespace VeryActiveDebugProfile.Models;

/// <summary>
/// Class representing a log entry with a timestamp and message.
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public required string Message { get; set; }
}