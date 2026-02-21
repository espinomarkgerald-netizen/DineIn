using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIFlowManager : MonoBehaviour
{
    [System.Serializable]
    public class UITab
    {
        public string tabID;
        public RectTransform panel;
        public Button tabButton;      // Button to open this tab
        public Button exitButton;     // Button to close this tab / go back
    }

    [Header("Tabs")]
    public List<UITab> tabs = new List<UITab>();

    [Header("Global Buttons")]
    [SerializeField] private Button quitButton;

    [Header("Animation")]
    public float slideDuration = 0.25f;
    public Vector2 offscreenOffset = new Vector2(1600f, 0f);
    public float newPanelDelay = 0.1f;

    [Header("Startup")]
    [Tooltip("If true, restores the last opened tab (saved in PlayerPrefs).")]
    [SerializeField] private bool restoreLastTab = true;

    [Tooltip("If restoreLastTab is OFF or no saved tab exists, this tabID will open first. Leave blank to use tabs[0].")]
    [SerializeField] private string defaultTabID = "";

    private const string PREF_LAST_UIFLOW_TAB = "UIFlow_LastTab";

    private UITab currentTab;
    private bool isTransitioning;

    void Start()
    {
        // Hook up tab open buttons automatically
        foreach (var tab in tabs)
        {
            if (tab.tabButton != null)
            {
                string id = tab.tabID;
                tab.tabButton.onClick.RemoveListener(() => OpenTab(id)); // harmless, Unity ignores mismatched delegates
                tab.tabButton.onClick.AddListener(() => OpenTab(id));
            }

            if (tab.exitButton != null)
            {
                UITab localTab = tab;
                tab.exitButton.onClick.RemoveListener(() => OnExitTab(localTab));
                tab.exitButton.onClick.AddListener(() => OnExitTab(localTab));
            }
        }

        // Hook up quit button
        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(QuitGame);
            quitButton.onClick.AddListener(QuitGame);
        }

        // Hide all first (clean reset)
        for (int i = 0; i < tabs.Count; i++)
        {
            if (tabs[i].panel != null)
                tabs[i].panel.gameObject.SetActive(false);
        }

        // Decide which tab to show on start
        string targetID = "";

        if (restoreLastTab)
            targetID = PlayerPrefs.GetString(PREF_LAST_UIFLOW_TAB, "");

        if (string.IsNullOrEmpty(targetID))
            targetID = !string.IsNullOrEmpty(defaultTabID) ? defaultTabID : (tabs.Count > 0 ? tabs[0].tabID : "");

        // Show target tab immediately (no animation on startup)
        UITab target = tabs.Find(t => t.tabID == targetID);
        if (target == null && tabs.Count > 0) target = tabs[0];

        if (target != null && target.panel != null)
        {
            currentTab = target;
            currentTab.panel.gameObject.SetActive(true);
            currentTab.panel.anchoredPosition = Vector2.zero;

            SaveCurrentTab();
        }
    }

    public void OpenTab(string tabID)
    {
        if (isTransitioning) return;
        if (currentTab != null && currentTab.tabID == tabID) return;

        UITab target = tabs.Find(t => t.tabID == tabID);
        if (target == null || target.panel == null) return;

        // If currentTab is null (failsafe), just activate target
        if (currentTab == null || currentTab.panel == null)
        {
            for (int i = 0; i < tabs.Count; i++)
                if (tabs[i].panel != null) tabs[i].panel.gameObject.SetActive(false);

            target.panel.gameObject.SetActive(true);
            target.panel.anchoredPosition = Vector2.zero;
            currentTab = target;
            SaveCurrentTab();
            return;
        }

        StartCoroutine(SwitchTabsLeftToLeft(currentTab, target));
    }

    private void OnExitTab(UITab tab)
    {
        if (currentTab == tab && tabs.Count > 0)
            OpenTab(tabs[0].tabID);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        Debug.Log("Quit Game pressed (Editor)");
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    IEnumerator SwitchTabsLeftToLeft(UITab from, UITab to)
    {
        isTransitioning = true;

        to.panel.gameObject.SetActive(true);
        to.panel.anchoredPosition = -offscreenOffset;

        float t = 0f;
        Vector2 fromStart = from.panel.anchoredPosition;
        Vector2 fromEnd = fromStart - offscreenOffset;

        while (t < 1f)
        {
            t += Time.deltaTime / slideDuration;
            float eased = EaseInOut(t);
            from.panel.anchoredPosition = Vector2.Lerp(fromStart, fromEnd, eased);
            yield return null;
        }

        yield return new WaitForSeconds(newPanelDelay);

        t = 0f;
        Vector2 toStart = to.panel.anchoredPosition;
        Vector2 toEnd = Vector2.zero;

        while (t < 1f)
        {
            t += Time.deltaTime / slideDuration;
            float eased = EaseInOut(t);
            to.panel.anchoredPosition = Vector2.Lerp(toStart, toEnd, eased);
            yield return null;
        }

        from.panel.gameObject.SetActive(false);
        currentTab = to;

        SaveCurrentTab();

        isTransitioning = false;
    }

    private void SaveCurrentTab()
    {
        if (currentTab != null && !string.IsNullOrEmpty(currentTab.tabID))
        {
            PlayerPrefs.SetString(PREF_LAST_UIFLOW_TAB, currentTab.tabID);
            PlayerPrefs.Save();
        }
    }

    float EaseInOut(float t)
    {
        return t * t * (3f - 2f * t);
    }
}
