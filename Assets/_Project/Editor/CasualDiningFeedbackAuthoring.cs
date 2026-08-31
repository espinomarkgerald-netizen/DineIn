#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Deterministic entry point used by validation and available to designers.</summary>
public static class CasualDiningFeedbackAuthoring
{
    [MenuItem("Tools/Dine In/UI/Apply Casual Dining Feedback Authoring")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            Debug.LogWarning("[CasualDiningFeedback] Wait for Unity compilation to finish, then run again.");
            return;
        }

        LoadingScreenFlavorAuthoring.CreateMissingFlavorStrip();
        ManagementHRApplicantAuthoring.UpgradeMissingAuthoring();
        ManagementUIAnimationAuthoring.CreateMissingAnimations();
        AssetDatabase.SaveAssets();
        Debug.Log("[CasualDiningFeedback] Editable loading, Staff, and animation prefabs are up to date.");
    }
}
#endif
