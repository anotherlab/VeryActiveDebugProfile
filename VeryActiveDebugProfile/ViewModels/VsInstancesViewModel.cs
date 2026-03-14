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


    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    public void AddLog(string message)
    {
        LogEntries.Add(new LogEntry
        {
            Timestamp = DateTime.Now,
            Message = message
        });

        if (LogEntries.Count > MaxEntries)
            LogEntries.RemoveAt(0); // remove oldest
    }

    [RelayCommand]
    private void Refresh()
    {
        RefreshVs();
    }

    [RelayCommand]
    private static void ShowMainWindow()
    {
        App.Current.MainWindow!.Show();
        App.Current.MainWindow!.WindowState = System.Windows.WindowState.Normal;
        App.Current.MainWindow.Activate();
    }

    [RelayCommand]
    private static void Exit()
    {
        App.TrayIcon?.Dispose();
        App.Current.Shutdown();
    }

    public VsInstancesViewModel()
    {
        WeakReferenceMessenger.Default.Register<StatusChangedMessage>(
            this,
            (r, m) =>
            {
                AddLog(m.Value);
            });
    }

    private Uri? _imagePath;
    private Uri GetImagePath(string imageFileName)
    {
        _imagePath ??= new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", imageFileName)); // Assumes an 'Images' folder in output directory
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
            var service = GetService();
            var instances = service.GetVsInstances();

            var mauiProjects = VsProjectService.GetMauiProjectsByInstances(instances);

            AddLog($"Found {mauiProjects.Count} MAUI projects.");

            var UpdateCount = 0;

            foreach (var project in mauiProjects)
            {
                var thisUpdate = UpdateProjectFile(project);
                UpdateCount += thisUpdate;

                if (thisUpdate > 0)
                    AddLog($"☑️ Updated {project}");
                else
                    AddLog($"Skipped {project}");
            }

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
