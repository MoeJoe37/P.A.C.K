using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using PACK.Helpers;
using PACK.Models;

namespace PACK
{
    public partial class MainWindow : Window
    {
        // ── State ────────────────────────────────────────────────────────────
        private readonly List<(int X, int Y)>              _locations     = new();
        private readonly List<SequenceItem>                _sequence      = new();
        private readonly ObservableCollection<MainTabAction> _mainActions = new();
        private List<WindowInfo>                           _appItems      = new();

        private IntPtr _targetHwnd = IntPtr.Zero;
        private bool   _running    = false;
        private bool   _seqRunning = false;
        private CancellationTokenSource? _cts;
        private CancellationTokenSource? _seqCts;

        // ── Global hotkey hook (always-on) ───────────────────────────────────
        private IntPtr        _globalHook     = IntPtr.Zero;
        private HookProc?     _globalRef;
        private List<int>?    _hotkeyVkList;
        private string        _hotkeyDisplay  = "(none set)";

        // ── Background sequence recording (hotkey-triggered → Sequence tab) ──
        private bool          _bgRecording    = false;
        private readonly List<SequenceItem>  _bgItems   = new();
        private readonly HashSet<uint>       _bgMods    = new();
        private DateTime      _bgLastTime;
        private IntPtr        _bgMouseHook    = IntPtr.Zero;
        private IntPtr        _bgKbdHook      = IntPtr.Zero;
        private HookProc?     _bgMouseRef;
        private HookProc?     _bgKbdRef;
        private const int     BgWaitThreshMs  = 200;

        // ── Main-tab action recording (Record Action button → Main tab list) ─
        private bool          _mainTabRecording = false;
        private readonly List<MainTabAction>  _mainTabItems  = new();
        private IntPtr        _mainMouseHook    = IntPtr.Zero;
        private HookProc?     _mainMouseRef;

        // Move throttle
        private int  _bgMoveLastX, _bgMoveLastY;
        private long _bgMoveLastTick;
        private bool _bgMoveRunActive;
        private int  _bgMoveRunCount;
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

        // ── Init ─────────────────────────────────────────────────────────────
        public MainWindow()
        {
            InitializeComponent();
            MainActionListBox.ItemsSource = _mainActions;
            RefreshAppsInternal();
            Loaded  += (_, _) => { CheckAdmin(); InstallGlobalHook(); UpdateRecordHint(); };
            Closed  += (_, _) => { RemoveGlobalHook(); RemoveBgHooks(); RemoveMainMouseHook(); };
        }

        private void CheckAdmin()
        {
            bool admin;
            try { admin = NativeMethods.IsUserAnAdmin(); } catch { admin = false; }
            if (!admin)
                MessageBox.Show(
                    "For best compatibility inside games, run PACK as Administrator.\n" +
                    "Right-click the executable and choose 'Run as administrator'.",
                    "Administrator Recommended",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UpdateRecordHint()
        {
            if (RecordActionHint == null) return;
            bool hasHotkey = _hotkeyVkList != null;
            RecordActionHint.Text = hasHotkey
                ? $"Hotkey: {_hotkeyDisplay} — press it to stop recording"
                : "(set a hotkey in Settings first)";
            if (RecordActionBtn != null) RecordActionBtn.IsEnabled = hasHotkey;
        }

        // ── Navigation ───────────────────────────────────────────────────────
        private void Nav_Click(object s, RoutedEventArgs e)
        {
            PanelMain.Visibility     = Visibility.Collapsed;
            PanelLoc.Visibility      = Visibility.Collapsed;
            PanelSeq.Visibility      = Visibility.Collapsed;
            PanelApps.Visibility     = Visibility.Collapsed;
            PanelSettings.Visibility = Visibility.Collapsed;
            PanelAbout.Visibility    = Visibility.Collapsed;

            foreach (var b in new[] { NavMain, NavLoc, NavSeq, NavApps, NavSettings, NavAbout })
                b.Style = (Style)Resources["BNav"];

            var btn = (Button)s;
            btn.Style = (Style)Resources["BNavA"];

            if      (btn == NavMain)     PanelMain.Visibility     = Visibility.Visible;
            else if (btn == NavLoc)      PanelLoc.Visibility      = Visibility.Visible;
            else if (btn == NavSeq)      PanelSeq.Visibility      = Visibility.Visible;
            else if (btn == NavApps)     PanelApps.Visibility     = Visibility.Visible;
            else if (btn == NavSettings) PanelSettings.Visibility = Visibility.Visible;
            else if (btn == NavAbout)    PanelAbout.Visibility    = Visibility.Visible;
        }

        // ── Status helper ────────────────────────────────────────────────────
        private void SetStatus(string text, string hex) =>
            Dispatcher.Invoke(() =>
            {
                StatusLabel.Text = text;
                StatusLabel.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
            });

        // ── Repeat / timer checkbox ──────────────────────────────────────────
        private void RepForever_Changed(object s, RoutedEventArgs e)
        {
            if (RepeatCountTxt != null)
                RepeatCountTxt.IsEnabled = RepForeverChk.IsChecked != true;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  MAIN TAB — Record Action
        // ═════════════════════════════════════════════════════════════════════

        private void RecordActionBtn_Click(object s, RoutedEventArgs e)
        {
            if (_hotkeyVkList == null)
            {
                MessageBox.Show(
                    "Please set a hotkey in the Settings tab first.\n" +
                    "The hotkey is used to stop recording.",
                    "No Hotkey Set", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_bgRecording || _mainTabRecording) return; // already recording

            // Minimise PACK so the user can record freely
            WindowState = WindowState.Minimized;
            StartMainTabRecording();
        }

        private void StartMainTabRecording()
        {
            _mainTabItems.Clear();
            _mainTabRecording = true;

            using var proc = Process.GetCurrentProcess();
            using var mod  = proc.MainModule!;
            IntPtr hMod = NativeMethods.GetModuleHandle(mod.ModuleName);
            _mainMouseRef  = MainMouseHookProc;
            _mainMouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mainMouseRef, hMod, 0);

            Dispatcher.BeginInvoke(() =>
            {
                MainRecordBanner.Visibility = Visibility.Visible;
                MainRecordCountLabel.Text   = "Click or scroll anywhere. Press hotkey to stop.";
                SetStatus($"⏺  Recording actions… (press {_hotkeyDisplay} to stop)", "#F38BA8");
            });
        }

        private void StopMainTabRecording()
        {
            if (!_mainTabRecording) return;
            _mainTabRecording = false;
            RemoveMainMouseHook();

            Dispatcher.Invoke(() =>
            {
                MainRecordBanner.Visibility = Visibility.Collapsed;
                SetStatus("Ready", "#A6E3A1");

                // Restore the window
                WindowState = WindowState.Normal;
                Activate();

                // Populate the observable collection
                _mainActions.Clear();
                foreach (var a in _mainTabItems) _mainActions.Add(a);
                MainActionListBox.ItemsSource = _mainActions;
            });
        }

        private void StopMainRecord_Click(object s, RoutedEventArgs e) => StopMainTabRecording();

        private IntPtr MainMouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _mainTabRecording)
            {
                int msg = (int)wParam;
                if (msg == 0x0201 || msg == 0x0204) // LBUTTONDOWN / RBUTTONDOWN
                {
                    var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    bool left = msg == 0x0201;
                    Dispatcher.BeginInvoke(() => MainAddClick(info.pt.X, info.pt.Y, left));
                }
                else if (msg == NativeMethods.WM_MOUSEWHEEL)
                {
                    var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    Dispatcher.BeginInvoke(() => MainAddScroll(info.pt.X, info.pt.Y));
                }
            }
            return NativeMethods.CallNextHookEx(_mainMouseHook, nCode, wParam, lParam);
        }

        private void MainAddClick(int x, int y, bool left)
        {
            var action = new MainTabAction { X = x, Y = y, IsLeftClick = left, IsRightClick = !left };
            _mainTabItems.Add(action);
            UpdateMainRecordCount();
        }

        private void MainAddScroll(int x, int y)
        {
            _mainTabItems.Add(new MainTabAction { X = x, Y = y, IsScroll = true });
            UpdateMainRecordCount();
        }

        private void UpdateMainRecordCount()
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (MainRecordCountLabel != null)
                    MainRecordCountLabel.Text = $"{_mainTabItems.Count} action(s) recorded — press {_hotkeyDisplay} to stop";
            });
        }

        private void RemoveMainMouseHook()
        {
            if (_mainMouseHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_mainMouseHook);
                _mainMouseHook = IntPtr.Zero;
            }
        }

        // ── Save recorded main-tab actions ────────────────────────────────────
        private void SaveActionsToLocations_Click(object s, RoutedEventArgs e)
        {
            if (_mainActions.Count == 0)
            { MessageBox.Show("No recorded actions to save.", "Empty", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            int added = 0;
            foreach (var a in _mainActions)
            {
                if (a.IsMouseClick || a.IsScroll) { _locations.Add((a.X, a.Y)); added++; }
            }
            RefreshLocListBox();
            MessageBox.Show($"Saved {added} location(s) to the Locations tab.", "Saved",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveActionsToSequences_Click(object s, RoutedEventArgs e)
        {
            if (_mainActions.Count == 0)
            { MessageBox.Show("No recorded actions to save.", "Empty", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            foreach (var a in _mainActions)
            {
                if (a.IsScroll)
                {
                    _sequence.Add(new SequenceItem { Type = SeqType.Click, ClickX = a.X, ClickY = a.Y, ComboLabel = "scroll" });
                }
                else
                {
                    _sequence.Add(new SequenceItem
                    {
                        Type       = SeqType.Click,
                        ClickX     = a.X,
                        ClickY     = a.Y,
                        ComboLabel = a.IsLeftClick ? "left" : "right",
                        WaitMs     = a.Hold ? -1 : 0   // -1 signals hold mode
                    });
                }
            }
            RefreshSeqListBox();
            MessageBox.Show($"Saved {_mainActions.Count} action(s) to the Sequence tab.", "Saved",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ClearMainActions_Click(object s, RoutedEventArgs e)
        {
            _mainActions.Clear();
            _mainTabItems.Clear();
        }

        // ── Test Once ────────────────────────────────────────────────────────
        private void TestOnce_Click(object s, RoutedEventArgs e)
        {
            if (_mainActions.Count == 0 && _locations.Count == 0)
            { MessageBox.Show("No recorded actions or locations to test.", "Nothing to test", MessageBoxButton.OK, MessageBoxImage.Information); return; }

            IntPtr hwnd = GetTargetHwnd();
            if (_mainActions.Count > 0)
            {
                foreach (var a in _mainActions)
                {
                    NativeMethods.SetCursorPos(a.X, a.Y);
                    if (a.IsScroll) InputHelper.SendMouseScroll(-120);
                    else if (a.Hold) { InputHelper.SendMouseDown(a.IsLeftClick); Thread.Sleep(200); InputHelper.SendMouseUp(a.IsLeftClick); }
                    else DoClickOnce(a.IsLeftClick, hwnd, 0, false);
                }
            }
            else
            {
                if (hwnd != IntPtr.Zero) { WindowHelper.BringToForeground(hwnd); InputHelper.MoveCursorToWindowCenter(hwnd); }
                else if (_locations.Count > 0) NativeMethods.SetCursorPos(_locations[0].X, _locations[0].Y);
                DoClickOnce(true, hwnd, GetHoldMs(), GetDoubleClick());
            }
            MessageBox.Show("Test complete.", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── Run ──────────────────────────────────────────────────────────────
        private void RunBtn_Click(object s, RoutedEventArgs e)
        {
            if (_running) return;

            if (!double.TryParse(CpsTxt.Text, out double cps) || cps <= 0)
            { MessageBox.Show("Enter a positive CPS value.", "Invalid CPS", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            bool   repeatForever = MainRepeatForeverChk.IsChecked == true || RepForeverChk.IsChecked == true;
            int?   repeatCount   = null;
            if (!repeatForever)
            {
                if (!int.TryParse(RepeatCountTxt.Text, out int rc) || rc < 0)
                { MessageBox.Show("Enter a valid repeat count.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                repeatCount = rc;
            }

            double.TryParse(TimerSecTxt.Text, out double timerSec);
            if (timerSec < 0) timerSec = 0;
            double.TryParse(StartDelaySec.Text, out double startDelay);
            if (startDelay < 0) startDelay = 0;

            bool   doubleClick = DoubleClickChk.IsChecked == true;
            int    holdMs      = GetHoldMs();
            bool   randomize   = RandomizeChk.IsChecked == true;
            double randMin = 0, randMax = 0;
            if (randomize) { double.TryParse(RandMinTxt.Text, out randMin); double.TryParse(RandMaxTxt.Text, out randMax); }

            var locations  = new List<(int, int)>(_locations);
            var sequence   = new List<SequenceItem>(_sequence);
            var mainActs   = new List<MainTabAction>(_mainActions);

            _running = true;
            _cts     = new CancellationTokenSource();
            RunBtn.IsEnabled  = false;
            StopBtn.IsEnabled = true;
            SetStatus("Starting…", "#F9E2AF");

            var token = _cts.Token;
            new Thread(() => WorkerMain(cps, repeatCount, timerSec, startDelay,
                                        doubleClick, holdMs, randomize, randMin, randMax,
                                        locations, sequence, mainActs, token))
            { IsBackground = true }.Start();
        }

        private void StopBtn_Click(object s, RoutedEventArgs e)
        {
            _cts?.Cancel();
            SetStatus("Stopping…", "#F9E2AF");
        }

        // ── Worker ───────────────────────────────────────────────────────────
        private void WorkerMain(
            double cps, int? repeatCount, double timerSec,
            double startDelay, bool doubleClick, int holdMs,
            bool randomize, double randMinMs, double randMaxMs,
            List<(int X, int Y)> locations, List<SequenceItem> sequence,
            List<MainTabAction> mainActs, CancellationToken token)
        {
            for (int i = (int)startDelay; i > 0 && !token.IsCancellationRequested; i--)
            {
                SetStatus($"Starting in {i}s…", "#F9E2AF");
                Thread.Sleep(1000);
            }
            if (token.IsCancellationRequested) { FinalizeUi(); return; }
            SetStatus("Running…", "#F38BA8");

            IntPtr hwnd = GetTargetHwnd();
            if (hwnd != IntPtr.Zero) WindowHelper.BringToForeground(hwnd);

            var rng      = new Random();
            var sw       = Stopwatch.StartNew();
            int executed = 0;
            double randMin = randMinMs / 1000.0;
            double randMax = randMaxMs / 1000.0;
            if (randMax < randMin) randMax = randMin;

            while (!token.IsCancellationRequested)
            {
                if (timerSec  > 0 && sw.Elapsed.TotalSeconds >= timerSec) break;
                if (repeatCount.HasValue && executed >= repeatCount.Value) break;

                if (sequence.Count > 0)
                {
                    if (hwnd != IntPtr.Zero) WindowHelper.BringToForeground(hwnd);
                    RunSequenceOnce(sequence, hwnd, doubleClick, holdMs, token);
                }
                else if (mainActs.Count > 0)
                {
                    // Run all recorded main-tab actions (respecting their Hold state)
                    foreach (var a in mainActs)
                    {
                        if (token.IsCancellationRequested) break;
                        NativeMethods.SetCursorPos(a.X, a.Y);
                        if (a.IsScroll)
                            InputHelper.SendMouseScroll(-120);
                        else if (a.Hold)
                        {
                            InputHelper.SendMouseDown(a.IsLeftClick);
                            Thread.Sleep(Math.Max(holdMs, 200));
                            InputHelper.SendMouseUp(a.IsLeftClick);
                        }
                        else
                            DoClickOnce(a.IsLeftClick, hwnd, holdMs, doubleClick);
                        Thread.Sleep(1);
                    }
                }
                else if (locations.Count > 0)
                {
                    var (lx, ly) = locations[executed % locations.Count];
                    NativeMethods.SetCursorPos(lx, ly);
                    DoClickOnce(true, hwnd, holdMs, doubleClick);
                }
                else break;

                executed++;
                double interval = cps > 0 ? 1.0 / cps : 0;
                if (randomize) interval += randMin + rng.NextDouble() * (randMax - randMin);
                Thread.Sleep(interval > 0 ? (int)(interval * 1000) : 1);
            }

            FinalizeUi();
        }

        private void RunSequenceOnce(List<SequenceItem> seq, IntPtr hwnd,
                                     bool doubleClick, int holdMs, CancellationToken token)
        {
            Stopwatch? moveSw    = null;
            long       moveAccum = 0;

            foreach (var item in seq)
            {
                if (token.IsCancellationRequested) break;

                switch (item.Type)
                {
                    case SeqType.Move:
                        if (moveSw == null) { moveSw = Stopwatch.StartNew(); moveAccum = 0; }
                        moveAccum += item.WaitMs;
                        { long rem = moveAccum - moveSw.ElapsedMilliseconds; if (rem > 0) Thread.Sleep((int)rem); }
                        if (item.ClickX.HasValue) NativeMethods.SetCursorPos(item.ClickX.Value, item.ClickY!.Value);
                        break;

                    case SeqType.Click:
                        moveSw = null;
                        if (item.WindowCenter && hwnd != IntPtr.Zero) InputHelper.MoveCursorToWindowCenter(hwnd);
                        else if (item.ClickX.HasValue) NativeMethods.SetCursorPos(item.ClickX.Value, item.ClickY!.Value);
                        // item.WaitMs == -1 signals a "hold" action saved from main tab
                        if (item.WaitMs == -1)
                        {
                            bool lft = item.ComboLabel != "right";
                            InputHelper.SendMouseDown(lft);
                            Thread.Sleep(200);
                            InputHelper.SendMouseUp(lft);
                        }
                        else
                        {
                            bool left2 = item.ComboLabel != "right";
                            DoClickOnce(left2, hwnd, holdMs, doubleClick);
                        }
                        Thread.Sleep(1);
                        break;

                    case SeqType.Key:
                        moveSw = null;
                        if (item.VkList != null) InputHelper.SendKeySequenceVk(item.VkList);
                        Thread.Sleep(1);
                        break;

                    case SeqType.Wait:
                        moveSw = null;
                        Thread.Sleep(item.WaitMs);
                        break;
                }
            }
        }

        private void FinalizeUi()
        {
            _running = false;
            Dispatcher.Invoke(() =>
            {
                RunBtn.IsEnabled  = true;
                StopBtn.IsEnabled = false;
                SetStatus("Done", "#A6E3A1");
                // FEATURE 3 — restore window to normal after sequence finishes
                if (WindowState == WindowState.Minimized)
                    WindowState = WindowState.Normal;
                Activate();
            });
        }

        private static void DoClickOnce(bool left, IntPtr hwnd, int holdMs, bool doubleClick)
        {
            if (doubleClick)
            {
                InputHelper.SendMouseClick(left);
                Thread.Sleep(20);
                InputHelper.SendMouseClick(left);
            }
            else if (holdMs > 0)
            {
                InputHelper.SendMouseDown(left);
                Thread.Sleep(holdMs);
                InputHelper.SendMouseUp(left);
            }
            else
            {
                InputHelper.SendMouseClick(left);
            }
            if (hwnd != IntPtr.Zero) InputHelper.PostMouseClickToWindow(hwnd, left);
        }

        private string GetAction()      => "Left Click";
        private bool   GetDoubleClick() => Dispatcher.Invoke(() => DoubleClickChk.IsChecked == true);
        private int    GetHoldMs()      => Dispatcher.Invoke(() => int.TryParse(HoldMsTxt.Text, out int v) ? Math.Max(0, v) : 0);

        private IntPtr GetTargetHwnd()
        {
            if (_targetHwnd != IntPtr.Zero) return _targetHwnd;
            string title = Dispatcher.Invoke(() => TargetTitleTxt.Text.Trim());
            return !string.IsNullOrEmpty(title) ? WindowHelper.FindWindowByTitle(title) : IntPtr.Zero;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  LOCATIONS TAB
        // ═════════════════════════════════════════════════════════════════════
        private void ClearLocations_Click(object s, RoutedEventArgs e)
        {
            if (MessageBox.Show("Remove all saved locations?", "Clear Locations",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            _locations.Clear();
            RefreshLocListBox();
        }

        private void LocRemove_Click(object s, RoutedEventArgs e)
        {
            int idx = LocListBox.SelectedIndex;
            if (idx < 0) return;
            _locations.RemoveAt(idx);
            RefreshLocListBox();
            LocListBox.SelectedIndex = Math.Min(idx, _locations.Count - 1);
        }

        private void LocMoveUp_Click(object s, RoutedEventArgs e)
        {
            int idx = LocListBox.SelectedIndex;
            if (idx <= 0) return;
            SwapLocations(idx, idx - 1);
            LocListBox.SelectedIndex = idx - 1;
        }

        private void LocMoveTop_Click(object s, RoutedEventArgs e)
        {
            int idx = LocListBox.SelectedIndex;
            if (idx <= 0) return;
            var item = _locations[idx];
            _locations.RemoveAt(idx);
            _locations.Insert(0, item);
            RefreshLocListBox();
            LocListBox.SelectedIndex = 0;
        }

        private void SwapLocations(int a, int b)
        {
            (_locations[a], _locations[b]) = (_locations[b], _locations[a]);
            RefreshLocListBox();
        }

        private void RefreshLocListBox()
        {
            LocListBox.Items.Clear();
            for (int i = 0; i < _locations.Count; i++)
                LocListBox.Items.Add($"[{i + 1}]  {_locations[i].X}, {_locations[i].Y}");
        }

        // ═════════════════════════════════════════════════════════════════════
        //  SEQUENCE TAB
        // ═════════════════════════════════════════════════════════════════════
        private void SeqRecord_Click(object s, RoutedEventArgs e)
        {
            var dlg = new RecordSequenceWindow { Owner = this };
            if (dlg.ShowDialog() == true && dlg.RecordedSequence?.Count > 0)
            {
                bool append = _sequence.Count > 0 &&
                    MessageBox.Show("Append to existing sequence?\n(No = replace)",
                        "Append or Replace?", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
                if (!append) _sequence.Clear();
                _sequence.AddRange(dlg.RecordedSequence);
                RefreshSeqListBox();
                MessageBox.Show($"Added {dlg.RecordedSequence.Count} recorded action(s).", "Done",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SeqAddClick_Click(object s, RoutedEventArgs e)
        {
            var dlg = new ClickOptionWindow { Owner = this };
            if (dlg.ShowDialog() != true) return;
            SequenceItem item;
            switch (dlg.SelectedOption)
            {
                case 1:
                    if (LocListBox.SelectedIndex < 0 || _locations.Count == 0)
                    { MessageBox.Show("Select a saved location first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                    var (lx, ly) = _locations[LocListBox.SelectedIndex];
                    item = new SequenceItem { Type = SeqType.Click, ClickX = lx, ClickY = ly };
                    break;
                case 2:
                    if (!NativeMethods.GetCursorPos(out POINT pt)) return;
                    item = new SequenceItem { Type = SeqType.Click, ClickX = pt.X, ClickY = pt.Y };
                    break;
                default:
                    item = new SequenceItem { Type = SeqType.Click, WindowCenter = true };
                    break;
            }
            _sequence.Add(item);
            RefreshSeqListBox();
        }

        private void SeqAddKey_Click(object s, RoutedEventArgs e)
        {
            var inp = new SimpleInputWindow("Add Key Step", "Enter combo (e.g. Ctrl+Shift+A or F5):") { Owner = this };
            if (inp.ShowDialog() != true || string.IsNullOrWhiteSpace(inp.Result)) return;
            var vkl = InputHelper.ParseComboToVkList(inp.Result.Trim());
            if (vkl == null)
            { MessageBox.Show($"Could not map '{inp.Result}'.", "Unsupported", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            _sequence.Add(new SequenceItem { Type = SeqType.Key, VkList = vkl, ComboLabel = inp.Result.Trim() });
            RefreshSeqListBox();
        }

        private void SeqAddWait_Click(object s, RoutedEventArgs e)
        {
            var inp = new SimpleInputWindow("Add Wait Step", "Wait how many milliseconds?", "100") { Owner = this };
            if (inp.ShowDialog() != true || !int.TryParse(inp.Result, out int ms) || ms < 0) return;
            _sequence.Add(new SequenceItem { Type = SeqType.Wait, WaitMs = ms });
            RefreshSeqListBox();
        }

        private void SeqImportLoc_Click(object s, RoutedEventArgs e)
        {
            if (_locations.Count == 0)
            { MessageBox.Show("No saved locations.", "Empty", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            foreach (var (x, y) in _locations)
                _sequence.Add(new SequenceItem { Type = SeqType.Click, ClickX = x, ClickY = y });
            RefreshSeqListBox();
            MessageBox.Show($"Imported {_locations.Count} location(s).", "Imported", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SeqClear_Click(object s, RoutedEventArgs e)    { _sequence.Clear(); RefreshSeqListBox(); }
        private void SeqRemove_Click(object s, RoutedEventArgs e)
        {
            int idx = SeqListBox.SelectedIndex;
            if (idx < 0) return;
            _sequence.RemoveAt(idx);
            RefreshSeqListBox();
            SeqListBox.SelectedIndex = Math.Min(idx, _sequence.Count - 1);
        }

        private void SeqMoveUp_Click(object s, RoutedEventArgs e)
        {
            int idx = SeqListBox.SelectedIndex;
            if (idx <= 0) return;
            (_sequence[idx], _sequence[idx - 1]) = (_sequence[idx - 1], _sequence[idx]);
            RefreshSeqListBox();
            SeqListBox.SelectedIndex = idx - 1;
        }

        private void SeqMoveDown_Click(object s, RoutedEventArgs e)
        {
            int idx = SeqListBox.SelectedIndex;
            if (idx < 0 || idx >= _sequence.Count - 1) return;
            (_sequence[idx], _sequence[idx + 1]) = (_sequence[idx + 1], _sequence[idx]);
            RefreshSeqListBox();
            SeqListBox.SelectedIndex = idx + 1;
        }

        // ── Sequence tab: Run / Stop / Save / Load / Export ───────────────────

        private void SeqRun_Click(object s, RoutedEventArgs e)
        {
            if (_seqRunning) return;
            if (_sequence.Count == 0)
            { MessageBox.Show("Sequence is empty.", "Nothing to run", MessageBoxButton.OK, MessageBoxImage.Information); return; }

            var seq   = new List<SequenceItem>(_sequence);
            bool dbl  = GetDoubleClick();
            int hldMs = GetHoldMs();
            _seqRunning = true;
            _seqCts     = new CancellationTokenSource();
            SeqStopBtn.IsEnabled = true;
            SetStatus("Running sequence…", "#F38BA8");

            var token = _seqCts.Token;
            new Thread(() =>
            {
                IntPtr hwnd = GetTargetHwnd();
                if (hwnd != IntPtr.Zero) WindowHelper.BringToForeground(hwnd);
                RunSequenceOnce(seq, hwnd, dbl, hldMs, token);
                _seqRunning = false;
                Dispatcher.Invoke(() =>
                {
                    SeqStopBtn.IsEnabled = false;
                    SetStatus("Done", "#A6E3A1");
                    // FEATURE 3 — restore window after sequence finishes
                    if (WindowState == WindowState.Minimized)
                        WindowState = WindowState.Normal;
                    Activate();
                });
            })
            { IsBackground = true }.Start();
        }

        private void SeqStopBtn_Click(object s, RoutedEventArgs e)
        {
            _seqCts?.Cancel();
            SeqStopBtn.IsEnabled = false;
            SetStatus("Stopped", "#A6E3A1");
        }

        // ── Seq test (kept as alias to SeqRun) ───────────────────────────────
        private void SeqTest_Click(object s, RoutedEventArgs e) => SeqRun_Click(s, e);

        // ── Save / Load / Export ──────────────────────────────────────────────
        private void SeqSave_Click(object s, RoutedEventArgs e)
        {
            if (_sequence.Count == 0)
            { MessageBox.Show("Sequence is empty.", "Nothing to save", MessageBoxButton.OK, MessageBoxImage.Information); return; }

            var dlg = new SaveFileDialog
            {
                Title  = "Save Sequence",
                Filter = "JSON sequence|*.json|All files|*.*",
                DefaultExt = "json",
                FileName = "sequence"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var data = _sequence.ConvertAll(i => new
                {
                    i.Type,
                    i.ClickX, i.ClickY,
                    i.WindowCenter,
                    i.ComboLabel,
                    i.WaitMs,
                    VkList = i.VkList
                });
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dlg.FileName, json);
                MessageBox.Show($"Saved {_sequence.Count} step(s) to:\n{dlg.FileName}", "Saved",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SeqLoad_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title  = "Load Sequence",
                Filter = "JSON sequence|*.json|All files|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                string json = File.ReadAllText(dlg.FileName);
                using var doc = JsonDocument.Parse(json);
                var loaded = new List<SequenceItem>();
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var item = new SequenceItem
                    {
                        Type        = Enum.Parse<SeqType>(el.GetProperty("Type").GetString()!),
                        ClickX      = el.TryGetProperty("ClickX", out var cx) && cx.ValueKind != JsonValueKind.Null ? cx.GetInt32() : null,
                        ClickY      = el.TryGetProperty("ClickY", out var cy) && cy.ValueKind != JsonValueKind.Null ? cy.GetInt32() : null,
                        WindowCenter= el.TryGetProperty("WindowCenter", out var wc) && wc.GetBoolean(),
                        ComboLabel  = el.TryGetProperty("ComboLabel", out var cl) && cl.ValueKind != JsonValueKind.Null ? cl.GetString() : null,
                        WaitMs      = el.TryGetProperty("WaitMs", out var wm) ? wm.GetInt32() : 0,
                    };
                    if (el.TryGetProperty("VkList", out var vkEl) && vkEl.ValueKind == JsonValueKind.Array)
                    {
                        item.VkList = new List<int>();
                        foreach (var v in vkEl.EnumerateArray()) item.VkList.Add(v.GetInt32());
                    }
                    loaded.Add(item);
                }

                bool append = _sequence.Count > 0 &&
                    MessageBox.Show("Append to existing sequence?\n(No = replace)",
                        "Append or Replace?", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
                if (!append) _sequence.Clear();
                _sequence.AddRange(loaded);
                RefreshSeqListBox();
                MessageBox.Show($"Loaded {loaded.Count} step(s).", "Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Load failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SeqExport_Click(object s, RoutedEventArgs e)
        {
            if (_sequence.Count == 0)
            { MessageBox.Show("Sequence is empty.", "Nothing to export", MessageBoxButton.OK, MessageBoxImage.Information); return; }

            var dlg = new SaveFileDialog
            {
                Title  = "Export Sequence as Text",
                Filter = "Text file|*.txt|All files|*.*",
                DefaultExt = "txt",
                FileName = "sequence_export"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var lines = new List<string> { "P.A.C.K Sequence Export", new string('─', 40), "" };
                for (int i = 0; i < _sequence.Count; i++)
                    lines.Add($"  {i + 1,3}.  {_sequence[i].Display}");
                lines.Add("");
                lines.Add($"Total steps: {_sequence.Count}");
                lines.Add($"Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                File.WriteAllLines(dlg.FileName, lines);
                MessageBox.Show($"Exported {_sequence.Count} step(s) to:\n{dlg.FileName}", "Exported",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshSeqListBox()
        {
            int sel = SeqListBox.SelectedIndex;
            SeqListBox.Items.Clear();
            foreach (var item in _sequence) SeqListBox.Items.Add($"  {item.Display}");
            if (sel >= 0 && sel < SeqListBox.Items.Count) SeqListBox.SelectedIndex = sel;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  RUNNING APPS TAB
        // ═════════════════════════════════════════════════════════════════════
        private void AppsRefresh_Click(object s, RoutedEventArgs e) => RefreshAppsInternal();

        private void RefreshAppsInternal()
        {
            try
            {
                _appItems = WindowHelper.EnumerateVisibleWindows();
                AppsListBox.Items.Clear();
                foreach (var w in _appItems) AppsListBox.Items.Add(w.Display);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to enumerate windows:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AppsUse_Click(object s, RoutedEventArgs e)       => UseSelectedApp();
        private void AppsListBox_DoubleClick(object s, MouseButtonEventArgs e) => UseSelectedApp();

        private void UseSelectedApp()
        {
            int idx = AppsListBox.SelectedIndex;
            if (idx < 0) return;
            var w = _appItems[idx];
            _targetHwnd = w.Hwnd;
            TargetTitleTxt.Text = w.Title;
            MessageBox.Show($"Target: {w.Title}\n{w.ExePath ?? "(unknown)"}", "Target Selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AppsShowExe_Click(object s, RoutedEventArgs e)
        {
            int idx = AppsListBox.SelectedIndex;
            if (idx < 0) return;
            MessageBox.Show(_appItems[idx].ExePath ?? "Unknown", "Executable Path",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AppsSelectExe_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Title = "Select executable", Filter = "EXE files|*.exe|All files|*.*" };
            if (dlg.ShowDialog() != true) return;
            var w = WindowHelper.FindWindowByExePath(dlg.FileName);
            if (w != null)
            {
                _targetHwnd = w.Hwnd;
                TargetTitleTxt.Text = w.Title;
                MessageBox.Show($"Found: {w.Title}\n{w.ExePath}", "Window Found", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshAppsInternal();
            }
            else
                MessageBox.Show("No visible window found for that exe.", "Not Found",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  SETTINGS TAB
        // ═════════════════════════════════════════════════════════════════════
        private void SetHotkey_Click(object s, RoutedEventArgs e)
        {
            var dlg = new RecordComboWindow { Owner = this };
            if (dlg.ShowDialog() == true && !string.IsNullOrEmpty(dlg.RecordedCombo))
            {
                var vkl = InputHelper.ParseComboToVkList(dlg.RecordedCombo);
                if (vkl == null)
                {
                    MessageBox.Show($"Could not map '{dlg.RecordedCombo}'.", "Unsupported",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _hotkeyVkList  = vkl;
                _hotkeyDisplay = dlg.RecordedCombo;
                HotkeyLabel.Text = _hotkeyDisplay;
                UpdateRecordHint();
            }
        }

        private void ClearHotkey_Click(object s, RoutedEventArgs e)
        {
            _hotkeyVkList  = null;
            _hotkeyDisplay = "(none set)";
            HotkeyLabel.Text = _hotkeyDisplay;
            UpdateRecordHint();
        }

        private void StopBgRecord_Click(object s, RoutedEventArgs e) => StopBgRecording();

        // ═════════════════════════════════════════════════════════════════════
        //  GLOBAL HOTKEY HOOK
        // ═════════════════════════════════════════════════════════════════════
        private void InstallGlobalHook()
        {
            using var proc = Process.GetCurrentProcess();
            using var mod  = proc.MainModule!;
            IntPtr hMod = NativeMethods.GetModuleHandle(mod.ModuleName);
            _globalRef  = GlobalHookProc;
            _globalHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _globalRef, hMod, 0);
        }

        private void RemoveGlobalHook()
        {
            if (_globalHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_globalHook);
                _globalHook = IntPtr.Zero;
            }
        }

        private IntPtr GlobalHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _hotkeyVkList != null &&
                (int)wParam == NativeMethods.WM_KEYDOWN)
            {
                var info   = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                uint mainVk = (uint)_hotkeyVkList[^1];

                if (info.vkCode == mainVk && HotkeyModifiersMatch())
                    Dispatcher.BeginInvoke(ToggleRecording);
            }
            return NativeMethods.CallNextHookEx(_globalHook, nCode, wParam, lParam);
        }

        private bool HotkeyModifiersMatch()
        {
            if (_hotkeyVkList == null) return false;
            bool needCtrl  = _hotkeyVkList.Contains(0x11);
            bool needShift = _hotkeyVkList.Contains(0x10);
            bool needAlt   = _hotkeyVkList.Contains(0x12);
            return IsVkDown(0x11) == needCtrl &&
                   IsVkDown(0x10) == needShift &&
                   IsVkDown(0x12) == needAlt;
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        private static bool IsVkDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

        /// <summary>
        /// Called when the hotkey fires. Routes to whichever recording mode is active,
        /// or starts background sequence recording and minimizes the window.
        /// </summary>
        private void ToggleRecording()
        {
            if (_mainTabRecording)
            {
                StopMainTabRecording();
            }
            else if (_bgRecording)
            {
                StopBgRecording();
            }
            else
            {
                // FEATURE 2 — minimize when hotkey starts bg sequence recording
                WindowState = WindowState.Minimized;
                StartBgRecording();
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  BACKGROUND SEQUENCE RECORDING  (hotkey → Sequence tab)
        // ═════════════════════════════════════════════════════════════════════
        private void StartBgRecording()
        {
            _bgItems.Clear();
            _bgMods.Clear();
            _bgLastTime      = DateTime.UtcNow;
            _bgMoveRunActive = false;
            _bgMoveRunCount  = 0;
            _bgMoveLastTick  = 0;
            _bgRecording     = true;

            using var proc = Process.GetCurrentProcess();
            using var mod  = proc.MainModule!;
            IntPtr hMod = NativeMethods.GetModuleHandle(mod.ModuleName);
            _bgMouseRef  = BgMouseHookProc;
            _bgKbdRef    = BgKbdHookProc;
            _bgMouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _bgMouseRef, hMod, 0);
            _bgKbdHook   = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _bgKbdRef, hMod, 0);

            SetStatus($"⏺  Recording sequence… (press {_hotkeyDisplay} to stop)", "#F38BA8");
            BgRecordCard.Visibility     = Visibility.Visible;
            BgRecordCountLabel.Text     = "0 actions captured";
        }

        private void StopBgRecording()
        {
            if (!_bgRecording) return;
            _bgRecording = false;
            RemoveBgHooks();

            BgRecordCard.Visibility = Visibility.Collapsed;
            SetStatus("Ready", "#A6E3A1");

            // FEATURE 2/3 — restore window when hotkey stops recording
            WindowState = WindowState.Normal;
            Activate();

            if (_bgItems.Count == 0)
            {
                MessageBox.Show("Nothing was recorded.", "Empty Recording",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var answer = MessageBox.Show(
                $"Recorded {_bgItems.Count} action(s).\n\nYES = append, NO = replace existing sequence.",
                "Recording Finished",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (answer == MessageBoxResult.Cancel) return;
            if (answer == MessageBoxResult.No)     _sequence.Clear();
            _sequence.AddRange(_bgItems);
            RefreshSeqListBox();

            Nav_Click(NavSeq, new RoutedEventArgs());
        }

        private void RemoveBgHooks()
        {
            if (_bgMouseHook != IntPtr.Zero) { NativeMethods.UnhookWindowsHookEx(_bgMouseHook); _bgMouseHook = IntPtr.Zero; }
            if (_bgKbdHook   != IntPtr.Zero) { NativeMethods.UnhookWindowsHookEx(_bgKbdHook);   _bgKbdHook   = IntPtr.Zero; }
        }

        // ── Bg mouse hook ─────────────────────────────────────────────────────
        private const int BgWM_LDown = 0x0201;
        private const int BgWM_RDown = 0x0204;

        private IntPtr BgMouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _bgRecording)
            {
                int msg = (int)wParam;
                if (msg == BgWM_LDown || msg == BgWM_RDown)
                {
                    var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    bool left = msg == BgWM_LDown;
                    Dispatcher.BeginInvoke(() => BgAddClick(info.pt.X, info.pt.Y, left));
                }
                else if (msg == NativeMethods.WM_MOUSEMOVE)
                {
                    var  info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    long now  = Environment.TickCount64;
                    int  dx   = info.pt.X - _bgMoveLastX;
                    int  dy   = info.pt.Y - _bgMoveLastY;
                    long dt   = now - _bgMoveLastTick;

                    if (dt >= MoveIntervalMs && dx * dx + dy * dy >= MoveMinDist2)
                    {
                        int deltaMs     = (int)Math.Min(dt, 5000);
                        _bgMoveLastX    = info.pt.X;
                        _bgMoveLastY    = info.pt.Y;
                        _bgMoveLastTick = now;
                        int cx = info.pt.X, cy = info.pt.Y;
                        Dispatcher.BeginInvoke(() => BgAddMove(cx, cy, deltaMs));
                    }
                }
            }
            return NativeMethods.CallNextHookEx(_bgMouseHook, nCode, wParam, lParam);
        }

        private void BgAddMove(int x, int y, int deltaMs)
        {
            _bgItems.Add(new SequenceItem { Type = SeqType.Move, ClickX = x, ClickY = y, WaitMs = deltaMs });
            _bgLastTime = DateTime.UtcNow;
            if (!_bgMoveRunActive) { _bgMoveRunActive = true; _bgMoveRunCount = 1; }
            else _bgMoveRunCount++;
            BgUpdateCount();
        }

        private void BgAddClick(int x, int y, bool left)
        {
            _bgMoveRunActive = false;
            BgMaybeAddWait();
            _bgItems.Add(new SequenceItem { Type = SeqType.Click, ClickX = x, ClickY = y, ComboLabel = left ? "left" : "right" });
            BgUpdateCount();
        }

        // ── Bg keyboard hook ──────────────────────────────────────────────────
        private IntPtr BgKbdHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _bgRecording)
            {
                int msg  = (int)wParam;
                var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
                    Dispatcher.BeginInvoke(() => BgOnKeyDown(info.vkCode));
                else if (msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP)
                    Dispatcher.BeginInvoke(() => _bgMods.Remove(NormMod(info.vkCode)));
            }
            return NativeMethods.CallNextHookEx(_bgKbdHook, nCode, wParam, lParam);
        }

        private void BgOnKeyDown(uint vk)
        {
            if (ModVKs.Contains(vk)) { _bgMods.Add(NormMod(vk)); return; }
            if (_hotkeyVkList != null && (int)vk == _hotkeyVkList[^1] && HotkeyModifiersMatch()) return;
            _bgMoveRunActive = false;
            BgMaybeAddWait();
            var vkList = new List<int>();
            foreach (uint m in _bgMods) vkList.Add((int)m);
            vkList.Add((int)vk);
            string label = BgBuildLabel(_bgMods, vk);
            _bgItems.Add(new SequenceItem { Type = SeqType.Key, VkList = vkList, ComboLabel = label });
            BgUpdateCount();
        }

        private void BgMaybeAddWait()
        {
            if (_bgItems.Count == 0) { _bgLastTime = DateTime.UtcNow; return; }
            int gap = (int)(DateTime.UtcNow - _bgLastTime).TotalMilliseconds;
            if (gap >= BgWaitThreshMs && gap < 30_000)
                _bgItems.Add(new SequenceItem { Type = SeqType.Wait, WaitMs = gap });
            _bgLastTime = DateTime.UtcNow;
        }

        private void BgUpdateCount()
        {
            if (BgRecordCountLabel != null)
                BgRecordCountLabel.Text = $"{_bgItems.Count} action{(_bgItems.Count == 1 ? "" : "s")} captured";
        }

        private static string BgBuildLabel(HashSet<uint> mods, uint vk)
        {
            var parts = new List<string>();
            if (mods.Contains(0x11)) parts.Add("Ctrl");
            if (mods.Contains(0x10)) parts.Add("Shift");
            if (mods.Contains(0x12)) parts.Add("Alt");
            parts.Add(BgVkToLabel(vk));
            return string.Join("+", parts);
        }

        private static string BgVkToLabel(uint vk) => vk switch
        {
            0x08 => "Backspace", 0x09 => "Tab",    0x0D => "Enter",
            0x1B => "Escape",    0x20 => "Space",
            0x21 => "PageUp",    0x22 => "PageDown",
            0x23 => "End",       0x24 => "Home",
            0x25 => "Left",      0x26 => "Up",      0x27 => "Right", 0x28 => "Down",
            0x2D => "Insert",    0x2E => "Delete",
            >= 0x30 and <= 0x39 => ((char)vk).ToString(),
            >= 0x41 and <= 0x5A => ((char)vk).ToString(),
            >= 0x60 and <= 0x69 => $"Num{vk - 0x60}",
            >= 0x70 and <= 0x87 => $"F{vk - 0x6F}",
            _ => $"VK{vk:X2}"
        };

        // ═════════════════════════════════════════════════════════════════════
        //  ABOUT TAB
        // ═════════════════════════════════════════════════════════════════════
        private void ContactEmail_Click(object s, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("mailto:ashmandeadwarf@gmail.com") { UseShellExecute = true }); }
            catch { MessageBox.Show("Email: ashmandeadwarf@gmail.com", "Contact", MessageBoxButton.OK, MessageBoxImage.Information); }
        }
    }
}
