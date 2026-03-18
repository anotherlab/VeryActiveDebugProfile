using System.Management;

namespace VeryActiveDebugProfile.Services;

// Querying Win32_PnPEntity for common Android identifiers
// For my phone, the service is "WUDFWpdMtp" and description is "SM-908U"
// https://learn.microsoft.com/en-us/windows/win32/wmisdk/wql-sql-for-wmi

// If you search for Android devices, it's a more accurate query to look for Android devices
// but it doesn't return the model name, just something like "SAMSUNG Android ADB Interface".
// Searching for the WUDFWpdMtp service returns the model name but may be less reliable
// across different devices. Adjust as needed for your specific devices and drivers.
// Sample query for Android devices:

// SELECT Description, Name, Manufacturer, Service, PNPClass, PNPDeviceID FROM Win32_PnPEntity WHERE Description LIKE '%Android%'

/* PowerShell command to find Android devices:
 * Get-PnpDevice -FriendlyName "*Android*" -Status OK | Select-Object -Property FriendlyName, Service, PNPDeviceID
 * or
 * Get-CimInstance -ClassName Win32_PnPEntity |
 *     Where-Object { $_.Service -eq 'WUDFWpdMtp' } |
 *     Select-Object Description, DeviceID, PNPDeviceID, Manufacturer
 * 
 * This can help you identify the correct WMI query for your specific devices.
 * https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_wql?view=powershell-7.5
 */

/// <summary>
/// Class to help identify connected devices using WMI queries. 
/// This is used to find the model name of connected Android devices, 
/// but can be adapted for other types of devices as well.
/// </summary>
public class PnpHelper
{
    public static string GetVendorAndProductfromPath(string devicePath)
    {
        string cleanPath = devicePath.Replace(@"\\?\", "");

        // 2. Split by '#' to isolate the components
        string[] parts = cleanPath.Split('#');

        // 3. Return the vendor and product id
        string result = $"{parts[1]}";

        return result;
    }

    public static string? GetDeviceName(string ThisDevice =  "VID")
    {
        try
        {
            var query = $"SELECT Description, Service, PNPDeviceID, Present FROM Win32_PnPEntity " +
                        "WHERE (Service='WUDFWpdMtp') " +
                        "AND PNPDeviceID LIKE '%" + ThisDevice + "%'" +
                        "AND Present = True";

            // Walk through the results and return the description of the first matching device
            foreach (var mo in new ManagementObjectSearcher(null, query).Get().OfType<ManagementObject>())
            {
                return mo.Properties["Description"].Value.ToString();
            }
        }
        catch (InvalidCastException)
        {
            // Some property couldn't be cast. This can happen when no rows are returned
            // Just ignore it
        }

        return null;
    }
}
