using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class RoleSwitchWarningUI : MonoBehaviour
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private float hiddenX = -700f;
    [SerializeField] private float shownX = 30f;
    [SerializeField] private float slideDuration = 0.2f;
    [SerializeField] private float stayDuration = 1.5f;

    private Coroutine routine;

    private void Awake()
    {
        if (panel != null)
        {
            Vector2 p = panel.anchoredPosition;
            p.x = hiddenX;
            panel.anchoredPosition = p;
        }
    }

    public void ShowWarning()
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        yield return SlideTo(shownX);
        yield return new WaitForSeconds(stayDuration);
        yield return SlideTo(hiddenX);
        routine = null;
    }

    private IEnumerator SlideTo(float targetX)
    {
        if (panel == null) yield break;

        Vector2 start = panel.anchoredPosition;
        Vector2 end = new Vector2(targetX, start.y);

        float t = 0f;
        while (t < slideDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / slideDuration);
            panel.anchoredPosition = Vector2.Lerp(start, end, k);
            yield return null;
        }

        panel.anchoredPosition = end;
    }
}