using UnityEngine;

// Shared build-time asset references; no editor-only asset lookup at runtime.
public sealed class HygieneCleaningPresentation : ScriptableObject
{
    public Sprite choiceButton;
    public GameObject mop;
    public GameObject bucket;
    [Header("Mop animation")]
    [Min(.5f)] public float mopCycleSeconds = 1.4f;
    [Range(.04f, .25f)] public float mopSweepWidth = .14f;
    public static HygieneCleaningPresentation Load() => Resources.Load<HygieneCleaningPresentation>("Hygiene/CleaningPresentation");
}
