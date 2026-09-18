using UnityEngine;

// Shared build-time asset references; no editor-only asset lookup at runtime.
public sealed class HygieneCleaningPresentation : ScriptableObject
{
    public Sprite choiceButton;
    public GameObject mop;
    public GameObject bucket;
    public static HygieneCleaningPresentation Load() => Resources.Load<HygieneCleaningPresentation>("Hygiene/CleaningPresentation");
}
