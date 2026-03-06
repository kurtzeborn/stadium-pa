using System.Runtime.InteropServices;

namespace StadiumPA.Services;

/// <summary>
/// Prevents Windows from sleeping the display or the machine while the app is running.
/// Uses SetThreadExecutionState P/Invoke.
/// </summary>
public sealed class SleepSuppressionService : IDisposable
{
    [Flags]
    private enum EXECUTION_STATE : uint
    {
        ES_CONTINUOUS = 0x80000000,
        ES_DISPLAY_REQUIRED = 0x00000002,
        ES_SYSTEM_REQUIRED = 0x00000001,
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

    private bool _suppressing;

    /// <summary>
    /// Enables sleep and screen suppression. Call once on startup.
    /// </summary>
    public void Enable()
    {
        if (_suppressing) return;
        SetThreadExecutionState(
            EXECUTION_STATE.ES_CONTINUOUS |
            EXECUTION_STATE.ES_SYSTEM_REQUIRED |
            EXECUTION_STATE.ES_DISPLAY_REQUIRED);
        _suppressing = true;
    }

    /// <summary>
    /// Disables suppression, restoring normal Windows sleep behavior.
    /// </summary>
    public void Disable()
    {
        if (!_suppressing) return;
        SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
        _suppressing = false;
    }

    public void Dispose()
    {
        Disable();
    }
}
