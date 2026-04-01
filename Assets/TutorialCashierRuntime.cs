using System.Globalization;
using TMPro;
using UnityEngine;

public class TutorialCashierTotalsReader : MonoBehaviour
{
    [Header("Source Texts")]
    [SerializeField] private TMP_Text foodPriceSource;
    [SerializeField] private TMP_Text drinkPriceSource;
    [SerializeField] private TMP_Text tableNumberSource;

    [Header("Target Texts")]
    [SerializeField] private TMP_Text receivedTarget;
    [SerializeField] private TMP_Text totalTarget;
    [SerializeField] private TMP_Text changeTarget;
    [SerializeField] private TMP_Text tableNumberTarget;

    [Header("Tutorial Payment")]
    [SerializeField] private bool autoPickReceived = true;
    [SerializeField] private int forcedReceivedAmount = 0;

    [Header("Timing")]
    [SerializeField] private bool refreshOnEnable = true;
    [SerializeField] private float delayedRefreshSeconds = 0.1f;

    private void OnEnable()
    {
        if (refreshOnEnable)
            Invoke(nameof(RefreshValues), delayedRefreshSeconds);
    }

    [ContextMenu("Refresh Values")]
    public void RefreshValues()
    {
        int food = ReadMoney(foodPriceSource);
        int drink = ReadMoney(drinkPriceSource);
        int total = food + drink;

        int received = autoPickReceived
            ? GetSuggestedReceived(total)
            : Mathf.Max(forcedReceivedAmount, total);

        int change = Mathf.Max(0, received - total);

        if (tableNumberTarget != null && tableNumberSource != null)
            tableNumberTarget.text = tableNumberSource.text;

        if (totalTarget != null)
            totalTarget.text = FormatMoney(total);

        if (receivedTarget != null)
            receivedTarget.text = FormatMoney(received);

        if (changeTarget != null)
            changeTarget.text = FormatMoney(change);

        Debug.Log($"[TutorialCashierTotalsReader] Food={food} Drink={drink} Total={total} Received={received} Change={change}");
    }

    private int ReadMoney(TMP_Text textComp)
    {
        if (textComp == null || string.IsNullOrWhiteSpace(textComp.text))
            return 0;

        string raw = textComp.text.Trim();
        raw = raw.Replace("₱", "");
        raw = raw.Replace(",", "");

        if (float.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out float value))
            return Mathf.RoundToInt(value);

        return 0;
    }

    private int GetSuggestedReceived(int total)
    {
        if (total <= 50) return 50;
        if (total <= 100) return 100;
        if (total <= 200) return 200;
        if (total <= 500) return 500;
        return 1000;
    }

    private string FormatMoney(int value)
    {
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }
}