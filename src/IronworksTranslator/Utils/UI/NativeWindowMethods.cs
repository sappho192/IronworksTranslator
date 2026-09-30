using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace IronworksTranslator.Utils.UI
{
    internal static class NativeWindowMethods
    {
        private const int GwlExStyle = -20;
        internal const int WsExToolWindow = 0x00000080;
        internal const int WsExAppWindow = 0x00040000;
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpFrameChanged = 0x0020;

        internal static int GetProcessId(IntPtr handle)
        {
            // The return value is a nonzero thread ID on success, not zero.
            return handle != IntPtr.Zero && GetWindowThreadProcessId(handle, out var processId) != 0
                ? processId : 0;
        }

        internal static IntPtr FindProcessWindow(int processId)
        {
            if (processId <= 0) return IntPtr.Zero;
            var result = IntPtr.Zero;
            EnumWindows((handle, _) =>
            {
                if (GetProcessId(handle) == processId && IsWindowVisible(handle) &&
                    GetWindow(handle, 4) == IntPtr.Zero)
                {
                    result = handle;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return result;
        }

        internal static int GetExtendedStyle(IntPtr handle) => GetWindowLong(handle, GwlExStyle);

        internal static int GetAltTabStyle(int style, bool excluded) => excluded
            ? (style | WsExToolWindow) & ~WsExAppWindow
            : (style & ~WsExToolWindow) | WsExAppWindow;

        internal static void SetAltTabExcluded(IntPtr handle, bool excluded)
        {
            var style = GetExtendedStyle(handle);
            var newStyle = GetAltTabStyle(style, excluded);
            if (style == newStyle) return;
            Marshal.SetLastPInvokeError(0);
            var previousStyle = SetWindowLong(handle, GwlExStyle, newStyle);
            if (previousStyle == 0 && Marshal.GetLastPInvokeError() != 0)
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            if (!SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0,
                SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged))
                throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        internal static void SetWindowStateWithoutActivation(Window window, WindowState state)
        {
            if (!window.IsVisible)
            {
                window.WindowState = state;
                return;
            }
            var handle = new WindowInteropHelper(window).Handle;
            // SW_SHOWMINNOACTIVE / SW_SHOWNOACTIVATE preserve focus and the native
            // restore-to-maximized placement. WM_SIZE synchronizes WPF WindowState.
            // Setting WindowState first would instead run WPF's activating ShowWindow.
            ShowWindow(handle, state == WindowState.Minimized ? 7 : 4);
        }

        private delegate bool EnumWindowsCallback(IntPtr handle, IntPtr parameter);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsIconic(IntPtr handle);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr handle, out int processId);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr handle);
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr handle, uint command);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr handle, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr handle, int index, int value);
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter,
            int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr handle, int command);
    }
}
