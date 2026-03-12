using UnityEngine;

public class RoleIndicator : MonoBehaviour
{
    [Header("Indicator")]
    [SerializeField] private GameObject indicatorVisual;

    [Header("Floating Burger")]
    [SerializeField] private Transform burgerVisual;
    [SerializeField] private float floatHeight = 0.15f;
    [SerializeField] private float floatSpeed = 2f;
    [SerializeField] private float spinSpeed = 90f;

    private Vector3 burgerStartLocalPos;
    private bool isSelected;
    private float spinAngle;

    private void Awake()
    {
        if (burgerVisual != null)
            burgerStartLocalPos = burgerVisual.localPosition;
    }

    private void Update()
    {
        if (!isSelected || burgerVisual == null) return;

        float yOffset = Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        burgerVisual.localPosition = burgerStartLocalPos + new Vector3(0f, yOffset, 0f);

        spinAngle += spinSpeed * Time.deltaTime;
        burgerVisual.localRotation = Quaternion.Euler(0f, spinAngle, 0f);
    }

    public void SetSelected(bool value)
    {
        isSelected = value;

        if (indicatorVisual != null)
            indicatorVisual.SetActive(value);

        if (burgerVisual != null)
        {
            burgerVisual.gameObject.SetActive(value);

            if (value)
            {
                burgerVisual.localPosition = burgerStartLocalPos;
                burgerVisual.localRotation = Quaternion.identity;
                spinAngle = 0f;
            }
        }
    }
}