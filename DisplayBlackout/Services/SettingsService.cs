using Windows.Graphics;
using Windows.Storage;

namespace DisplayBlackout.Services;

public sealed class SettingsService
{
    private const string SelectedMonitorBoundsKey = "SelectedMonitorBounds";
    private const string OpacityKey = "Opacity";
    private const string ClickThroughKey = "ClickThrough";
    private const int DefaultOpacity = 100;
    private const bool DefaultClickThrough = false;

    private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

    /// <summary>
    /// Creates a stable identifier for a monitor based on its bounds.
    /// Format: "X,Y,W,H" (e.g., "0,0,1920,1080")
    /// </summary>
    public static string GetMonitorKey(RectInt32 bounds)
        => $"{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}";

    public HashSet<string>? LoadSelectedMonitorBounds()
    {
        if (_localSettings.Values.TryGetValue(SelectedMonitorBoundsKey, out var value) && value is string str)
        {
            var bounds = new HashSet<string>();
            foreach (var part in str.Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                bounds.Add(part);
            }
            return bounds.Count > 0 ? bounds : null;
        }

        return null;
    }

    public void SaveSelectedMonitorBounds(HashSet<string>? monitorBounds)
    {
        if (monitorBounds is null || monitorBounds.Count == 0)
        {
            _localSettings.Values.Remove(SelectedMonitorBoundsKey);
        }
        else
        {
            _localSettings.Values[SelectedMonitorBoundsKey] = string.Join('|', monitorBounds);
        }
    }

    public int LoadOpacity()
    {
        if (_localSettings.Values.TryGetValue(OpacityKey, out var value) && value is int opacity)
        {
            return Math.Clamp(opacity, 0, 100);
        }
        return DefaultOpacity;
    }

    public void SaveOpacity(int opacity)
    {
        _localSettings.Values[OpacityKey] = Math.Clamp(opacity, 0, 100);
    }

    public bool LoadClickThrough()
    {
        if (_localSettings.Values.TryGetValue(ClickThroughKey, out var value) && value is bool clickThrough)
        {
            return clickThrough;
        }
        return DefaultClickThrough;
    }

    public void SaveClickThrough(bool clickThrough)
    {
        _localSettings.Values[ClickThroughKey] = clickThrough;
    }
}
