#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds editable flavor text to the active loading-screen prefab once.
/// Existing authored children are never rebuilt automatically.
/// </summary>
[InitializeOnLoad]
internal static class LoadingScreenFlavorAuthoring
{
    private const string PrefabPath =
        "Assets/_Project/MainMenu/NewDesign/LoadingScreens/NormalLoadingScreen/LoadingScreen.prefab";
    private const string FontPath =
        "Assets/_Project/UI/Assets/Legacy/Fonts/Fredoka,Lilita_One/Fredoka/Fredoka-VariableFont_wdth,wght SDF.asset";
    static LoadingScreenFlavorAuthoring()
    {
        EditorApplication.delayCall += CreateMissingFlavorStrip;
    }

    [MenuItem("Tools/Dine In/UI/Create Missing Loading Flavor Text")]
    public static void CreateMissingFlavorStrip()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (contents == null)
            return;

        bool changed = false;
        try
        {
            Canvas canvas = contents.GetComponentInChildren<Canvas>(true);
            if (canvas == null)
            {
                Debug.LogError("[LoadingFlavor] Current loading prefab has no Canvas.");
                return;
            }

            RectTransform safeArea = canvas.transform.Find("FlavorTipSafeArea") as RectTransform;
            CanvasGroup tipGroup = null;
            TMP_Text tipText = null;
            if (safeArea == null)
            {
                GameObject safeObject = CreateUI("FlavorTipSafeArea", canvas.transform);
                safeArea = safeObject.GetComponent<RectTransform>();
                safeArea.anchorMin = Vector2.zero;
                safeArea.anchorMax = Vector2.one;
                safeArea.offsetMin = Vector2.zero;
                safeArea.offsetMax = Vector2.zero;

                GameObject panelObject = CreateUI("FlavorTipPanel", safeArea);
                RectTransform panel = panelObject.GetComponent<RectTransform>();
                panel.anchorMin = new Vector2(0.08f, 0f);
                panel.anchorMax = new Vector2(0.92f, 0f);
                panel.pivot = new Vector2(0.5f, 0f);
                panel.offsetMin = new Vector2(0f, 42f);
                panel.offsetMax = new Vector2(0f, 94f);
                tipGroup = panelObject.AddComponent<CanvasGroup>();
                tipGroup.interactable = false;
                tipGroup.blocksRaycasts = false;

                GameObject textObject = CreateUI("FlavorTipText", panel);
                RectTransform textRect = textObject.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(26f, 15f);
                textRect.offsetMax = new Vector2(-26f, -15f);
                tipText = textObject.AddComponent<TextMeshProUGUI>();
                tipText.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                tipText.text = "TIP — Seat waiting groups quickly before their patience falls.";
                tipText.fontSize = 23f;
                tipText.enableAutoSizing = true;
                tipText.fontSizeMin = 16f;
                tipText.fontSizeMax = 25f;
                tipText.alignment = TextAlignmentOptions.Center;
                tipText.color = Color.white;
                tipText.raycastTarget = false;
                tipText.textWrappingMode = TextWrappingModes.Normal;
                changed = true;
            }
            else
            {
                tipGroup = safeArea.GetComponentInChildren<CanvasGroup>(true);
                tipText = safeArea.GetComponentInChildren<TMP_Text>(true);

                Transform panelTransform = safeArea.Find("FlavorTipPanel");
                if (panelTransform != null)
                {
                    Image oldFrame = panelTransform.GetComponent<Image>();
                    if (oldFrame != null)
                    {
                        Object.DestroyImmediate(oldFrame);
                        changed = true;
                    }

                    RectTransform panel = panelTransform as RectTransform;
                    if (panel != null)
                    {
                        panel.anchorMin = new Vector2(0.08f, 0f);
                        panel.anchorMax = new Vector2(0.92f, 0f);
                        panel.pivot = new Vector2(0.5f, 0f);
                        panel.offsetMin = new Vector2(0f, 42f);
                        panel.offsetMax = new Vector2(0f, 94f);
                        changed = true;
                    }
                }
            }

            LoadingScreenUI presenter = contents.GetComponentInChildren<LoadingScreenUI>(true);
            if (presenter == null)
            {
                presenter = canvas.gameObject.AddComponent<LoadingScreenUI>();
                changed = true;
            }

            if (tipText != null && tipGroup != null)
            {
                presenter.ConfigureTipsForEditor(tipText, tipGroup, safeArea);
                EditorUtility.SetDirty(presenter);
                changed = true;
            }

            if (!changed)
                return;

            SetLayerRecursively(safeArea.gameObject, LayerMask.NameToLayer("UI"));
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[LoadingFlavor] Saved the editable randomized bottom loading tip.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    [MenuItem("Tools/Dine In/UI/Open Loading Screen Prefab")]
    private static void OpenLoadingPrefab()
    {
        Object prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null)
            AssetDatabase.OpenAsset(prefab);
    }

    private static GameObject CreateUI(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null || layer < 0)
            return;
        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
#endif
