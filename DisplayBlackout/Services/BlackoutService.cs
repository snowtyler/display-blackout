using Microsoft.UI.Windowing;

namespace DisplayBlackout.Services;

public sealed partial class BlackoutService : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly Dictionary<ulong, BlackoutOverlay> _blackoutOverlays = [];
    private HashSet<ulong>? _selectedMonitorIds;
    private bool _isBlackedOut;
    private bool _disposed;

    public BlackoutService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _selectedMonitorIds = _settingsService.LoadSelectedMonitorIds();
    }

    public bool IsBlackedOut => _isBlackedOut;

    public event EventHandler<BlackoutStateChangedEventArgs>? BlackoutStateChanged;

    /// <summary>
    /// Updates which monitors should be blacked out. Null means default (all non-primary).
    /// </summary>
    public void UpdateSelectedMonitors(HashSet<ulong>? monitorIds)
    {
        _selectedMonitorIds = monitorIds;
        _settingsService.SaveSelectedMonitorIds(monitorIds);
    }

    /// <summary>
    /// Gets the currently selected monitor IDs for UI initialization.
    /// </summary>
    public IReadOnlySet<ulong>? SelectedMonitorIds => _selectedMonitorIds;

    public void Toggle()
    {
        if (_isBlackedOut)
        {
            Restore();
        }
        else
        {
            BlackOut();
        }
    }

    public void BlackOut()
    {
        if (_isBlackedOut) return;

        var displays = DisplayArea.FindAll();
        var primaryId = DisplayArea.Primary?.DisplayId.Value;

        // Use indexed loop - foreach throws InvalidCastException due to CsWinRT bug:
        // https://github.com/microsoft/WindowsAppSDK/issues/3484
        for (int i = 0; i < displays.Count; i++)
        {
            var display = displays[i];
            var displayId = display.DisplayId.Value;

            // If selection is set, use it; otherwise default to all non-primary
            bool shouldBlackOut = _selectedMonitorIds != null
                ? _selectedMonitorIds.Contains(displayId)
                : displayId != primaryId;

            if (!shouldBlackOut) continue;

            var overlay = new BlackoutOverlay(display.OuterBounds);
            _blackoutOverlays[displayId] = overlay;
        }

        _isBlackedOut = true;
        BlackoutStateChanged?.Invoke(this, new BlackoutStateChangedEventArgs(true));
    }

    public void Restore()
    {
        if (!_isBlackedOut) return;

        foreach (var overlay in _blackoutOverlays.Values)
        {
            overlay.Dispose();
        }
        _blackoutOverlays.Clear();

        _isBlackedOut = false;
        BlackoutStateChanged?.Invoke(this, new BlackoutStateChangedEventArgs(false));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Restore();
    }
}

public sealed class BlackoutStateChangedEventArgs(bool isBlackedOut) : EventArgs
{
    public bool IsBlackedOut { get; } = isBlackedOut;
}
