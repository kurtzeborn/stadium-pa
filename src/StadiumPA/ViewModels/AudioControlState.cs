namespace StadiumPA.ViewModels;

/// <summary>
/// State machine for the DIM / FADE OUT / KILL audio controls.
/// </summary>
public enum AudioControlState
{
    Normal,
    Dimmed,
    FadedOut,
    Killed
}
