using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Prefab-authored shortcuts to the original validated command backend.</summary>
public sealed class DeveloperSettingsView : MonoBehaviour
{
    [Serializable] public class Option
    {
        public string label, command;
        [TextArea] public string description;
        public bool needsValue, changesSave;
        public int fixedValue;
        public TMP_InputField input;
        public Button button;
    }
    public Option[] options;
    public Button[] tabs;
    public GameObject[] pages;
    public Sprite selectedSprite, idleSprite;
    public TMP_Text result, warning;
    public Button close, confirm, cancel;
    public GameObject confirmation;
    private DevSettingsConsole owner;
    private Option pending;
    private int pendingValue;
    private bool bound;
    private Button practiceButton;
    private TMP_Text practiceTitle;
    private bool confirmPractice;
    public void Bind(DevSettingsConsole console)
    {
        if(bound)return;
        bound=true; owner=console;
        for(int i=0;i<tabs.Length;i++) { int index=i; tabs[i].onClick.AddListener(()=>SelectTab(index)); }
        foreach(var option in options) { var captured=option; option.button.onClick.AddListener(()=>Prepare(captured)); }
        close.onClick.AddListener(owner.ClosePanel);
        cancel.onClick.AddListener(()=>confirmation.SetActive(false));
        confirm.onClick.AddListener(ExecutePending);
        InstallRewardPreviewOption();
        InstallPracticeOption();
        SelectTab(0);
    }

    private void InstallPracticeOption()
    {
        if (!DeveloperPracticeSession.IsWindows || options.Length == 0 || options[0].button == null)
            return;
        Transform original = options[0].button.transform.parent;
        var row = Instantiate(original.gameObject, original.parent);
        row.name = "Practice";
        row.transform.SetSiblingIndex(original.GetSiblingIndex());
        practiceButton = row.GetComponentInChildren<Button>(true);
        var labels = row.GetComponentsInChildren<TMP_Text>(true);
        foreach (var input in row.GetComponentsInChildren<TMP_InputField>(true))
            input.gameObject.SetActive(false);
        foreach (var label in labels)
        {
            if (label.GetComponentInParent<TMP_InputField>(true) != null) continue;
            if (label.transform.IsChildOf(practiceButton.transform)) label.text = "Select";
            else if (practiceTitle == null) practiceTitle = label;
            else label.text = "Practice controls are available in solo restaurants.";
        }
        practiceButton.onClick.RemoveAllListeners();
        practiceButton.onClick.AddListener(() =>
        {
            pending = null;
            confirmPractice = true;
            warning.text = DeveloperPracticeSession.IsActive
                ? "Exit Practice and return to the game menu?"
                : "Start a fresh practice session in this restaurant? Your current shift will end. Practice progress won’t save.";
            confirmation.SetActive(true);
            confirmation.transform.SetAsLastSibling();
        });
        GameplayButtonSound.Ensure(practiceButton);
        row.SetActive(true);
    }
    private void InstallRewardPreviewOption()
    {
        Option template = Array.Find(options, option => option.command == "cardpayment");
        if (template?.button == null || template.button.transform.parent == null) return;
        Transform original = template.button.transform.parent;
        var row = Instantiate(original.gameObject, original.parent);
        row.name = "Preview Daily Reward";
        var button = row.GetComponentInChildren<Button>(true);
        if (button == null) { Destroy(row); return; }
        int index = 0;
        foreach (var label in row.GetComponentsInChildren<TMP_Text>(true))
            if (!label.transform.IsChildOf(button.transform)) label.text = index++ == 0 ? "Daily reward preview" : "Test the slot machine without finishing a day. No currency or attempts consumed.";
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => owner.TryExecuteCode("dailyreward()"));
        GameplayButtonSound.Ensure(button);
        row.SetActive(true);
    }
    private void OnEnable()
    {
        if (owner == null) return;
        foreach (var option in options)
            option.button.interactable = owner.CanUseCommand(option.command);
        if (practiceButton != null)
        {
            practiceButton.interactable = DeveloperPracticeSession.IsActive || DeveloperPracticeSession.CanStart;
            if (practiceTitle != null)
                practiceTitle.text = DeveloperPracticeSession.IsActive ? "Exit Practice" : "Start Practice";
        }
        owner.TryExecuteCode("status()");
    }
    private void OnDisable() { if(confirmation!=null) confirmation.SetActive(false); pending=null; confirmPractice=false; }
    public void SelectTab(int index)
    {
        for(int i=0;i<pages.Length;i++)
        {
            pages[i].SetActive(i==index);
            tabs[i].GetComponent<Image>().sprite=i==index?selectedSprite:idleSprite;
        }
    }
    private void Prepare(Option option)
    {
        confirmPractice=false;
        pending=option; pendingValue=option.fixedValue;
        if(option.needsValue && option.input!=null && !int.TryParse(option.input.text,out pendingValue))
        { result.text="Enter a whole number."; pending=null; return; }
        if(option.changesSave)
        { warning.text=option.description; confirmation.SetActive(true); confirmation.transform.SetAsLastSibling(); }
        else ExecutePending();
    }
    private void ExecutePending()
    {
        if (confirmPractice)
        {
            confirmPractice = false;
            confirmation.SetActive(false);
            owner.TryExecuteCode("practice()");
            return;
        }
        if(pending==null)return;
        var option=pending; pending=null;
        confirmation.SetActive(false);
        // Keep original validation, account authorization, error handling and async results.
        owner.TryExecuteCode(option.command+"("+(option.needsValue?pendingValue.ToString(System.Globalization.CultureInfo.InvariantCulture):"")+")");
    }
}
