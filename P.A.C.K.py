import ctypes
import ctypes.wintypes
import threading
import time
import random
import json
import os
import webbrowser
import tkinter as tk
from tkinter import ttk, messagebox, simpledialog, filedialog

# -------------------- Win32 / SendInput helpers --------------------
user32 = ctypes.windll.user32
kernel32 = ctypes.windll.kernel32

ULONG_PTR = ctypes.c_ulonglong if ctypes.sizeof(ctypes.c_void_p) == 8 else ctypes.c_ulong

class MOUSEINPUT(ctypes.Structure):
    _fields_ = [("dx", ctypes.c_long), ("dy", ctypes.c_long), ("mouseData", ctypes.c_ulong),
                ("dwFlags", ctypes.c_ulong), ("time", ctypes.c_ulong), ("dwExtraInfo", ULONG_PTR)]

class KEYBDINPUT(ctypes.Structure):
    _fields_ = [("wVk", ctypes.c_ushort), ("wScan", ctypes.c_ushort),
                ("dwFlags", ctypes.c_ulong), ("time", ctypes.c_ulong), ("dwExtraInfo", ULONG_PTR)]

class HARDWAREINPUT(ctypes.Structure):
    _fields_ = [("uMsg", ctypes.c_ulong), ("wParamL", ctypes.c_short), ("wParamH", ctypes.c_ushort)]

class _INPUTunion(ctypes.Union):
    _fields_ = [("mi", MOUSEINPUT), ("ki", KEYBDINPUT), ("hi", HARDWAREINPUT)]

class INPUT(ctypes.Structure):
    _anonymous_ = ("union",)
    _fields_ = [("type", ctypes.c_ulong), ("union", _INPUTunion)]

INPUT_MOUSE = 0
INPUT_KEYBOARD = 1

MOUSEEVENTF_LEFTDOWN  = 0x0002
MOUSEEVENTF_LEFTUP    = 0x0004
MOUSEEVENTF_RIGHTDOWN = 0x0008
MOUSEEVENTF_RIGHTUP   = 0x0010
KEYEVENTF_KEYUP       = 0x0002
KEYEVENTF_SCANCODE    = 0x0008
MAPVK_VK_TO_VSC       = 0

SendInput = user32.SendInput
SendInput.argtypes = (ctypes.c_uint, ctypes.POINTER(INPUT), ctypes.c_int)
SendInput.restype  = ctypes.c_uint

MapVirtualKey = user32.MapVirtualKeyW
MapVirtualKey.argtypes = (ctypes.c_uint, ctypes.c_uint)
MapVirtualKey.restype = ctypes.c_uint

VkKeyScan = user32.VkKeyScanW
VkKeyScan.argtypes = (ctypes.c_wchar,)
VkKeyScan.restype = ctypes.c_short

SetCursorPos = user32.SetCursorPos
SetCursorPos.argtypes = (ctypes.c_int, ctypes.c_int)
SetCursorPos.restype = ctypes.c_bool

GetCursorPos = user32.GetCursorPos
GetCursorPos.argtypes = (ctypes.POINTER(ctypes.wintypes.POINT),)
GetCursorPos.restype = ctypes.c_bool

GetClientRect = user32.GetClientRect
GetClientRect.argtypes = (ctypes.c_void_p, ctypes.POINTER(ctypes.wintypes.RECT))
GetClientRect.restype = ctypes.c_bool

ClientToScreen = user32.ClientToScreen
ClientToScreen.argtypes = (ctypes.c_void_p, ctypes.POINTER(ctypes.wintypes.POINT))
ClientToScreen.restype = ctypes.c_bool

EnumWindows = user32.EnumWindows
EnumWindows.argtypes = (ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.c_void_p, ctypes.c_void_p), ctypes.c_void_p)
EnumWindows.restype = ctypes.c_bool

GetWindowTextLength = user32.GetWindowTextLengthW
GetWindowText = user32.GetWindowTextW
IsWindowVisible = user32.IsWindowVisible
GetWindowThreadProcessId = user32.GetWindowThreadProcessId
AttachThreadInput = user32.AttachThreadInput
GetForegroundWindow = user32.GetForegroundWindow
SetForegroundWindow = user32.SetForegroundWindow
BringWindowToTop = user32.BringWindowToTop
SetActiveWindow = user32.SetActiveWindow
GetCurrentThreadId = kernel32.GetCurrentThreadId
PostMessage = user32.PostMessageW

WM_LBUTTONDOWN = 0x0201
WM_LBUTTONUP   = 0x0202
WM_RBUTTONDOWN = 0x0204
WM_RBUTTONUP   = 0x0205
MK_LBUTTON     = 0x0001

# For QueryFullProcessImageName
OpenProcess = kernel32.OpenProcess
OpenProcess.argtypes = (ctypes.c_ulong, ctypes.c_bool, ctypes.c_ulong)
OpenProcess.restype = ctypes.c_void_p
CloseHandle = kernel32.CloseHandle
QueryFullProcessImageName = kernel32.QueryFullProcessImageNameW
QueryFullProcessImageName.argtypes = (ctypes.c_void_p, ctypes.c_ulong, ctypes.c_wchar_p, ctypes.POINTER(ctypes.c_ulong))
QueryFullProcessImageName.restype = ctypes.c_bool

PROCESS_QUERY_LIMITED_INFORMATION = 0x1000

# -------------------- Low-level input functions --------------------
def send_mouse_click_sendinput(left=True):
    if left:
        down = INPUT(type=INPUT_MOUSE, mi=MOUSEINPUT(0,0,0,MOUSEEVENTF_LEFTDOWN,0,0))
        up   = INPUT(type=INPUT_MOUSE, mi=MOUSEINPUT(0,0,0,MOUSEEVENTF_LEFTUP,  0,0))
    else:
        down = INPUT(type=INPUT_MOUSE, mi=MOUSEINPUT(0,0,0,MOUSEEVENTF_RIGHTDOWN,0,0))
        up   = INPUT(type=INPUT_MOUSE, mi=MOUSEINPUT(0,0,0,MOUSEEVENTF_RIGHTUP,  0,0))
    arr = (INPUT * 2)(down, up)
    SendInput(2, arr, ctypes.sizeof(INPUT))

def send_mouse_down(left=True):
    if left:
        inp = INPUT(type=INPUT_MOUSE, mi=MOUSEINPUT(0,0,0,MOUSEEVENTF_LEFTDOWN,0,0))
    else:
        inp = INPUT(type=INPUT_MOUSE, mi=MOUSEINPUT(0,0,0,MOUSEEVENTF_RIGHTDOWN,0,0))
    SendInput(1, ctypes.byref(inp), ctypes.sizeof(INPUT))

def send_mouse_up(left=True):
    if left:
        inp = INPUT(type=INPUT_MOUSE, mi=MOUSEINPUT(0,0,0,MOUSEEVENTF_LEFTUP,0,0))
    else:
        inp = INPUT(type=INPUT_MOUSE, mi=MOUSEINPUT(0,0,0,MOUSEEVENTF_RIGHTUP,0,0))
    SendInput(1, ctypes.byref(inp), ctypes.sizeof(INPUT))

def send_key_sequence_vk_combo(vk_list):
    for vk in vk_list:
        sc = MapVirtualKey(vk, MAPVK_VK_TO_VSC)
        ki = KEYBDINPUT(wVk=0, wScan=sc, dwFlags=KEYEVENTF_SCANCODE, time=0, dwExtraInfo=0)
        inp = INPUT(type=INPUT_KEYBOARD, ki=ki)
        SendInput(1, ctypes.byref(inp), ctypes.sizeof(INPUT))
        time.sleep(0.001)
    for vk in reversed(vk_list):
        sc = MapVirtualKey(vk, MAPVK_VK_TO_VSC)
        ki = KEYBDINPUT(wVk=0, wScan=sc, dwFlags=KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP, time=0, dwExtraInfo=0)
        inp = INPUT(type=INPUT_KEYBOARD, ki=ki)
        SendInput(1, ctypes.byref(inp), ctypes.sizeof(INPUT))
        time.sleep(0.001)

def send_key_press_scancode_vk(vk_code):
    sc = MapVirtualKey(vk_code, MAPVK_VK_TO_VSC)
    ki_down = KEYBDINPUT(wVk=0, wScan=sc, dwFlags=KEYEVENTF_SCANCODE, time=0, dwExtraInfo=0)
    ki_up   = KEYBDINPUT(wVk=0, wScan=sc, dwFlags=KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP, time=0, dwExtraInfo=0)
    inp_down = INPUT(type=INPUT_KEYBOARD, ki=ki_down)
    inp_up   = INPUT(type=INPUT_KEYBOARD, ki=ki_up)
    arr = (INPUT * 2)(inp_down, inp_up)
    SendInput(2, arr, ctypes.sizeof(INPUT))

def post_mouse_click_to_window(hwnd, left=True):
    if not hwnd:
        return False
    rect = ctypes.wintypes.RECT()
    if not GetClientRect(hwnd, ctypes.byref(rect)):
        return False
    cx = (rect.right - rect.left) // 2
    cy = (rect.bottom - rect.top) // 2
    lparam = (cy << 16) | (cx & 0xffff)
    if left:
        PostMessage(hwnd, WM_LBUTTONDOWN, MK_LBUTTON, lparam)
        PostMessage(hwnd, WM_LBUTTONUP, 0, lparam)
    else:
        PostMessage(hwnd, WM_RBUTTONDOWN, 0, lparam)
        PostMessage(hwnd, WM_RBUTTONUP, 0, lparam)
    return True

def move_cursor_to_window_center(hwnd):
    if not hwnd:
        return False
    rect = ctypes.wintypes.RECT()
    if not GetClientRect(hwnd, ctypes.byref(rect)):
        return False
    cx = (rect.right - rect.left) // 2
    cy = (rect.bottom - rect.top) // 2
    pt = ctypes.wintypes.POINT(cx, cy)
    if not ClientToScreen(hwnd, ctypes.byref(pt)):
        return False
    SetCursorPos(pt.x, pt.y)
    return True

# -------------------- Window / process helpers --------------------
def find_window_by_title(substring):
    found = {"hwnd": None}
    Sub = substring.lower()

    @ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.c_void_p, ctypes.c_void_p)
    def _cb(hwnd, lParam):
        if IsWindowVisible(hwnd):
            length = GetWindowTextLength(hwnd)
            if length > 0:
                buf = ctypes.create_unicode_buffer(length + 1)
                GetWindowText(hwnd, buf, length + 1)
                title = buf.value
                if Sub in title.lower():
                    found["hwnd"] = hwnd
                    return False
        return True

    EnumWindows(_cb, 0)
    return found["hwnd"]

def get_process_image_path(pid):
    # Return full path to process image or None
    hproc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, False, pid)
    if not hproc:
        return None
    try:
        size = ctypes.c_ulong(260)
        buf = ctypes.create_unicode_buffer(260)
        res = QueryFullProcessImageName(hproc, 0, buf, ctypes.byref(size))
        if res:
            return buf.value
        return None
    finally:
        CloseHandle(hproc)

def enumerate_visible_windows_with_paths():
    results = []  # list of (hwnd, title, exe_path_or_None)
    @ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.c_void_p, ctypes.c_void_p)
    def _cb(hwnd, lParam):
        if IsWindowVisible(hwnd):
            length = GetWindowTextLength(hwnd)
            if length > 0:
                buf = ctypes.create_unicode_buffer(length + 1)
                GetWindowText(hwnd, buf, length + 1)
                title = buf.value
                # get pid
                pid = ctypes.c_ulong()
                GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
                exe = get_process_image_path(pid.value)
                results.append((hwnd, title, exe))
        return True
    EnumWindows(_cb, 0)
    return results

def find_window_by_exe_path(target_path):
    # Normalize for comparison
    if not target_path:
        return None
    target_path = os.path.normcase(os.path.abspath(target_path))
    for hwnd, title, exe in enumerate_visible_windows_with_paths():
        if exe:
            try:
                if os.path.normcase(os.path.abspath(exe)) == target_path:
                    return hwnd, title, exe
            except Exception:
                continue
    return None

def bring_window_to_foreground(hwnd):
    try:
        SW_RESTORE = 9
        user32.ShowWindow(hwnd, SW_RESTORE)
    except Exception:
        pass
    try:
        fg = GetForegroundWindow()
        if not fg:
            fg = 0
        current_thread = GetCurrentThreadId()
        target_thread = ctypes.c_ulong()
        GetWindowThreadProcessId(hwnd, ctypes.byref(target_thread))
        fg_thread = ctypes.c_ulong()
        GetWindowThreadProcessId(fg, ctypes.byref(fg_thread))
        fg_tid = fg_thread.value
        target_tid = target_thread.value
        if fg_tid and fg_tid != current_thread:
            AttachThreadInput(current_thread, fg_tid, True)
        if target_tid and target_tid != current_thread:
            AttachThreadInput(current_thread, target_tid, True)
        SetForegroundWindow(hwnd)
        BringWindowToTop(hwnd)
        SetActiveWindow(hwnd)
        if target_tid and target_tid != current_thread:
            AttachThreadInput(current_thread, target_tid, False)
        if fg_tid and fg_tid != current_thread:
            AttachThreadInput(current_thread, fg_tid, False)
        return True
    except Exception:
        return False

# -------------------- Simple VK mapping --------------------
VK_COMMON = {
    'shift': 0x10, 'ctrl': 0x11, 'alt': 0x12, 'space':0x20, 'enter':0x0D, 'escape':0x1B,
    'left':0x25,'up':0x26,'right':0x27,'down':0x28
}
for i in range(1,13): VK_COMMON[f"f{i}"] = 0x70 + (i-1)

# -------------------- UI & main app --------------------
class PACKApp:
    def __init__(self, root):
        self.root = root
        root.title("P.A.C.K")
        root.geometry("880x600")
        self.running = False
        self.stop_event = threading.Event()
        self.worker_thread = None

        # State
        self.action_var = tk.StringVar(value="Left Click")
        self.cps_var = tk.StringVar(value="10")
        self.randomize_var = tk.BooleanVar(value=False)
        self.rand_min_ms = tk.StringVar(value="0")
        self.rand_max_ms = tk.StringVar(value="10")
        self.target_title_var = tk.StringVar(value="")
        self.target_hwnd = None  # directly store selected HWND when user picks from running apps or exe
        self.locations = []
        self.use_follow_cursor = tk.BooleanVar(value=False)
        self.mode_repeat_forever = tk.BooleanVar(value=True)
        self.repeat_count_var = tk.StringVar(value="0")
        self.timer_seconds_var = tk.StringVar(value="0")
        self.keycombo_str = tk.StringVar(value="")
        self.max_cps_limit = 1000
        self.sequence = []
        self.double_click_var = tk.BooleanVar(value=False)
        self.hold_duration_ms = tk.StringVar(value="0")
        self.start_delay_sec = tk.StringVar(value="0")

        self._build_ui()

    def _build_ui(self):
        nb = ttk.Notebook(self.root)
        nb.pack(fill="both", expand=True, padx=8, pady=8)

        # --- Main tab ---
        main = ttk.Frame(nb, padding=10)
        nb.add(main, text="Main")

        left = ttk.Frame(main)
        left.pack(side="left", fill="both", expand=True)
        right = ttk.Frame(main)
        right.pack(side="right", fill="y", padx=(12,0))

        ttk.Label(left, text="Action:").grid(row=0,column=0, sticky="w")
        act_combo = ttk.Combobox(left, values=["Left Click","Right Click","Keyboard Combo"], textvariable=self.action_var, state="readonly", width=30)
        act_combo.grid(row=0,column=1, sticky="w")

        ttk.Label(left, text="Key Combo:").grid(row=1, column=0, sticky="w", pady=(8,0))
        key_frame = ttk.Frame(left)
        key_frame.grid(row=1, column=1, sticky="w", pady=(8,0))
        ttk.Entry(key_frame, textvariable=self.keycombo_str, width=28).grid(row=0, column=0, sticky="w")
        ttk.Button(key_frame, text="Record combo", command=self._record_combo_popup).grid(row=0, column=1, padx=(6,0))

        ttk.Label(left, text="CPS (clicks/sec):").grid(row=2, column=0, sticky="w", pady=(8,0))
        ttk.Entry(left, textvariable=self.cps_var, width=12).grid(row=2, column=1, sticky="w", pady=(8,0))

        ttk.Checkbutton(left, text="Randomize interval (ms)", variable=self.randomize_var).grid(row=3, column=0, columnspan=2, sticky="w", pady=(8,0))
        rand_frame = ttk.Frame(left)
        rand_frame.grid(row=4, column=0, columnspan=2, sticky="w")
        ttk.Label(rand_frame, text="Min ms:").grid(row=0,column=0)
        ttk.Entry(rand_frame, textvariable=self.rand_min_ms, width=8).grid(row=0,column=1, padx=(2,8))
        ttk.Label(rand_frame, text="Max ms:").grid(row=0,column=2)
        ttk.Entry(rand_frame, textvariable=self.rand_max_ms, width=8).grid(row=0,column=3, padx=(2,8))

        ttk.Checkbutton(left, text="Double-click each action", variable=self.double_click_var).grid(row=5, column=0, columnspan=2, sticky="w", pady=(8,0))
        hold_frame = ttk.Frame(left)
        hold_frame.grid(row=6, column=0, columnspan=2, sticky="w")
        ttk.Label(hold_frame, text="If click-and-hold use ms:").grid(row=0,column=0, sticky="w")
        ttk.Entry(hold_frame, textvariable=self.hold_duration_ms, width=8).grid(row=0,column=1, padx=(6,0))

        ttk.Label(left, text="Start delay (sec):").grid(row=7, column=0, sticky="w", pady=(8,0))
        ttk.Entry(left, textvariable=self.start_delay_sec, width=8).grid(row=7, column=1, sticky="w", pady=(8,0))

        ttk.Label(left, text="Target window (selected below):").grid(row=8, column=0, sticky="w", pady=(8,0))
        ttk.Entry(left, textvariable=self.target_title_var, width=40).grid(row=8, column=1, sticky="w", pady=(8,0))

        # Run controls
        run_frame = ttk.Frame(left)
        run_frame.grid(row=11, column=0, columnspan=2, pady=(14,0))
        self.run_btn = ttk.Button(run_frame, text="Run", command=self.run)
        self.run_btn.grid(row=0,column=0, padx=(0,6))
        self.stop_btn = ttk.Button(run_frame, text="Stop", command=self.stop, state="disabled")
        self.stop_btn.grid(row=0,column=1)
        ttk.Button(run_frame, text="Test click once", command=self._test_once).grid(row=0,column=2, padx=(6,0))

        # Right column: Repeat/timer & info
        ttk.Label(right, text="Mode:").grid(row=0,column=0, sticky="w")
        ttk.Checkbutton(right, text="Repeat forever", variable=self.mode_repeat_forever).grid(row=1,column=0, sticky="w")
        ttk.Label(right, text="Repeat count (if not forever):").grid(row=2,column=0, sticky="w")
        ttk.Entry(right, textvariable=self.repeat_count_var, width=12).grid(row=3,column=0, sticky="w")
        ttk.Label(right, text="Timer (seconds, optional):").grid(row=4,column=0, sticky="w", pady=(8,0))
        ttk.Entry(right, textvariable=self.timer_seconds_var, width=12).grid(row=5,column=0, sticky="w")

        # Status label
        self.status_label = ttk.Label(self.root, text="Ready", foreground="green")
        self.status_label.pack(side="bottom", fill="x", padx=8, pady=(0,8))

        # --- Locations tab ---
        loc_tab = ttk.Frame(nb, padding=8)
        nb.add(loc_tab, text="Locations")
        left_l = ttk.Frame(loc_tab)
        left_l.pack(side="left", fill="both", expand=True)
        ttk.Button(left_l, text="Capture current cursor position", command=self._capture_current_cursor).pack(anchor="w", pady=(0,8))
        ttk.Button(left_l, text="Clear locations", command=self._clear_locations).pack(anchor="w")
        ttk.Label(left_l, text="Saved positions:").pack(anchor="w", pady=(10,0))
        self.loc_listbox = tk.Listbox(left_l, height=14)
        self.loc_listbox.pack(fill="both", expand=True, pady=(2,0))
        loc_btns = ttk.Frame(left_l)
        loc_btns.pack(anchor="w", pady=(6,0))
        ttk.Button(loc_btns, text="Remove selected", command=self._remove_selected_location).grid(row=0,column=0)
        ttk.Button(loc_btns, text="Move selected to top", command=lambda: self._move_location(-1)).grid(row=0,column=1, padx=(6,0))
        right_l = ttk.Frame(loc_tab)
        right_l.pack(side="right", fill="y", padx=(8,0))
        ttk.Label(right_l, text="Follow cursor (don't use saved positions):").pack(anchor="w")
        ttk.Checkbutton(right_l, variable=self.use_follow_cursor).pack(anchor="w")

        # --- Script tab ---
        script_tab = ttk.Frame(nb, padding=8)
        nb.add(script_tab, text="Script / Sequence")
        seq_frame = ttk.Frame(script_tab)
        seq_frame.pack(fill="both", expand=True)
        btns = ttk.Frame(seq_frame)
        btns.pack(anchor="w", pady=(0,6))
        ttk.Button(btns, text="Add Click", command=self._seq_add_click).grid(row=0,column=0)
        ttk.Button(btns, text="Add Key", command=self._seq_add_key).grid(row=0,column=1, padx=(6,0))
        ttk.Button(btns, text="Add Wait", command=self._seq_add_wait).grid(row=0,column=2, padx=(6,0))
        ttk.Button(btns, text="Clear Sequence", command=lambda: self._set_sequence([])).grid(row=0,column=3, padx=(6,0))
        ttk.Button(btns, text="Test Play Sequence (once)", command=lambda: self._play_sequence_once()).grid(row=0,column=4, padx=(6,0))
        ttk.Label(seq_frame, text="Sequence (ordered):").pack(anchor="w")
        self.seq_listbox = tk.Listbox(seq_frame, height=12)
        self.seq_listbox.pack(fill="both", expand=True)
        seq_ops = ttk.Frame(seq_frame)
        seq_ops.pack(anchor="w", pady=(6,0))
        ttk.Button(seq_ops, text="Remove selected", command=self._seq_remove_selected).grid(row=0,column=0)
        ttk.Button(seq_ops, text="Move up", command=lambda: self._seq_move(-1)).grid(row=0,column=1, padx=(6,0))
        ttk.Button(seq_ops, text="Move down", command=lambda: self._seq_move(1)).grid(row=0,column=2, padx=(6,0))

        # --- Running Apps tab (new easy chooser) ---
        apps_tab = ttk.Frame(nb, padding=8)
        nb.add(apps_tab, text="Running Apps (choose target)")

        top_row = ttk.Frame(apps_tab)
        top_row.pack(fill="x")
        ttk.Button(top_row, text="Refresh running apps", command=self._refresh_running_apps).pack(side="left")
        ttk.Button(top_row, text="Select by executable...", command=self._select_by_exe_dialog).pack(side="left", padx=(6,0))
        ttk.Label(top_row, text="(Double-click entry to use it)").pack(side="left", padx=(12,0))

        self.apps_listbox = tk.Listbox(apps_tab, height=18)
        self.apps_listbox.pack(fill="both", expand=True, pady=(8,0))
        apps_btns = ttk.Frame(apps_tab)
        apps_btns.pack(fill="x", pady=(6,0))
        ttk.Button(apps_btns, text="Use selected", command=self._use_selected_running_app).pack(side="left")
        ttk.Button(apps_btns, text="Show exe path", command=self._show_selected_app_path).pack(side="left", padx=(6,0))

        # bind double-click to select
        self.apps_listbox.bind("<Double-Button-1>", lambda e: self._use_selected_running_app())

        # --- About tab ---
        about_tab = ttk.Frame(nb, padding=12)
        nb.add(about_tab, text="About P.A.C.K")
        ttk.Label(about_tab, text="P.A.C.K - Perfect Auto Clicker & Keyboard", font=("Segoe UI", 14, "bold")).pack(anchor="w")
        ttk.Label(about_tab, text="P.A.C.K stands for Perfect Auto Clicker & Keyboard.\n"
                                  "This tool injects input using Windows APIs (SendInput + fallbacks) and provides locations, sequences, and randomization.\n"
                                  "Use responsibly — do not attempt to bypass anti-cheat or server rules.").pack(anchor="w", pady=(8,8))
        contact_frame = ttk.Frame(about_tab)
        contact_frame.pack(anchor="w", pady=(8,0))
        ttk.Label(contact_frame, text="Contact:").grid(row=0,column=0, sticky="w")
        ttk.Button(contact_frame, text="✉ Contact (ashmandeadwarf@gmail.com)", command=self._open_contact_email).grid(row=0,column=1, padx=(8,0))

        # initial population
        self._refresh_locations_listbox()
        self._refresh_sequence_listbox()
        self._refresh_running_apps()

    # ------------------- UI helpers: combos, locations, sequences -------------------
    def _record_combo_popup(self):
        popup = tk.Toplevel(self.root)
        popup.title("Record key combo")
        ttk.Label(popup, text="Press the combo you want (e.g., Ctrl+Shift+A). Press Esc to finish.", padding=8).pack()
        popup.geometry("+%d+%d" % (self.root.winfo_rootx()+120, self.root.winfo_rooty()+120))
        parts = []
        popup.grab_set()
        popup.focus_force()
        def on_key(event):
            ks = event.keysym
            parts.append(ks)
            popup.title(" + ".join(parts))
        def on_done(event=None):
            if parts:
                norm = "+".join(parts)
                self.keycombo_str.set(norm)
            popup.grab_release()
            popup.destroy()
        popup.bind("<Key>", on_key)
        ttk.Button(popup, text="Done", command=on_done).pack(pady=(6,8))
        popup.bind("<Escape>", lambda e: on_done())

    def _capture_current_cursor(self):
        pt = ctypes.wintypes.POINT()
        if GetCursorPos(ctypes.byref(pt)):
            self.locations.append((pt.x, pt.y))
            self._refresh_locations_listbox()
            messagebox.showinfo("Captured", f"Captured cursor at ({pt.x}, {pt.y}).")
        else:
            messagebox.showerror("Error", "Could not read cursor position.")

    def _clear_locations(self):
        if messagebox.askyesno("Clear locations", "Remove all saved locations?"):
            self.locations.clear()
            self._refresh_locations_listbox()

    def _remove_selected_location(self):
        sel = self.loc_listbox.curselection()
        if not sel:
            return
        idx = sel[0]
        self.locations.pop(idx)
        self._refresh_locations_listbox()

    def _move_location(self, direction):
        sel = self.loc_listbox.curselection()
        if not sel:
            return
        idx = sel[0]
        new = max(0, min(len(self.locations)-1, idx + direction))
        item = self.locations.pop(idx)
        self.locations.insert(new, item)
        self._refresh_locations_listbox()
        self.loc_listbox.selection_set(new)

    def _refresh_locations_listbox(self):
        self.loc_listbox.delete(0, tk.END)
        for x,y in self.locations:
            self.loc_listbox.insert(tk.END, f"{x}, {y}")

    def _set_sequence(self, seq):
        self.sequence = seq
        self._refresh_sequence_listbox()

    def _seq_add_click(self):
        opts = ["Use selected saved position", "Use current cursor position", "Use window center (target)"]
        choice = simpledialog.askstring("Click action", f"Choose option (type 1/2/3):\n1) {opts[0]}\n2) {opts[1]}\n3) {opts[2]}")
        if not choice: return
        choice = choice.strip()
        if choice == "1":
            sel = self.loc_listbox.curselection()
            if not sel:
                messagebox.showwarning("No selection", "Please select a saved position first.")
                return
            x,y = self.locations[sel[0]]
            self.sequence.append(("click", (x,y)))
        elif choice == "2":
            pt = ctypes.wintypes.POINT()
            if GetCursorPos(ctypes.byref(pt)):
                self.sequence.append(("click", (pt.x, pt.y)))
            else:
                messagebox.showerror("Error", "Could not get cursor position.")
                return
        elif choice == "3":
            self.sequence.append(("click", None))
        else:
            return
        self._refresh_sequence_listbox()

    def _seq_add_key(self):
        combo = self.keycombo_str.get().strip()
        if not combo:
            combo = simpledialog.askstring("Key combo", "Enter combo like Ctrl+Shift+A or a single key:")
            if not combo: return
        vklist = self._parse_combo_to_vk_list(combo)
        if not vklist:
            messagebox.showerror("Unsupported combo", f"Could not map combo '{combo}'.")
            return
        self.sequence.append(("key", vklist, combo))
        self._refresh_sequence_listbox()

    def _seq_add_wait(self):
        ms = simpledialog.askinteger("Wait", "Wait how many milliseconds?", minvalue=0, initialvalue=100)
        if ms is None:
            return
        self.sequence.append(("wait", int(ms)))
        self._refresh_sequence_listbox()

    def _seq_remove_selected(self):
        sel = self.seq_listbox.curselection()
        if not sel:
            return
        idx = sel[0]
        self.sequence.pop(idx)
        self._refresh_sequence_listbox()

    def _seq_move(self, delta):
        sel = self.seq_listbox.curselection()
        if not sel:
            return
        idx = sel[0]
        new = max(0, min(len(self.sequence)-1, idx + delta))
        item = self.sequence.pop(idx)
        self.sequence.insert(new, item)
        self._refresh_sequence_listbox()
        self.seq_listbox.selection_set(new)

    def _refresh_sequence_listbox(self):
        self.seq_listbox.delete(0, tk.END)
        for item in self.sequence:
            if item[0] == "click":
                loc = item[1]
                txt = "Click at target window center" if loc is None else f"Click at {loc[0]},{loc[1]}"
            elif item[0] == "key":
                txt = f"Key combo: {item[2] if len(item)>=3 else item[1]}"
            elif item[0] == "wait":
                txt = f"Wait {item[1]} ms"
            else:
                txt = str(item)
            self.seq_listbox.insert(tk.END, txt)

    def _import_locations_as_sequence(self):
        if not self.locations:
            messagebox.showinfo("No locations", "No saved locations to import.")
            return
        seq = [("click", loc) for loc in self.locations]
        self._set_sequence(seq)
        messagebox.showinfo("Imported", f"Imported {len(seq)} locations as click sequence.")

    # ---------------- parsing combos ----------------
    def _parse_combo_to_vk_list(self, combo_str):
        parts = [p.strip() for p in combo_str.replace("+", " + ").split() if p.strip() and p.strip() != "+"]
        vklist = []
        for p in parts:
            k = p.lower()
            if k in ("ctrl","control","control_l","control_r"): k = "ctrl"
            if k in ("shift","shift_l","shift_r"): k = "shift"
            if k in ("alt","alt_l","alt_r"): k = "alt"
            if k in ("enter","return"): k = "enter"
            if k in VK_COMMON:
                vklist.append(VK_COMMON[k])
                continue
            if len(k) == 1:
                try:
                    v = VkKeyScan(k)
                    if v != -1:
                        vklist.append(v & 0xff)
                        continue
                except Exception:
                    pass
            if k.startswith("f") and k[1:].isdigit():
                vk = VK_COMMON.get(k)
                if vk:
                    vklist.append(vk)
                    continue
            try:
                v = VkKeyScan(k[0])
                if v != -1:
                    vklist.append(v & 0xff)
                    continue
            except Exception:
                pass
            return None
        return vklist

    # ---------------- test / run helpers ----------------
    def _test_once(self):
        action = self.action_var.get()
        target_hwnd = self.target_hwnd
        if self.target_title_var.get().strip() and not target_hwnd:
            target_hwnd = find_window_by_title(self.target_title_var.get().strip())
        if action in ("Left Click","Right Click"):
            left = (action == "Left Click")
            if target_hwnd:
                bring_window_to_foreground(target_hwnd)
                move_cursor_to_window_center(target_hwnd)
                self._do_click_once(left, target_hwnd)
            else:
                if self.use_follow_cursor.get():
                    self._do_click_once(left, None)
                elif self.locations:
                    x,y = self.locations[0]
                    SetCursorPos(x,y)
                    self._do_click_once(left, None)
                else:
                    self._do_click_once(left, None)
            messagebox.showinfo("Done", "Test click sent.")
        else:
            combo = self.keycombo_str.get().strip()
            vklist = self._parse_combo_to_vk_list(combo)
            if not vklist:
                messagebox.showerror("No combo", "No valid key combo available.")
                return
            send_key_sequence_vk_combo(vklist)
            messagebox.showinfo("Done", "Key combo sent once.")

    def _do_click_once(self, left, hwnd):
        if self.double_click_var.get():
            send_mouse_click_sendinput(left=left)
            time.sleep(0.02)
            send_mouse_click_sendinput(left=left)
        else:
            try:
                hold_ms = int(self.hold_duration_ms.get())
            except Exception:
                hold_ms = 0
            if hold_ms > 0:
                send_mouse_down(left=left)
                time.sleep(hold_ms / 1000.0)
                send_mouse_up(left=left)
            else:
                send_mouse_click_sendinput(left=left)
        if hwnd:
            post_mouse_click_to_window(hwnd, left=left)

    def _compute_interval(self):
        try:
            cps = float(self.cps_var.get())
            if cps <= 0:
                return 0.0
        except Exception:
            return 0.0
        base_interval = 1.0 / cps
        if self.randomize_var.get():
            try:
                mn = float(self.rand_min_ms.get()) / 1000.0
                mx = float(self.rand_max_ms.get()) / 1000.0
                if mn < 0: mn = 0
                if mx < mn: mx = mn
                jitter = random.uniform(mn, mx)
                return base_interval + jitter
            except Exception:
                return base_interval
        else:
            return base_interval

    def _get_target_hwnd(self):
        if self.target_hwnd:
            return self.target_hwnd
        t = self.target_title_var.get().strip()
        if t:
            return find_window_by_title(t)
        return None

    def _play_sequence_once(self):
        hwnd = self._get_target_hwnd()
        if hwnd:
            bring_window_to_foreground(hwnd)
        for item in self.sequence:
            if item[0] == "click":
                loc = item[1]
                if loc is None and hwnd:
                    move_cursor_to_window_center(hwnd)
                elif loc is not None:
                    SetCursorPos(loc[0], loc[1])
                self._do_click_once(True, hwnd)
                time.sleep(0.02)
            elif item[0] == "key":
                vklist = item[1]
                send_key_sequence_vk_combo(vklist)
                time.sleep(0.02)
            elif item[0] == "wait":
                time.sleep(item[1]/1000.0)
        messagebox.showinfo("Sequence", "Sequence playback finished.")

    # ---------------- worker main loop ----------------
    def run(self):
        if self.running:
            return
        # validate CPS
        try:
            cps = float(self.cps_var.get())
            if cps <= 0:
                raise ValueError()
        except Exception:
            messagebox.showerror("Invalid CPS", "Please enter a positive number for CPS.")
            return
        if cps > self.max_cps_limit:
            if not messagebox.askyesno("High CPS", f"CPS={cps} exceeds {self.max_cps_limit}. Continue anyway?"):
                return
        # randomization bounds
        if self.randomize_var.get():
            try:
                mn = float(self.rand_min_ms.get())
                mx = float(self.rand_max_ms.get())
                if mn < 0 or mx < 0 or mx < mn:
                    raise ValueError()
            except Exception:
                messagebox.showerror("Randomization", "Please set valid Min/Max ms for randomization.")
                return
        # repeat / timer
        repeat_forever = self.mode_repeat_forever.get()
        if not repeat_forever:
            try:
                rc = int(self.repeat_count_var.get())
                if rc < 0:
                    raise ValueError()
            except Exception:
                messagebox.showerror("Repeat count", "Please enter a valid non-negative integer repeat count.")
                return
        else:
            rc = None
        try:
            timer_seconds = float(self.timer_seconds_var.get())
            if timer_seconds < 0:
                timer_seconds = 0
        except Exception:
            timer_seconds = 0
        # if keyboard combo ensure mapping
        if self.action_var.get() == "Keyboard Combo":
            if not self.keycombo_str.get().strip():
                messagebox.showerror("Combo missing", "Enter a key combo first.")
                return
            vklist = self._parse_combo_to_vk_list(self.keycombo_str.get().strip())
            if not vklist:
                messagebox.showerror("Combo mapping", "Could not map the combo to keys.")
                return

        # start delay
        try:
            delay = float(self.start_delay_sec.get())
            if delay < 0: delay = 0
        except Exception:
            delay = 0
        if delay > 0:
            for i in range(int(delay),0,-1):
                if self.stop_event.is_set():
                    return
                self.status_label.config(text=f"Starting in {i} s...", foreground="orange")
                time.sleep(1)
            self.status_label.config(text="Starting...", foreground="orange")

        self.stop_event.clear()
        self.running = True
        self.run_btn.config(state="disabled")
        self.stop_btn.config(state="normal")
        self.status_label.config(text="Running...", foreground="red")
        self.worker_thread = threading.Thread(target=self._worker_loop, args=(rc, timer_seconds), daemon=True)
        self.worker_thread.start()

    def stop(self):
        if not self.running:
            return
        self.stop_event.set()
        self.run_btn.config(state="normal")
        self.stop_btn.config(state="disabled")
        self.status_label.config(text="Stopping...", foreground="orange")
        if self.worker_thread:
            self.worker_thread.join(timeout=1.0)
        self.running = False
        self.status_label.config(text="Stopped", foreground="green")

    def _worker_loop(self, repeat_count, timer_seconds):
        start_time = time.perf_counter()
        executed = 0
        action = self.action_var.get()
        hwnd = self._get_target_hwnd()
        if hwnd:
            bring_window_to_foreground(hwnd)

        while not self.stop_event.is_set():
            if timer_seconds > 0 and (time.perf_counter() - start_time) >= timer_seconds:
                break
            if repeat_count is not None and executed >= repeat_count:
                break

            if self.sequence:
                if hwnd:
                    bring_window_to_foreground(hwnd)
                for item in self.sequence:
                    if self.stop_event.is_set(): break
                    if item[0] == "click":
                        loc = item[1]
                        if loc is None and hwnd:
                            move_cursor_to_window_center(hwnd)
                        elif loc is not None:
                            SetCursorPos(loc[0], loc[1])
                        self._do_click_once(True, hwnd)
                        time.sleep(0.001)
                    elif item[0] == "key":
                        vklist = item[1]
                        send_key_sequence_vk_combo(vklist)
                        time.sleep(0.001)
                    elif item[0] == "wait":
                        time.sleep(item[1]/1000.0)
                executed += 1
                interval = self._compute_interval()
                if interval <= 0:
                    time.sleep(0.001)
                else:
                    time.sleep(interval)
            else:
                if action in ("Left Click","Right Click"):
                    left = (action == "Left Click")
                    if self.use_follow_cursor.get():
                        pass
                    elif self.locations:
                        idx = executed % len(self.locations)
                        x,y = self.locations[idx]
                        SetCursorPos(x,y)
                    else:
                        if hwnd:
                            move_cursor_to_window_center(hwnd)
                    self._do_click_once(left, hwnd)
                else:
                    vklist = self._parse_combo_to_vk_list(self.keycombo_str.get().strip())
                    if vklist:
                        if hwnd:
                            bring_window_to_foreground(hwnd)
                        send_key_sequence_vk_combo(vklist)

                executed += 1
                interval = self._compute_interval()
                if interval <= 0:
                    time.sleep(0.001)
                else:
                    time.sleep(interval)

        self.root.after(0, self._finalize_ui)

    def _finalize_ui(self):
        self.running = False
        self.run_btn.config(state="normal")
        self.stop_btn.config(state="disabled")
        self.status_label.config(text="Stopped", foreground="green")

    # -------------------- Running apps UI handlers --------------------
    def _refresh_running_apps(self):
        self.apps_listbox.delete(0, tk.END)
        try:
            items = enumerate_visible_windows_with_paths()
            # store mapping in the listbox via a separate list
            self._apps_items = items
            for hwnd, title, exe in items:
                display = title if title else "<no title>"
                if exe:
                    display += f"  —  {os.path.basename(exe)}"
                self.apps_listbox.insert(tk.END, display)
        except Exception as e:
            messagebox.showerror("Refresh", f"Failed to enumerate windows: {e}")

    def _use_selected_running_app(self):
        sel = self.apps_listbox.curselection()
        if not sel:
            messagebox.showwarning("Select", "Please select an app from the list first.")
            return
        idx = sel[0]
        hwnd, title, exe = self._apps_items[idx]
        self.target_hwnd = hwnd
        # fill target title for clarity
        self.target_title_var.set(title if title else "")
        messagebox.showinfo("Target selected", f"Selected window:\n{title}\n{exe if exe else '(exe unknown)'}")

    def _show_selected_app_path(self):
        sel = self.apps_listbox.curselection()
        if not sel:
            messagebox.showwarning("Select", "Please select an app from the list first.")
            return
        idx = sel[0]
        hwnd, title, exe = self._apps_items[idx]
        messagebox.showinfo("Executable path", exe if exe else "Unknown / could not read path")

    def _select_by_exe_dialog(self):
        path = filedialog.askopenfilename(title="Select executable", filetypes=[("EXE files","*.exe"),("All files","*.*")])
        if not path:
            return
        res = find_window_by_exe_path(path)
        if res:
            hwnd, title, exe = res
            self.target_hwnd = hwnd
            self.target_title_var.set(title if title else "")
            messagebox.showinfo("Found window", f"Found window:\n{title}\n{exe}")
            self._refresh_running_apps()
        else:
            messagebox.showwarning("Not found", "Could not find a visible window owned by that executable. Try launching the program first and refresh the list.")

    # ---------------- contact / about ----------------
    def _open_contact_email(self):
        mailto = "mailto:ashmandeadwarf@gmail.com"
        try:
            webbrowser.open(mailto)
        except Exception:
            messagebox.showinfo("Contact", "Please email: ashmandeadwarf@gmail.com")

# -------------------- Admin check & run --------------------
def is_admin():
    try:
        return ctypes.windll.shell32.IsUserAnAdmin() != 0
    except Exception:
        return False

def main():
    root = tk.Tk()
    app = PACKApp(root)
    if not is_admin():
        root.after(800, lambda: messagebox.showwarning(
            "Administrator recommended",
            "For best compatibility inside games, run this script as Administrator.\nRight-click Python/script and choose 'Run as administrator'."
        ))
    root.mainloop()

if __name__ == "__main__":
    main()
