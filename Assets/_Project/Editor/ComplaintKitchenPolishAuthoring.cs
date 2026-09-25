#if UNITY_EDITOR
using System;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class ComplaintKitchenPolishAuthoring
{
    const string ComplaintPath = "Assets/_Project/Resources/ManagerComplaints/ManagerComplaintSystem.prefab";
    const string TicketPath = "Assets/_Project/Restaurant/CookingAssets/Order Ticket.prefab";
    const string Theme = "Assets/_Project/Restaurant/CookingAssets/Theme/";

    [MenuItem("Dine In/Fast Food/Apply Complaint and Ticket Feedback Polish")]
    public static string Apply()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        var grey = AssetDatabase.LoadAssetAtPath<Sprite>(Theme + "Grey depth_flat.asset");
        if (grey == null) throw new InvalidOperationException("Missing the authored Double UI grey sprite.");
        var root = PrefabUtility.LoadPrefabContents(ComplaintPath);
        try
        {
            var system = root.GetComponent<ManagerComplaintSystem>();
            var data = new SerializedObject(system);
            T Ref<T>(string field) where T : UnityEngine.Object => (T)data.FindProperty(field).objectReferenceValue;
            var panel = Ref<RectTransform>("dialoguePanel");
            var blocker = Ref<GameObject>("dialogueRoot");
            var ink = new Color(.12f, .20f, .24f);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            panel.anchoredPosition = Vector2.zero; panel.sizeDelta = new Vector2(960, 660);
            panel.localScale = Vector3.one;
            var background = panel.GetComponent<UnityEngine.UI.Image>();
            background.sprite = grey; background.type = UnityEngine.UI.Image.Type.Sliced;
            background.color = new Color(1, .985f, .94f);
            var dimmer = blocker.GetComponent<UnityEngine.UI.Image>();
            dimmer.color = new Color(.035f, .075f, .10f, .68f); dimmer.raycastTarget = true;
            var visibility = blocker.GetComponent<CanvasGroup>();
            if (visibility == null) visibility = blocker.AddComponent<CanvasGroup>();
            visibility.alpha = 1; visibility.blocksRaycasts = visibility.interactable = true;
            data.FindProperty("dialogueVisibility").objectReferenceValue = visibility;
            var portrait = Ref<UnityEngine.UI.Image>("portraitImage");
            Place(portrait.rectTransform, new Vector2(38, -36), new Vector2(100, 100), true);
            portrait.preserveAspect = true; portrait.raycastTarget = false;
            var title = Ref<TMP_Text>("headlineText");
            Place(title.rectTransform, new Vector2(164, -24), new Vector2(756, 64), true);
            var bodyFont = Ref<TMP_Text>("customerLineText");
            title.font = bodyFont.font; title.fontSharedMaterial = bodyFont.fontSharedMaterial;
            title.fontStyle = FontStyles.Bold; StyleText(title, 34, ink);
            var customer = Ref<TMP_Text>("customerLineText");
            Place(customer.rectTransform, new Vector2(164, -84), new Vector2(748, 66), true);
            StyleText(customer, 24, ink);
            var prompt = Ref<TMP_Text>("managerResponseText");
            Place(prompt.rectTransform, new Vector2(38, -167), new Vector2(884, 34), true);
            StyleText(prompt, 23, ink); prompt.text = "How should we handle this?";
            var coaching = Ref<TMP_Text>("coachingText");
            Place(coaching.rectTransform, new Vector2(38, -565), new Vector2(884, 72), true);
            StyleText(coaching, 20, ink); coaching.text = "";
            var names = new[] { "professional", "acceptable", "poor" };
            var accents = new[] { new Color(.10f, .57f, .41f), new Color(.83f, .56f, .14f), new Color(.75f, .29f, .34f) };
            for (int i = 0; i < names.Length; i++)
            {
                var button = Ref<UnityEngine.UI.Button>(names[i] + "Button");
                var label = Ref<TMP_Text>(names[i] + "ButtonText");
                button.gameObject.SetActive(true);
                Place((RectTransform)button.transform, new Vector2(0, -212 - i * 114), new Vector2(884, 102), false);
                var image = button.GetComponent<UnityEngine.UI.Image>();
                image.sprite = grey; image.overrideSprite = null; image.type = UnityEngine.UI.Image.Type.Sliced;
                image.color = Color.Lerp(Color.white, accents[i], .075f); image.raycastTarget = true;
                button.targetGraphic = image; button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
                var colors = UnityEngine.UI.ColorBlock.defaultColorBlock;
                colors.highlightedColor = Color.white; colors.selectedColor = new Color(.93f, 1, .97f);
                colors.pressedColor = new Color(.82f, .89f, .89f); colors.fadeDuration = .08f;
                button.colors = colors;
                var navigation = button.navigation; navigation.mode = UnityEngine.UI.Navigation.Mode.Automatic; button.navigation = navigation;
                if (button.GetComponent<UISubtlePressFeedback>() == null) button.gameObject.AddComponent<UISubtlePressFeedback>();
                label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(38, 12); label.rectTransform.offsetMax = new Vector2(-24, -12);
                StyleText(label, 25, ink); label.richText = true;
                var accent = button.transform.Find("Choice Accent") as RectTransform;
                if (accent == null)
                {
                    accent = (RectTransform)new GameObject("Choice Accent", typeof(RectTransform), typeof(UnityEngine.UI.Image)).transform;
                    accent.SetParent(button.transform, false);
                }
                accent.anchorMin = new Vector2(0, .18f); accent.anchorMax = new Vector2(0, .82f);
                accent.pivot = new Vector2(0, .5f); accent.anchoredPosition = new Vector2(18, 0); accent.sizeDelta = new Vector2(5, 0);
                var strip = accent.GetComponent<UnityEngine.UI.Image>(); strip.color = accents[i]; strip.raycastTarget = false;
                var settings = Ref<ManagerComplaintSettings>("settings");
                var definitions = new[] { settings.wrongOrder.professional, settings.wrongOrder.acceptable, settings.wrongOrder.poor };
                label.text = "<b>" + definitions[i].buttonHeading + "</b>\n<size=80%>\"" + definitions[i].managerLine + "\"</size>";
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            blocker.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, ComplaintPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        root = PrefabUtility.LoadPrefabContents(TicketPath);
        try
        {
            var ticket = root.GetComponent<FastFoodCookingTicketView>();
            ticket.slideSeconds = .34f; ticket.exitSeconds = .22f;
            ticket.slideDistance = 380;
            PrefabUtility.SaveAsPrefabAsset(root, TicketPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return "Saved three complaint response cards, editable modal motion, button feedback and full-width ticket transitions.";
    }

    static void Place(RectTransform rect, Vector2 position, Vector2 size, bool left)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(left ? 0 : .5f, 1);
        rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one;
    }
    static void StyleText(TMP_Text label, float size, Color color)
    {
        label.fontSize = size; label.enableAutoSizing = false; label.color = color;
        label.alignment = TextAlignmentOptions.MidlineLeft; label.raycastTarget = false;
        label.margin = Vector4.zero; label.overflowMode = TextOverflowModes.Overflow;
    }
}
#endif
