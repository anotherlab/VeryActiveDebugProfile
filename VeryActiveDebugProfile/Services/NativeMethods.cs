using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace VeryActiveDebugProfile.Services
{
    public sealed class SafeDeviceNotificationHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        // The constructor must be public for the P/Invoke marshaller
        public SafeDeviceNotificationHandle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            // This is called automatically by the GC/Finalizer
            return NativeMethods.UnregisterDeviceNotification(handle);
        }
    }

    public static partial class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        internal delegate bool MonitorEnumProc(
            IntPtr hMonitor,
            IntPtr hdcMonitor,
            ref RECT lprcMonitor,
            IntPtr dwData);

        [LibraryImport("user32.dll", EntryPoint = "RegisterDeviceNotificationW", SetLastError = true)]
        public static partial SafeDeviceNotificationHandle RegisterDeviceNotification(
            IntPtr hRecipient,
            IntPtr notificationFilter,
            uint flags);

        // Unregister is usually fine, but it's good practice to be explicit
        [LibraryImport("user32.dll", EntryPoint = "UnregisterDeviceNotification", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool UnregisterDeviceNotification(IntPtr handle);

        [LibraryImport("user32.dll", EntryPoint = "EnumDisplayMonitors")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool EnumDisplayMonitors(
            IntPtr hdc,
            IntPtr lprcClip,
            MonitorEnumProc lpfnEnum,
            IntPtr dwData);
    }

}
