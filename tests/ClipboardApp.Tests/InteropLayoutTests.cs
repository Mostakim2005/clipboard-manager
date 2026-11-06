using ClipboardApp.Core.Interop;
using System;
using System.Runtime.InteropServices;
using Xunit;

namespace ClipboardApp.Tests
{
    public class InteropLayoutTests
    {
        [Fact]
        public void InputStructMatchesWin32Size()
        {
            // SendInput silently returns 0 when cbSize is wrong. Win32 INPUT is
            // 40 bytes on x64 and 28 bytes on x86.
            int expected = IntPtr.Size == 8 ? 40 : 28;
            Assert.Equal(expected, Marshal.SizeOf<NativeMethods.INPUT>());
        }
    }
}
