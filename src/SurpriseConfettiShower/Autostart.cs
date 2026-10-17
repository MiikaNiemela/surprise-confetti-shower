using Microsoft.Win32;

namespace SurpriseConfettiShower;

/// <summary>The "start with Windows" setting, stored as a per-user Run entry in the registry.</summary>
public static class Autostart
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SurpriseConfettiShower";

    public static void Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key is null)
            {
                return;
            }

            if (enabled)
            {
                key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception)
        {
            // Autostart is a convenience: a locked-down or unwritable registry must not kill the app.
        }
    }
}
