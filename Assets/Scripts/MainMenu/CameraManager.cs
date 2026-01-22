using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class CameraManager : MonoBehaviour
{
    [System.Serializable]
    public class CameraTab
    {
        public string tabID;            // Name of tab
        public Transform cameraRig;     // Camera rig for this tab
        public Button button;           // Inspector-assigned button to open this tab
        public Button exitButton;       // Inspector-assigned button to return to main
    }

    [Header("Main Camera")]
    [SerializeField] private Camera mainCamera;

    [Header("Main Camera Rig")]
    [Tooltip("This is the camera rig the main camera should start at")]
    [SerializeField] private Transform mainCameraRig;

    [Header("Tabs (Assign buttons here)")]
    [Tooltip("Assign the buttons here to let the script know which tab is selected")]
    [SerializeField] private List<CameraTab> tabs = new List<CameraTab>();

    [Header("Transition Settings")]
    [SerializeField] private float transitionDuration = 0.75f;
    [SerializeField] private AnimationCurve cameraEase =
        AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Coroutine cameraRoutine;
    private CameraTab currentTab;

    private void Start()
    {
        // Hook up tab open buttons automatically
        foreach (var tab in tabs)
        {
            if (tab.button != null)
            {
                string id = tab.tabID; // capture local variable
                tab.button.onClick.AddListener(() => OnTabSelected(id));
            }

            // Hook up exit/back button for this tab
            if (tab.exitButton != null)
            {
                tab.exitButton.onClick.AddListener(() => OnExitTab(tab));
            }
        }

        // Snap to the MAIN camera rig explicitly
        if (mainCameraRig != null)
        {
            SnapToRig(mainCameraRig);
            currentTab = null; // no tab selected yet
        }
        else if (tabs.Count > 0)
        {
            // fallback: use first tab rig if mainCameraRig is missing
            currentTab = tabs[0];
            SnapToRig(currentTab.cameraRig);
            Debug.LogWarning("CameraManager: Main Camera Rig not assigned. Using first tab rig instead.");
        }
    }

    // Called when a button is pressed
    public void OnTabSelected(string tabID)
    {
        CameraTab tab = tabs.Find(t => t.tabID == tabID);
        if (tab == null || tab.cameraRig == null)
            return;

        if (tab == currentTab)
            return;

        currentTab = tab;
        MoveToRig(tab.cameraRig);
    }

    // Called when a tab's exit/back button is pressed
    private void OnExitTab(CameraTab tab)
    {
        if (tab == null) return;

        // Return to main camera rig
        currentTab = null;
        MoveToRig(mainCameraRig);
    }

    private void SnapToRig(Transform rig)
    {
        mainCamera.transform.SetPositionAndRotation(rig.position, rig.rotation);
    }

    private void MoveToRig(Transform target)
    {
        if (cameraRoutine != null)
            StopCoroutine(cameraRoutine);

        cameraRoutine = StartCoroutine(CameraTransition(target));
    }

    private IEnumerator CameraTransition(Transform target)
    {
        Transform cam = mainCamera.transform;
        Vector3 startPos = cam.position;
        Quaternion startRot = cam.rotation;

        float t = 0f;

        while (t < transitionDuration)
        {
            float eased = cameraEase.Evaluate(t / transitionDuration);
            cam.position = Vector3.Lerp(startPos, target.position, eased);
            cam.rotation = Quaternion.Slerp(startRot, target.rotation, eased);

            t += Time.deltaTime;
            yield return null;
        }

        cam.SetPositionAndRotation(target.position, target.rotation);
        cameraRoutine = null;
    }
}
