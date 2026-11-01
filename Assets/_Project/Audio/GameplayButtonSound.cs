using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Sound only: the existing button components keep ownership of animations and actions.
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class GameplayButtonSound : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    public enum SoundContext { Automatic, Menu, Computer, Hud }
    public SoundContext soundContext;
    private Button button;
    private GameplaySfx.ButtonFamily Family()
    {
        if (soundContext != SoundContext.Automatic) return (GameplaySfx.ButtonFamily)((int)soundContext - 1);
        if (GetComponentInParent<ManagementComputerController>(true) != null || GetComponentInParent<ManagementComputerWindow>(true) != null) return GameplaySfx.ButtonFamily.Computer;
        string scene = gameObject.scene.name;
        return (scene.StartsWith("Lobby", System.StringComparison.Ordinal) || scene == "RestockScene") ? GameplaySfx.ButtonFamily.Hud : GameplaySfx.ButtonFamily.Menu;
    }
    private int playedFrame = -1;
    public static void Ensure(Button target)
    {
        if (target == null) return;
        var sound = target.GetComponent<GameplayButtonSound>();
        if (sound == null) sound = target.gameObject.AddComponent<GameplayButtonSound>();
        sound.button = target;
        sound.Bind();
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
        GameplaySfx.ButtonClick(Family());
    }
    private void OnDestroy() { if (button != null) button.onClick.RemoveListener(Activated); }
    private void Activate()
    {
        if (button == null || !button.isActiveAndEnabled) return;
        if (button.IsInteractable()) Activated();
        else if (playedFrame != Time.frameCount)
        {
            playedFrame = Time.frameCount;
            GameplaySfx.Play(GameplaySfx.Cue.Blocked);
        }
    }
    public void OnPointerClick(PointerEventData data)
    { if (data != null && data.button == PointerEventData.InputButton.Left) Activate(); }
    public void OnSubmit(BaseEventData data) => Activate();
}
