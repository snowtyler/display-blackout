using Microsoft.UI.Windowing;
using Windows.Devices.Display;
using Windows.Devices.Enumeration;
using Windows.Graphics;

namespace DisplayBlackout.Services;

/// <summary>
/// Helper to get stable hardware-based identifiers for displays.
/// Uses DeviceId which contains PnP hardware path with vendor/product info.
/// </summary>
public static class DisplayMonitorHelper
{
    /// <summary>
    /// Gets hardware-based information for all connected monitors.
    /// </summary>
    public static async Task<Dictionary<string, MonitorInfo>> GetMonitorInfoAsync()
    {
        var result = new Dictionary<string, MonitorInfo>();

        try
        {
            // Enumerate all display monitors
            var devices = await DeviceInformation.FindAllAsync(DisplayMonitor.GetDeviceSelector()).AsTask().ConfigureAwait(false);

            foreach (var device in devices)
            {
                try
                {
                    var monitor = await DisplayMonitor.FromInterfaceIdAsync(device.Id).AsTask().ConfigureAwait(false);
                    if (monitor == null) continue;

                    var info = new MonitorInfo
                    {
                        InterfaceId = device.Id,
                        DeviceId = monitor.DeviceId,
                        DisplayName = monitor.DisplayName ?? device.Name ?? "Unknown",
                        Connector = monitor.PhysicalConnector.ToString()
                    };

                    // Build stable key from device ID
                    info.StableKey = BuildStableKey(info);

                    // Use interface ID as the lookup key
                    result[device.Id] = info;
                }
#pragma warning disable CA1031 // Catch all exceptions for resilience - skip monitors that fail
                catch (Exception)
                {
                    // Skip monitors that fail to load (e.g., disconnected during enumeration)
                }
#pragma warning restore CA1031
            }
        }
#pragma warning disable CA1031 // Catch all exceptions for resilience
        catch (Exception)
        {
            // If enumeration fails entirely, return empty dict
        }
#pragma warning restore CA1031

        return result;
    }

    /// <summary>
    /// Gets hardware-based keys for all current DisplayAreas.
    /// Falls back to bounds-based key if hardware ID is unavailable.
    /// </summary>
    public static async Task<Dictionary<ulong, string>> GetDisplayAreaKeysAsync()
    {
        var result = new Dictionary<ulong, string>();
        var displays = DisplayArea.FindAll();
        var monitorInfos = await GetMonitorInfoAsync().ConfigureAwait(false);

        // Group monitors by stable key to detect duplicates (identical monitors)
        var keyGroups = monitorInfos.Values
            .GroupBy(m => m.StableKey)
            .ToDictionary(g => g.Key, g => g.ToList());

        // For displays, we'll assign hardware keys
        // If multiple monitors have the same hardware key, append position-based suffix
        var usedKeys = new Dictionary<string, int>();

        for (int i = 0; i < displays.Count; i++)
        {
            var display = displays[i];
            var displayId = display.DisplayId.Value;
            var bounds = display.OuterBounds;

            // Try to find a matching monitor by matching count/order
            // This is imperfect but works for most setups
            string key;

            if (monitorInfos.Count == displays.Count && i < monitorInfos.Count)
            {
                // Same count - try positional matching after sorting both lists
                var sortedMonitors = monitorInfos.Values.OrderBy(m => m.InterfaceId).ToList();
                var sortedDisplays = new List<(DisplayArea display, int origIndex)>();
                for (int j = 0; j < displays.Count; j++)
                {
                    sortedDisplays.Add((displays[j], j));
                }
                sortedDisplays.Sort((a, b) => a.display.OuterBounds.X.CompareTo(b.display.OuterBounds.X));

                // Find this display's position in sorted order
                int sortedIndex = sortedDisplays.FindIndex(d => d.origIndex == i);
                if (sortedIndex >= 0 && sortedIndex < sortedMonitors.Count)
                {
                    var baseKey = sortedMonitors[sortedIndex].StableKey;

                    // Handle duplicate keys (identical monitors)
                    if (usedKeys.TryGetValue(baseKey, out int count))
                    {
                        key = $"{baseKey}:{count}";
                        usedKeys[baseKey] = count + 1;
                    }
                    else
                    {
                        key = baseKey;
                        usedKeys[baseKey] = 1;
                    }
                }
                else
                {
                    // Fallback to bounds
                    key = GetBoundsKey(bounds);
                }
            }
            else
            {
                // Count mismatch - fallback to bounds
                key = GetBoundsKey(bounds);
            }

            result[displayId] = key;
        }

        return result;
    }

    private static string BuildStableKey(MonitorInfo info)
    {
        // DeviceId contains PnP path like: \\?\DISPLAY#DELA1D4#5&12345#...
        // Extract the vendor/product portion (e.g., DELA1D4)
        if (!string.IsNullOrEmpty(info.DeviceId))
        {
            var parts = info.DeviceId.Split('#', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                // parts[0] = "\\?\DISPLAY" or similar, parts[1] = "DELA1D4" (vendor+product)
                return parts[1];
            }
        }

        // Fallback: use display name hash
        return $"UNKNOWN-{info.DisplayName.GetHashCode(StringComparison.Ordinal):X8}";
    }

    private static string GetBoundsKey(RectInt32 bounds)
        => $"BOUNDS:{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}";
}

public sealed class MonitorInfo
{
    public string InterfaceId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Connector { get; set; } = string.Empty;
    public string StableKey { get; set; } = string.Empty;
}
