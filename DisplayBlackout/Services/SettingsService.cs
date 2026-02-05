using Windows.Storage;

namespace DisplayBlackout.Services;

public sealed class SettingsService
{
    private const string SelectedMonitorsKey = "SelectedMonitors_v2"; // v2 = EDID-based keys
    private const string OpacityKey = "Opacity";
    private const string ClickThroughKey = "ClickThrough";
    private const int DefaultOpacity = 100;
    private const bool DefaultClickThrough = false;

    private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

    public HashSet<string>? LoadSelectedMonitors()
    {
        if (_localSettings.Values.TryGetValue(SelectedMonitorsKey, out var value) && value is string str)
        {
            var keys = new HashSet<string>();
            foreach (var part in str.Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                keys.Add(part);
            }
            return keys.Count > 0 ? keys : null;
        }

        return null;
    }

    public void SaveSelectedMonitors(HashSet<string>? monitorKeys)
    {
        if (monitorKeys is null || monitorKeys.Count == 0)
        {
            _localSettings.Values.Remove(SelectedMonitorsKey);
        }
        else
        {
            _localSettings.Values[SelectedMonitorsKey] = string.Join('|', monitorKeys);
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
