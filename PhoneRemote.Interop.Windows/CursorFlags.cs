using System;

namespace PhoneRemote.Interop.Windows
{
    public class CursorFlags
    {
        public const int HIDDEN = 0x0;
        public const int CURSOR_SHOWING = 0x00000001;
        public const int CURSOR_SUPPRESSED = 0x00000002;

        [Flags]
        public enum Flags
        {
            /// <summary>
            /// Cursor is hidden
            /// </summary>
            HIDDEN = 0x0,

            /// <summary>
            /// The cursor is showing.
            /// </summary>
            CURSOR_SHOWING = 0x00000001,

            /// <summary>
            /// (Windows 8 and above.) The cursor is suppressed. This flag indicates that the system is not drawing the cursor because the user is providing input through touch or pen instead of the mouse.
            /// </summary>
            CURSOR_SUPPRESSED = 0x00000002
        }
    }
}
