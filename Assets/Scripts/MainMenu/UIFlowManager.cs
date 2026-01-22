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

    [Header("Animation")]
    public float slideDuration = 0.25f;
    public Vector2 offscreenOffset = new Vector2(1600f, 0f); // horizontal offset
    public float newPanelDelay = 0.1f; // small delay before new panel slides in

    private UITab currentTab;
    private bool isTransitioning;

    void Start()
    {
        // Hook up tab open buttons automatically
        foreach (var tab in tabs)
        {
            if (tab.tabButton != null)
            {
                string id = tab.tabID; // capture local variable
                tab.tabButton.onClick.AddListener(() => OpenTab(id));
            }

            // Hook up exit/back button for this tab
            if (tab.exitButton != null)
            {
                tab.exitButton.onClick.AddListener(() => OnExitTab(tab));
            }
        }

        // Initialize: show first tab, hide others
        for (int i = 0; i < tabs.Count; i++)
        {
            if (i == 0)
            {
                currentTab = tabs[i];
                tabs[i].panel.gameObject.SetActive(true);
                tabs[i].panel.anchoredPosition = Vector2.zero;
            }
            else
            {
                tabs[i].panel.gameObject.SetActive(false);
            }
        }
    }

    public void OpenTab(string tabID)
    {
        if (isTransitioning) return;
        if (currentTab != null && currentTab.tabID == tabID) return;

        UITab target = tabs.Find(t => t.tabID == tabID);
        if (target == null) return;

        StartCoroutine(SwitchTabsLeftToLeft(currentTab, target));
    }

    private void OnExitTab(UITab tab)
    {
        // Optional: define behavior when a panel's exit button is pressed
        // For example, return to first tab (Main Menu)
        if (currentTab == tab && tabs.Count > 0)
        {
            UITab mainTab = tabs[0];
            if (mainTab != null)
            {
                OpenTab(mainTab.tabID);
            }
        }
    }

    IEnumerator SwitchTabsLeftToLeft(UITab from, UITab to)
    {
        isTransitioning = true;

        // Prepare new panel offscreen to the left
        to.panel.gameObject.SetActive(true);
        to.panel.anchoredPosition = -offscreenOffset; // start from left

        // Animate old panel sliding left
        float t = 0f;
        Vector2 fromStart = from.panel.anchoredPosition;
        Vector2 fromEnd = fromStart - offscreenOffset; // move further left

        while (t < 1f)
        {
            t += Time.deltaTime / slideDuration;
            float eased = EaseInOut(t);
            from.panel.anchoredPosition = Vector2.Lerp(fromStart, fromEnd, eased);
            yield return null;
        }
        from.panel.anchoredPosition = fromEnd;

        // Small delay before new panel slides in
        yield return new WaitForSeconds(newPanelDelay);

        // Animate new panel sliding from left into position
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
        to.panel.anchoredPosition = toEnd;

        // Hide old panel
        from.panel.gameObject.SetActive(false);

        currentTab = to;
        isTransitioning = false;
    }

    float EaseInOut(float t)
    {
        return t * t * (3f - 2f * t);
    }
}
