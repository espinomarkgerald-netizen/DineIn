using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TabManager : MonoBehaviour
{
    [System.Serializable]
    public class Tab
    {
        public Button button;
        public GameObject content;
        [Tooltip("Optional ID so we can restore a specific tab.")]
        public string tabID;
    }

    [Header("Tabs (Add as many as you want)")]
    [SerializeField] private List<Tab> tabs = new List<Tab>();

    [Header("Button Scale Settings")]
    [SerializeField] private Vector3 inactiveScale = Vector3.one;
    [SerializeField] private Vector3 activeScale = new Vector3(1.1f, 1.1f, 1.1f);

    [Header("Optional Colors")]
    [SerializeField] private Color inactiveColor = Color.white;
    [SerializeField] private Color activeColor = Color.white;

    [Header("Startup")]
    [SerializeField] private bool restoreLastTab = true;

    [Tooltip("If restoreLastTab is OFF or no saved tab exists, this index is selected.")]
    [SerializeField] private int defaultTabIndex = 0;

    private const string PREF_LAST_TABMANAGER_TAB = "TabManager_LastTab";

    private Tab currentTab;

    private void Awake()
    {
        foreach (Tab tab in tabs)
        {
            if (tab.button == null) continue;

            Tab localTab = tab;
            tab.button.onClick.RemoveListener(() => SwitchToTab(localTab));
            tab.button.onClick.AddListener(() => SwitchToTab(localTab));
        }
    }

    private void Start()
    {
        if (tabs.Count == 0) return;

        // Hide all first to avoid “ghost states”
        foreach (var tab in tabs)
        {
            if (tab.content != null) tab.content.SetActive(false);
            if (tab.button != null) tab.button.transform.localScale = inactiveScale;
            if (tab.button != null && tab.button.image != null) tab.button.image.color = inactiveColor;
        }

        Tab startTab = null;

        if (restoreLastTab)
        {
            string saved = PlayerPrefs.GetString(PREF_LAST_TABMANAGER_TAB, "");
            if (!string.IsNullOrEmpty(saved))
                startTab = tabs.Find(t => t.tabID == saved);
        }

        if (startTab == null)
        {
            int idx = Mathf.Clamp(defaultTabIndex, 0, tabs.Count - 1);
            startTab = tabs[idx];
        }

        SwitchToTab(startTab);
    }

    public void SwitchToTab(Tab selectedTab)
    {
        if (selectedTab == null) return;

        foreach (Tab tab in tabs)
        {
            bool isActive = tab == selectedTab;

            if (tab.content != null)
                tab.content.SetActive(isActive);

            if (tab.button != null)
                tab.button.transform.localScale = isActive ? activeScale : inactiveScale;

            if (tab.button != null && tab.button.image != null)
                tab.button.image.color = isActive ? activeColor : inactiveColor;
        }

        currentTab = selectedTab;

        // Save selection if it has an ID
        if (!string.IsNullOrEmpty(currentTab.tabID))
        {
            PlayerPrefs.SetString(PREF_LAST_TABMANAGER_TAB, currentTab.tabID);
            PlayerPrefs.Save();
        }
    }
}
