using System.Runtime.InteropServices;

namespace StadiumPA.Services;

/// <summary>
/// Simulates media key presses (play/pause, next, previous) to control Spotify
/// without needing the Spotify API or internet connectivity.
/// </summary>
public static class MediaKeyService
{
    private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
    private const byte VK_MEDIA_PREV_TRACK = 0xB1;
    private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;

    private const uint KEYEVENTF_KEYDOWN = 0x0000;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    /// <summary>
    /// Sends a media key press (key-down followed by key-up).
    /// </summary>
    private static void SendMediaKey(byte vk)
    {
        keybd_event(vk, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
        keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    /// <summary>Sends Play/Pause media key.</summary>
    public static void PlayPause() => SendMediaKey(VK_MEDIA_PLAY_PAUSE);

    /// <summary>Sends Next Track media key.</summary>
    public static void NextTrack() => SendMediaKey(VK_MEDIA_NEXT_TRACK);

    /// <summary>Sends Previous Track media key.</summary>
    public static void PreviousTrack() => SendMediaKey(VK_MEDIA_PREV_TRACK);
}
