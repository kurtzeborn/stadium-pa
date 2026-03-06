using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace StadiumPA.Services;

/// <summary>
/// Controls Spotify's per-process audio volume via Windows Core Audio API.
/// Finds the Spotify audio session and adjusts its <see cref="SimpleAudioVolume"/>.
/// </summary>
public sealed class SpotifyVolumeService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator;
    private readonly MMDevice _device;

    public SpotifyVolumeService()
    {
        _enumerator = new MMDeviceEnumerator();
        _device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
    }

    /// <summary>
    /// Returns true if a Spotify process is currently running.
    /// </summary>
    public bool IsSpotifyRunning => FindSpotifyProcessId() is not null;

    /// <summary>
    /// Returns true if Spotify is actively producing audio (session state is Active).
    /// Used by DIM/FADE OUT/KILL to avoid toggling Spotify that isn't playing.
    /// </summary>
    public bool IsSpotifyActive
    {
        get
        {
            var session = GetSpotifySession();
            return session?.State == AudioSessionState.AudioSessionStateActive;
        }
    }

    /// <summary>
    /// Gets or sets Spotify's per-process volume (0.0 to 1.0).
    /// Returns null if Spotify audio session is not found.
    /// </summary>
    public float? Volume
    {
        get => GetSpotifySession()?.SimpleAudioVolume.Volume;
        set
        {
            var session = GetSpotifySession();
            if (session is not null && value.HasValue)
            {
                session.SimpleAudioVolume.Volume = Math.Clamp(value.Value, 0f, 1f);
            }
        }
    }

    /// <summary>
    /// Finds the Spotify process ID, or null if not running.
    /// </summary>
    private static uint? FindSpotifyProcessId()
    {
        var processes = Process.GetProcessesByName("Spotify");
        try
        {
            return processes.Length > 0 ? (uint)processes[0].Id : null;
        }
        finally
        {
            foreach (var p in processes) p.Dispose();
        }
    }

    /// <summary>
    /// Finds the audio session belonging to Spotify by matching process ID.
    /// Gets the Spotify PID first, then scans sessions — avoids creating
    /// a Process object for every audio session.
    /// </summary>
    private AudioSessionControl? GetSpotifySession()
    {
        var spotifyPid = FindSpotifyProcessId();
        if (spotifyPid is null) return null;

        var sessions = _device.AudioSessionManager.Sessions;
        for (int i = 0; i < sessions.Count; i++)
        {
            var session = sessions[i];
            if (session.GetProcessID == spotifyPid.Value)
            {
                return session;
            }
        }

        return null;
    }

    public void Dispose()
    {
        _device?.Dispose();
        _enumerator?.Dispose();
    }
}
