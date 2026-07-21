namespace HidBridge;

internal static class Keys
{
    // Friendly name -> Virtual-Key code (Win32 VK_*)
    private static readonly Dictionary<string, ushort> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // letters
        ["A"] = 0x41, ["B"] = 0x42, ["C"] = 0x43, ["D"] = 0x44, ["E"] = 0x45,
        ["F"] = 0x46, ["G"] = 0x47, ["H"] = 0x48, ["I"] = 0x49, ["J"] = 0x4A,
        ["K"] = 0x4B, ["L"] = 0x4C, ["M"] = 0x4D, ["N"] = 0x4E, ["O"] = 0x4F,
        ["P"] = 0x50, ["Q"] = 0x51, ["R"] = 0x52, ["S"] = 0x53, ["T"] = 0x54,
        ["U"] = 0x55, ["V"] = 0x56, ["W"] = 0x57, ["X"] = 0x58, ["Y"] = 0x59, ["Z"] = 0x5A,

        // digits (top row)
        ["0"] = 0x30, ["1"] = 0x31, ["2"] = 0x32, ["3"] = 0x33, ["4"] = 0x34,
        ["5"] = 0x35, ["6"] = 0x36, ["7"] = 0x37, ["8"] = 0x38, ["9"] = 0x39,

        // function keys
        ["F1"] = 0x70, ["F2"] = 0x71, ["F3"] = 0x72, ["F4"] = 0x73,
        ["F5"] = 0x74, ["F6"] = 0x75, ["F7"] = 0x76, ["F8"] = 0x77,
        ["F9"] = 0x78, ["F10"] = 0x79, ["F11"] = 0x7A, ["F12"] = 0x7B,

        // navigation / control
        ["UP"] = 0x26, ["DOWN"] = 0x28, ["LEFT"] = 0x25, ["RIGHT"] = 0x27,
        ["ENTER"] = 0x0D, ["RETURN"] = 0x0D, ["ESC"] = 0x1B, ["ESCAPE"] = 0x1B,
        ["SPACE"] = 0x20, ["TAB"] = 0x09, ["BACKSPACE"] = 0x08, ["DELETE"] = 0x2E, ["DEL"] = 0x2E,
        ["INSERT"] = 0x2D, ["HOME"] = 0x24, ["END"] = 0x23, ["PAGEUP"] = 0x21, ["PAGEDOWN"] = 0x22,

        // modifiers (generic + left/right specific)
        ["SHIFT"] = 0x10, ["LSHIFT"] = 0xA0, ["RSHIFT"] = 0xA1,
        ["CTRL"] = 0x11, ["CONTROL"] = 0x11, ["LCTRL"] = 0xA2, ["RCTRL"] = 0xA3,
        ["ALT"] = 0x12, ["LALT"] = 0xA4, ["RALT"] = 0xA5,
        ["WIN"] = 0x5B, ["LWIN"] = 0x5B, ["RWIN"] = 0x5C,

        // punctuation commonly needed (e.g. typing an IP:port)
        ["PERIOD"] = 0xBE, ["."] = 0xBE, ["COMMA"] = 0xBC, [","] = 0xBC,
        ["COLON"] = 0xBA, [":"] = 0xBA, ["MINUS"] = 0xBD, ["-"] = 0xBD,
    };

    // Keys whose scan code must carry the "extended" prefix bit (SendInput's
    // KEYEVENTF_EXTENDEDKEY) to be disambiguated from their non-extended twin — e.g. the
    // dedicated Up arrow and Numpad-8 share the same base scan code (0x48); only the
    // extended bit tells a scancode-reading app (GLFW's Win32 backend, notably) which one
    // was pressed. MapVirtualKey's "extended" mapping mode is not reliable for this on all
    // systems, so this list is maintained explicitly per the standard PS/2 Set 1 table.
    private static readonly HashSet<ushort> ExtendedVks = new()
    {
        0x26, 0x28, 0x25, 0x27, // UP DOWN LEFT RIGHT
        0x2D, 0x2E, 0x24, 0x23, 0x21, 0x22, // INSERT DELETE HOME END PAGEUP PAGEDOWN
        0xA3, 0xA5, // RCTRL RALT
        0x5B, 0x5C, // LWIN RWIN
    };

    public static bool TryGet(string name, out ushort vk) => Map.TryGetValue(name, out vk);

    public static bool IsExtended(ushort vk) => ExtendedVks.Contains(vk);

    public static IEnumerable<string> KnownNames => Map.Keys;
}
