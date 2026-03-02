using System;
using System.Runtime.InteropServices;

namespace PACK.Helpers
{
    [StructLayout(LayoutKind.Sequential)] public struct POINT  { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT   { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT  { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT  { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT { public uint uMsg; public ushort wParamL, wParamH; }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT   mi;
        [FieldOffset(0)] public KEYBDINPUT   ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public uint type; public InputUnion u; }

    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT  pt;
        public uint   mouseData, flags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint   vkCode, scanCode, flags, time;
        public IntPtr dwExtraInfo;
    }

    public static class NativeMethods
    {
        public const uint INPUT_MOUSE    = 0;
        public const uint INPUT_KEYBOARD = 1;
        public const uint MOUSEEVENTF_LEFTDOWN  = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP    = 0x0004;
        public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const uint MOUSEEVENTF_RIGHTUP   = 0x0010;
        public const uint KEYEVENTF_KEYUP    = 0x0002;
        public const uint KEYEVENTF_SCANCODE = 0x0008;
        public const uint MAPVK_VK_TO_VSC   = 0;
        public const uint WM_LBUTTONDOWN = 0x0201;
        public const uint WM_LBUTTONUP   = 0x0202;
        public const uint WM_RBUTTONDOWN = 0x0204;
        public const uint WM_RBUTTONUP   = 0x0205;
        public const uint MK_LBUTTON = 0x0001;
        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        public const int  SW_RESTORE = 9;
        public const int  WH_MOUSE_LL    = 14;
        public const int  WH_KEYBOARD_LL = 13;
        public const int  WM_MOUSEMOVE   = 0x0200;
        public const int  WM_KEYDOWN     = 0x0100;
        public const int  WM_SYSKEYDOWN  = 0x0104;
        public const int  WM_KEYUP       = 0x0101;
        public const int  WM_SYSKEYUP    = 0x0105;
        public const int  WM_MOUSEWHEEL  = 0x020A;
        public const uint MOUSEEVENTF_WHEEL = 0x0800;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll")] public static extern uint SendInput(uint n, INPUT[] p, int cb);
        [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint uCode, uint uMapType);
        [DllImport("user32.dll")] public static extern short VkKeyScan(char ch);
        [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT p);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern int  GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder s, int n);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool f);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern IntPtr SetActiveWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmd);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint acc, bool inh, uint pid);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern bool QueryFullProcessImageName(IntPtr hProc, uint flags, System.Text.StringBuilder name, ref uint size);
        [DllImport("shell32.dll")] public static extern bool IsUserAnAdmin();
    }
}
