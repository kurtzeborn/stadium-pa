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

    // Cached session avoids repeated session enumeration on every call
    // — critical during fades (~20 calls/sec).
    private AudioSessionControl? _cachedSession;
    private DateTime _cacheTimestamp;

    public SpotifyVolumeService()
    {
        _enumerator = new MMDeviceEnumerator();
        _device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
    }

    /// <summary>
    /// Forces the cached session to be cleared and re-discovered on next access.
    /// Useful when Spotify shifts audio between subprocesses or connection is lost.
    /// </summary>
    public void InvalidateCache()
    {
        _cachedSession = null;
    }

    /// <summary>
    /// Returns true if a Spotify process is currently running.
    /// </summary>
    public bool IsSpotifyRunning
    {
        get
        {
            // Fast path: if we have a cached active session, Spotify is running
            if (_cachedSession is not null) return true;
            return FindSpotifyProcessIds().Count > 0;
        }
    }

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
    /// Invalidates cache on get/set failures to force fresh lookup on next access.
    /// </summary>
    public float? Volume
    {
        get
        {
            try
            {
                var session = GetSpotifySession();
                if (session is null) return null;
                var vol = session.SimpleAudioVolume.Volume;
                return vol;
            }
            catch
            {
                // Cache became stale (Spotify shifted subprocess) — force refresh
                _cachedSession = null;
                return null;
            }
        }
        set
        {
            if (!value.HasValue) return;
            try
            {
                var session = GetSpotifySession();
                if (session is not null)
                {
                    session.SimpleAudioVolume.Volume = Math.Clamp(value.Value, 0f, 1f);
                }
            }
            catch
            {
                // Cache became stale — force refresh on next access
                _cachedSession = null;
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
    /// Finds the audio session belonging to Spotify by checking session identifiers
    /// for "Spotify.exe". This is more reliable than PID matching because Spotify
    /// (a Store/UWP app) can shift audio between subprocesses across tracks.
    /// Cache is time-limited to 3 seconds (reduced from 5) to handle subprocess shifts faster.
    /// Validates cached session is still alive AND has valid volume access before reusing.
    /// </summary>
    private AudioSessionControl? GetSpotifySession()
    {
        // Use cached session if it's fresh (< 3 seconds old) and still functional
        if (_cachedSession is not null && (DateTime.UtcNow - _cacheTimestamp).TotalSeconds < 3)
        {
            try
            {
                // Validate the session is still alive and accessible
                _ = _cachedSession.GetProcessID;
                _ = _cachedSession.SimpleAudioVolume.Volume; // Test volume access
                return _cachedSession;
            }
            catch
            {
                // Session became invalid — clear cache and search for new one
                _cachedSession = null;
            }
        }

        // Enumerate sessions and match by identifier containing "Spotify.exe"
        var sessions = _device.AudioSessionManager.Sessions;
        for (int i = 0; i < sessions.Count; i++)
        {
            var session = sessions[i];
            try
            {
                var id = session.GetSessionIdentifier;
                if (id is not null && id.Contains("Spotify.exe", StringComparison.OrdinalIgnoreCase))
                {
                    // Validate the session is accessible before caching
                    _ = session.SimpleAudioVolume.Volume;
                    _cachedSession = session;
                    _cacheTimestamp = DateTime.UtcNow;
                    return session;
                }
            }
            catch
            {
                // Skip sessions that throw on property access
            }
        }

        _cachedSession = null;
        return null;
    }

    public void Dispose()
    {
        _device?.Dispose();
        _enumerator?.Dispose();
    }

    /// <summary>
    /// Diagnostic: dumps all audio sessions and Spotify PIDs for troubleshooting.
    /// </summary>
    public string GetDiagnostics()
    {
        var sb = new System.Text.StringBuilder();

        var pids = FindSpotifyProcessIds();
        sb.AppendLine($"Spotify PIDs ({pids.Count}): {string.Join(", ", pids)}");

        // Also show all process names containing "spot" for good measure
        var allProcs = Process.GetProcesses();
        var spotProcs = allProcs.Where(p => p.ProcessName.Contains("spot", StringComparison.OrdinalIgnoreCase)).ToList();
        sb.AppendLine($"Processes matching 'spot': {string.Join(", ", spotProcs.Select(p => $"{p.ProcessName}({p.Id})"))}");
        foreach (var p in allProcs) p.Dispose();

        sb.AppendLine();
        var sessions = _device.AudioSessionManager.Sessions;
        sb.AppendLine($"Audio sessions ({sessions.Count}):");
        for (int i = 0; i < sessions.Count; i++)
        {
            var s = sessions[i];
            try
            {
                var pid = s.GetProcessID;
                var id = s.GetSessionIdentifier ?? "(null)";
                var state = s.State;
                sb.AppendLine($"  [{i}] PID={pid}, State={state}, Id={id}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  [{i}] ERROR: {ex.Message}");
            }
        }

        return sb.ToString();
    }
}
