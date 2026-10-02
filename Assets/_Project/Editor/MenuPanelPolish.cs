#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

public static class MenuPanelPolish
{
    public static string Apply()
    {
        const string scenePath = "Assets/_Project/Scenes/NewMenu/NewGameMenu.unity";
        var scene = SceneManager.GetSceneByPath(scenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        try
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
                    if (rect.name == "ShopPanelUI" || rect.name == "GamemodePopUpUI")
                    {
                        Stretch(rect);
                        var background = rect.Find("Background") as RectTransform;
                        if (background == null) throw new Exception("Missing background: " + rect.name);
                        Stretch(background);
                        EditorUtility.SetDirty(rect);
                        EditorUtility.SetDirty(background);
                    }
            // Full-screen artwork must not inherit notch margins. Keep those
            // margins on the close controls instead of shrinking the entire panel.
            foreach (var root in scene.GetRootGameObjects())
                foreach (var safeArea in root.GetComponentsInChildren<UIScreenSafeArea>(true))
                {
                    var data = new SerializedObject(safeArea);
                    var targets = data.FindProperty("targets");
                    for (int i = targets.arraySize - 1; i >= 0; i--)
                    {
                        var target = targets.GetArrayElementAtIndex(i);
                        var rect = target.FindPropertyRelative("rect").objectReferenceValue as RectTransform;
                        if (rect == null || (rect.name != "ShopPanelUI" && rect.name != "GamemodePopUpUI")) continue;
                        var close = rect.name == "ShopPanelUI" ? rect.Find("CloseButton") : rect.Find("Dialogue/CancelButton ");
                        if (close == null)
                            foreach (var child in rect.GetComponentsInChildren<RectTransform>(true))
                                if (child.name.Trim() == "CancelButton") { close = child; break; }
                        if (close == null) throw new Exception("Missing close control: " + rect.name);
                        var closeRect = (RectTransform)close;
                        target.FindPropertyRelative("rect").objectReferenceValue = closeRect;
                        target.FindPropertyRelative("anchorMin").vector2Value = closeRect.anchorMin;
                        target.FindPropertyRelative("anchorMax").vector2Value = closeRect.anchorMax;
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/UI/Assets/Fonts/Anton/Anton-Regular SDF.asset");
        foreach (var path in new[] { "Assets/_Project/Gameplay/UI/Resources/LobbyPauseMenu.prefab", "Assets/_Project/Resources/UI/LobbyHUD.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponentInChildren<LobbyPauseMenuView>(true);
                foreach (var button in new[] { view.ResumeButton, view.GameMenuButton })
                    foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
                    { text.font = font; text.fontSharedMaterial = font.material; text.fontStyle = FontStyles.Normal; }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        return "Stretched Shop/game-mode panels and backgrounds; pause footer uses normal Anton.";
    }
    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f); rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}
#endif
