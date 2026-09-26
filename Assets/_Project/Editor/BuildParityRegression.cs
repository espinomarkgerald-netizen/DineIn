#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Cheap, opt-in checks. Does not enter Play Mode, connect accounts, or modify preferences.</summary>
public static class BuildParityRegression
{
    [MenuItem("Dine In/Validation/Run Build Parity Regressions")]
    public static void Run()
    {
        BuildReferenceIntegrityGuard.ValidateOrThrow();
        for (int count=2; count<=4; count++)
        {
            var actors = new int[count]; for(int i=0;i<count;i++) actors[i]=i+1;
            var request = MultiplayerDayReadiness.Create(1,1,actors,"test");
            Check(request.Count == 1 && !request.BeginCountdown(10), "Host cannot start without the other players.");
            for(int i=1;i<count;i++) Check(request.SetVote(request.id, actors[i],1,true), "Ready vote rejected.");
            Check(request.AllReady && request.Count==count && request.BeginCountdown(10), "Party of " + count + " cannot start.");
            Check(!request.SetVote(request.id,actors[count-1],1,false), "Duplicate vote accepted.");
            Check(request.SetVote(request.id,actors[count-1],2,false) && !request.CountingDown, "Withdrawal did not cancel countdown.");
        }
        CheckPausePrefab(LobbyPauseMenuPrefabInstaller.PrefabPath);
        CheckPausePrefab("Assets/_Project/Resources/UI/LobbyHUD.prefab");
        foreach(float scale in new[]{1f,.75f,.5f})
            Check(Mathf.Abs(MobileUIAccessibility.MinimumCanvasTouchSizeForScale(scale)*scale-MobileUISettings.Active.touchTargetPixels)<.01f,
                "Touch target physical size changed with canvas scaling.");
        Debug.Log("[Build parity] PASS: build references, 2/3/4-player votes and withdrawal, saved pause controls, idempotent Resume, touch target conversions. No network/device run performed.");
    }

    private static void CheckPausePrefab(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        var helper = new GameObject("Pause regression"); helper.SetActive(false);
        float previousTime = Time.timeScale;
        try
        {
            var view = root.GetComponentInChildren<LobbyPauseMenuView>(true);
            var controller = helper.AddComponent<LobbyPauseMenu>();
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            typeof(LobbyPauseMenu).GetField("overlay",flags).SetValue(controller,view.Overlay);
            typeof(LobbyPauseMenu).GetField("pauseButton",flags).SetValue(controller,view.PauseButton);
            typeof(LobbyPauseMenu).GetField("pauseWindow",flags).SetValue(controller,view.Overlay.transform.Find("PauseWindow") as RectTransform);
            Check(!view.Overlay.activeSelf,"Pause was saved open.");
            var content = view.Overlay.transform.Find("PauseWindow/SettingsContent");
            var panel = content != null ? content.GetComponent<PauseSettingsPanel>() : null;
            Check(panel != null && panel.tabs.Length==4 && panel.pages.Length==4,"Authored settings tabs missing.");
            Check(panel.rows.Length==18 && System.Array.TrueForAll(panel.rows,r=>r.root!=null && (r.slider!=null || r.toggle!=null || r.dropdown!=null)),"Settings control references missing.");
            // Simulate a visible stale view whose internal paused flag was reset.
            view.Overlay.SetActive(true);
            typeof(LobbyPauseMenu).GetMethod("Resume",flags).Invoke(controller,null);
            Check(!view.Overlay.activeSelf && Time.timeScale==previousTime,"Resume failed to dismiss a stale view safely.");
            typeof(LobbyPauseMenu).GetMethod("Resume",flags).Invoke(controller,null);
            Check(!view.Overlay.activeSelf,"Second Resume reopened the view.");
        }
        finally { Time.timeScale=previousTime; UnityEngine.Object.DestroyImmediate(helper); PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void Check(bool passed,string message) { if(!passed) throw new InvalidOperationException(message); }
}
#endif
