using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Sound only: the existing button components keep ownership of animations and actions.
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class GameplayButtonSound : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    private Button button;
    private int playedFrame = -1;
    public static void Ensure(Button target)
    {
        if (target != null && target.GetComponent<GameplayButtonSound>() == null)
            target.gameObject.AddComponent<GameplayButtonSound>();
    }
    private void Awake() { button = GetComponent<Button>(); Bind(); }
    private void OnEnable() { if (button == null) button = GetComponent<Button>(); Bind(); }
    private void Bind()
    {
        if (button == null) return;
        button.onClick.RemoveListener(Activated);
        button.onClick.AddListener(Activated);
    }
    // onClick already passed Unity's activation checks. It may have closed its panel
    // or disabled its button before this listener runs, so do not recheck that state.
    private void Activated()
    {
        if (playedFrame == Time.frameCount) return;
        playedFrame = Time.frameCount;
        GameplaySfx.Play(GameplaySfx.Cue.Click);
    }
    private void OnDestroy() { if (button != null) button.onClick.RemoveListener(Activated); }
    private void Activate()
    {
        if (button != null && button.IsInteractable()) Activated();
    }
    public void OnPointerClick(PointerEventData data)
    { if (data != null && data.button == PointerEventData.InputButton.Left) Activate(); }
    public void OnSubmit(BaseEventData data) => Activate();
}
