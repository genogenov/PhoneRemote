using PhoneRemote.Protobuf.ProtoModels;
using System;
using System.Runtime.InteropServices;

namespace PhoneRemote.Interop.Windows
{
    public static class CursorUtils
    {
        public static CursorInfo GetCursorInfo()
        {
            var info = new CursorInfo();

            info.cbSize = Marshal.SizeOf(typeof(CursorInfo));

            GetCursorInfo(ref info);

            return info;
        }

        public static bool MoveCursor(int x, int y)
        {
            return SetCursorPos(x, y);
        }

        public static void DispatchMouseEvent(CursorAction cursorAction)
        {
            mouse_event(cursorAction.ActionFlags, cursorAction.DX, cursorAction.DY, cursorAction.ActionData, cursorAction.ExtraInfo);
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorInfo(ref CursorInfo pci);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hwnd, ref Point lpPoint);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, int dwData, int dwExtraInfo);
    }
}
