using TMPro;
using UnityEngine;

/// <summary>Presentation for one saved cooking bay timer. The kitchen ledger owns all timing.</summary>
public sealed class FastFoodCookingTimer : MonoBehaviour
{
    public MultiplayerWorkCircleGraphic ring;
    public TMP_Text seconds, caption;
    public Color cookingColor = new Color(.10f,.62f,.83f);
    public Color readyColor = new Color(.07f,.70f,.43f);
    public Color warningColor = new Color(1,.73f,0);
    public Color burntColor = new Color(.98f,.12f,.27f);
    [Range(0,.2f)] public float readyPulse = .04f;
    [Min(0)] public float pulseFrequency = 1.5f;
    [Min(0)] public float worldLift = .22f;
    public Vector2 screenOffset = new Vector2(0,48);
    private Vector3 authoredScale = Vector3.one;

    private void OnEnable() => authoredScale = transform.localScale;
    private void OnDisable() => transform.localScale = authoredScale;

    public void Present(FastFoodCookingState.Portion portion, float cookSeconds, float overcookSeconds, float speed, bool staff, bool paused)
    {
        bool cooking = portion.stage == FastFoodCookingStage.Cooking;
        bool ready = portion.stage == FastFoodCookingStage.Ready;
        float remaining = Mathf.Max(0, (cooking ? cookSeconds : overcookSeconds) - portion.elapsed);
        float amount = cooking ? Mathf.Clamp01(portion.elapsed / Mathf.Max(.01f,cookSeconds))
            : ready ? 1-Mathf.Clamp01(portion.elapsed / Mathf.Max(.01f,overcookSeconds)) : 1;
        ring.SetAmount(amount);
        ring.color = cooking ? cookingColor : ready ? amount <= .25f ? warningColor : readyColor : burntColor;
        seconds.text = cooking || ready ? Mathf.CeilToInt(remaining / Mathf.Max(.01f,speed)).ToString() : "!";
        caption.text = ready ? "READY" : portion.stage == FastFoodCookingStage.Burnt ? "BURNT" : staff ? "STAFF" : "";
        float pulse = ready && !paused && !LevelOneUIAccessibility.ReducedMotion
            ? readyPulse * (.5f+.5f*Mathf.Sin(Time.time*pulseFrequency*Mathf.PI*2)) : 0;
        transform.localScale = authoredScale*(1+pulse);
    }
}
