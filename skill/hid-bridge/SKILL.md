---
name: hid-bridge
description: |
  Simulate real keyboard/mouse input (Windows SendInput) and capture screenshots or
  short GIFs of any window — built for playtesting and validating desktop apps/games
  (e.g. a raylib/GLFW or similar native game window) from the CLI. Use this whenever
  a task requires actually driving a running Windows app with input and visually
  confirming the result: playtesting a game build, exercising a GUI, reproducing a
  bug interactively, or capturing before/after evidence of a UI change.
keywords:
  - playtest
  - keyboard simulation
  - mouse simulation
  - screenshot
  - gif recording
  - SendInput
  - window automation
  - game testing
---

# HID Bridge

A single self-contained CLI (`hidbridge.exe`) that simulates keyboard and mouse input
at the Windows `SendInput` level (the same input-stack layer real HID devices report
through) and captures screenshots / short GIFs of a target window. Built so an agent
can *actually play* a game or *actually operate* an app rather than just reading its
source.

Binary: `bin\hidbridge.exe` (next to this file — self-contained, no .NET install
required). Source and build instructions: https://github.com/PageMastr/AIID

Every command prints exactly one JSON line to stdout: `{"ok": true/false, ...}`.
Always check `ok` before trusting the rest of a script.

## Core workflow

1. **Find and focus the target window first.** `SendInput` delivers events to
   whichever window currently has OS focus — it is not addressed by window handle.
   Any keyboard/mouse command will go to the wrong place (or nowhere useful) if the
   target window isn't focused.
   ```
   hidbridge.exe window focus <title-or-process-substring>
   ```
2. **Drive it** with `key`/`type`/`mouse` commands.
3. **Look at what happened** with `screenshot`, or capture a short clip with `gif`.
   Read the returned `path` with the Read tool — screenshots and GIFs are real image
   files, so you can literally see the game/app state, not just infer it from logs.

Re-run `window focus` if focus may have been lost (e.g. after alt-tabbing, or if a
previous test spawned/killed a process).

## Command reference

### Window discovery
```
hidbridge.exe window list                      # all visible top-level windows
hidbridge.exe window find <substring>           # first match, no side effects
hidbridge.exe window focus <substring>           # bring to foreground + activate
hidbridge.exe window active                      # what currently has OS focus (debugging)
hidbridge.exe window rect <substring>            # client-area rect in screen coords (debugging)
```
Matches against both window title and process name, case-insensitive substring.
`window focus` verifies the target actually became the foreground window (`ok` is
`false` if something else grabbed focus first) — if that happens, just retry the
focus call right before the input you're about to send.

### Keyboard
```
hidbridge.exe key press <name> [--hold ms]       # tap (default 40ms), then release
hidbridge.exe key down <name>                    # press and hold (does not release)
hidbridge.exe key up <name>                      # release a held key
hidbridge.exe key hold <name> <ms>                # press, wait ms, release (blocking)
hidbridge.exe type <text>                         # types literal text (menus, IP:port entry, etc.)
hidbridge.exe keys                                # list every known key name
```
Key names: letters `A`-`Z`, digits `0`-`9`, `F1`-`F12`, `UP DOWN LEFT RIGHT`,
`ENTER ESC SPACE TAB BACKSPACE DELETE INSERT HOME END PAGEUP PAGEDOWN`,
`SHIFT CTRL ALT` (+ `L`/`R` variants), `WIN`, and punctuation `PERIOD COMMA COLON MINUS`
(also usable as `. , : -`).

**Movement / held input pattern** — to walk, fly, or move while simultaneously
capturing footage, use `down` then `up` around a blocking capture call:
```
hidbridge.exe key down W
hidbridge.exe gif 4 --window "MyGame"      # blocks 4s while W stays held
hidbridge.exe key up W
```

### Mouse
```
hidbridge.exe mouse move <x> <y> [--relative]     # absolute screen coords, or relative delta
hidbridge.exe mouse click <left|right|middle>
hidbridge.exe mouse down <left|right|middle>
hidbridge.exe mouse up <left|right|middle>
hidbridge.exe mouse scroll <delta>                 # positive = up/away
```
Absolute moves are in primary-screen pixel coordinates.

### Capture
```
hidbridge.exe screenshot [--window <substring>] [--out <path>]
hidbridge.exe gif <seconds 1-5> [--fps 10] [--maxwidth 640] [--window <substring>] [--out <path>]
```
- `--window` crops to that window's **client area** (title bar/borders excluded). Omit
  it to capture the full primary screen.
- If `--out` is omitted, files are written to `%TEMP%\hidbridge-captures\` with a
  timestamped name, and the JSON `path` field tells you exactly where.
- `gif` is a **blocking** call for its full duration — any `key down` issued beforehand
  stays held throughout, which is the intended way to record motion.
- GIF frames are downscaled to `--maxwidth` (default 640px wide) specifically to stay
  well under the ~5MB image size the Read tool can display — a busy scene at native
  1080p and 5 seconds can otherwise produce a multi-MB file. Drop `--fps` or
  `--maxwidth` further if a capture still comes back too large; raise `--maxwidth` (or
  pass `0` to disable resizing) if you need full detail and are prepared to view it a
  different way.

## Gotchas learned from testing

- A freshly launched app may take a moment before its window is enumerable — if
  `window find` comes back empty right after launch, wait briefly and retry.
- Context-sensitive "use" actions (e.g. an interact-key prompt near an object) require
  being in range first — a `key hold W <ms>` to move into position before the interact
  key is often necessary, the same way a human would play it.
- If a target process disappears (crash, or the user quit it), `window focus` will
  fail with `ok:false`; relaunch the executable and re-focus before continuing.
- Menu-driven UIs typically respond to arrow keys / `ENTER` even when WASD does
  nothing (WASD is usually gameplay-only) — check the app's own control scheme rather
  than assuming.
- If a specific key (arrows especially) seems to reach the window (focus confirmed)
  but visibly does nothing, and an alternate binding for the same action exists (e.g.
  WASD instead of arrows), try that first before assuming the tool is broken — some
  apps only bind the arrow keys to certain screens. Mouse clicks are also a reliable
  fallback for menu items when keyboard nav is being finicky:
  `hidbridge.exe window rect <substring>` gives you the client-area origin to convert
  an on-screen element's position (read off a screenshot) into absolute coordinates
  for `mouse move` / `mouse click`.
- A `key down` that's still held when the target window loses and regains OS focus
  can get silently released — many apps clear all input state on focus loss to avoid
  stuck-key bugs. Re-run `window focus` immediately before each input burst rather
  than once at the start of a long sequence.
- Radar/compass-style HUD indicators are often easier to navigate by than an on-screen
  distance readout: if flying/driving toward a target makes the readout distance
  *increase*, the readout may be path-based rather than straight-line — trust a
  compass needle or an in-world marker's screen position over the number, and turn
  until they align before committing to forward throttle.

## Example: playtest loop

```
hidbridge.exe window focus MyGame
hidbridge.exe key press ENTER --hold 80          # confirm a menu selection
hidbridge.exe screenshot --window MyGame --out shot1.png
# ... Read shot1.png to see the result ...
hidbridge.exe key down W
hidbridge.exe gif 4 --window MyGame --fps 10 --out clip.gif
hidbridge.exe key up W
# ... Read clip.gif to confirm the expected motion/behavior occurred ...
```
