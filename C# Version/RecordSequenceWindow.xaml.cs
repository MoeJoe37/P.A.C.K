using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using PACK.Helpers;
using PACK.Models;

namespace PACK
{
    public partial class RecordSequenceWindow : Window
    {
        public List<SequenceItem>? RecordedSequence { get; private set; }

        private readonly List<SequenceItem> _items   = new();
        private readonly HashSet<uint>      _mods    = new();
        private DateTime                    _lastTime;

        private IntPtr    _mouseHook  = IntPtr.Zero;
        private IntPtr    _kbdHook    = IntPtr.Zero;
        private HookProc? _mouseRef;
        private HookProc? _kbdRef;

        private int  _moveLastX, _moveLastY;
        private long _moveLastTick;
        private const int MoveIntervalMs = 16;
        private const int MoveMinDist2   = 9;

        private static readonly HashSet<uint> ModVKs = new()
            { 0x10, 0x11, 0x12, 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5 };

        private static uint NormMod(uint vk) => vk switch
        {
            0xA0 or 0xA1 => 0x10,
            0xA2 or 0xA3 => 0x11,
            0xA4 or 0xA5 => 0x12,
            _ => vk
        };

        public RecordSequenceWindow() => InitializeComponent();

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            StartHooks();
            _lastTime = DateTime.UtcNow;
        }

        private void StartHooks()
        {
            using var proc = Process.GetCurrentProcess();
            using var mod  = proc.MainModule!;
            IntPtr hMod = NativeMethods.GetModuleHandle(mod.ModuleName);
            _mouseRef  = MouseHookProc;
            _kbdRef    = KbdHookProc;
            _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseRef, hMod, 0);
            _kbdHook   = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _kbdRef,  hMod, 0);
        }

        private void StopHooks()
        {
            if (_mouseHook != IntPtr.Zero) { NativeMethods.UnhookWindowsHookEx(_mouseHook); _mouseHook = IntPtr.Zero; }
            if (_kbdHook   != IntPtr.Zero) { NativeMethods.UnhookWindowsHookEx(_kbdHook);   _kbdHook   = IntPtr.Zero; }
        }

        // ── Mouse hook ────────────────────────────────────────────────────────
        private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = (int)wParam;
                if (msg == 0x0201 || msg == 0x0204) // LBUTTONDOWN | RBUTTONDOWN
                {
                    var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    bool left = msg == 0x0201;
                    Dispatcher.BeginInvoke(() => AddClick(info.pt.X, info.pt.Y, left));
                }
                else if (msg == NativeMethods.WM_MOUSEMOVE)
                {
                    var  info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    long now  = Environment.TickCount64;
                    int  dx   = info.pt.X - _moveLastX;
                    int  dy   = info.pt.Y - _moveLastY;
                    long dt   = now - _moveLastTick;
                    if (dt >= MoveIntervalMs && dx * dx + dy * dy >= MoveMinDist2)
                    {
                        int deltaMs = (int)Math.Min(dt, 5000);
                        _moveLastX = info.pt.X; _moveLastY = info.pt.Y; _moveLastTick = now;
                        int cx = info.pt.X, cy = info.pt.Y;
                        Dispatcher.BeginInvoke(() => AddMove(cx, cy, deltaMs));
                    }
                }
            }
            return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        // ── Keyboard hook ─────────────────────────────────────────────────────
        private IntPtr KbdHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg  = (int)wParam;
                var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
                    Dispatcher.BeginInvoke(() => OnKeyDown(info.vkCode));
                else if (msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP)
                    Dispatcher.BeginInvoke(() => _mods.Remove(NormMod(info.vkCode)));
            }
            return NativeMethods.CallNextHookEx(_kbdHook, nCode, wParam, lParam);
        }

        // ── Recording helpers ─────────────────────────────────────────────────
        private void AddClick(int x, int y, bool left)
        {
            MaybeAddWait();
            _items.Add(new SequenceItem
            {
                Type       = SeqType.Click,
                ClickX     = x, ClickY = y,
                ComboLabel = left ? "left" : "right"
            });
            RefreshList();
        }

        private void AddMove(int x, int y, int deltaMs)
        {
            _items.Add(new SequenceItem { Type = SeqType.Move, ClickX = x, ClickY = y, WaitMs = deltaMs });
            _lastTime = DateTime.UtcNow;
            RefreshList();
        }

        private void OnKeyDown(uint vk)
        {
            if (ModVKs.Contains(vk)) { _mods.Add(NormMod(vk)); return; }
            MaybeAddWait();
            var vkList = new List<int>();
            foreach (uint m in _mods) vkList.Add((int)m);
            vkList.Add((int)vk);
            string label = BuildLabel(_mods, vk);
            _items.Add(new SequenceItem { Type = SeqType.Key, VkList = vkList, ComboLabel = label });
            RefreshList();
        }

        private void MaybeAddWait()
        {
            if (_items.Count == 0) { _lastTime = DateTime.UtcNow; return; }
            if (RecordWaitsChk.IsChecked != true) { _lastTime = DateTime.UtcNow; return; }
            int threshold = int.TryParse(WaitThresholdTxt.Text, out int t) ? Math.Max(50, t) : 200;
            int gap = (int)(DateTime.UtcNow - _lastTime).TotalMilliseconds;
            if (gap >= threshold && gap < 30_000)
                _items.Add(new SequenceItem { Type = SeqType.Wait, WaitMs = gap });
            _lastTime = DateTime.UtcNow;
        }

        private void RefreshList()
        {
            ActionsList.Items.Clear();
            foreach (var item in _items) ActionsList.Items.Add($"  {item.Display}");
            ActionsList.ScrollIntoView(ActionsList.Items[^1]);
            CountLabel.Text = $"{_items.Count} action{(_items.Count == 1 ? "" : "s")} captured";
        }

        // ── Button handlers ───────────────────────────────────────────────────
        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            _items.Clear();
            ActionsList.Items.Clear();
            CountLabel.Text = "0 actions captured";
            _lastTime = DateTime.UtcNow;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            StopHooks();
            DialogResult = false;
        }

        private void StopDone_Click(object sender, RoutedEventArgs e)
        {
            StopHooks();
            RecordedSequence = new List<SequenceItem>(_items);
            DialogResult     = true;
        }

        protected override void OnClosed(EventArgs e)
        {
            StopHooks();
            base.OnClosed(e);
        }

        // ── Label helpers ─────────────────────────────────────────────────────
        private static string BuildLabel(HashSet<uint> mods, uint vk)
        {
            var parts = new List<string>();
            if (mods.Contains(0x11)) parts.Add("Ctrl");
            if (mods.Contains(0x10)) parts.Add("Shift");
            if (mods.Contains(0x12)) parts.Add("Alt");
            parts.Add(VkToLabel(vk));
            return string.Join("+", parts);
        }

        private static string VkToLabel(uint vk) => vk switch
        {
            0x08 => "Backspace", 0x09 => "Tab", 0x0D => "Enter",
            0x1B => "Escape",    0x20 => "Space",
            0x21 => "PageUp",    0x22 => "PageDown",
            0x23 => "End",       0x24 => "Home",
            0x25 => "Left",      0x26 => "Up", 0x27 => "Right", 0x28 => "Down",
            0x2D => "Insert",    0x2E => "Delete",
            >= 0x30 and <= 0x39 => ((char)vk).ToString(),
            >= 0x41 and <= 0x5A => ((char)vk).ToString(),
            >= 0x60 and <= 0x69 => $"Num{vk - 0x60}",
            >= 0x70 and <= 0x87 => $"F{vk - 0x6F}",
            _ => $"VK{vk:X2}"
        };
    }
}
