# P.A.C.K — Perfect Auto Clicker & Keyboard

**Version:** 1.0
**File:** `pack_autoclicker.py` (Windows-only)
**Author / Contact:** `ashmandeadwarf@gmail.com`

---

## Overview

P.A.C.K is a single-file, portable Windows app (Tkinter + Win32 `SendInput`) that automates mouse clicks and keyboard input with game-friendly injection techniques. It aims to reproduce the most useful features from advanced auto-clickers while keeping the UI simple and usable.

**Key capabilities**

* Left / Right mouse click automation and keyboard key-combinations.
* Capture and use multiple screen **Locations** (x,y), or follow the current cursor.
* **Script / Sequence** builder: create sequences of `Click`, `Key`, and `Wait(ms)` steps and replay them.
* **Randomized timing** (min/max jitter in ms) to avoid perfectly uniform intervals.
* **CPS (clicks per second)** control (decimal values allowed).
* **Double-click** option and **Click-and-hold** (hold for N ms).
* **Repeat / Timer** modes (repeat forever / N times / run for T seconds).
* **Target selection made easy**:

  * **Running Apps** tab lists visible windows with executable names so you can choose a target window directly (double-click or *Use selected*).
  * **Select by executable**: browse to an `.exe` and P.A.C.K will try to find and choose a visible window owned by that executable.
* **About** screen with contact button that opens your default mail client pre-addressed to `ashmandeadwarf@gmail.com`.
* Game-friendly injection backends: `SendInput` with scancodes, and `PostMessage` fallback; attempts to bring target window to foreground.

---

## Important safety & legal notes

* **Use responsibly.** Do **not** use P.A.C.K to gain unfair advantage in multiplayer games or to violate terms of service. I will not help bypass anti-cheat systems.
* **Anti-cheat & raw input:** Some games ignore synthetic input (exclusive DirectInput/RawInput) or detect injected input. If you use P.A.C.K in a game, only do so in single-player/offline or where explicitly allowed.
* **Administrator recommended.** For best compatibility, run P.A.C.K as Administrator (right-click → *Run as administrator*). Running both the game and P.A.C.K as Administrator increases the chance of success.
* **Antivirus false positives:** Some AV engines flag programs that send synthetic inputs. If that happens, whitelist the app in your AV or scan it locally before running.

---

## System requirements

* Windows 10 / 11 (32-bit or 64-bit).
* Python 3.8+ to run the script directly. (Optional: build a standalone EXE — instructions below.)
* No third-party Python modules required to run `pack_autoclicker.py` itself. (Only `tkinter` and `ctypes` are used; `tkinter` is included with most standard Windows Python installers; on some systems you may need to install the `python3-tk` package).

---

## Files

* `pack_autoclicker.py` — main Python application (single-file).
* (optional) `app.ico` — your icon file (used when building EXE).

---

## UI walkthrough

### Main Tab

* **Action**: choose `Left Click`, `Right Click`, or `Keyboard Combo`.
* **Key Combo**: record or type combos like `Ctrl+Shift+A`. Use `Record combo` to capture key presses.
* **CPS**: clicks-per-second. Use decimal values if needed.
* **Randomize interval**: enable and set `Min ms` / `Max ms` jitter (these values are added to the base interval derived from CPS).
* **Double-click**: if checked, each action sends two clicks close together.
* **Click-and-hold ms**: if set >0, press down for that many ms instead of a normal click.
* **Start delay (sec)**: countdown before actions start.
* **Target window**: filled automatically when you select a running app or when you use *Select by executable...*. You can also type a substring manually.

**Run / Stop / Test click once**: control the running of the automation.

### Locations Tab

* Capture current cursor positions (x,y) to a list. Use them in round-robin or with sequences. Options to remove, reorder, clear.

### Script / Sequence Tab

* Build ordered sequences consisting of:

  * `("click", (x,y))` — click at screen coordinate
  * `("click", None)` — click target window center
  * `("key", [vklist], "ComboString")` — key-combo by VK list
  * `("wait", ms)` — wait
* Use `Add Click`, `Add Key`, `Add Wait` to build sequences. `Test Play Sequence` runs it once.

### Running Apps Tab (easier target selection)

* **Refresh running apps** enumerates visible windows and shows their titles and EXE names (where readable).
* **Double-click an entry** to set it as the target (fills Target window). This makes selecting a game/program fast.
* **Select by executable**: browse to an `.exe` file and P.A.C.K will auto-find a matching visible window owned by that process (useful when multiple windows have similar titles).

### About Tab

* Displays: **P.A.C.K — Perfect Auto Clicker & Keyboard** and the contact button `✉ Contact (ashmandeadwarf@gmail.com)` which opens the default mail client with the TO field set.

---

## Usage examples

1. **Quick single-action clicks on a game button**

   * Start your game in Borderless Windowed (if possible).
   * Open P.A.C.K (run as Admin).
   * In *Running Apps*, `Refresh running apps`, pick the game window → `Use selected`.
   * In *Main* choose `Left Click`, set CPS (e.g., `10`) and optionally randomize.
   * Press **Run** and focus the game if required.

2. **Click a sequence of points in a desktop app**

   * Go to *Locations*, `Capture current cursor position` while hovering each required point.
   * Go to *Script / Sequence*, `Import locations as sequence` or add `Click` items referencing saved positions.
   * Press **Test Play Sequence** to verify, then **Run** to execute.

3. **Keyboard combo automation**

   * In *Main*, choose `Keyboard Combo`, click `Record combo` and press the combo you want.
   * Set repeats or timer, then **Run**.

---

## Troubleshooting / Tips

* **If clicks/keys are ignored by a game:**

  1. Make sure you ran P.A.C.K as **Administrator**.
  2. Try launching the game in **Borderless Windowed** instead of exclusive Fullscreen.
  3. Run both game and P.A.C.K as Admin.
  4. Use *Select by executable* to ensure the exact window was selected.
  5. Use the *Script / Sequence* to explicitly `move cursor` (SetCursorPos) then click — sometimes moving hardware cursor + `SendInput` helps.
  6. If all fail, an AutoHotkey compiled EXE sometimes works better for certain games. (I can provide that if you want.)

* **If `Refresh running apps` shows no exe path**: Windows may restrict reading process names for some system processes or elevated/other-user processes. Run P.A.C.K as Admin.

* **If the app crashes on startup**: verify your Python has `tkinter` available and you are running on Windows.

---

## Profile & sequence import/export

* You can save/restore a profile (JSON) of your settings including locations and sequences (via Advanced tab in previous variants; current file supports JSON save/load routines).
* You can export `sequence` as JSON, edit it, and reimport.

---

## Developer notes / internal behavior (summary)

* Input injection uses `SendInput` with scancodes (MapVirtualKey) — this is preferred to emulate hardware-like input.
* When targeting a window, P.A.C.K attempts to bring it to foreground using `AttachThreadInput` / `SetForegroundWindow` / `ShowWindow`.
* Fallbacks: `PostMessage` (WM\_LBUTTONDOWN/UP) are posted to the target client area center if necessary.
* Limitations: some exclusive or low-level input modes can ignore synthetic input — this is a Windows & application behavior, not a bug in P.A.C.K.

---

## Building a single-file Windows EXE (no console window) with an icon

**Prerequisites**

1. Install Python 3.8+ and `pip`.
2. (Optional but recommended) Create and activate a virtual environment:

   ```powershell
   python -m venv venv
   .\venv\Scripts\activate
   ```
3. Install PyInstaller:

   ```powershell
   pip install pyinstaller
   ```
4. Prepare your icon file in `.ico` format (for example `pack_icon.ico`). Put it in the same directory as `pack_autoclicker.py`.

**Single-command build** (produces a single EXE with no terminal window, using your icon):

```powershell
pyinstaller --onefile --windowed --icon "pack_icon.ico" pack_autoclicker.py
```

**Notes about that command**

* `--onefile` bundles everything into a single EXE in the `dist` folder.
* `--windowed` (or `-w`) builds the app without opening a console/terminal window when launched.
* `--icon "pack_icon.ico"` sets the application icon (you must supply an `.ico` file).
* The generated EXE will be at `dist\pack_autoclicker.exe` (or `dist\pack_autoclicker.exe` renamed by PyInstaller if necessary).
* If you want to rename the EXE, you can pass `--name "P.A.C.K"`:

  ```powershell
  pyinstaller --onefile --windowed --icon "pack_icon.ico" --name "PACK" pack_autoclicker.py
  ```

  The output will then be `dist\PACK.exe`.

**Signing / Antivirus**

* Unsigned executables may trigger warnings from Windows SmartScreen or antivirus engines. Code-signing requires an appropriate code-signing certificate (outside the scope here).

---

## Example build workflow (concise)

```powershell
# inside project folder where pack_autoclicker.py & pack_icon.ico exist
python -m venv venv
.\venv\Scripts\activate
pip install pyinstaller
pyinstaller --onefile --windowed --icon "pack_icon.ico" --name "PACK" pack_autoclicker.py
# After completion, the single EXE will be in the dist\PACK.exe
```

Run the EXE by right-click → *Run as administrator* when testing in games to maximize compatibility.

---

## Contact & support

If you find issues, need a new feature (tray icon, AHK companion, compiled AHK EXE, global emergency stop hotkey, or custom sequence templates), or want help compiling/signing, contact:

**Email:** `ashmandeadwarf@gmail.com`
(Clicking the Contact button in the About tab opens your default mail client with this address in the TO field.)

---

## Final remarks

P.A.C.K is designed to be a powerful, flexible automation tool for desktop tasks and offline/permitted game testing. It intentionally avoids kernel-level/hardware emulation tricks and will not help bypass anti-cheat. If a specific game still ignores synthetic input after following the troubleshooting steps, tell me the **exact game name**, whether it runs **exclusive fullscreen** or **borderless/windowed**, and whether you're willing to try an AutoHotkey compiled helper — I can prepare that next.

Happy automating — use responsibly!
