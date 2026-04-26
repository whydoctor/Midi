using System.IO;
using System.Text.Json;

namespace Chordara.Services;

/// <summary>
/// Tiny JSON-backed user-preferences store at %APPDATA%/Chordara/settings.json.
/// Failures are swallowed: settings are convenience, not correctness.
/// </summary>
public sealed class AppSettings
{
    public string? SoundFontPath { get; set; }

    private static readonly string SettingsPath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Chordara",
            "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { /* ignore corrupted settings */ }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch { /* best effort */ }
    }
}
