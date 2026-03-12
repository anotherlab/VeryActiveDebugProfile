using CommunityToolkit.Mvvm.Messaging;
using stdole;
using System.Management;
using System.Runtime.InteropServices;
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

        // Add these constants at class level
        private const int DBT_DEVTYP_DEVICEINTERFACE = 0x00000005;
        private const uint DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;

        // GUIDs for USB and Windows Portable Devices (WPD)
        private static readonly Guid GUID_DEVINTERFACE_USB_DEVICE = new Guid("A5DCBF10-6530-11D2-901F-00C04FB951ED");
        //private static readonly Guid GUID_DEVINTERFACE_WPD = new Guid("EEC5AD98-8080-425F-922A-DABF3DE3F69A");

        //private IntPtr _deviceNotificationHandle = IntPtr.Zero;
        private IntPtr _deviceNotificationHandleUsb = IntPtr.Zero;
        //private IntPtr _deviceNotificationHandleWpd = IntPtr.Zero;

        private readonly IWindowPlacementService _placementService =
            new WindowPlacementService();

        VsInstancesViewModel _viewModel = new VsInstancesViewModel();

        string vid = string.Empty;
        string lastDevice = string.Empty;

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

            UpdateAndroidStatus("VID");
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

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source.AddHook(WndProc);

            // Register for device notifications

            RegisterForDeviceNotifications(source.Handle);
        }

        // Add cleanup
        protected override void OnClosed(EventArgs e)
        {
            if (_deviceNotificationHandleUsb != IntPtr.Zero)
            {
                UnregisterDeviceNotification(_deviceNotificationHandleUsb);
                _deviceNotificationHandleUsb = IntPtr.Zero;
            }
            //if (_deviceNotificationHandleWpd != IntPtr.Zero)
            //{
            //    UnregisterDeviceNotification(_deviceNotificationHandleWpd);
            //    _deviceNotificationHandleWpd = IntPtr.Zero;
            //}
            base.OnClosed(e);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // If a USB device is connected or disconnected, wParam will indicate the event type (arrival or removal)
            if (msg == WM_DEVICECHANGE)
            {
                int eventType = wParam.ToInt32();

                if (eventType == DBT_DEVICEARRIVAL)
                {
                    // When a device is connected, lParam points to a structure with details about the device.
                    // We can extract the device path from it.
                    string devicePath = GetDeviceInterfacePath(lParam);

                    if (!string.IsNullOrEmpty(devicePath))
                    {
                        // Notify the UI about the new device
                        SendMessage($"Device arrived: {devicePath}");

                        var vid = PnpHelper.GetVendorAndProductfromPath(devicePath);
                        //var tmpVid = PnpHelper.GetVendorAndProductfromPath(devicePath);

                        //if (PnpHelper.GetDeviceName(tmpVid) is string deviceName)
                        //{
                        //    vid = tmpVid;
                        //    SendMessage("Android device detected!");
                        //}
                    }
                    else
                    {
                        // no device path means we can't identify the device, but we know something was connected
                        // It wont be an Android device and we can ignore it
                        return IntPtr.Zero;
                    }

                    // Small delay to allow Windows to finish driver initialization
                    // This will tell us if the connected device is an Android device or not
                    Dispatcher.InvokeAsync(async () => {
                        await System.Threading.Tasks.Task.Delay(10);
                        UpdateAndroidStatus(vid);
                    });
                }
                else if (eventType == DBT_DEVICEREMOVECOMPLETE)
                {
                    SendMessage("Device disconnected");

                    vid = string.Empty;

                    UpdateAndroidStatus(vid);
                }
            }
            return IntPtr.Zero;
        }

        private bool UpdateAndroidStatus(string deviceVendor)
        {
            string vid = PnpHelper.GetDeviceName(deviceVendor) ?? String.Empty;

            _viewModel.DeviceName = vid;

            return vid != String.Empty;
        }

        // Register for device notifications

        // Extract device interface path from lParam
        private string GetDeviceInterfacePath(IntPtr lParam)
        {
            if (lParam == IntPtr.Zero)
                return string.Empty;

            try
            {
                var hdr = Marshal.PtrToStructure<DEV_BROADCAST_HDR>(lParam);
                if (hdr.dbch_devicetype == DBT_DEVTYP_DEVICEINTERFACE)
                {
                    var deviceInterface = Marshal.PtrToStructure<DEV_BROADCAST_DEVICEINTERFACE>(lParam);
                    return deviceInterface.dbcc_name?.TrimEnd('\0') ?? string.Empty;
                }
            }
            catch
            {
                // Ignore marshaling errors
            }
            return string.Empty;
        }

        private void RegisterForDeviceNotifications(IntPtr windowHandle)
        {
            // Register for USB device interface notifications
            var filterUsb = new DEV_BROADCAST_DEVICEINTERFACE
            {
                dbcc_size = Marshal.SizeOf(typeof(DEV_BROADCAST_DEVICEINTERFACE)),
                dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE,
                dbcc_reserved = 0,
                dbcc_classguid = GUID_DEVINTERFACE_USB_DEVICE,
                dbcc_name = string.Empty
            };

            IntPtr bufferUsb = Marshal.AllocHGlobal(filterUsb.dbcc_size);
            try
            {
                Marshal.StructureToPtr(filterUsb, bufferUsb, false);
                _deviceNotificationHandleUsb = RegisterDeviceNotification(windowHandle, bufferUsb, DEVICE_NOTIFY_WINDOW_HANDLE);
                if (_deviceNotificationHandleUsb == IntPtr.Zero)
                {
                    SendMessage("Failed to register for USB device notifications");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(bufferUsb);
            }

            // Register for WPD (portable devices) notifications
            //var filterWpd = new DEV_BROADCAST_DEVICEINTERFACE
            //{
            //    dbcc_size = Marshal.SizeOf(typeof(DEV_BROADCAST_DEVICEINTERFACE)),
            //    dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE,
            //    dbcc_reserved = 0,
            //    dbcc_classguid = GUID_DEVINTERFACE_WPD,
            //    dbcc_name = string.Empty
            //};

            //IntPtr bufferWpd = Marshal.AllocHGlobal(filterWpd.dbcc_size);
            //try
            //{
            //    Marshal.StructureToPtr(filterWpd, bufferWpd, false);
            //    _deviceNotificationHandleWpd = RegisterDeviceNotification(windowHandle, bufferWpd, DEVICE_NOTIFY_WINDOW_HANDLE);
            //    if (_deviceNotificationHandleWpd == IntPtr.Zero)
            //    {
            //        SendMessage("Failed to register for WPD device notifications");
            //    }
            //}
            //finally
            //{
            //    Marshal.FreeHGlobal(bufferWpd);
            //}
        }

        #region P/Invoke Declarations

        [StructLayout(LayoutKind.Sequential)]
        private struct DEV_BROADCAST_HDR
        {
            public int dbch_size;
            public int dbch_devicetype;
            public int dbch_reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DEV_BROADCAST_DEVICEINTERFACE
        {
            public int dbcc_size;
            public int dbcc_devicetype;
            public int dbcc_reserved;
            public Guid dbcc_classguid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string dbcc_name;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr RegisterDeviceNotification(IntPtr hRecipient, IntPtr notificationFilter, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterDeviceNotification(IntPtr handle);

        #endregion
    }
}