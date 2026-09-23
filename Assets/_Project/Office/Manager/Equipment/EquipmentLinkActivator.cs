using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Keep this coordinator active so locked children can appear immediately after purchase or save load.</summary>
public class EquipmentLinkActivator : MonoBehaviour
{
    private EquipmentManager manager;
    private readonly List<EquipmentLink> links = new();

    private void OnEnable() => StartCoroutine(BindAfterSave());

    private IEnumerator BindAfterSave()
    {
        while (EquipmentManager.Instance == null || GameSaveManager.Instance != null &&
            (!GameSaveManager.Instance.HasCompletedInitialLoad || GameSaveManager.Instance.IsApplyingSave))
            yield return null;

        links.Clear();
        foreach (var root in gameObject.scene.GetRootGameObjects())
            links.AddRange(root.GetComponentsInChildren<EquipmentLink>(true));
        manager = EquipmentManager.Instance;
        manager.PurchasesChanged += Refresh;
        Refresh();
    }

    private void Refresh()
    {
        if (manager == null) return;
        foreach (var link in links)
            if (link != null && !string.IsNullOrEmpty(link.itemID))
                link.gameObject.SetActive(manager.Purchased(link.itemID));
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (manager != null) manager.PurchasesChanged -= Refresh;
        manager = null;
    }
}
