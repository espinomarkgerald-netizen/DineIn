using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BoothHoldToCleanUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("Refs")]
    [SerializeField] private Booth booth;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Slider radialFill;
    [SerializeField] private Camera cam;

    [Header("World Space")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.35f, 0f);
    [SerializeField] private bool followInWorld = false;

    [Header("Cleaning")]
    [SerializeField] private float holdSeconds = 1.25f;

    private bool isHolding;
    private float holdTimer;

    private void Awake()
    {
        if (booth == null)
            booth = GetComponentInParent<Booth>();

        if (followTarget == null && booth != null)
            followTarget = booth.transform;

        if (cam == null)
            cam = Camera.main;

        ResetUI();
    }

    private void OnEnable()
    {
        ResetUI();
    }

    private void OnDisable()
    {
        ResetUI();
    }

    private void Update()
    {
        if (booth == null)
            return;

        if (followInWorld && followTarget != null)
            transform.position = followTarget.position + worldOffset;

        if (!booth.IsDirty)
        {
            ResetUI();
            return;
        }

        if (label != null)
            label.text = "Clean";

        if (!isHolding)
            return;

        holdTimer += Time.deltaTime;

        float pct = Mathf.Clamp01(holdTimer / Mathf.Max(0.05f, holdSeconds));
        if (radialFill != null)
            radialFill.value = pct;

        if (pct >= 1f)
        {
            isHolding = false;
            holdTimer = 0f;

            booth.CleanMess();

            if (radialFill != null)
                radialFill.value = 0f;
        }
    }

    private void LateUpdate()
    {
        if (cam == null)
            cam = Camera.main;

        if (cam == null)
            return;

        transform.LookAt(
            transform.position + cam.transform.rotation * Vector3.forward,
            cam.transform.rotation * Vector3.up
        );
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (booth == null || !booth.IsDirty)
            return;

        isHolding = true;
        holdTimer = 0f;

        if (radialFill != null)
            radialFill.value = 0f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        StopHolding();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StopHolding();
    }

    private void StopHolding()
    {
        isHolding = false;
        holdTimer = 0f;

        if (radialFill != null)
            radialFill.value = 0f;
    }

    private void ResetUI()
    {
        isHolding = false;
        holdTimer = 0f;

        if (label != null)
            label.text = "Clean";

        if (radialFill != null)
            radialFill.value = 0f;
    }
}