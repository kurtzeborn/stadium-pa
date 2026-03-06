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

    // Cached session avoids repeated Process.GetProcessesByName + session
    // enumeration on every call — critical during fades (~20 calls/sec).
    private AudioSessionControl? _cachedSession;
    private uint _cachedSessionPid;

    public SpotifyVolumeService()
    {
        _enumerator = new MMDeviceEnumerator();
        _device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
    }

    /// <summary>
    /// Returns true if a Spotify process is currently running.
    /// </summary>
    public bool IsSpotifyRunning => FindSpotifyProcessIds().Count > 0;

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
    /// Returns the set of all Spotify process IDs (Spotify is Electron-based
    /// and spawns multiple processes — the audio session can belong to any of them).
    /// </summary>
    private static HashSet<uint> FindSpotifyProcessIds()
    {
        var processes = Process.GetProcessesByName("Spotify");
        try
        {
            var pids = new HashSet<uint>(processes.Length);
            foreach (var p in processes)
                pids.Add((uint)p.Id);
            return pids;
        }
        finally
        {
            foreach (var p in processes) p.Dispose();
        }
    }

    /// <summary>
    /// Finds the audio session belonging to Spotify by matching process ID.
    /// Caches the result to avoid expensive Process.GetProcessesByName and
    /// session enumeration on every call. Cache is validated by checking
    /// the stored PID via a lightweight COM property get.
    /// </summary>
    private AudioSessionControl? GetSpotifySession()
    {
        // Try cached session first (single COM property get vs full enumeration)
        if (_cachedSession is not null)
        {
            try
            {
                if (_cachedSession.GetProcessID == _cachedSessionPid)
                    return _cachedSession;
            }
            catch
            {
                // COM object went stale (Spotify exited)
            }
            _cachedSession = null;
        }

        var spotifyPids = FindSpotifyProcessIds();
        if (spotifyPids.Count == 0) return null;

        var sessions = _device.AudioSessionManager.Sessions;
        for (int i = 0; i < sessions.Count; i++)
        {
            var session = sessions[i];
            if (spotifyPids.Contains(session.GetProcessID))
            {
                _cachedSession = session;
                _cachedSessionPid = session.GetProcessID;
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
