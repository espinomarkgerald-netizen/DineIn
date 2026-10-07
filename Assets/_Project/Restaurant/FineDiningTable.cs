using UnityEngine;

/// <summary>Persistent scene binding; the supplied FBX remains the table's visual.</summary>
[DisallowMultipleComponent]
public sealed class FineDiningTable : MonoBehaviour
{
    [Min(1)] public int unlockDay = 1;
    public bool Unlocked => Owned && equipment != null || (GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1) >= unlockDay;
    [Header("Existing equipment ownership")]
    public Equipment equipment;
    public GameObject[] presentationObjects;
    public bool Owned => equipment == null || EquipmentManager.Instance != null && EquipmentManager.Instance.Purchased(equipment.itemID);
    private EquipmentManager equipmentManager;
    private System.Collections.IEnumerator Start()
    {
        while (EquipmentManager.Instance == null || GameSaveManager.Instance != null && !GameSaveManager.Instance.HasCompletedInitialLoad) yield return null;
        equipmentManager = EquipmentManager.Instance;
        equipmentManager.PurchasesChanged += RefreshPresentation;
        RefreshPresentation();
    }
    private void OnDestroy() { if (equipmentManager != null) equipmentManager.PurchasesChanged -= RefreshPresentation; }
    public void RefreshPresentation()
    {
        bool visible = Owned;
        if (presentationObjects != null) foreach (var target in presentationObjects) if (target != null) target.SetActive(visible);
        if (booth != null) booth.gameObject.SetActive(visible);
    }
    public string tableId;
    public string displayName;
    public Booth booth;
    public MeshRenderer diningSurface;
    [Header("Reservations presentation")]
    public Sprite reservationPreview;
    public int Capacity => booth != null ? booth.seats.FindAll(seat => seat != null).Count : 0;
    public bool Usable => Owned && Unlocked && booth != null && booth.isActiveAndEnabled && Capacity > 0;
}

