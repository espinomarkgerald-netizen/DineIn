using UnityEngine;

/// <summary>
/// Place one instance of this on any active GameObject in each scene that uses EquipmentLink.
/// Runs on Start (after all Awakes) so EquipmentManager.Instance is guaranteed to exist.
/// Activates all purchased equipment in the scene, including GameObjects that start inactive.
/// </summary>
public class EquipmentLinkActivator : MonoBehaviour
{
    private void Start()
    {
        if (EquipmentManager.Instance == null)
        {
            Debug.LogWarning("EquipmentLinkActivator: EquipmentManager.Instance is null.");
            return;
        }

        EquipmentLink[] allLinks = FindObjectsByType<EquipmentLink>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (var link in allLinks)
        {
            var session = MultiplayerSessionManager.Instance;
            // Authored multiplayer seating is available without campaign purchases.
            if (session != null && session.IsMultiplayerSession && link.gameObject.scene == session.gameObject.scene
                && link.GetComponentInChildren<Booth>(true) != null)
            {
                link.gameObject.SetActive(true);
                continue;
            }
            bool purchased = EquipmentManager.Instance.Purchased(link.itemID);
            link.gameObject.SetActive(purchased);
        }
    }
}
