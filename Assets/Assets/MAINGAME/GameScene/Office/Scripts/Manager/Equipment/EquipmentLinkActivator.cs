using UnityEngine;

/// <summary>
/// Place one instance of this on any active GameObject in each scene that uses EquipmentLink.
/// Runs on Awake and activates all purchased equipment in the scene,
/// including GameObjects that start inactive (which never call their own Start/Awake).
/// </summary>
public class EquipmentLinkActivator : MonoBehaviour
{
    private void Awake()
    {
        if (EquipmentManager.Instance == null) return;

        EquipmentLink[] allLinks = FindObjectsByType<EquipmentLink>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (var link in allLinks)
        {
            bool purchased = EquipmentManager.Instance.Purchased(link.itemID);
            link.gameObject.SetActive(purchased);
        }
    }
}
