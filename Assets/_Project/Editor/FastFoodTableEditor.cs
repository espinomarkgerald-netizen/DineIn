#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FastFoodTable))]
public sealed class FastFoodTableEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var table = (FastFoodTable)target;
        var booth = table.GetComponent<Booth>();
        EditorGUILayout.HelpBox("Edit Furniture and service anchors in Prefab Mode. The Booth Seats list is the seating capacity. " +
            "Scene instances may override placement; keep each anchor inside its own table. Rebake/validate navigation after moving furniture.", MessageType.Info);
        if (!table.ValidateAuthoring(out var problem)) EditorGUILayout.HelpBox(problem, MessageType.Error);
        if (booth == null) return;
        EditorGUILayout.LabelField("Seat capacity", (booth.seats?.Count ?? 0).ToString());
        SelectButton("Select approach point", booth.approachPoint);
        SelectButton("Select facing point", booth.tableLookTarget);
        SelectButton("Select tray point", booth.NetworkTrayPoint);
        SelectButton("Select number anchor", booth.tableNumberAnchor);
    }

    private static void SelectButton(string label, Transform point)
    {
        using (new EditorGUI.DisabledScope(point == null))
            if (GUILayout.Button(label)) { Selection.activeTransform = point; SceneView.lastActiveSceneView?.FrameSelected(); }
    }
}
#endif
