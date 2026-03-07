using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MoneyBubbleUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text amountText;

    private MoneyPickup money;
    private PlayerMovement player;

    private void Awake()
    {
        if (button == null)
            button = GetComponentInChildren<Button>(true);

        player = FindFirstObjectByType<PlayerMovement>();
    }

    public void Init(int amount, MoneyPickup m)
    {
        money = m;

        if (amountText != null)
            amountText.text = amount.ToString();

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClickCollect);
        }
    }

    private void OnClickCollect()
    {
        if (money == null) return;
        if (player == null) return;

        player.UI_MoveTo(money);

        Destroy(gameObject);
    }
}