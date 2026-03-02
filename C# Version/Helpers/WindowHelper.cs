using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PACK.Helpers
{
    public class WindowInfo
    {
        public IntPtr Hwnd     { get; set; }
        public string Title    { get; set; } = "";
        public string? ExePath { get; set; }
        public string Display  => string.IsNullOrEmpty(ExePath)
            ? Title : $"{Title}  —  {Path.GetFileName(ExePath)}";
    }

    public static class WindowHelper
    {
        public static IntPtr FindWindowByTitle(string sub)
        {
            string lower = sub.ToLowerInvariant();
            IntPtr found = IntPtr.Zero;
            NativeMethods.EnumWindows((hwnd, _) =>
            {
                if (!NativeMethods.IsWindowVisible(hwnd)) return true;
                int len = NativeMethods.GetWindowTextLength(hwnd);
                if (len <= 0) return true;
                var sb = new StringBuilder(len + 1);
                NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
                if (sb.ToString().ToLowerInvariant().Contains(lower)) { found = hwnd; return false; }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        public static string? GetProcessImagePath(uint pid)
        {
            IntPtr h = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero) return null;
            try { var sb = new StringBuilder(260); uint sz = 260; return NativeMethods.QueryFullProcessImageName(h, 0, sb, ref sz) ? sb.ToString() : null; }
            finally { NativeMethods.CloseHandle(h); }
        }

        public static List<WindowInfo> EnumerateVisibleWindows()
        {
            var list = new List<WindowInfo>();
            NativeMethods.EnumWindows((hwnd, _) =>
            {
                if (!NativeMethods.IsWindowVisible(hwnd)) return true;
                int len = NativeMethods.GetWindowTextLength(hwnd);
                if (len <= 0) return true;
                var sb = new StringBuilder(len + 1);
                NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
                NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
                list.Add(new WindowInfo { Hwnd = hwnd, Title = sb.ToString(), ExePath = GetProcessImagePath(pid) });
                return true;
            }, IntPtr.Zero);
            return list;
        }

        public static WindowInfo? FindWindowByExePath(string targetPath)
        {
            string norm = Path.GetFullPath(targetPath).ToLowerInvariant();
            foreach (var w in EnumerateVisibleWindows())
                if (w.ExePath != null)
                    try { if (Path.GetFullPath(w.ExePath).ToLowerInvariant() == norm) return w; } catch { }
            return null;
        }

        public static void BringToForeground(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            try { NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE); } catch { }
            try
            {
                IntPtr fg = NativeMethods.GetForegroundWindow();
                uint cur = NativeMethods.GetCurrentThreadId();
                NativeMethods.GetWindowThreadProcessId(hwnd, out uint tgt);
                uint fgTid = 0;
                if (fg != IntPtr.Zero) NativeMethods.GetWindowThreadProcessId(fg, out fgTid);
                if (fgTid != 0 && fgTid != cur) NativeMethods.AttachThreadInput(cur, fgTid, true);
                if (tgt   != 0 && tgt   != cur) NativeMethods.AttachThreadInput(cur, tgt,   true);
                NativeMethods.SetForegroundWindow(hwnd);
                NativeMethods.BringWindowToTop(hwnd);
                NativeMethods.SetActiveWindow(hwnd);
                if (tgt   != 0 && tgt   != cur) NativeMethods.AttachThreadInput(cur, tgt,   false);
                if (fgTid != 0 && fgTid != cur) NativeMethods.AttachThreadInput(cur, fgTid, false);
            }
            catch { }
        }
    }
}
