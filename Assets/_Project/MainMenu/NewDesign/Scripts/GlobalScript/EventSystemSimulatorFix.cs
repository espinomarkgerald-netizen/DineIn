#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Project-wide, zero-setup workaround for the Unity Device Simulator's
/// device-registration race condition.
///
/// The problem: the Device Simulator adds a virtual Touchscreen device
/// dynamically once its window activates - not at process start. If you
/// enter Play mode directly in Simulator Mode, the scene's EventSystem
/// (and its input module) can finish its own early initialization before
/// that virtual device is registered. It then locks onto "no usable
/// pointer device" for the rest of the session - buttons stop responding,
/// and switching back to Game Mode afterward does NOT fix it, because the
/// module already made its decision; it isn't re-detecting anything.
///
/// The fix: wait one frame (so the Simulator's device is guaranteed to
/// exist), then disable/re-enable the EventSystem GameObject so its input
/// module rebuilds its device state from scratch against whatever is
/// actually present.
///
/// Why this needs no manual setup: [RuntimeInitializeOnLoadMethod] runs
/// this automatically before/around the first scene load, and we then
/// hook SceneManager.sceneLoaded so every subsequently loaded scene gets
/// the same fix applied to its own EventSystem - no prefab, no dragging
/// this onto anything, nothing to remember when you create a new scene.
///
/// Why it's safe to leave in the project forever: the whole file is
/// wrapped in UNITY_EDITOR, so it compiles out of real builds completely.
/// Actual devices don't have this race - their touchscreen exists from
/// process start - so this logic has nothing to do there anyway.
/// </summary>
public static class EventSystemSimulatorFix
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Register()
    {
        // Fix whatever scene is loaded right now...
        RequestFix();

        // ...and every scene loaded after this point, for the rest of the session.
        SceneManager.sceneLoaded -= OnSceneLoaded; // avoid double-subscribe on domain reload edge cases
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RequestFix();

    private static void RequestFix()
    {
        // Use a throwaway runner object since this is a static class and
        // can't itself host a coroutine.
        var runner = new GameObject("~EventSystemSimulatorFixRunner")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        runner.AddComponent<FixRunner>();
    }

    private class FixRunner : MonoBehaviour
    {
        private IEnumerator Start()
        {
            yield return null; // let the Simulator finish registering its virtual device(s)

            EventSystem es = EventSystem.current;
            if (es != null)
            {
                es.gameObject.SetActive(false);
                es.gameObject.SetActive(true);
            }

            Destroy(gameObject);
        }
    }
}
#endif
