using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace DisplayBlackout.Services;

public sealed partial class BlackoutService : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly Dictionary<ulong, BlackoutOverlay> _blackoutOverlays = [];
    private HashSet<string>? _selectedMonitorKeys;
    private Dictionary<ulong, string>? _displayAreaKeys;
    private int _opacity;
    private bool _clickThrough;
    private bool _isBlackedOut;
    private bool _disposed;

    public BlackoutService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _selectedMonitorKeys = _settingsService.LoadSelectedMonitors();
        _opacity = _settingsService.LoadOpacity();
        _clickThrough = _settingsService.LoadClickThrough();
    }

    public bool IsBlackedOut => _isBlackedOut;

    public event EventHandler<BlackoutStateChangedEventArgs>? BlackoutStateChanged;

    /// <summary>
    /// Initializes EDID-based display keys. Call this once at startup.
    /// </summary>
    public async Task InitializeAsync()
    {
        _displayAreaKeys = await DisplayMonitorHelper.GetDisplayAreaKeysAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Updates which monitors should be blacked out using their stable EDID-based keys.
    /// Null means default (all non-primary).
    /// </summary>
    public void UpdateSelectedMonitors(HashSet<string>? monitorKeys)
    {
        _selectedMonitorKeys = monitorKeys;
        _settingsService.SaveSelectedMonitors(monitorKeys);

        // Refresh overlays if currently blacked out
        if (_isBlackedOut)
        {
            RefreshOverlays();
        }
    }

    private void RefreshOverlays()
    {
        if (_displayAreaKeys == null) return;

        var displays = DisplayArea.FindAll();
        var primaryId = DisplayArea.Primary?.DisplayId.Value;
        var currentDisplayIds = new HashSet<ulong>();

        for (int i = 0; i < displays.Count; i++)
        {
            var display = displays[i];
            var displayId = display.DisplayId.Value;
            var bounds = display.OuterBounds;
            currentDisplayIds.Add(displayId);

            // Get the EDID-based key for this display
            _displayAreaKeys.TryGetValue(displayId, out var monitorKey);

            bool shouldBlackOut = _selectedMonitorKeys != null && monitorKey != null
                ? _selectedMonitorKeys.Contains(monitorKey)
                : displayId != primaryId;

            bool hasOverlay = _blackoutOverlays.ContainsKey(displayId);

            if (shouldBlackOut && !hasOverlay)
            {
                // Add overlay for newly selected monitor
                var overlay = new BlackoutOverlay(bounds, _opacity, _clickThrough);
                _blackoutOverlays[displayId] = overlay;
            }
            else if (!shouldBlackOut && hasOverlay)
            {
                // Remove overlay for deselected monitor
                _blackoutOverlays[displayId].Dispose();
                _blackoutOverlays.Remove(displayId);
            }
        }

        // Remove overlays for displays that no longer exist
        var toRemove = _blackoutOverlays.Keys.Where(id => !currentDisplayIds.Contains(id)).ToList();
        foreach (var id in toRemove)
        {
            _blackoutOverlays[id].Dispose();
            _blackoutOverlays.Remove(id);
        }
    }

    /// <summary>
    /// Checks if a monitor with the given EDID key is selected for blackout.
    /// </summary>
    public bool IsMonitorSelected(string monitorKey)
    {
        if (_selectedMonitorKeys is null) return false;
        return _selectedMonitorKeys.Contains(monitorKey);
    }

    /// <summary>
    /// Gets the currently selected monitor keys for UI initialization.
    /// </summary>
    public IReadOnlySet<string>? SelectedMonitorKeys => _selectedMonitorKeys;

    /// <summary>
    /// Gets the cached display area keys (EDID-based).
    /// </summary>
    public IReadOnlyDictionary<ulong, string>? DisplayAreaKeys => _displayAreaKeys;

    /// <summary>
    /// Gets the current opacity percentage (0-100).
    /// </summary>
    public int Opacity => _opacity;

    /// <summary>
    /// Gets whether click-through is enabled.
    /// </summary>
    public bool ClickThrough => _clickThrough;

    /// <summary>
    /// Updates the opacity of the blackout overlays.
    /// </summary>
    public void UpdateOpacity(int opacity)
    {
        _opacity = Math.Clamp(opacity, 0, 100);
        _settingsService.SaveOpacity(_opacity);

        // Update existing overlays
        foreach (var overlay in _blackoutOverlays.Values)
        {
            overlay.SetOpacity(_opacity);
        }
    }

    /// <summary>
    /// Updates whether the overlay is click-through.
    /// </summary>
    public void UpdateClickThrough(bool clickThrough)
    {
        _clickThrough = clickThrough;
        _settingsService.SaveClickThrough(_clickThrough);

        // Update existing overlays
        foreach (var overlay in _blackoutOverlays.Values)
        {
            overlay.SetClickThrough(_clickThrough);
        }
    }

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
            var bounds = display.OuterBounds;

            // Get the hardware-based key for this display
            string? monitorKey = null;
            _displayAreaKeys?.TryGetValue(displayId, out monitorKey);

            // If selection is set, use it; otherwise default to all non-primary
            bool shouldBlackOut = _selectedMonitorKeys != null && monitorKey != null
                ? _selectedMonitorKeys.Contains(monitorKey)
                : displayId != primaryId;

            if (!shouldBlackOut) continue;

            var overlay = new BlackoutOverlay(bounds, _opacity, _clickThrough);
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
