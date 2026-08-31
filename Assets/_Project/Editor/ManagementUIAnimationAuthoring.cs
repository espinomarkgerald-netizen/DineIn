#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Adds the shared, editable reveal transition to management UI prefabs once.</summary>
[InitializeOnLoad]
internal static class ManagementUIAnimationAuthoring
{
    private struct Target
    {
        public string path;
        public bool playOnEnable;
        public float duration;
        public float scale;
        public Vector2 offset;

        public Target(string targetPath, bool play, float seconds, float startScale, Vector2 startOffset)
        {
            path = targetPath;
            playOnEnable = play;
            duration = seconds;
            scale = startScale;
            offset = startOffset;
        }
    }

    private static readonly Target[] Targets =
    {
        new Target(
            "Assets/_Project/ManagementComputer/Prefabs/ManagementComputerAppWindow.prefab",
            false, 0.2f, 0.96f, Vector2.zero),
        new Target(
            "Assets/_Project/ManagementComputer/Prefabs/ManagementComputerRow.prefab",
            false, 0.16f, 0.97f, new Vector2(0f, -10f)),
        new Target(
            "Assets/_Project/ManagementComputer/Prefabs/ManagementHRPanel.prefab",
            false, 0.18f, 0.97f, Vector2.zero),
        new Target(
            "Assets/_Project/ManagementComputer/Prefabs/ManagementHRRoleSection.prefab",
            false, 0.16f, 0.98f, new Vector2(0f, -8f)),
        new Target(
            "Assets/_Project/ManagementComputer/Prefabs/ManagementEmployeeCard.prefab",
            false, 0.16f, 0.96f, Vector2.zero)
    };

    static ManagementUIAnimationAuthoring()
    {
        EditorApplication.delayCall += CreateMissingAnimations;
    }

    [MenuItem("Tools/Dine In/UI/Add Missing Management UI Animations")]
    public static void CreateMissingAnimations()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        foreach (Target target in Targets)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(target.path);
            if (contents == null)
                continue;

            try
            {
                UIRevealAnimation reveal = contents.GetComponent<UIRevealAnimation>();
                if (reveal != null)
                    continue;

                reveal = contents.AddComponent<UIRevealAnimation>();
                reveal.ConfigureForEditor(
                    target.playOnEnable,
                    target.duration,
                    0f,
                    target.scale,
                    target.offset);
                PrefabUtility.SaveAsPrefabAsset(contents, target.path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        AssetDatabase.SaveAssets();
    }
}
#endif
