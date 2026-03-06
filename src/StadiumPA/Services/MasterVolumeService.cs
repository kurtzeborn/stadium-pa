using NAudio.CoreAudioApi;

namespace StadiumPA.Services;

/// <summary>
/// Controls system-wide master volume via Windows Core Audio API.
/// </summary>
public sealed class MasterVolumeService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator;
    private readonly MMDevice _device;
    private readonly AudioEndpointVolume _endpointVolume;

    public MasterVolumeService()
    {
        _enumerator = new MMDeviceEnumerator();
        _device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        _endpointVolume = _device.AudioEndpointVolume;
    }

    /// <summary>
    /// Gets or sets the master volume level (0.0 to 1.0).
    /// </summary>
    public float Volume
    {
        get => _endpointVolume.MasterVolumeLevelScalar;
        set => _endpointVolume.MasterVolumeLevelScalar = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Gets or sets the system mute state.
    /// </summary>
    public bool IsMuted
    {
        get => _endpointVolume.Mute;
        set => _endpointVolume.Mute = value;
    }

    public void Dispose()
    {
        _device?.Dispose();
        _enumerator?.Dispose();
    }
}
