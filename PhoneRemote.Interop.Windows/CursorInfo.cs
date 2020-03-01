using System;
using System.Runtime.InteropServices;

namespace PhoneRemote.Interop.Windows
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CursorInfo
    {
        /// <summary>
        /// Specifies the size, in bytes, of the structure.
        /// The caller must set this to Marshal.SizeOf(typeof(CURSORINFO)).
        /// </summary>
        public int cbSize;
        /// <summary>
        /// Specifies the cursor state. This parameter can be one of the following values:
        /// </summary>
        public CursorFlags.Flags flags;
        /// <summary>
        /// // Handle to the cursor.
        /// </summary>
        public IntPtr hCursor;
        // A POINT structure that receives the screen coordinates of the cursor.
        public Point ptScreenPos;
    }
}
