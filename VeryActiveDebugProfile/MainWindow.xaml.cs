using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32.SafeHandles;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Management;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

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
        private static readonly Guid GUID_DEVINTERFACE_USB_DEVICE = new("A5DCBF10-6530-11D2-901F-00C04FB951ED");

        private SafeDeviceNotificationHandle? _deviceNotifyHandle;

        private readonly WindowPlacementService _placementService = new();

        private static readonly VsInstancesViewModel _viewModel = new();

        string vid = string.Empty;

        public MainWindow()
        {
            InitializeComponent();

            var appVersion = GetAppVersion();

            DataContext = _viewModel;

            _viewModel.LogEntries.CollectionChanged += MyItems_CollectionChanged;

            //            Loaded += (_, _) => _placementService.Restore(this);
            Loaded += (_, _) =>
            {
                _placementService.Restore(this);
                ScrollToEnd();
            };

            Closing += (_, _) => _placementService.Save(this);

            SendMessage($"App started");
            SendMessage($"Version: {GetAppVersion()}");

            // Force an initial update to set the device name if an
            // Android device is already connected when the app starts
            UpdateAndroidStatus("VID");
        }

        private void MyItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Only scroll if an item was actually added
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                // Ensure the UI is ready before scrolling
                ScrollToEnd();
            }
        }

        private void ScrollToEnd()
        {
            _ = Dispatcher.BeginInvoke(new Action( () =>
            {
                if (LogGrid.Items.Count > 0)
                {
                    LogGrid.ScrollIntoView(LogGrid.Items[^1]);
                }
            }));
        }

        public static string GetAppVersion()
        {
            // Get the entry assembly (the .exe or main project)
            var assembly = Assembly.GetEntryAssembly();

            // Retrieve the InformationalVersion attribute
            var version = assembly?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            // Fallback to a standard version or "Unknown" if null
            return version ?? assembly?.GetName().Version?.ToString() ?? "1.0.0";
        }

        public static void SendMessage(string message)
        {
            WeakReferenceMessenger.Default.Send(new StatusChangedMessage(message));
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Get the window handle and add a hook to listen for Windows messages
            HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source.AddHook(WndProc);

            // Register for USB device notifications
            RegisterForDeviceNotifications(source.Handle);
        }

        // Add cleanup
        protected override void OnClosed(EventArgs e)
        {
            //if (_deviceNotificationHandleUsb != IntPtr.Zero)
            //{
            //    UnregisterDeviceNotification(_deviceNotificationHandleUsb);
            //    _deviceNotificationHandleUsb = IntPtr.Zero;
            //}

            _deviceNotifyHandle?.Dispose();

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
                        // Notify the UI about the new device.  Not all of them will be Android devices, but it's useful to see all device connections in the log.
                        SendMessage($"Device arrived: {devicePath}");

                        var vid = PnpHelper.GetVendorAndProductfromPath(devicePath);

                        // Next check to see if we have an Android device.
                        var manufacturer = AndroidDeviceHelper.GetManufacturerFromHardwareId(vid);

                        if (manufacturer != null)
                        {
                            SendMessage($"{manufacturer} device detected ");

                            Dispatcher.InvokeAsync(async () => {
                                await System.Threading.Tasks.Task.Delay(100);
                                UpdateAndroidStatus(vid);
                            });

                        }
                    }
                    else
                    {
                        // no device path means we can't identify the device, but we know something was connected
                        // It wont be an Android device and we can ignore it
                        return IntPtr.Zero;
                    }
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

        /// <summary>
        /// Updates the device name in the view model based on the specified device vendor.
        /// </summary>
        /// <param name="deviceVendor">The vendor identifier used to determine the device name. Cannot be null.</param>
        private static void UpdateAndroidStatus(string deviceVendor)
        {
            _viewModel.DeviceName = PnpHelper.GetDeviceName(deviceVendor) ?? String.Empty;
        }


        /// <summary>
        /// Retrieves the device interface path from a pointer to a device broadcast structure.
        /// </summary>
        /// <remarks>This method attempts to extract the device interface path from the provided pointer
        /// if it references a device interface notification. If the pointer is invalid or does not represent a device
        /// interface, an empty string is returned.</remarks>
        /// <param name="lParam">A pointer to a device broadcast structure containing device notification data. Must not be <see
        /// cref="IntPtr.Zero"/>.</param>
        /// <returns>A string containing the device interface path if available; otherwise, an empty string.</returns>
        private static string GetDeviceInterfacePath(IntPtr lParam)
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

        /// <summary>
        /// We need to register for device notifications to get the device path in the WM_DEVICECHANGE message. 
        /// This allows us to identify the connected device and check if it's an Android device.
        /// </summary>
        /// <param name="windowHandle"></param>
        private void RegisterForDeviceNotifications(IntPtr windowHandle)
        {
            // Register for USB device interface notifications
            //var filterUsb = new DEV_BROADCAST_DEVICEINTERFACE
            //{
            //    dbcc_size = Marshal.SizeOf(typeof(DEV_BROADCAST_DEVICEINTERFACE)),
            //    dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE,
            //    dbcc_reserved = 0,
            //    dbcc_classguid = GUID_DEVINTERFACE_USB_DEVICE,
            //    dbcc_name = string.Empty
            //};
            var filterUsb = new DEV_BROADCAST_DEVICEINTERFACE
            {
                dbcc_size = Marshal.SizeOf<DEV_BROADCAST_DEVICEINTERFACE>(),
                dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE,
                dbcc_reserved = 0,
                dbcc_classguid = GUID_DEVINTERFACE_USB_DEVICE,
                dbcc_name = string.Empty
            };

            IntPtr bufferUsb = Marshal.AllocHGlobal(filterUsb.dbcc_size);
            try
            {
                Marshal.StructureToPtr(filterUsb, bufferUsb, false);
                _deviceNotifyHandle = NativeMethods.RegisterDeviceNotification(windowHandle, bufferUsb, DEVICE_NOTIFY_WINDOW_HANDLE);
                if (_deviceNotifyHandle == null || _deviceNotifyHandle.IsInvalid)
                {
                    SendMessage("Failed to register for USB device notifications");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(bufferUsb);
            }
        }
        //private void RegisterForDeviceNotifications(IntPtr windowHandle)
        //{
        //    // 1. Get the size using the generic method (fixes CA2263)
        //    int size = Marshal.SizeOf<DEV_BROADCAST_DEVICEINTERFACE>();

        //    // 2. Allocate memory on the stack (zero-allocation on the heap)
        //    // We use byte so we can control the size exactly
        //    Span<byte> buffer = stackalloc byte[size];

        //    // 3. Initialize the struct
        //    var filterUsb = new DEV_BROADCAST_DEVICEINTERFACE
        //    {
        //        dbcc_size = size,
        //        dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE,
        //        dbcc_classguid = GUID_DEVINTERFACE_USB_DEVICE,
        //        dbcc_name = string.Empty
        //    };

        //    // 4. Copy the struct into our stack memory
        //    MemoryMarshal.Write(buffer, ref filterUsb);

        //    unsafe
        //    {
        //        // 5. Get a pointer to the stack memory and call the native method
        //        fixed (byte* pBuffer = buffer)
        //        {
        //            _deviceNotifyHandle = NativeMethods.RegisterDeviceNotification(
        //                windowHandle,
        //                (IntPtr)pBuffer,
        //                DEVICE_NOTIFY_WINDOW_HANDLE);
        //        }
        //    }

        //    if (_deviceNotifyHandle == null || _deviceNotifyHandle.IsInvalid)
        //    {
        //        SendMessage("Failed to register for USB device notifications");
        //    }

        //    // Look, Ma! No Marshal.FreeHGlobal()!
        //}
        #region P/Invoke Declarations

        [StructLayout(LayoutKind.Sequential)]
        private struct DEV_BROADCAST_HDR
        {
            public int dbch_size;
            public int dbch_devicetype;
            public int dbch_reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private unsafe struct DEV_BROADCAST_DEVICEINTERFACE
        {
            public int dbcc_size;
            public int dbcc_devicetype;
            public int dbcc_reserved;
            public Guid dbcc_classguid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string dbcc_name;
            //public fixed char dbcc_name[256];
        }

        //[DllImport("user32.dll", SetLastError = true)]
        //private static extern IntPtr RegisterDeviceNotification(IntPtr hRecipient, IntPtr notificationFilter, uint flags);

        //[DllImport("user32.dll", SetLastError = true)]
        //private static extern bool UnregisterDeviceNotification(IntPtr handle);

        #endregion
    }

}