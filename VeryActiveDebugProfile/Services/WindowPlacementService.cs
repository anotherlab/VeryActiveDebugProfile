using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;

namespace VeryActiveDebugProfile.Services;

public class WindowPlacementService : IWindowPlacementService
{
    private readonly string _filePath;

    public WindowPlacementService()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YourApp");

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

        var json = JsonSerializer.Serialize(placement, new JsonSerializerOptions
        {
            WriteIndented = true
        });

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

        EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            new WindowPlacementService.MonitorEnumProc(
                (IntPtr hMonitor, IntPtr hdcMonitor, ref WindowPlacementService.RECT lprcMonitor, IntPtr dwData) =>
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

    private delegate bool MonitorEnumProc(
        IntPtr hMonitor,
        IntPtr hdcMonitor,
        ref RECT lprcMonitor,
        IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr lprcClip,
        MonitorEnumProc lpfnEnum,
        IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}