using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ClipboardApp.Core.Interop
{
    public sealed class TargetWindowInfo
    {
        public IntPtr Handle { get; init; }
        public string Title { get; init; } = "";
        public string ProcessName { get; init; } = "";
        public bool IsElevated { get; init; }
        public bool IsMinimized { get; init; }

        public string DisplayName => string.IsNullOrWhiteSpace(Title)
            ? (string.IsNullOrWhiteSpace(ProcessName) ? "Untitled window" : ProcessName)
            : Title;
    }

    public sealed class TargetWindowTracker : IDisposable
    {
        private static TargetWindowTracker? _instance;
        public static TargetWindowTracker Instance => _instance ??= new TargetWindowTracker();

        private readonly NativeMethods.WinEventDelegate _winEventDelegate;
        private IntPtr _hHook;
        private readonly object _lock = new();
        private readonly List<IntPtr> _recentWindows = new();
        private const int MaxHistory = 12;
        private readonly uint _ownProcessId;

        public TargetWindowTracker()
        {
            _ownProcessId = (uint)Process.GetCurrentProcess().Id;
            _winEventDelegate = OnWinEvent;
            try
            {
                _hHook = NativeMethods.SetWinEventHook(
                    NativeMethods.EVENT_SYSTEM_FOREGROUND,
                    NativeMethods.EVENT_SYSTEM_FOREGROUND,
                    IntPtr.Zero,
                    _winEventDelegate,
                    0,
                    0,
                    NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);
            }
            catch
            {
                _hHook = IntPtr.Zero;
            }
        }

        private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (hwnd == IntPtr.Zero) return;
            if (IsValidTargetWindow(hwnd))
            {
                RecordManualWindow(hwnd);
            }
        }

        public bool IsValidTargetWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;

            var root = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
            if (root != IntPtr.Zero) hwnd = root;

            if (!NativeMethods.IsWindow(hwnd) || !NativeMethods.IsWindowVisible(hwnd)) return false;

            NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == _ownProcessId || pid == 0) return false;

            var sb = new StringBuilder(256);
            NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
            var className = sb.ToString();

            if (className is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Progman" or "WorkerW"
                or "TaskSwitcherWnd" or "Windows.UI.Core.CoreWindow")
            {
                return false;
            }

            // A visible top-level window with no title is usually a splash,
            // tooltip, IME or hidden helper. It is a poor paste target.
            if (NativeMethods.GetWindowTextLength(hwnd) == 0) return false;

            return true;
        }

        public IntPtr GetLastActiveClientWindow()
        {
            lock (_lock)
            {
                _recentWindows.RemoveAll(h => !NativeMethods.IsWindow(h));
                for (int i = 0; i < _recentWindows.Count; i++)
                {
                    var root = NormalizeWindow(_recentWindows[i]);
                    if (IsValidTargetWindow(root))
                    {
                        _recentWindows[i] = root;
                        return root;
                    }
                }
            }
            return IntPtr.Zero;
        }

        public IntPtr FindTopmostClientWindow(IntPtr excludeHwnd)
        {
            lock (_lock)
            {
                _recentWindows.RemoveAll(h => !NativeMethods.IsWindow(h));
                foreach (var h in _recentWindows)
                {
                    var root = NormalizeWindow(h);
                    if (root != excludeHwnd && IsValidTargetWindow(root))
                    {
                        return root;
                    }
                }
            }

            foreach (var candidate in GetCandidates(excludeHwnd))
            {
                return candidate.Handle;
            }

            return IntPtr.Zero;
        }

        public IReadOnlyList<TargetWindowInfo> GetCandidates(IntPtr excludeHwnd = default)
        {
            var ordered = new List<IntPtr>();
            lock (_lock)
            {
                _recentWindows.RemoveAll(h => !NativeMethods.IsWindow(h));
                ordered.AddRange(_recentWindows.Select(NormalizeWindow));
            }

            NativeMethods.EnumWindows((hwnd, _) =>
            {
                var root = NormalizeWindow(hwnd);
                if (root != IntPtr.Zero) ordered.Add(root);
                return true;
            }, IntPtr.Zero);

            var seen = new HashSet<IntPtr>();
            var result = new List<TargetWindowInfo>();
            foreach (var handle in ordered)
            {
                if (handle == IntPtr.Zero || handle == excludeHwnd) continue;
                if (!seen.Add(handle)) continue;
                if (!IsValidTargetWindow(handle)) continue;

                var info = GetTargetInfo(handle);
                if (info != null) result.Add(info);
                if (result.Count >= 20) break;
            }
            return result;
        }

        public TargetWindowInfo? GetTargetInfo(IntPtr hwnd)
        {
            var root = NormalizeWindow(hwnd);
            if (!IsValidTargetWindow(root)) return null;

            NativeMethods.GetWindowThreadProcessId(root, out var pid);
            string processName = "";
            bool elevated = false;
            try
            {
                using var process = Process.GetProcessById((int)pid);
                processName = process.ProcessName;
                elevated = NativeMethods.IsProcessElevated(pid);
            }
            catch { }

            var title = new StringBuilder(NativeMethods.GetWindowTextLength(root) + 2);
            NativeMethods.GetWindowText(root, title, title.Capacity);

            return new TargetWindowInfo
            {
                Handle = root,
                Title = title.ToString().Trim(),
                ProcessName = processName,
                IsElevated = elevated,
                IsMinimized = NativeMethods.IsIconic(root)
            };
        }

        public void RecordManualWindow(IntPtr hwnd)
        {
            var root = NormalizeWindow(hwnd);
            if (IsValidTargetWindow(root))
            {
                lock (_lock)
                {
                    _recentWindows.Remove(root);
                    _recentWindows.Insert(0, root);
                    while (_recentWindows.Count > MaxHistory)
                    {
                        _recentWindows.RemoveAt(_recentWindows.Count - 1);
                    }
                }
            }
        }

        private static IntPtr NormalizeWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return IntPtr.Zero;
            var root = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
            return root == IntPtr.Zero ? hwnd : root;
        }

        public void Dispose()
        {
            if (_hHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWinEvent(_hHook);
                _hHook = IntPtr.Zero;
            }
        }
    }
}
