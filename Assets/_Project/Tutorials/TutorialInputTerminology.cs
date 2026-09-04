using UnityEngine;

public enum TutorialInputMode
{
    Auto,
    Mobile,
    PC
}

/// <summary>One tutorial-owned source for platform-specific control wording.</summary>
public static class TutorialInputTerminology
{
    public static TutorialInputMode Mode { get; private set; } = TutorialInputMode.Auto;

    public static bool IsMobile => Mode == TutorialInputMode.Mobile ||
        Mode == TutorialInputMode.Auto &&
        (Application.platform == RuntimePlatform.Android ||
         Application.platform == RuntimePlatform.IPhonePlayer);

    public static void Configure(TutorialInputMode mode) => Mode = mode;

    public static string PanInstruction => IsMobile
        ? "Swipe and drag the screen to move the camera."
        : "Hold the Right Mouse Button and drag to move the camera.";

    public static string ZoomInstruction => IsMobile
        ? "Pinch with two fingers to zoom in and out."
        : "Use the mouse Scroll Wheel to zoom in and out.";

    public static string InteractionInstruction => IsMobile
        ? "Tap the highlighted booth once to interact with it."
        : "Left Click the highlighted booth once to interact with it.";

    public static string ActivateWord => IsMobile ? "Tap" : "Left Click";

    public static string Resolve(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        return message
            .Replace("{PAN_INSTRUCTION}", PanInstruction)
            .Replace("{ZOOM_INSTRUCTION}", ZoomInstruction)
            .Replace("{INTERACTION_INSTRUCTION}", InteractionInstruction)
            .Replace("{INTERACT}", ActivateWord);
    }
}
