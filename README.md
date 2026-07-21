# AIID — AI Input Device

An HID-style input bridge and matching [Claude Code](https://claude.com/claude-code)
Skill that gives an AI CLI agent the ability to actually *use* a Windows app: simulate
real keyboard and mouse input, take screenshots, and record short GIFs of a target
window. Built so an agent developing or reviewing something like a game can playtest
it directly — press keys, fly the ship, land, walk around — and visually confirm the
result instead of only reading source or logs.

- **Input**: keyboard press/hold/release and text typing, mouse move/click/scroll —
  all via the Win32 `SendInput` API, the same input-stack layer real HID devices
  report through.
- **Capture**: full-window screenshots (PNG) and 1–5 second GIFs, auto-downscaled to
  stay within the image size an agent can actually view inline.
- **Delivery**: one self-contained `hidbridge.exe` (no .NET runtime install required)
  plus a `SKILL.md` that documents it for Claude Code.

## Repo layout

```
src/HidBridge/        C# source for hidbridge.exe (.NET 8)
skill/hid-bridge/      SKILL.md — the Claude Code skill definition
scripts/install.ps1    Installs the skill + binary into ~/.claude/skills/hid-bridge
dist/                  Local build output (gitignored)
```

## Requirements

- Windows 10/11 x64
- Nothing else at runtime — `hidbridge.exe` is a self-contained single-file publish.
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) only if building from
  source.

## Install (deploy the skill for Claude Code)

Claude Code loads skills from `%USERPROFILE%\.claude\skills\<name>\SKILL.md`. To make
`hid-bridge` available to Claude:

### Option A — scripted install (recommended)

```powershell
git clone https://github.com/PageMastr/AIID.git
cd AIID
./scripts/install.ps1
```

This copies `skill/hid-bridge/SKILL.md` into
`%USERPROFILE%\.claude\skills\hid-bridge\SKILL.md`, then either copies a local
`dist/hidbridge.exe` (if you've already built one) or downloads the latest prebuilt
binary from this repo's [Releases](https://github.com/PageMastr/AIID/releases) page
into `%USERPROFILE%\.claude\skills\hid-bridge\bin\hidbridge.exe`.

### Option B — manual install

1. Download `hidbridge.exe` from the [latest release](https://github.com/PageMastr/AIID/releases/latest)
   (or build it yourself — see below).
2. Create `%USERPROFILE%\.claude\skills\hid-bridge\bin\`.
3. Copy `hidbridge.exe` into that `bin\` folder.
4. Copy `skill/hid-bridge/SKILL.md` from this repo into
   `%USERPROFILE%\.claude\skills\hid-bridge\SKILL.md` (next to, not inside, `bin\`).

Final layout:
```
%USERPROFILE%\.claude\skills\hid-bridge\
├── SKILL.md
└── bin\
    └── hidbridge.exe
```

### After installing

Start a **new** Claude Code session (skills are loaded at session start). `hid-bridge`
should then appear in Claude's available-skills list, and you can just ask it to
playtest, screenshot, or interact with a running Windows app — it will invoke
`hidbridge.exe` via its Bash tool on its own once the skill is loaded. You can also
say `/hid-bridge` or reference it explicitly if you want to steer it directly.

No MCP server registration, restart of Claude Code's config, or extra setup is
needed beyond the files being in place.

## Building from source

```powershell
cd src/HidBridge
dotnet publish -c Release -r win-x64 --self-contained true -o ../../dist
```

Produces `dist/hidbridge.exe` (~36MB, self-contained). Re-run `scripts/install.ps1`
afterwards to push the freshly built binary into the installed skill.

## Command reference

See [`skill/hid-bridge/SKILL.md`](skill/hid-bridge/SKILL.md) for the full command
reference, key-name list, and usage patterns (it's the same document Claude reads).
Quick taste:

```
hidbridge.exe window focus RetroCitizen
hidbridge.exe key hold W 800
hidbridge.exe screenshot --window RetroCitizen --out shot.png
hidbridge.exe key down W
hidbridge.exe gif 4 --window RetroCitizen --fps 10 --out clip.gif
hidbridge.exe key up W
```

Every command prints one JSON line to stdout (`{"ok": true, ...}` / `{"ok": false, "error": "..."}`).

## Design notes

- Input is simulated purely in software via `SendInput` — there's no physical HID
  hardware (Arduino/Pico) or kernel driver involved. This is sufficient for apps that
  read the standard Win32 input queue (which covers the large majority of games and
  GUIs, including raylib/GLFW-based ones). It will **not** bypass anti-cheat or
  raw-input filtering that specifically distrusts synthetic `SendInput` events — that
  would require real HID hardware or a kernel-level driver instead.
- `SendInput` always targets whichever window currently has OS focus, not a specific
  window handle — hence `window focus` is a required first step before any key/mouse
  command will land where you expect.
- GIF capture blocks for its full duration so that a `key down` issued beforehand
  stays held for the whole recording — this is the intended way to capture motion
  (walking, flying, etc.) rather than a limitation.
- Arrow keys, Insert/Delete/Home/End/PageUp/PageDown, and right-Ctrl/right-Alt are sent
  as real hardware scan codes with the extended-key prefix bit forced on. Apps that
  resolve keys from the raw scancode (GLFW's Win32 backend, notably) otherwise can't
  tell e.g. the dedicated Up arrow from Numpad-8, since both share a base scan code —
  found and fixed during the RetroCitizen playtest, where arrow-key menu navigation
  silently did nothing until this was in place.
- Some apps clear all "held" input state when they lose OS focus (to avoid stuck-key
  bugs), so a `key down` that's still active when focus moves away and back can
  effectively get released out from under you. `window focus` right before each
  input burst avoids this.

## Feedback and support

Found a bug or want a feature? [Open an issue](https://github.com/PageMastr/AIID/issues).

Like this tool? Consider donating to help fund the project:
Venmo `@NachosWorld712` · CashApp `$TheLessBeatenPath`

## License

MIT — see [LICENSE](LICENSE).
