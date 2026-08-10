using UnityEngine;
using Photon.Pun;

public class CharacterHatCustomizer : MonoBehaviourPun
{
    [SerializeField] private Transform hatAnchor;
    [SerializeField] private GameObject[] hatPrefabs;

    private GameObject currentHat;

    void Start()
    {
        RefreshFromData();
    }

    public void RefreshFromData()
    {
        ApplyHat(PlayerCustomizationData.EquippedHatId);
    }

    public void NextHat() => Change(1);
    public void PreviousHat() => Change(-1);

    void Change(int dir)
    {
        if (hatPrefabs == null || hatPrefabs.Length == 0) return;

        PlayerCustomizationData.EquippedHatId += dir;

        if (PlayerCustomizationData.EquippedHatId < 0)
            PlayerCustomizationData.EquippedHatId = hatPrefabs.Length - 1;

        if (PlayerCustomizationData.EquippedHatId >= hatPrefabs.Length)
            PlayerCustomizationData.EquippedHatId = 0;

        ApplyHat(PlayerCustomizationData.EquippedHatId);
    }

    void ApplyHat(int index)
    {
        if (hatAnchor == null || hatPrefabs[index] == null) return;

        if (currentHat != null) Destroy(currentHat);

        currentHat = Instantiate(hatPrefabs[index], hatAnchor);
        currentHat.transform.localPosition = Vector3.zero;
        currentHat.transform.localRotation = Quaternion.identity;
    }
}
