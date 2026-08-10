using UnityEngine;

public class EquipmentLink : MonoBehaviour
{
    public string itemID;

    private void Start()
    {
        // Reactivate if this equipment was purchased in a previous scene load.
        if (EquipmentManager.Instance != null && EquipmentManager.Instance.Purchased(itemID))
            gameObject.SetActive(true);
    }
}

