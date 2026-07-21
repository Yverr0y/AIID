using System.Text.Json;
using HidBridge;

int exitCode = 0;
try
{
    exitCode = Run(args);
}
catch (Exception ex)
{
    WriteJson(new { ok = false, error = ex.Message });
    exitCode = 1;
}
return exitCode;

static int Run(string[] args)
{
    if (args.Length == 0)
    {
        PrintUsage();
        return 1;
    }

    var (flags, positionals) = ParseArgs(args.Skip(1).ToArray());
    string command = args[0].ToLowerInvariant();

    switch (command)
    {
        case "window":
            return HandleWindow(positionals, flags);
        case "key":
            return HandleKey(positionals, flags);
        case "type":
            return HandleType(positionals, flags);
        case "mouse":
            return HandleMouse(positionals, flags);
        case "screenshot":
            return HandleScreenshot(positionals, flags);
        case "gif":
            return HandleGif(positionals, flags);
        case "keys":
            WriteJson(new { ok = true, keys = Keys.KnownNames.OrderBy(k => k).ToArray() });
            return 0;
        case "keycode":
        {
            if (positionals.Count < 1 || !Keys.TryGet(positionals[0], out ushort dvk))
            { WriteJson(new { ok = false, error = "usage: keycode <name>" }); return 1; }
            uint scan = HidBridge.NativeMethods.MapVirtualKey(dvk, HidBridge.NativeMethods.MAPVK_VK_TO_VSC_EX);
            WriteJson(new { ok = true, vk = dvk, scanRaw = scan, scanHex = scan.ToString("X4") });
            return 0;
        }
        case "help":
        case "--help":
        case "-h":
            PrintUsage();
            return 0;
        default:
            WriteJson(new { ok = false, error = $"Unknown command '{command}'. Run 'hidbridge help'." });
            return 1;
    }
}

static int HandleWindow(List<string> pos, Dictionary<string, string?> flags)
{
    if (pos.Count == 0) { WriteJson(new { ok = false, error = "usage: window <list|find|focus> [substring]" }); return 1; }

    switch (pos[0].ToLowerInvariant())
    {
        case "list":
            var windows = WindowFinder.ListVisibleWindows()
                .Select(w => new { handle = w.Handle.ToInt64(), title = w.Title, process = w.ProcessName, pid = w.ProcessId });
            WriteJson(new { ok = true, windows });
            return 0;

        case "find":
        {
            if (pos.Count < 2) { WriteJson(new { ok = false, error = "usage: window find <substring>" }); return 1; }
            var w = WindowFinder.Find(pos[1]);
            if (w == null) { WriteJson(new { ok = false, error = $"No visible window matching '{pos[1]}'." }); return 1; }
            WriteJson(new { ok = true, handle = w.Handle.ToInt64(), title = w.Title, process = w.ProcessName, pid = w.ProcessId });
            return 0;
        }

        case "focus":
        {
            if (pos.Count < 2) { WriteJson(new { ok = false, error = "usage: window focus <substring>" }); return 1; }
            var w = WindowFinder.Find(pos[1]);
            if (w == null) { WriteJson(new { ok = false, error = $"No visible window matching '{pos[1]}'." }); return 1; }
            bool ok = WindowFinder.Focus(w.Handle);
            Thread.Sleep(150); // let the OS settle focus before further input/capture
            bool confirmed = WindowFinder.GetForeground() == w.Handle;
            WriteJson(new { ok = ok && confirmed, title = w.Title, process = w.ProcessName, confirmed });
            return ok && confirmed ? 0 : 1;
        }

        case "rect":
        {
            if (pos.Count < 2) { WriteJson(new { ok = false, error = "usage: window rect <substring>" }); return 1; }
            var w = WindowFinder.Find(pos[1]);
            if (w == null) { WriteJson(new { ok = false, error = $"No visible window matching '{pos[1]}'." }); return 1; }
            var r = WindowFinder.GetClientRectOnScreen(w.Handle);
            WriteJson(new { ok = true, left = r.Left, top = r.Top, right = r.Right, bottom = r.Bottom, width = r.Right - r.Left, height = r.Bottom - r.Top });
            return 0;
        }

        case "active":
        {
            IntPtr fg = WindowFinder.GetForeground();
            var w = WindowFinder.ListVisibleWindows().FirstOrDefault(x => x.Handle == fg);
            WriteJson(new { ok = true, handle = fg.ToInt64(), title = w?.Title, process = w?.ProcessName });
            return 0;
        }

        default:
            WriteJson(new { ok = false, error = "usage: window <list|find|focus> [substring]" });
            return 1;
    }
}

static int HandleKey(List<string> pos, Dictionary<string, string?> flags)
{
    if (pos.Count < 2) { WriteJson(new { ok = false, error = "usage: key <press|down|up|hold> <name> [ms]" }); return 1; }

    string action = pos[0].ToLowerInvariant();
    string name = pos[1];

    if (!Keys.TryGet(name, out ushort vk))
    {
        WriteJson(new { ok = false, error = $"Unknown key '{name}'. Run 'hidbridge keys' for the list." });
        return 1;
    }

    switch (action)
    {
        case "press":
            int holdMs = flags.TryGetValue("hold", out var h) && int.TryParse(h, out var hv) ? hv : 40;
            InputSimulator.KeyPress(vk, holdMs);
            break;
        case "down":
            InputSimulator.KeyDown(vk);
            break;
        case "up":
            InputSimulator.KeyUp(vk);
            break;
        case "hold":
            int ms = pos.Count >= 3 && int.TryParse(pos[2], out var mv) ? mv : 500;
            InputSimulator.KeyHold(vk, ms);
            break;
        default:
            WriteJson(new { ok = false, error = "usage: key <press|down|up|hold> <name> [ms]" });
            return 1;
    }

    WriteJson(new { ok = true, action, key = name.ToUpperInvariant() });
    return 0;
}

static int HandleType(List<string> pos, Dictionary<string, string?> flags)
{
    if (pos.Count < 1) { WriteJson(new { ok = false, error = "usage: type <text>" }); return 1; }
    string text = string.Join(' ', pos);
    InputSimulator.TypeText(text);
    WriteJson(new { ok = true, typed = text });
    return 0;
}

static int HandleMouse(List<string> pos, Dictionary<string, string?> flags)
{
    if (pos.Count == 0) { WriteJson(new { ok = false, error = "usage: mouse <move|click|down|up|scroll> ..." }); return 1; }

    switch (pos[0].ToLowerInvariant())
    {
        case "move":
        {
            if (pos.Count < 3 || !int.TryParse(pos[1], out int x) || !int.TryParse(pos[2], out int y))
            { WriteJson(new { ok = false, error = "usage: mouse move <x> <y> [--relative]" }); return 1; }

            if (flags.ContainsKey("relative")) InputSimulator.MouseMoveRelative(x, y);
            else InputSimulator.MouseMoveAbsolute(x, y);
            WriteJson(new { ok = true, x, y, relative = flags.ContainsKey("relative") });
            return 0;
        }

        case "click":
        case "down":
        case "up":
        {
            string btnName = pos.Count >= 2 ? pos[1] : "left";
            if (!TryParseButton(btnName, out var btn))
            { WriteJson(new { ok = false, error = "button must be left, right, or middle" }); return 1; }

            if (pos[0] == "click") InputSimulator.MouseClick(btn);
            else if (pos[0] == "down") InputSimulator.MouseButtonDown(btn);
            else InputSimulator.MouseButtonUp(btn);

            WriteJson(new { ok = true, action = pos[0], button = btnName });
            return 0;
        }

        case "scroll":
        {
            int delta = pos.Count >= 2 && int.TryParse(pos[1], out var d) ? d : 1;
            InputSimulator.MouseScroll(delta);
            WriteJson(new { ok = true, delta });
            return 0;
        }

        default:
            WriteJson(new { ok = false, error = "usage: mouse <move|click|down|up|scroll> ..." });
            return 1;
    }
}

static int HandleScreenshot(List<string> pos, Dictionary<string, string?> flags)
{
    WindowInfo? window = null;
    if (flags.TryGetValue("window", out var wname) && !string.IsNullOrEmpty(wname))
    {
        window = WindowFinder.Find(wname);
        if (window == null) { WriteJson(new { ok = false, error = $"No visible window matching '{wname}'." }); return 1; }
    }

    var rect = Capture.ResolveRect(window);
    string outPath = flags.TryGetValue("out", out var o) && !string.IsNullOrEmpty(o)
        ? o!
        : DefaultCapturePath("png");

    string saved = Capture.SaveScreenshot(rect, outPath);
    WriteJson(new { ok = true, path = Path.GetFullPath(saved), window = window?.Title });
    return 0;
}

static int HandleGif(List<string> pos, Dictionary<string, string?> flags)
{
    double seconds = pos.Count >= 1 && double.TryParse(pos[0], out var s) ? s : 3.0;
    seconds = Math.Clamp(seconds, 1.0, 5.0);

    int fps = flags.TryGetValue("fps", out var f) && int.TryParse(f, out var fv) ? fv : 10;

    WindowInfo? window = null;
    if (flags.TryGetValue("window", out var wname) && !string.IsNullOrEmpty(wname))
    {
        window = WindowFinder.Find(wname);
        if (window == null) { WriteJson(new { ok = false, error = $"No visible window matching '{wname}'." }); return 1; }
    }

    int maxWidth = flags.TryGetValue("maxwidth", out var mw) && int.TryParse(mw, out var mwv) ? mwv : 640;

    var rect = Capture.ResolveRect(window);
    string outPath = flags.TryGetValue("out", out var o) && !string.IsNullOrEmpty(o)
        ? o!
        : DefaultCapturePath("gif");

    string saved = Capture.SaveGif(rect, seconds, fps, outPath, maxWidth);
    WriteJson(new { ok = true, path = Path.GetFullPath(saved), seconds, fps, maxWidth, window = window?.Title });
    return 0;
}

static bool TryParseButton(string name, out InputSimulator.MouseButton button)
{
    switch (name.ToLowerInvariant())
    {
        case "left": button = InputSimulator.MouseButton.Left; return true;
        case "right": button = InputSimulator.MouseButton.Right; return true;
        case "middle": button = InputSimulator.MouseButton.Middle; return true;
        default: button = InputSimulator.MouseButton.Left; return false;
    }
}

static string DefaultCapturePath(string ext)
{
    string dir = Path.Combine(Path.GetTempPath(), "hidbridge-captures");
    Directory.CreateDirectory(dir);
    string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
    return Path.Combine(dir, $"capture-{stamp}.{ext}");
}

static (Dictionary<string, string?> flags, List<string> positionals) ParseArgs(string[] rest)
{
    var flags = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    var positionals = new List<string>();

    for (int i = 0; i < rest.Length; i++)
    {
        var a = rest[i];
        if (a.StartsWith("--"))
        {
            string key = a[2..];
            if (i + 1 < rest.Length && !rest[i + 1].StartsWith("--"))
            {
                flags[key] = rest[i + 1];
                i++;
            }
            else
            {
                flags[key] = null;
            }
        }
        else
        {
            positionals.Add(a);
        }
    }

    return (flags, positionals);
}

static void WriteJson(object payload)
{
    Console.WriteLine(JsonSerializer.Serialize(payload));
}

static void PrintUsage()
{
    Console.WriteLine("""
    hidbridge - HID-style input + capture bridge for AI CLI agents

    WINDOW
      hidbridge window list
      hidbridge window find <substring>
      hidbridge window focus <substring>
      hidbridge window active                          (what currently has OS focus)
      hidbridge window rect <substring>                 (client-area rect in screen coords)

    KEYBOARD
      hidbridge key press <name> [--hold ms]
      hidbridge key down <name>
      hidbridge key up <name>
      hidbridge key hold <name> <ms>
      hidbridge type <text>
      hidbridge keys                     (list known key names)

    MOUSE
      hidbridge mouse move <x> <y> [--relative]
      hidbridge mouse click <left|right|middle>
      hidbridge mouse down <left|right|middle>
      hidbridge mouse up <left|right|middle>
      hidbridge mouse scroll <delta>

    CAPTURE
      hidbridge screenshot [--window <substring>] [--out <path>]
      hidbridge gif <seconds 1-5> [--fps 10] [--maxwidth 640] [--window <substring>] [--out <path>]

    All commands print a single JSON line to stdout: {"ok": true/false, ...}
    """);
}
