using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BoothMessCleanUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("UI")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private Slider radialFill;

    [Header("Billboard")]
    [SerializeField] private Transform billboardRoot;
    [SerializeField] private Camera cam;
    [SerializeField] private bool faceCamera = true;

    [Header("Blocked Feedback")]
    [SerializeField] private float blockedMessageSeconds = 0.9f;

    private Booth booth;
    private bool isHolding;
    private bool automatedCleaning;
    private float holdTimer;
    private float blockedTimer;

    public bool IsAutomatedCleaning => automatedCleaning && isHolding;

    public void Setup(Booth targetBooth, Camera sceneCamera)
    {
        booth = targetBooth;
        cam = sceneCamera;

        if (billboardRoot == null)
            billboardRoot = transform;

        AssignWorldCamera();
        ResetUI();
    }

    private void OnEnable()
    {
        AssignWorldCamera();
        ResetUI();
    }

    private void Update()
    {
        if (booth == null)
            return;

        if (!booth.IsDirty)
        {
            ResetUI();
            return;
        }

        if (blockedTimer > 0f)
        {
            blockedTimer -= Time.deltaTime;

            if (blockedTimer <= 0f && !isHolding && label != null)
                label.text = "Clean";
        }

        if (!isHolding)
            return;

        if (!automatedCleaning && !CanCurrentPlayerClean(out string blockedReason))
        {
            StopHold(blockedReason);
            return;
        }

        holdTimer += Time.deltaTime;

        float pct = Mathf.Clamp01(holdTimer / Mathf.Max(0.05f, booth.MessHoldSeconds));

        if (radialFill != null)
            radialFill.value = pct;

        if (pct < 1f)
            return;

        isHolding = false;
        automatedCleaning = false;
        holdTimer = 0f;
        booth.CleanMess();
        ResetUI();
    }

    private void LateUpdate()
    {
        if (!faceCamera || billboardRoot == null)
            return;

        if (cam == null)
            cam = Camera.main;

        if (cam == null)
            return;

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            return;

        billboardRoot.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (automatedCleaning || booth == null || !booth.IsDirty)
            return;

        if (!CanCurrentPlayerClean(out string blockedReason))
        {
            ShowBlocked(blockedReason);
            return;
        }

        isHolding = true;
        automatedCleaning = false;
        holdTimer = 0f;
        blockedTimer = 0f;

        if (label != null)
            label.text = "Cleaning...";

        if (radialFill != null)
            radialFill.value = 0f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (automatedCleaning)
            return;

        StopHold();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (automatedCleaning)
            return;

        StopHold();
    }

    public bool BeginAutomatedCleaning()
    {
        if (booth == null || !booth.CanCleanMessNow)
            return false;

        automatedCleaning = true;
        isHolding = true;
        holdTimer = 0f;
        blockedTimer = 0f;

        if (label != null)
            label.text = "Cleaning...";

        if (radialFill != null)
            radialFill.value = 0f;

        return true;
    }

    public void CancelAutomatedCleaning()
    {
        if (!automatedCleaning)
            return;

        ResetUI();
    }

    private void StopHold()
    {
        isHolding = false;
        automatedCleaning = false;
        holdTimer = 0f;

        if (label != null)
            label.text = "Clean";

        if (radialFill != null)
            radialFill.value = 0f;
    }

    private void StopHold(string blockedReason)
    {
        isHolding = false;
        automatedCleaning = false;
        holdTimer = 0f;

        if (label != null)
            label.text = blockedReason;

        if (radialFill != null)
            radialFill.value = 0f;

        blockedTimer = blockedMessageSeconds;
    }

    private void ShowBlocked(string blockedReason)
    {
        isHolding = false;
        automatedCleaning = false;
        holdTimer = 0f;

        if (label != null)
            label.text = blockedReason;

        if (radialFill != null)
            radialFill.value = 0f;

        blockedTimer = blockedMessageSeconds;
    }

    private void ResetUI()
    {
        isHolding = false;
        automatedCleaning = false;
        holdTimer = 0f;
        blockedTimer = 0f;

        if (label != null)
            label.text = "Clean";

        if (radialFill != null)
            radialFill.value = 0f;
    }

    private void AssignWorldCamera()
    {
        if (cam == null)
            cam = Camera.main;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.WorldSpace && cam != null)
            canvas.worldCamera = cam;
    }

    private bool CanCurrentPlayerClean(out string blockedReason)
    {
        if (!IsBusserRoleActive())
        {
            blockedReason = "Busser Only";
            return false;
        }

        if (IsBusserHandsBusy())
        {
            blockedReason = "Hands Full";
            return false;
        }

        blockedReason = null;
        return true;
    }

    private bool IsBusserHandsBusy()
    {
        if (BusserHands.Instance == null)
            return false;

        return BusserHands.Instance.HasTray;
    }

    private bool IsBusserRoleActive()
    {
        MonoBehaviour roleManager = FindRoleManager();
        if (roleManager == null)
            return true;

        object roleValue =
            GetMemberValue(roleManager, "CurrentRole") ??
            GetMemberValue(roleManager, "currentRole") ??
            GetMemberValue(roleManager, "SelectedRole") ??
            GetMemberValue(roleManager, "selectedRole") ??
            GetMemberValue(roleManager, "ActiveRole") ??
            GetMemberValue(roleManager, "activeRole");

        if (roleValue == null)
            return true;

        string roleText = roleValue.ToString();
        return !string.IsNullOrWhiteSpace(roleText) &&
               roleText.IndexOf("Busser", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private MonoBehaviour FindRoleManager()
    {
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour mb = behaviours[i];
            if (mb == null)
                continue;

            if (mb.GetType().Name == "RoleManager")
                return mb;
        }

        return null;
    }

    private object GetMemberValue(object source, string memberName)
    {
        if (source == null)
            return null;

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Type type = source.GetType();

        FieldInfo field = type.GetField(memberName, flags);
        if (field != null)
            return field.GetValue(source);

        PropertyInfo prop = type.GetProperty(memberName, flags);
        if (prop != null && prop.CanRead)
            return prop.GetValue(source);

        return null;
    }
}
