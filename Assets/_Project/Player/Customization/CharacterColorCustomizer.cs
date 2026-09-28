using UnityEngine;
using Photon.Pun;

public class CharacterColorCustomizer : MonoBehaviourPun
{
    [Header("Body Parts")]
    [SerializeField] private Renderer head;
    [SerializeField] private Renderer body;
    [SerializeField] private Renderer arms;
    [SerializeField] private Renderer legs;

    [Header("Color Options")]
    [SerializeField] private Color[] colorOptions;

    void Start()
    {
        RefreshFromData();
    }

    public void RefreshFromData()
    {
        ApplyIndices(PlayerCustomizationData.HeadColorIndex, PlayerCustomizationData.BodyColorIndex,
            PlayerCustomizationData.ArmsColorIndex, PlayerCustomizationData.LegsColorIndex);
    }
    public void ApplyIndices(int headIndex, int bodyIndex, int armsIndex, int legsIndex)
    {
        if (GetComponentInParent<DineIn.Appearance.PlayerAppearanceBinding>() != null) return;
        ApplyIndex(head, headIndex); ApplyIndex(body, bodyIndex); ApplyIndex(arms, armsIndex); ApplyIndex(legs, legsIndex);
    }

    public void ChangeHead(int dir) => Change(ref PlayerCustomizationData.HeadColorIndex, dir, head);
    public void ChangeBody(int dir) => Change(ref PlayerCustomizationData.BodyColorIndex, dir, body);
    public void ChangeArms(int dir) => Change(ref PlayerCustomizationData.ArmsColorIndex, dir, arms);
    public void ChangeLegs(int dir) => Change(ref PlayerCustomizationData.LegsColorIndex, dir, legs);

    void Change(ref int index, int dir, Renderer r)
    {
        if (colorOptions == null || colorOptions.Length == 0) return;

        index += dir;
        if (index < 0) index = colorOptions.Length - 1;
        if (index >= colorOptions.Length) index = 0;

        ApplyIndex(r, index);
    }

    void ApplyIndex(Renderer r, int idx)
    {
        if (r == null || colorOptions == null || colorOptions.Length == 0) return;
        r.material.color = colorOptions[Mathf.Clamp(idx, 0, colorOptions.Length - 1)];
    }
}
