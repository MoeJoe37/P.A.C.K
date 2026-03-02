using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace PACK.Helpers
{
    public static class InputHelper
    {
        private static readonly int SZ = Marshal.SizeOf<INPUT>();

        // ── Mouse ────────────────────────────────────────────────────────────
        public static void SendMouseClick(bool left)
        {
            var a = new INPUT[2];
            a[0].type = NativeMethods.INPUT_MOUSE; a[0].u.mi.dwFlags = left ? NativeMethods.MOUSEEVENTF_LEFTDOWN  : NativeMethods.MOUSEEVENTF_RIGHTDOWN;
            a[1].type = NativeMethods.INPUT_MOUSE; a[1].u.mi.dwFlags = left ? NativeMethods.MOUSEEVENTF_LEFTUP    : NativeMethods.MOUSEEVENTF_RIGHTUP;
            NativeMethods.SendInput(2, a, SZ);
        }

        public static void SendMouseDown(bool left)
        {
            var a = new INPUT[1];
            a[0].type = NativeMethods.INPUT_MOUSE;
            a[0].u.mi.dwFlags = left ? NativeMethods.MOUSEEVENTF_LEFTDOWN : NativeMethods.MOUSEEVENTF_RIGHTDOWN;
            NativeMethods.SendInput(1, a, SZ);
        }

        public static void SendMouseUp(bool left)
        {
            var a = new INPUT[1];
            a[0].type = NativeMethods.INPUT_MOUSE;
            a[0].u.mi.dwFlags = left ? NativeMethods.MOUSEEVENTF_LEFTUP : NativeMethods.MOUSEEVENTF_RIGHTUP;
            NativeMethods.SendInput(1, a, SZ);
        }

        /// <summary>Sends a mouse scroll event at the current cursor position.
        /// delta: positive = scroll up, negative = scroll down. 120 = one notch.</summary>
        public static void SendMouseScroll(int delta = -120)
        {
            var a = new INPUT[1];
            a[0].type = NativeMethods.INPUT_MOUSE;
            a[0].u.mi.dwFlags    = NativeMethods.MOUSEEVENTF_WHEEL;
            a[0].u.mi.mouseData  = unchecked((uint)delta);
            NativeMethods.SendInput(1, a, SZ);
        }

        // ── Keyboard ─────────────────────────────────────────────────────────
        public static void SendKeySequenceVk(List<int> vkList)
        {
            foreach (int vk in vkList)
            {
                ushort sc = (ushort)NativeMethods.MapVirtualKey((uint)vk, NativeMethods.MAPVK_VK_TO_VSC);
                var a = new INPUT[1];
                a[0].type = NativeMethods.INPUT_KEYBOARD;
                a[0].u.ki = new KEYBDINPUT { wScan = sc, dwFlags = NativeMethods.KEYEVENTF_SCANCODE };
                NativeMethods.SendInput(1, a, SZ);
                Thread.Sleep(1);
            }
            for (int i = vkList.Count - 1; i >= 0; i--)
            {
                ushort sc = (ushort)NativeMethods.MapVirtualKey((uint)vkList[i], NativeMethods.MAPVK_VK_TO_VSC);
                var a = new INPUT[1];
                a[0].type = NativeMethods.INPUT_KEYBOARD;
                a[0].u.ki = new KEYBDINPUT { wScan = sc, dwFlags = NativeMethods.KEYEVENTF_SCANCODE | NativeMethods.KEYEVENTF_KEYUP };
                NativeMethods.SendInput(1, a, SZ);
                Thread.Sleep(1);
            }
        }

        // ── Window click ─────────────────────────────────────────────────────
        public static bool PostMouseClickToWindow(IntPtr hwnd, bool left)
        {
            if (hwnd == IntPtr.Zero) return false;
            if (!NativeMethods.GetClientRect(hwnd, out RECT r)) return false;
            int cx = (r.Right - r.Left) / 2, cy = (r.Bottom - r.Top) / 2;
            IntPtr lp = (IntPtr)((cy << 16) | (cx & 0xffff));
            if (left) { NativeMethods.PostMessage(hwnd, NativeMethods.WM_LBUTTONDOWN, (IntPtr)NativeMethods.MK_LBUTTON, lp); NativeMethods.PostMessage(hwnd, NativeMethods.WM_LBUTTONUP, IntPtr.Zero, lp); }
            else      { NativeMethods.PostMessage(hwnd, NativeMethods.WM_RBUTTONDOWN, IntPtr.Zero, lp); NativeMethods.PostMessage(hwnd, NativeMethods.WM_RBUTTONUP, IntPtr.Zero, lp); }
            return true;
        }

        public static bool MoveCursorToWindowCenter(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;
            if (!NativeMethods.GetClientRect(hwnd, out RECT r)) return false;
            var pt = new POINT { X = (r.Right - r.Left) / 2, Y = (r.Bottom - r.Top) / 2 };
            if (!NativeMethods.ClientToScreen(hwnd, ref pt)) return false;
            NativeMethods.SetCursorPos(pt.X, pt.Y);
            return true;
        }

        // ── VK mapping ───────────────────────────────────────────────────────
        private static readonly Dictionary<string, int> VkMap = new(StringComparer.OrdinalIgnoreCase)
        {
            {"shift",0x10},{"ctrl",0x11},{"control",0x11},{"alt",0x12},
            {"space",0x20},{"enter",0x0D},{"return",0x0D},{"escape",0x1B},{"esc",0x1B},
            {"left",0x25},{"up",0x26},{"right",0x27},{"down",0x28},
            {"tab",0x09},{"backspace",0x08},{"delete",0x2E},{"insert",0x2D},
            {"home",0x24},{"end",0x23},{"pageup",0x21},{"pagedown",0x22},
            {"numpad0",0x60},{"numpad1",0x61},{"numpad2",0x62},{"numpad3",0x63},
            {"numpad4",0x64},{"numpad5",0x65},{"numpad6",0x66},{"numpad7",0x67},
            {"numpad8",0x68},{"numpad9",0x69},
        };

        static InputHelper()
        {
            for (int i = 1; i <= 24; i++) VkMap[$"f{i}"] = 0x70 + i - 1;
            for (int i = 0; i <= 9;  i++) VkMap[$"{i}"]  = 0x30 + i;
            for (char c = 'a'; c <= 'z'; c++) VkMap[$"{c}"] = c - 'a' + 0x41;
        }

        /// <summary>Parses a combo string like "Ctrl+Shift+A" into a VK list. Returns null on failure.</summary>
        public static List<int>? ParseComboToVkList(string comboStr)
        {
            if (string.IsNullOrWhiteSpace(comboStr)) return null;
            var parts = comboStr.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            var list = new List<int>();
            foreach (var p in parts)
            {
                string k = p.Trim().ToLower() switch
                {
                    "control_l" or "control_r" or "ctrl_l" or "ctrl_r" => "ctrl",
                    "shift_l"   or "shift_r"                            => "shift",
                    "alt_l"     or "alt_r"                              => "alt",
                    string s => s
                };
                if (VkMap.TryGetValue(k, out int vk)) { list.Add(vk); continue; }
                if (k.Length == 1) { short r = NativeMethods.VkKeyScan(k[0]); if (r != -1) { list.Add(r & 0xff); continue; } }
                return null;
            }
            return list.Count > 0 ? list : null;
        }
    }
}
