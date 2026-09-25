#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class KitchenComplaintRegression
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
    static void Set(object target, string name, object value)
        => target.GetType().GetField(name, Private).SetValue(target, value);
    static object Get(object target, string name)
        => target.GetType().GetField(name, Private).GetValue(target);
    static void Call(object target, string name)
        => target.GetType().GetMethod(name, Private).Invoke(target, null);

    [MenuItem("Dine In/Fast Food/Check Complaint Interruption")]
    public static void Run()
    {
        Require(!Application.isPlaying, "Run outside Play Mode.");
        var prefab = PrefabUtility.LoadPrefabContents(
            "Assets/_Project/Resources/ManagerComplaints/ManagerComplaintSystem.prefab");
        try
        {
            var complaint = prefab.GetComponent<ManagerComplaintSystem>();
            var settings = (ManagerComplaintSettings)Get(complaint, "settings");
            foreach (ManagerComplaintType type in Enum.GetValues(typeof(ManagerComplaintType)))
            {
                Set(complaint, "activeDefinition", settings.GetDefinition(type));
                Call(complaint, "PopulateDialogue");
                var button = (UnityEngine.UI.Button)Get(complaint, "professionalButton");
                Require(button.gameObject.activeSelf && button.interactable, "Response unavailable.");
                var definition = settings.GetDefinition(type);
                var responses = new[] { definition.professional, definition.acceptable, definition.poor };
                var names = new[] { "professional", "acceptable", "poor" };
                var bounds = new System.Collections.Generic.List<Rect>();
                for (int i = 0; i < names.Length; i++)
                {
                    var choice = (UnityEngine.UI.Button)Get(complaint, names[i] + "Button");
                    var label = (TMPro.TMP_Text)Get(complaint, names[i] + "ButtonText");
                    Require(choice.gameObject.activeSelf && choice.interactable, "Missing complaint choice: " + names[i]);
                    Require(label.text.Contains(responses[i].buttonHeading) && label.text.Contains(responses[i].managerLine), "Choice copy is not data-bound.");
                    var rect = (RectTransform)choice.transform;
                    var area = new Rect(rect.anchoredPosition - Vector2.Scale(rect.sizeDelta, rect.pivot), rect.sizeDelta);
                    foreach (var previous in bounds) Require(!previous.Overlaps(area), "Complaint response cards overlap.");
                    bounds.Add(area);
                }
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }

        var scene = EditorSceneManager.NewPreviewScene();
        var cursor = Cursor.lockState;
        bool cursorVisible = Cursor.visible;
        try
        {
            GameObject Create(string name, params Type[] components)
            {
                var go = new GameObject(name, components);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                return go;
            }
            var bubble = Create("Bubble", typeof(RectTransform), typeof(UIFollowWorldPoint))
                .GetComponent<UIFollowWorldPoint>();
            bubble.target = Create("Target").transform;
            Set(bubble, "cam", null);
            // This is the exact method that threw during complaint tray cleanup.
            Call(bubble, "UpdateWorldSpacePose");

            var go = Create("Kitchen fixture");
            var controller = go.AddComponent<FastFoodCookingController>();
            var view = go.AddComponent<FastFoodCookingView>();
            var state = new FastFoodCookingState(_ => 100, _ => true);
            typeof(FastFoodCookingController).GetProperty("State").SetValue(controller, state);
            Set(controller, "view", view);
            Set(view, "owner", controller);
            Set(view, "selection", Create("Selection", typeof(RectTransform)).transform);
            Set(view, "station", Create("Station", typeof(RectTransform)).transform);
            Set(view, "help", Create("Help", typeof(RectTransform), typeof(UnityEngine.UI.Button))
                .GetComponent<UnityEngine.UI.Button>());
            var camera = Create("Lobby camera", typeof(Camera)).GetComponent<Camera>();
            camera.enabled = false;
            Set(view, "previousCamera", camera);
            Set(view, "previousCameraEnabled", true);
            foreach (var mode in new[] { FastFoodStationMode.Grill, FastFoodStationMode.Fry, FastFoodStationMode.Assembler })
            {
                state.Enter(mode);
                Set(view, "opened", true);
                controller.ExitKitchen();
                Require(!view.IsOpen && state.Mode == FastFoodStationMode.None && camera.enabled,
                    "Complaint exit did not restore the lobby from " + mode);
                controller.ExitKitchen(); // Idempotent when already closed.
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            Cursor.lockState = cursor;
            Cursor.visible = cursorVisible;
        }
        Debug.Log("[Kitchen complaint] PASS: both complaint types, three distinct authored responses, missing-camera guard, all station exits.");
    }
}
#endif
