using System;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

// Read-only asset/reference inspection, not a gameplay walkthrough.
public static class InspectCustomers
{
    public static string Main()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorUtility.scriptCompilationFailed)
            throw new InvalidOperationException("Expected compiled Edit Mode.");
        var report = new StringBuilder();
        const string folder = "Assets/_Project/Art/Models/Customer/New Alien Models/PlayerType/";
        string[] names = { "Green", "Pink", "Blue", "Purple", "Orange", "Elderly", "Yellow" };
        int[] portraitNumbers = { 1, 3, 2, 6, 4, 7, 5 };
        for (int i = 0; i < names.Length; i++)
        {
            var p = AssetDatabase.LoadAssetAtPath<CustomerTypeProfile>(folder + names[i] + "Customer.asset");
            if (p == null || (int)p.customerType != i || p.customerImage == null || string.IsNullOrWhiteSpace(p.introduction))
                throw new InvalidOperationException("Invalid customer identity/presentation: " + names[i]);
            string expected = "Assets/_Project/Art/Models/Customer/New Alien Models/2dCloseupImages/" + portraitNumbers[i] + ".png";
            if (AssetDatabase.GetAssetPath(p.customerImage) != expected) throw new InvalidOperationException("Incorrect portrait: " + names[i]);
            if (i >= 3)
            {
                if (p.customerPrefab == null || p.unlockDay <= 1) throw new InvalidOperationException("Invalid new customer configuration: " + names[i]);
                var animator = p.customerPrefab.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman || !animator.avatar.isValid)
                    throw new InvalidOperationException("Invalid rigged customer: " + names[i]);
            }
            report.AppendLine(names[i] + ": id=" + i + ", portrait=" + portraitNumbers[i] + ", day=" + (i < 3 ? "existing restaurant rule" : p.unlockDay.ToString()) +
                ", order=" + p.orderingDurationMultiplier + ", eat=" + p.eatDurationMultiplier + ", walk=" + p.walkingSpeedMultiplier);
        }
        var scene = SceneManager.GetSceneByName("Lobby2");
        var spawner = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GroupSpawner>(true)).Single();
        var data = new SerializedObject(spawner);
        foreach (string field in new[] { "greenEnabled", "pinkEnabled", "blueEnabled" })
            if (!data.FindProperty(field).boolValue) throw new InvalidOperationException("Day 1 starting type disabled: " + field);
        if (data.FindProperty("campaignCustomers").arraySize != 7 || data.FindProperty("regularCustomerVisualVariants").arraySize != 0)
            throw new InvalidOperationException("Invalid normal Lobby2 customer mapping.");
        report.AppendLine("Legacy weights: " + data.FindProperty("weightGreen").floatValue + "/" + data.FindProperty("weightPink").floatValue + "/" + data.FindProperty("weightBlue").floatValue);
        int zones = 0;
        foreach (var table in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FastFoodTable>(true)))
        {
            var tableData = new SerializedObject(table);
            if (!tableData.FindProperty("familyPlayEnabled").boolValue) throw new InvalidOperationException("Missing dining play zone: " + table.name);
            var bounds = tableData.FindProperty("familyPlayBounds").boundsValue;
            if (table.StandPoint == null || !bounds.Contains(table.transform.InverseTransformPoint(table.StandPoint.position)))
                throw new InvalidOperationException("Play zone does not include the dining approach: " + table.name);
            zones++;
        }
        var guide = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/UI/RestaurantUnlockGuide.prefab");
        if (guide == null || !guide.GetComponent<RestaurantUnlockCoachUI>().HasCustomerShowcase)
            throw new InvalidOperationException("Missing customer portrait showcase.");
        if (guide.GetComponentInChildren<TutorialSystem>(true) != null)
            throw new InvalidOperationException("Campaign presentation must not contain a tutorial runner.");
        report.AppendLine("Authored dining play zones: " + zones + "; campaign showcase references valid; no tutorial runner.");
        report.AppendLine("Read-only inspection only. No customers spawned, simulation run, saves loaded/written, or Play Mode entered.");
        return report.ToString();
    }
}
