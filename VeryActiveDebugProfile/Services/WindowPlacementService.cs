using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;

namespace VeryActiveDebugProfile.Services;

public class WindowPlacementService : IWindowPlacementService
{
    private readonly string _filePath;

    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new()
    {
        WriteIndented = true
    };

    public WindowPlacementService()
    {
        var appName = Assembly.GetEntryAssembly()?.GetName().Name
                      ?? Assembly.GetExecutingAssembly().GetName().Name;

        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            appName);

        Directory.CreateDirectory(folder);

        _filePath = Path.Combine(folder, "windowplacement.json");
    }

    public void Restore(Window window)
    {
        if (!File.Exists(_filePath))
            return;

        var json = File.ReadAllText(_filePath);
        var placement = JsonSerializer.Deserialize<WindowPlacement>(json);

        if (placement == null)
            return;

        var rect = new Rect(
            placement.Left,
            placement.Top,
            placement.Width,
            placement.Height);

        if (!IsVisibleOnAnyScreen(rect))
        {
            // Fallback to center screen
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        window.Left = placement.Left;
        window.Top = placement.Top;
        window.Width = placement.Width;
        window.Height = placement.Height;

        if (Enum.TryParse(placement.WindowState, out WindowState state))
        {
            window.WindowState = state;
        }
    }

    public void Save(Window window)
    {
        Rect bounds = window.WindowState == WindowState.Normal
            ? new Rect(window.Left, window.Top, window.Width, window.Height)
            : window.RestoreBounds;

        var placement = new WindowPlacement
        {
            Top = bounds.Top,
            Left = bounds.Left,
            Width = bounds.Width,
            Height = bounds.Height,
            WindowState = window.WindowState.ToString()
        };

        var json = JsonSerializer.Serialize(placement, s_jsonSerializerOptions);

        File.WriteAllText(_filePath, json);
    }

    // ----------------------------
    // MULTI MONITOR SAFE SECTION
    // ----------------------------

    private static bool IsVisibleOnAnyScreen(Rect windowRect)
    {
        foreach (var monitor in GetAllMonitors())
        {
            if (monitor.IntersectsWith(windowRect))
                return true;
        }

        return false;
    }

    private static Rect[] GetAllMonitors()
    {
        var monitors = new System.Collections.Generic.List<Rect>();

        NativeMethods.EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            new NativeMethods.MonitorEnumProc(
                (hMonitor, hdcMonitor, ref lprcMonitor, dwData) =>
                {
                    monitors.Add(new Rect(
                        lprcMonitor.Left,
                        lprcMonitor.Top,
                        lprcMonitor.Right - lprcMonitor.Left,
                        lprcMonitor.Bottom - lprcMonitor.Top));

                    return true;
                }),
            IntPtr.Zero);

        return [.. monitors];
    }
}
