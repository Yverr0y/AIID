using static HidBridge.NativeMethods;

namespace HidBridge;

internal static class InputSimulator
{
    public static void KeyDown(ushort vk) => SendKey(vk, keyUp: false);
    public static void KeyUp(ushort vk) => SendKey(vk, keyUp: true);

    public static void KeyPress(ushort vk, int holdMs = 40)
    {
        KeyDown(vk);
        Thread.Sleep(holdMs);
        KeyUp(vk);
    }

    public static void KeyHold(ushort vk, int durationMs)
    {
        KeyDown(vk);
        Thread.Sleep(durationMs);
        KeyUp(vk);
    }

    /// <summary>Press each key in sequence with a small gap (chord-free, sequential typing of named keys).</summary>
    public static void TypeText(string text, int perCharDelayMs = 30)
    {
        foreach (var ch in text)
        {
            SendUnicodeChar(ch);
            Thread.Sleep(perCharDelayMs);
        }
    }

    private static void SendKey(ushort vk, bool keyUp)
    {
        // Send by hardware scan code (with the real extended-key prefix byte where
        // applicable) rather than letting Windows auto-translate the VK code. Apps that
        // resolve keys from the raw scancode in the message lParam (e.g. GLFW's Win32
        // backend) otherwise can't tell VK_UP from the Numpad-8 key, since both share
        // scancode 0x48 and only the extended-key bit disambiguates them.
        uint scan = MapVirtualKey(vk, MAPVK_VK_TO_VSC_EX);
        byte scanCode = (byte)(scan & 0xFF);
        bool extended = (scan & 0xFF00) == 0xE000 || (scan & 0xFF00) == 0xE100 || Keys.IsExtended(vk);

        uint flags = KEYEVENTF_SCANCODE;
        if (keyUp) flags |= KEYEVENTF_KEYUP;
        if (extended) flags |= KEYEVENTF_EXTENDEDKEY;

        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = scanCode,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
        SendInput(1, new[] { input }, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
    }

    private static void SendUnicodeChar(char ch)
    {
        var down = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT { wVk = 0, wScan = ch, dwFlags = KEYEVENTF_UNICODE, time = 0, dwExtraInfo = IntPtr.Zero }
            }
        };
        var up = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT { wVk = 0, wScan = ch, dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP, time = 0, dwExtraInfo = IntPtr.Zero }
            }
        };
        SendInput(1, new[] { down }, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
        SendInput(1, new[] { up }, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
    }

    // ---- Mouse ----

    public enum MouseButton { Left, Right, Middle }

    public static void MouseMoveAbsolute(int x, int y)
    {
        // Normalize to 0..65535 across the primary screen, per SendInput's MOUSEEVENTF_ABSOLUTE contract.
        int sw = NativeScreen.Width;
        int sh = NativeScreen.Height;
        int normX = (int)Math.Round(x * 65535.0 / Math.Max(1, sw - 1));
        int normY = (int)Math.Round(y * 65535.0 / Math.Max(1, sh - 1));

        var input = new INPUT
        {
            type = INPUT_MOUSE,
            u = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dx = normX,
                    dy = normY,
                    mouseData = 0,
                    dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
        SendInput(1, new[] { input }, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
    }

    public static void MouseMoveRelative(int dx, int dy)
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            u = new InputUnion
            {
                mi = new MOUSEINPUT { dx = dx, dy = dy, mouseData = 0, dwFlags = MOUSEEVENTF_MOVE, time = 0, dwExtraInfo = IntPtr.Zero }
            }
        };
        SendInput(1, new[] { input }, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
    }

    public static void MouseButtonDown(MouseButton button) => SendMouseButton(button, down: true);
    public static void MouseButtonUp(MouseButton button) => SendMouseButton(button, down: false);

    public static void MouseClick(MouseButton button)
    {
        MouseButtonDown(button);
        Thread.Sleep(30);
        MouseButtonUp(button);
    }

    public static void MouseScroll(int delta)
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            u = new InputUnion
            {
                mi = new MOUSEINPUT { dx = 0, dy = 0, mouseData = (uint)(delta * 120), dwFlags = MOUSEEVENTF_WHEEL, time = 0, dwExtraInfo = IntPtr.Zero }
            }
        };
        SendInput(1, new[] { input }, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
    }

    private static void SendMouseButton(MouseButton button, bool down)
    {
        uint flag = button switch
        {
            MouseButton.Left => down ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP,
            MouseButton.Right => down ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP,
            MouseButton.Middle => down ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP,
            _ => throw new ArgumentOutOfRangeException(nameof(button))
        };
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            u = new InputUnion
            {
                mi = new MOUSEINPUT { dx = 0, dy = 0, mouseData = 0, dwFlags = flag, time = 0, dwExtraInfo = IntPtr.Zero }
            }
        };
        SendInput(1, new[] { input }, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
    }

}

internal static class NativeScreen
{
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    public static int Width => GetSystemMetrics(SM_CXSCREEN);
    public static int Height => GetSystemMetrics(SM_CYSCREEN);
}
