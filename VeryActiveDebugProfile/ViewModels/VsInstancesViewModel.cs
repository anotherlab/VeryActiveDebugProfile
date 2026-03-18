using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Collections.ObjectModel;
using System.IO;

namespace VeryActiveDebugProfile.ViewModels;

public partial class VsInstancesViewModel : ObservableObject
{
    private const int MaxEntries = 100;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceStatusText))]
    [NotifyPropertyChangedFor(nameof(IsDeviceConnected))]
    string _deviceName = String.Empty;

    /// <summary>
    /// This method is called automatically by the source generator whenever the DeviceName property changes.
    /// This is what will trigger the code to log the change and refresh Visual Studio instances when a device 
    /// is connected or disconnected.
    /// </summary>
    /// <param name="oldValue"></param>
    /// <param name="newValue"></param>
    partial void OnDeviceNameChanged(string? oldValue, string newValue)
    {
        // Log the change and refresh Visual Studio instances when a device is connected or disconnected
        if (!string.IsNullOrWhiteSpace(newValue))
        {
            AddLog($"Device connected: {newValue}");

            RefreshVs();
        }

        // Reserved for future use: Show a balloon tip when device status changes. Must be on UI thread to interact with TaskbarIcon.
        App.Current.Dispatcher.Invoke(() =>
        {
            if (string.IsNullOrWhiteSpace(newValue))
            {
                App.TrayIcon?.ToolTipText = "No Android device connected";
                App.TrayIcon?.ShowBalloonTip("Device disconnected", "No Android device found", BalloonIcon.Warning);
            }
            else
            {
                App.TrayIcon?.ToolTipText = $"Device connected: {newValue}";
                App.TrayIcon?.ShowBalloonTip("Device connected", $"Device: {newValue}", BalloonIcon.Info);
            }
        });

    }

    public bool IsDeviceConnected => !string.IsNullOrWhiteSpace(DeviceName);

    public string DeviceStatusText =>
        string.IsNullOrWhiteSpace(DeviceName)
            ? "Status: No Android Devices Found"
            : $"Device connected: {DeviceName}";

    /// <summary>
    /// Gets the collection of log entries recorded by the logger.
    /// </summary>
    /// <remarks>The returned collection is observable, allowing clients to monitor changes such as additions
    /// or removals of log entries. The collection is read-only; to add or remove entries, use the appropriate logging
    /// methods provided by the class.</remarks>
    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    public void AddLog(string message)
    {
        LogEntries.Add(new LogEntry
        {
            Timestamp = DateTime.Now,
            Message = message
        });

        // Ensure we don't exceed the maximum number of log entries to prevent unbounded memory growth
        if (LogEntries.Count > MaxEntries)
            LogEntries.RemoveAt(0); // remove oldest
    }

    // Allows the user to manually refresh the list of Visual Studio instances and update project files,
    // in case they want to trigger it without connecting/disconnecting a device
    [RelayCommand]
    private void Refresh()
    {
        RefreshVs();
    }

    // Reserved for future use when the code to allow the app to run in the task tray is complete
    [RelayCommand]
    private static void ShowMainWindow()
    {
        App.Current.MainWindow!.Show();
        App.Current.MainWindow!.WindowState = System.Windows.WindowState.Normal;
        App.Current.MainWindow.Activate();
    }

    // Reserved for future use when the code to allow the app to run in the task tray is complete
    [RelayCommand]
    private static void Exit()
    {
        App.TrayIcon?.Dispose();
        App.Current.Shutdown();
    }

    public VsInstancesViewModel()
    {
        // Subscribe to status change messages from the PnpService.
        // This allows the view model to react to changes in device status
        WeakReferenceMessenger.Default.Register<StatusChangedMessage>(
            this,
            (r, m) =>
            {
                AddLog(m.Value);
            });
    }

    // This method is used to construct the URI for the image used in the toast notification.
    // It assumes that there is an "Images" folder in the output directory of the application,
    // and that it contains the specified image file. The URI is cached after the first
    // construction to avoid unnecessary overhead on subsequent calls.
    private Uri? _imagePath;
    private Uri GetImagePath(string imageFileName)
    {
        _imagePath ??= new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", imageFileName));
        return _imagePath;
    }


    private VsProjectService? _service;
    private VsProjectService GetService()
    {
        _service ??= new VsProjectService();
        return _service;
    }

    public void RefreshVs()
    {
        AddLog("Scanning for Visual Studio instances...");

        try
        {
            // Find all of the running Visual Studio instances
            var instances = GetService().GetVsInstances();

            // And their associated MAUI projects
            var mauiProjects = VsProjectService.GetMauiProjectsByInstances(instances);

            AddLog($"Found {mauiProjects.Count} MAUI projects.");

            var UpdateCount = 0;

            // Then update the project files to set the active debug profile to match the connected device
            foreach (var project in mauiProjects)
            {
                var thisUpdate = UpdateProjectFile(project);
                UpdateCount += thisUpdate;

                if (thisUpdate > 0)
                    AddLog($"☑️ Updated {project}");
                else
                    AddLog($"Skipped {project}");
            }

            // If we updated any projects, show a toast notification to let the user know
            if (UpdateCount > 0)
            {
                AddLog($"Updated {UpdateCount} MAUI project(s) successfully.");

                var sledgeUri = GetImagePath("sledge.jpg");

                // Requires Microsoft.Toolkit.Uwp.Notifications NuGet package version 7.0 or greater
                new ToastContentBuilder()
                    .AddArgument("action", "viewConversation")
                    .AddArgument("conversationId", 9813)
                    .AddText("MAUI Projects Updated")
                    .AddInlineImage(sledgeUri)
                    .AddText($"Updated {mauiProjects.Count} MAUI project(s) successfully.")
                    .Show();

                // Not seeing the Show() method? Make sure you have version 7.0, and if you're using .NET 6 (or later),
                // then your TFM must be net6.0-windows10.0.17763.0 or greater
            }
        }
        catch (Exception ex)
        {
            AddLog($"Error: {ex.Message}");
        }
    }

    public int UpdateProjectFile(string projectFileName)
    {
        return VsProjectService.UpdateProjectFile(projectFileName, DeviceName);
    }

}
