using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace PhoneRemote.Interop.Windows
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int x;
        public int y;
    }
}
