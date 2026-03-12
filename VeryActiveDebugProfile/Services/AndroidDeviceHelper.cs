using System.Collections.Frozen;

namespace VeryActiveDebugProfile.Services;

public static class AndroidDeviceHelper
{
    // FrozenDictionary is optimized for read-heavy lookup scenarios 
    // where the set of keys never changes after initialization.
    private static readonly FrozenDictionary<string, string> AndroidVendorIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "18D1", "Google (Nexus/Pixel/Generic)" },
        { "04E8", "Samsung" },
        { "12D1", "Huawei" },
        { "22B8", "Motorola" },
        { "1004", "LG Electronics" },
        { "2717", "Xiaomi" },
        { "0BB4", "HTC" },
        { "054C", "Sony" },
        { "22D9", "OPPO / OnePlus / Vivo" },
        { "0B05", "ASUS" },
        { "17EF", "Lenovo" },
        { "19D2", "ZTE" },
        { "0FCE", "Sony Ericsson" },
        { "0489", "Foxconn" },
        { "0955", "NVIDIA (Shield)" }
    }.ToFrozenDictionary();

    /// <summary>
    /// Identifies the manufacturer from a string formatted like "04E8"
    /// </summary>
    public static string GetManufacturer(string vid)
    {
        return AndroidVendorIds.TryGetValue(vid, out var manufacturer)
            ? manufacturer
            : "Unknown Vendor";
    }

    /// <summary>
    /// Identifies the manufacturer from a string formatted like "VID_04E8&PID_6860"
    /// </summary>
    /// <param name="hardwareId">The hardware ID string containing the VID and PID information.</param>
    /// <returns>The manufacturer name if found; otherwise, "Unknown".</returns>
    public static string? GetManufacturerFromHardwareId(string hardwareId)
    {
        if (string.IsNullOrWhiteSpace(hardwareId))
            return "Unknown";

        // Find the index of "VID_"
        int vidIndex = hardwareId.IndexOf("VID_", StringComparison.OrdinalIgnoreCase);

        // Ensure there are at least 4 characters after "VID_"
        if (vidIndex != -1 && hardwareId.Length >= vidIndex + 8)
        {
            // Extract the 4 hex characters following "VID_"
            string vid = hardwareId.Substring(vidIndex + 4, 4);

            if (AndroidVendorIds.TryGetValue(vid, out string? manufacturer))
            {
                return manufacturer;
            }
        }

        return null;
    }
}