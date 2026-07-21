using System.Diagnostics;
using System.Text;
using static HidBridge.NativeMethods;

namespace HidBridge;

internal sealed record WindowInfo(IntPtr Handle, string Title, string ProcessName, uint ProcessId);

internal static class WindowFinder
{
    public static List<WindowInfo> ListVisibleWindows()
    {
        var results = new List<WindowInfo>();

        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;

            int len = GetWindowTextLength(hWnd);
            if (len == 0) return true;

            var sb = new StringBuilder(len + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            string title = sb.ToString();
            if (string.IsNullOrWhiteSpace(title)) return true;

            GetWindowThreadProcessId(hWnd, out uint pid);
            string procName = "";
            try { procName = Process.GetProcessById((int)pid).ProcessName; } catch { /* process may have exited */ }

            results.Add(new WindowInfo(hWnd, title, procName, pid));
            return true;
        }, IntPtr.Zero);

        return results;
    }

    /// <summary>Finds the first visible window whose title or process name contains the given substring (case-insensitive).</summary>
    public static WindowInfo? Find(string needle)
    {
        return ListVisibleWindows().FirstOrDefault(w =>
            w.Title.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
            w.ProcessName.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    public static bool Focus(IntPtr hWnd)
    {
        if (IsIconic(hWnd)) ShowWindow(hWnd, SW_RESTORE);
        ShowWindow(hWnd, SW_SHOW);
        return SetForegroundWindow(hWnd);
    }

    public static RECT GetWindowRect(IntPtr hWnd)
    {
        NativeMethods.GetWindowRect(hWnd, out RECT rect);
        return rect;
    }

    /// <summary>Client area (excludes title bar/borders) translated into screen coordinates.</summary>
    public static RECT GetClientRectOnScreen(IntPtr hWnd)
    {
        GetClientRect(hWnd, out RECT client);
        var topLeft = new POINT { X = 0, Y = 0 };
        ClientToScreen(hWnd, ref topLeft);
        return new RECT
        {
            Left = topLeft.X,
            Top = topLeft.Y,
            Right = topLeft.X + (client.Right - client.Left),
            Bottom = topLeft.Y + (client.Bottom - client.Top)
        };
    }

    public static IntPtr GetForeground() => GetForegroundWindow();
}
