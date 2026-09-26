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
    public void Bind(DevSettingsConsole console)
    {
        if(bound)return;
        bound=true; owner=console;
        for(int i=0;i<tabs.Length;i++) { int index=i; tabs[i].onClick.AddListener(()=>SelectTab(index)); }
        foreach(var option in options) { var captured=option; option.button.onClick.AddListener(()=>Prepare(captured)); }
        close.onClick.AddListener(owner.ClosePanel);
        cancel.onClick.AddListener(()=>confirmation.SetActive(false));
        confirm.onClick.AddListener(ExecutePending);
        SelectTab(0);
    }
    private void OnEnable() { if(owner!=null)owner.TryExecuteCode("status()"); }
    private void OnDisable() { if(confirmation!=null) confirmation.SetActive(false); pending=null; }
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
        pending=option; pendingValue=option.fixedValue;
        if(option.needsValue && option.input!=null && !int.TryParse(option.input.text,out pendingValue))
        { result.text="Enter a whole number."; pending=null; return; }
        if(option.changesSave)
        { warning.text=option.description; confirmation.SetActive(true); confirmation.transform.SetAsLastSibling(); }
        else ExecutePending();
    }
    private void ExecutePending()
    {
        if(pending==null)return;
        var option=pending; pending=null;
        confirmation.SetActive(false);
        // Keep original validation, account authorization, error handling and async results.
        owner.TryExecuteCode(option.command+"("+(option.needsValue?pendingValue.ToString(System.Globalization.CultureInfo.InvariantCulture):"")+")");
    }
}
