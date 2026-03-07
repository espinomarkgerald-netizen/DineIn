using UnityEngine;

public class RoleIndicator : MonoBehaviour
{
    [SerializeField] private GameObject indicatorVisual;

    public void SetSelected(bool value)
    {
        if (indicatorVisual != null)
            indicatorVisual.SetActive(value);
    }
}