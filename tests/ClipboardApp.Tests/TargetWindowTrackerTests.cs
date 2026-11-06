using ClipboardApp.Core.Interop;
using System;
using Xunit;

namespace ClipboardApp.Tests
{
    public class TargetWindowTrackerTests
    {
        [Fact]
        public void TestZeroWindowIsNotValid()
        {
            var tracker = TargetWindowTracker.Instance;
            Assert.False(tracker.IsValidTargetWindow(IntPtr.Zero));
        }

        [Fact]
        public void TestFakeHwndIsNotValid()
        {
            var tracker = TargetWindowTracker.Instance;
            // High arbitrary HWND that doesn't exist
            var fakeHwnd = new IntPtr(0x12345678);
            Assert.False(tracker.IsValidTargetWindow(fakeHwnd));
        }
    }
}
