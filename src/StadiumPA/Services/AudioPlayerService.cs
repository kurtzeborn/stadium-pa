using System.IO;
using NAudio.Wave;

namespace StadiumPA.Services;

/// <summary>
/// Plays a local audio file (mp3/wav) using NAudio.
/// Files are pre-loaded into memory to eliminate playback delay.
/// Press to play, press again to stop. Exposes elapsed/total time for UI binding.
/// </summary>
public sealed class AudioPlayerService : IDisposable
{
    private MemoryStream? _audioData;
    private WaveStream? _reader;
    private WaveOutEvent? _waveOut;
    private string? _filePath;

    /// <summary>
    /// Fires periodically while playing, and once when playback stops.
    /// </summary>
    public event Action? PlaybackStateChanged;

    /// <summary>
    /// Whether audio is currently playing.
    /// </summary>
    public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;

    /// <summary>
    /// Current playback position, or zero if not loaded.
    /// </summary>
    public TimeSpan CurrentTime => _reader?.CurrentTime ?? TimeSpan.Zero;

    /// <summary>
    /// Total duration of the loaded file, or zero if not loaded.
    /// </summary>
    public TimeSpan TotalTime => _reader?.TotalTime ?? TimeSpan.Zero;

    /// <summary>
    /// Whether a file is loaded and ready for playback.
    /// </summary>
    public bool IsLoaded => _audioData is not null;

    /// <summary>
    /// The currently configured file path, or null.
    /// </summary>
    public string? FilePath => _filePath;

    /// <summary>
    /// Pre-loads an audio file into memory for instant playback.
    /// Call on startup or when the file path changes in settings.
    /// </summary>
    public void LoadFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return;

        Stop();
        DisposePlayback();

        _filePath = filePath;
        var fileBytes = File.ReadAllBytes(filePath);
        _audioData = new MemoryStream(fileBytes);

        // Create reader to get TotalTime — will be recreated on each Play()
        _reader = CreateReader(new MemoryStream(fileBytes));
    }

    /// <summary>
    /// Toggles playback: if playing, stops; if stopped, plays from the start.
    /// </summary>
    public void TogglePlayback()
    {
        if (IsPlaying)
        {
            Stop();
        }
        else
        {
            Play();
        }
    }

    /// <summary>
    /// Starts playback from the beginning.
    /// </summary>
    public void Play()
    {
        if (_audioData is null) return;

        // Stop any current playback
        DisposePlayback();

        // Create a fresh stream + reader from the pre-loaded bytes
        var stream = new MemoryStream(_audioData.ToArray());
        _reader = CreateReader(stream);

        _waveOut = new WaveOutEvent();
        _waveOut.Init(_reader);
        _waveOut.PlaybackStopped += OnPlaybackStopped;
        _waveOut.Play();

        PlaybackStateChanged?.Invoke();
    }

    /// <summary>
    /// Stops playback immediately.
    /// </summary>
    public void Stop()
    {
        if (_waveOut is null) return;

        _waveOut.Stop();
        PlaybackStateChanged?.Invoke();
    }

    /// <summary>
    /// Creates the appropriate WaveStream reader based on file extension.
    /// </summary>
    private WaveStream CreateReader(Stream stream)
    {
        var ext = Path.GetExtension(_filePath)?.ToLowerInvariant();
        return ext switch
        {
            ".wav" => new WaveFileReader(stream),
            _ => new Mp3FileReader(stream), // default to mp3
        };
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        PlaybackStateChanged?.Invoke();
    }

    private void DisposePlayback()
    {
        if (_waveOut is not null)
        {
            _waveOut.PlaybackStopped -= OnPlaybackStopped;
            _waveOut.Dispose();
            _waveOut = null;
        }

        // Don't dispose _audioData — it's our pre-loaded cache
        if (_reader is not null)
        {
            _reader.Dispose();
            _reader = null;
        }
    }

    public void Dispose()
    {
        DisposePlayback();
        _audioData?.Dispose();
        _audioData = null;
    }
}
