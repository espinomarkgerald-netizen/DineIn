using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class UIFollowWorldPoint : MonoBehaviour
{
    public Transform target;
    public Vector3 worldOffset = new Vector3(0f, 2.2f, 0f);

    [Header("Screen Offset")]
    [SerializeField] private Vector2 screenOffset;
    [SerializeField] private float stackStepY = 40f;

    [Header("Block When UI Open")]
    [SerializeField] private bool hideWhenGameplayUIBlocked = true;

    private RectTransform rect;
    private Camera cam;
    private int stackIndex;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Init(Transform followTarget, Vector3 offset, Camera followCam)
    {
        target = followTarget;
        worldOffset = offset;
        cam = followCam != null ? followCam : Camera.main;
    }

    public void SetScreenOffset(Vector2 offset)
    {
        screenOffset = offset;
    }

    public void SetStackIndex(int index)
    {
        stackIndex = Mathf.Max(0, index);
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            SetVisible(false);
            return;
        }

        if (hideWhenGameplayUIBlocked && GameplayUIBlocker.IsBlocked())
        {
            SetVisible(false);
            return;
        }

        if (cam == null)
            cam = Camera.main;

        if (cam == null)
        {
            SetVisible(false);
            return;
        }

        Vector3 screenPos = cam.WorldToScreenPoint(target.position + worldOffset);

        if (screenPos.z < 0f)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);

        screenPos.x += screenOffset.x;
        screenPos.y += screenOffset.y + (stackIndex * stackStepY);

        rect.position = screenPos;
    }

    private void SetVisible(bool value)
    {
        if (canvasGroup == null) return;

        canvasGroup.alpha = value ? 1f : 0f;
        canvasGroup.blocksRaycasts = value;
        canvasGroup.interactable = value;
    }
}