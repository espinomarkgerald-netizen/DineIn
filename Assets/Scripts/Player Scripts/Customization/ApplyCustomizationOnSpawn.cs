using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
// ✅ Force the Photon Hashtable (avoids System.Collections.Hashtable confusion)
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class ApplyCustomizationOnSpawn : MonoBehaviourPunCallbacks
{
    [System.Serializable]
    public class TintTarget
    {
        public Renderer renderer;

        [Header("Tint Selection (choose one approach)")]
        [Tooltip("Material slots to tint (0-based). If empty, uses Material Name Contains instead.")]
        public List<int> materialIndicesToTint = new List<int>();

        [Tooltip("Tint materials whose name CONTAINS this text. Example: 'Material.002'. If empty and no indices, tints ALL materials.")]
        public string materialNameContains = "";

        [Tooltip("Log what materials were tinted + which Photon value was used.")]
        public bool debugThisTarget = false;
    }

    [Header("Tint Targets (Assign your renderers here)")]
    [SerializeField] private TintTarget head = new TintTarget();
    [SerializeField] private TintTarget body = new TintTarget();
    [SerializeField] private TintTarget arms = new TintTarget();
    [SerializeField] private TintTarget legs = new TintTarget();

    [Header("Color Options")]
    [SerializeField] private Color[] colorOptions;

    [Header("Hats")]
    [SerializeField] private Transform hatAnchor;
    [SerializeField] private GameObject[] hatPrefabs;

    [Header("NameTag")]
    [SerializeField] private NameTagBillboard nameTag;
    [SerializeField] private Transform headFollowTarget;

    [Header("Debug")]
    [SerializeField] private bool debugPhotonValues = false;

    private GameObject currentHat;

    // Shader property IDs (supports URP + Standard)
    private static readonly int ColorProp = Shader.PropertyToID("_Color");
    private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");

    private MaterialPropertyBlock mpb;

    private void Awake()
    {
        mpb = new MaterialPropertyBlock();
    }

    private void Start()
    {
        ApplyFromPhoton();
    }

    private void ApplyFromPhoton()
    {
        if (photonView == null || photonView.Owner == null) return;

        Hashtable props = photonView.Owner.CustomProperties;

        if (debugPhotonValues)
        {
            int headI = GetInt(props, "HeadI", -999);
            int bodyI = GetInt(props, "BodyI", -999);
            int armsI = GetInt(props, "ArmsI", -999);
            int legsI = GetInt(props, "LegsI", -999);
            int hatI  = GetInt(props, "HatI",  -999);

            Debug.Log($"[ApplyCustomizationOnSpawn] Owner={photonView.Owner.NickName} " +
                      $"HeadI={headI} BodyI={bodyI} ArmsI={armsI} LegsI={legsI} HatI={hatI}");
        }

        // ---------- Name ----------
        if (nameTag != null)
        {
            string uname =
                (props != null && props.TryGetValue("Username", out object uObj) && uObj != null)
                ? uObj.ToString()
                : photonView.Owner.NickName;

            nameTag.SetName(uname);

            if (headFollowTarget != null) nameTag.SetFollowTarget(headFollowTarget);
            else if (head != null && head.renderer != null) nameTag.SetFollowTarget(head.renderer.transform);
            else nameTag.SetFollowTarget(transform);
        }

        // ---------- Colors ----------
        ApplyColor(props, "HeadI", head);
        ApplyColor(props, "BodyI", body);
        ApplyColor(props, "ArmsI", arms);
        ApplyColor(props, "LegsI", legs);

        // ---------- Hat ----------
        int hatIndex = GetInt(props, "HatI", 0);
        ApplyHat(hatIndex);
    }

    private void ApplyColor(Hashtable props, string key, TintTarget target)
    {
        if (target == null || target.renderer == null) return;
        if (colorOptions == null || colorOptions.Length == 0) return;

        int idx = GetInt(props, key, 0);
        idx = Mathf.Clamp(idx, 0, colorOptions.Length - 1);
        Color c = colorOptions[idx];

        Material[] mats = target.renderer.sharedMaterials;
        int matCount = (mats != null) ? mats.Length : 0;
        if (matCount <= 0) return;

        // 1) If indices provided, tint those
        if (target.materialIndicesToTint != null && target.materialIndicesToTint.Count > 0)
        {
            for (int k = 0; k < target.materialIndicesToTint.Count; k++)
            {
                int i = target.materialIndicesToTint[k];
                if (i < 0 || i >= matCount) continue;

                SetRendererColor(target.renderer, i, c);

                if (target.debugThisTarget)
                    Debug.Log($"[Tint:{key}] Tinted index {i} mat='{mats[i]?.name}' colorIdx={idx}");
            }
            return;
        }

        // 2) Otherwise, if name filter provided, tint any matching materials
        if (!string.IsNullOrEmpty(target.materialNameContains))
        {
            bool tintedAny = false;

            for (int i = 0; i < matCount; i++)
            {
                string matName = mats[i] != null ? mats[i].name : "";
                if (!string.IsNullOrEmpty(matName) && matName.Contains(target.materialNameContains))
                {
                    SetRendererColor(target.renderer, i, c);
                    tintedAny = true;

                    if (target.debugThisTarget)
                        Debug.Log($"[Tint:{key}] Tinted by name match index {i} mat='{matName}' colorIdx={idx}");
                }
            }

            if (!tintedAny && target.debugThisTarget)
            {
                Debug.LogWarning($"[Tint:{key}] No materials matched name '{target.materialNameContains}'. MatCount={matCount}.");
            }

            return;
        }

        // 3) Last fallback: tint all materials
        for (int i = 0; i < matCount; i++)
            SetRendererColor(target.renderer, i, c);

        if (target.debugThisTarget)
            Debug.Log($"[Tint:{key}] Tinted ALL materials. MatCount={matCount} colorIdx={idx}");
    }

    private void SetRendererColor(Renderer r, int materialIndex, Color c)
    {
        r.GetPropertyBlock(mpb, materialIndex);
        mpb.SetColor(BaseColorProp, c); // URP
        mpb.SetColor(ColorProp, c);     // Built-in/Standard
        r.SetPropertyBlock(mpb, materialIndex);
    }

    private void ApplyHat(int hatIndex)
    {
        if (hatAnchor == null) return;
        if (hatPrefabs == null || hatPrefabs.Length == 0) return;

        hatIndex = Mathf.Clamp(hatIndex, 0, hatPrefabs.Length - 1);
        GameObject prefab = hatPrefabs[hatIndex];
        if (prefab == null) return;

        if (currentHat != null) Destroy(currentHat);

        currentHat = Instantiate(prefab, hatAnchor);
        currentHat.transform.localPosition = Vector3.zero;
        currentHat.transform.localRotation = Quaternion.identity;
    }

    private int GetInt(Hashtable props, string key, int fallback)
    {
        if (props == null || !props.ContainsKey(key) || props[key] == null)
            return fallback;

        object o = props[key];

        if (o is int i) return i;
        if (o is byte b) return b;
        if (o is short s) return s;
        if (o is string str && int.TryParse(str, out int parsed)) return parsed;

        return fallback;
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (photonView == null || photonView.Owner == null) return;
        if (targetPlayer != photonView.Owner) return;

        ApplyFromPhoton();
    }
}
  