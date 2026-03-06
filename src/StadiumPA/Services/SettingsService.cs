using System.IO;
using System.Text.Json;
using StadiumPA.Models;

namespace StadiumPA.Services;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> to a JSON file
/// at %APPDATA%\StadiumPA\settings.json.
/// </summary>
public static class SettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StadiumPA");

    private static readonly string SettingsFile =
        Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Loads settings from disk, returning defaults if the file doesn't exist or is corrupt.
    /// </summary>
    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFile))
                return new AppSettings();

            var json = File.ReadAllText(SettingsFile);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            // Corrupt file — return defaults so the app still launches
            return new AppSettings();
        }
    }

    /// <summary>
    /// Saves settings to disk. Creates the directory if it doesn't exist.
    /// Failures are silently ignored (non-critical).
    /// </summary>
    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFile, json);
        }
        catch
        {
            // Non-critical — settings will be lost but app continues to work
        }
    }
}
