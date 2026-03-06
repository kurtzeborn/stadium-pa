using System.Windows.Threading;

namespace StadiumPA.Services;

/// <summary>
/// Smooth volume fade utility. Provides a progress value (0.0→1.0) over a
/// configurable duration via DispatcherTimer (~50ms steps). The caller uses
/// the progress to interpolate any number of volume sources simultaneously.
/// </summary>
public sealed class VolumeFader : IDisposable
{
    private DispatcherTimer? _timer;
    private int _totalSteps;
    private int _currentStep;
    private Action<float>? _onProgress;
    private Action? _onComplete;

    /// <summary>Whether a fade is currently in progress.</summary>
    public bool IsFading => _timer?.IsEnabled == true;

    /// <summary>
    /// Starts a fade, calling <paramref name="onProgress"/> with a value from 0.0 to 1.0
    /// over <paramref name="durationMs"/> milliseconds (~50ms steps).
    /// Cancels any in-flight fade first. Calls <paramref name="onComplete"/> when done.
    /// </summary>
    public void Start(int durationMs, Action<float> onProgress, Action? onComplete = null)
    {
        Cancel();

        _onProgress = onProgress;
        _onComplete = onComplete;

        const int stepMs = 50;
        _totalSteps = Math.Max(1, durationMs / stepMs);
        _currentStep = 0;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(stepMs) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    /// <summary>Cancels any in-progress fade immediately.</summary>
    public void Cancel()
    {
        if (_timer is not null)
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
            _timer = null;
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _currentStep++;
        float t = Math.Min(1f, (float)_currentStep / _totalSteps);
        _onProgress?.Invoke(t);

        if (_currentStep >= _totalSteps)
        {
            var complete = _onComplete;
            Cancel();
            complete?.Invoke();
        }
    }

    public void Dispose() => Cancel();
}
