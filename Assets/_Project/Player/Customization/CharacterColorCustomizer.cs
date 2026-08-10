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
        ApplyIndex(head, PlayerCustomizationData.HeadColorIndex);
        ApplyIndex(body, PlayerCustomizationData.BodyColorIndex);
        ApplyIndex(arms, PlayerCustomizationData.ArmsColorIndex);
        ApplyIndex(legs, PlayerCustomizationData.LegsColorIndex);
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
        if (r == null) return;
        r.material.color = colorOptions[idx];
    }
}
