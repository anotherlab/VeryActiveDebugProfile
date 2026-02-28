using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using System.Configuration;
using System.Data;
using System.Windows;
using VeryActiveDebugProfile.Services;

namespace VeryActiveDebugProfile;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private const string LightThemePath = "Themes/CustomLight.xaml";
    private const string DarkThemePath = "Themes/CustomDark.xaml";
    public static TaskbarIcon? TrayIcon { get; private set; }
    public App()
    {
        InitializeComponent();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        LoadCustomTheme();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        //TrayIcon = (TaskbarIcon)Resources["MyNotifyIcon"];
        // Initialize the AppNotificationManager
        //AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        //AppNotificationManager.Default.Unregister();
        //TrayIcon?.Dispose();
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        base.OnExit(e);
    }

    //private void OnNotificationInvoked(object sender, AppNotificationActivatedEventArgs args)
    //{
    //    // Process the arguments to determine user action
    //    string action = args.Arguments["action"];
    //    if (action == "OpenApp")
    //    {
    //        // Bring app to foreground or navigate to specific UI
    //    }
    //}

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
        {
            // Must switch dictionaries on UI thread
            Dispatcher.Invoke(LoadCustomTheme);
        }
    }

    private void LoadCustomTheme()
    {
        string newTheme = IsLightTheme() ? LightThemePath : DarkThemePath;

        // Remove existing custom theme dictionaries
        var dictionariesToRemove = Resources.MergedDictionaries
            .Where(d => d.Source != null &&
                       (d.Source.OriginalString.Contains("CustomLight.xaml") ||
                        d.Source.OriginalString.Contains("CustomDark.xaml")))
            .ToList();

        foreach (var dict in dictionariesToRemove)
            Resources.MergedDictionaries.Remove(dict);

        // Add new theme dictionary
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(newTheme, UriKind.Relative)
        });
    }

    private static bool IsLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

        return (int?)key?.GetValue("AppsUseLightTheme") == 1;
    }
}
