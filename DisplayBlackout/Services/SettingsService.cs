using Windows.Storage;

namespace DisplayBlackout.Services;

public sealed class SettingsService
{
    private const string SelectedMonitorIdsKey = "SelectedMonitorIds";

    private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

    public HashSet<ulong>? LoadSelectedMonitorIds()
    {
        if (_localSettings.Values.TryGetValue(SelectedMonitorIdsKey, out var value) && value is string str)
        {
            var ids = new HashSet<ulong>();
            foreach (var part in str.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (ulong.TryParse(part, out var id))
                {
                    ids.Add(id);
                }
            }
            return ids;
        }

        return null;
    }

    public void SaveSelectedMonitorIds(HashSet<ulong>? monitorIds)
    {
        if (monitorIds is null)
        {
            _localSettings.Values.Remove(SelectedMonitorIdsKey);
        }
        else
        {
            _localSettings.Values[SelectedMonitorIdsKey] = string.Join(',', monitorIds);
        }
    }
}
