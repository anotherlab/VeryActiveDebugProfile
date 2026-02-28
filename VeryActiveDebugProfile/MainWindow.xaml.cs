using CommunityToolkit.Mvvm.Messaging;
using stdole;
using System.Management;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using VeryActiveDebugProfile.Services;
using VeryActiveDebugProfile.ViewModels;

namespace VeryActiveDebugProfile
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int WM_DEVICECHANGE = 0x0219;
        private const int DBT_DEVICEARRIVAL = 0x8000;
        private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;

        private readonly IWindowPlacementService _placementService =
            new WindowPlacementService();

        VsInstancesViewModel _viewModel = new VsInstancesViewModel();

        public MainWindow()
        {
            InitializeComponent();

            DataContext = _viewModel;

            WeakReferenceMessenger.Default.Register<UpdateGridMessage>(
                this,
                (r, m) =>
                {
                    var item = LogGrid.Items[LogGrid.Items.Count - 1];

                    if (item != null)
                    {
                        LogGrid.Dispatcher.InvokeAsync(async () =>
                        {
                            await Task.Delay(1000);
                            LogGrid.UpdateLayout();
                            LogGrid.ScrollIntoView(item, null);
                        });
                    }

                });

            Loaded += (_, _) => _placementService.Restore(this);
            Closing += (_, _) => _placementService.Save(this);

            SendMessage("App started...");

            UpdateAndroidStatus();
        }
        public void SendMessage(string message)
        {
            WeakReferenceMessenger.Default.Send(new StatusChangedMessage(message));
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);

            if (WindowState == WindowState.Minimized)
            {
                Hide();
                // optionally update tooltip
                App.TrayIcon?.ToolTipText = "My WPF App (minimized)";
            }
        }

        //protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        //{
        //    e.Cancel = true;   // prevent app from exiting
        //    WindowState = WindowState.Minimized; // send to tray
        //}

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_DEVICECHANGE)
            {
                int eventType = wParam.ToInt32();

                if (eventType == DBT_DEVICEARRIVAL)
                {
                    SendMessage("Device connected, checking for Android devices...");
                    // Small delay to allow Windows to finish driver initialization
                    Dispatcher.InvokeAsync(async () => {
                        await System.Threading.Tasks.Task.Delay(1000);
                        UpdateAndroidStatus();
                    });
                }
                else if (eventType == DBT_DEVICEREMOVECOMPLETE)
                {
                    SendMessage("Device disconnected");
                    UpdateAndroidStatus();
                }
            }
            return IntPtr.Zero;
        }

        private void UpdateAndroidStatus()
        {
            _viewModel.DeviceName = GetConnectedAndroidDevice();
        }

        private string GetConnectedAndroidDevice()
        {
            try
            {
                // Querying Win32_PnPEntity for common Android identifiers
                // For my phone, the service is "WUDFWpdMtp" and description is "SM-908U"
                // https://learn.microsoft.com/en-us/windows/win32/wmisdk/wql-sql-for-wmi

                // If you search for Android devices, it's a more accurate query to look for Android devices
                // but it doesn't return the model name, just something like "SAMSUNG Android ADB Interface".
                // Searching for the WUDFWpdMtp service returns the model name but may be less reliable
                // across different devices. Adjust as needed for your specific devices and drivers.
                // Sample query for Android devices:

                // SELECT Description, Name, Manufacturer, Service, PNPClass, PNPDeviceID FROM Win32_PnPEntity WHERE Description LIKE '%Android%'

                /* PowerShell command to find Android devices:
                 * Get-PnpDevice -FriendlyName "*Android*" -Status OK | Select-Object -Property FriendlyName, Service, PNPDeviceID
                 * or
                 * Get-CimInstance -ClassName Win32_PnPEntity |
                 *     Where-Object { $_.Service -eq 'WUDFWpdMtp' } |
                 *     Select-Object Description, DeviceID, PNPDeviceID, Manufacturer
                 * 
                 * This can help you identify the correct WMI query for your specific devices.
                 * https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_wql?view=powershell-7.5
                 */


                using var searcher = new ManagementObjectSearcher(
                    "SELECT Description, Name FROM Win32_PnPEntity " +
                    "WHERE (Service='WUDFWpdMtp') " +
                    "AND PNPDeviceID LIKE'%ANDROID%' " +
                    "AND Present = True");

                var devices = searcher.Get();

                foreach (var device in devices)
                {
                    // Return the first matching description (e.g., "SAMSUNG Android ADB Interface")
                    var deviceName = device["Description"]?.ToString() ?? device["Name"]?.ToString();

                    if ( !String.IsNullOrEmpty(deviceName))
                    {
                        return deviceName;
                    }
                }
            }
            catch (ManagementException mex)
            {
                // WMI query failed
                SendMessage("WMI Error: " + mex.Message);
            }
            catch (InvalidCastException)
            {
                // Some property couldn't be cast. This can happen when no rows are returned
                // Just ignore it
            }
            catch (Exception ex)
            {
                SendMessage("WMI Error: " + ex.Message);
            }

            return string.Empty;
        }
    }
}