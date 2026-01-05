using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT.Interop;

namespace MonitorBlanker.Services;

public sealed partial class HotkeyService : IDisposable
{
    private const int HotkeyId = 1;
    private const uint WM_HOTKEY = 0x0312;

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern LRESULT CallWindowProc(nint lpPrevWndFunc, HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam);

    private delegate LRESULT WndProcDelegate(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam);

    private HWND _hwnd;
    private WndProcDelegate? _newWndProc;
    private nint _oldWndProc;
    private bool _disposed;

    public event EventHandler? HotkeyPressed;

    ~HotkeyService()
    {
        Dispose(disposing: false);
    }

    public void Register(Window window)
    {
        _hwnd = (HWND)WindowNative.GetWindowHandle(window);

        // Subclass the window to receive WM_HOTKEY
        _newWndProc = WndProc;
        _oldWndProc = PInvoke.SetWindowLongPtr(_hwnd, WINDOW_LONG_PTR_INDEX.GWL_WNDPROC,
            Marshal.GetFunctionPointerForDelegate(_newWndProc));

        // Register Win+Shift+B
        if (!PInvoke.RegisterHotKey(
            _hwnd,
            HotkeyId,
            HOT_KEY_MODIFIERS.MOD_WIN | HOT_KEY_MODIFIERS.MOD_SHIFT,
            (uint)VIRTUAL_KEY.VK_B))
        {
            int error = Marshal.GetLastPInvokeError();
            throw new Win32Exception(error, "Failed to register hotkey Win+Shift+B. It may be in use by another application.");
        }
    }

    private LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == WM_HOTKEY && (int)wParam.Value == HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            return new LRESULT(0);
        }

        return CallWindowProc(_oldWndProc, hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        // Unmanaged cleanup (runs in both disposing and finalizer)
        if (_hwnd != default)
        {
            PInvoke.UnregisterHotKey(_hwnd, HotkeyId);

            // Restore original window proc
            if (_oldWndProc != 0)
            {
                PInvoke.SetWindowLongPtr(_hwnd, WINDOW_LONG_PTR_INDEX.GWL_WNDPROC, _oldWndProc);
            }
        }
    }
}
