using UnityEngine;

public class TutorialRoleHighlight : MonoBehaviour
{
    [SerializeField] private GameObject currentHighlight;

    public void Show(GameObject target)
    {
        Hide();

        if (target == null)
            return;

        currentHighlight = target;
        currentHighlight.SetActive(true);
    }

    public void Hide()
    {
        if (currentHighlight != null)
            currentHighlight.SetActive(false);

        currentHighlight = null;
    }
}