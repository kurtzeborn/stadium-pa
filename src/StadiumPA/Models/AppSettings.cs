namespace StadiumPA.Models;

/// <summary>
/// User settings persisted to %APPDATA%\StadiumPA\settings.json.
/// All properties have sensible defaults so the app works out of the box.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Path to national anthem audio file (mp3/wav).</summary>
    public string? AnthemFilePath { get; set; }

    /// <summary>Path to goal celebration audio file (mp3/wav).</summary>
    public string? GoalFilePath { get; set; }

    /// <summary>Duration of DIM / FADE OUT fades in milliseconds.</summary>
    public int FadeDurationMs { get; set; } = 1000;

    /// <summary>Volume level for DIM (0.0–1.0).</summary>
    public float DimLevel { get; set; } = 0.10f;

    /// <summary>Master volume on startup (0.0–1.0).</summary>
    public float DefaultMasterVolume { get; set; } = 0.80f;

    /// <summary>Spotify volume on startup (0.0–1.0).</summary>
    public float DefaultSpotifyVolume { get; set; } = 0.80f;

    /// <summary>Keep window above all others.</summary>
    public bool AlwaysOnTop { get; set; } = true;
}
