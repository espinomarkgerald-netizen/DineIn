using UnityEngine;

public class TutorialSpawnDebugger : MonoBehaviour
{
    [SerializeField] private TutorialManager tutorialManager;
    [SerializeField] private GroupSpawner groupSpawner;
    [SerializeField] private float checkEverySeconds = 1f;

    private float timer;
    private float lastPracticeSpawnCheck = -999f;

    private void Awake()
    {
        if (tutorialManager == null)
            tutorialManager = FindFirstObjectByType<TutorialManager>();

        if (groupSpawner == null)
            groupSpawner = FindFirstObjectByType<GroupSpawner>();
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer < checkEverySeconds)
            return;

        timer = 0f;

        if (tutorialManager == null)
            return;

        if (!tutorialManager.TutorialStarted)
            return;

        if (tutorialManager.CurrentDay != TutorialManager.TutorialDay.Day1Host)
            return;

        if (tutorialManager.CurrentPhase != TutorialManager.TutorialPhase.PracticeGameplay)
            return;

        if (Time.time - lastPracticeSpawnCheck >= 29f)
        {
            lastPracticeSpawnCheck = Time.time;
            Debug.Log("[TutorialSpawnDebugger] Host practice is active. A new group should be spawning now.");
        }

        CustomerGroup[] groups = FindObjectsByType<CustomerGroup>(FindObjectsSortMode.None);
        Debug.Log("[TutorialSpawnDebugger] Current CustomerGroup count in scene = " + groups.Length);
    }
}