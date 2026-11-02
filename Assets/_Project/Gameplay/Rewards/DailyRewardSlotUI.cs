using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Local presentation. The developer path never calls a provider, wallet or save API.
public sealed partial class DailyRewardSlotUI : MonoBehaviour
{
    public static IDailyRewardBackend Backend { get; set; }
    private static DailyRewardSlotUI active;
    private DailyRewardSlotSettings settings;
    private readonly System.Random random = new System.Random();
    private readonly Image[,] symbols = new Image[3, 3];
    private readonly RectTransform[] reels = new RectTransform[3];
    private TMP_Text status;
    private Button spin, next;
    private CanvasGroup group;
    private AudioSource reelAudio;
    private bool preview, spun, waiting, closing, receivedResolution;
    private int forced = -2;
    private string offer, account, restaurant;
    private Action finished;
    private IDailyRewardBackend provider;
    private Scene origin;
    private static readonly Color Ink = new Color(.025f, .13f, .23f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime() { active = null; Backend = null; }
    public static bool ShowDeveloperPreview()
    {
        if (!DevSettingsConsole.HasAuthorizedDevAccess || active != null) return false;
        return Create(true, null) != null;
    }
    public static bool TryOfferBeforeResults(Action done)
    {
        if (Backend == null || active != null || CampaignSaveStore.ProtectedSession || !CampaignSaveStore.RuntimeCampaign ||
            GameSaveManager.Instance == null || GameSaveManager.Instance.IsApplyingSave) return false;
        string owner;
        try { owner = CampaignSaveStore.ReadAccountState().account; }
        catch { return false; }
        if (string.IsNullOrEmpty(owner)) return false;
        var panel = Create(false, done);
        if (panel == null) return false;
        panel.account = owner; panel.restaurant = CampaignSaveStore.RestaurantScene; panel.provider = Backend;
        panel.waiting = true; panel.spin.interactable = false; panel.status.text = "Checking your free spin…";
        try
        {
            panel.provider.GetEligibleOffer(owner, panel.restaurant, panel.settings.rulesVersion, id =>
            {
                if (panel == null || panel.closing || !panel.ValidContext) return;
                if (string.IsNullOrEmpty(id)) { panel.Close(); return; }
                panel.offer = id; panel.waiting = false; panel.spin.interactable = true;
                panel.status.text = "MATCH 3 TO WIN";
            });
        }
        catch (Exception error) { Debug.LogWarning("[Daily reward] Offer unavailable: " + error.Message); panel.Close(); }
        return true;
    }
    private bool ValidContext => preview || origin == SceneManager.GetActiveScene() && !CampaignSaveStore.ProtectedSession &&
        restaurant == CampaignSaveStore.RestaurantScene && account == CampaignSaveStore.ReadAccountState().account;
    private static DailyRewardSlotUI Create(bool isPreview, Action done)
    {
        var config = Resources.Load<DailyRewardSlotSettings>("DailyRewardSlot");
        if (config == null || config.prizes == null || config.prizes.Length != 6) return null;
        foreach (var prize in config.prizes) if (prize == null || prize.food == null) return null;
        var root = new GameObject("Daily Reward", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
        var ui = root.AddComponent<DailyRewardSlotUI>(); active = ui; ui.settings = config;
        ui.preview = isPreview; ui.finished = done; ui.origin = SceneManager.GetActiveScene();
        ui.Build(); GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(root, true);
        ui.StartCoroutine(ui.Entrance());
        return ui;
    }

    private void SetForced(int value)
    {
        if (waiting || closing) return;
        ResetPresentation();
        forced = value; spun = false; spin.interactable = true;
        SetButtonLabel(spin, "SPIN"); SetButtonLabel(next, "CLOSE");
        status.text = "PREVIEW • " + (value < 0 ? "No-match test" : settings.prizes[value].gold ? "GC test" : "Cash test");
    }
    private void StartSpin()
    {
        if (closing || spun || waiting || GameplaySfx.PresentationSuspended) return;
        spun = true; waiting = true; spin.interactable = false; next.interactable = false;
        status.text = "Spinning…";
        GameplaySfx.PlayUiClip(settings.spinSound, settings.audioLevel);
        if (preview)
        {
            int[] weights = Array.ConvertAll(settings.prizes, prize => prize.weight);
            int index = forced != -2 ? forced : DailyRewardSlotOdds.Select(random.Next(DailyRewardSlotOdds.Total(weights, settings.noPrizeWeight)), weights);
            StartCoroutine(Spin(new DailyRewardResolution { confirmed = true, prizeIndex = index,
                amount = index >= 0 ? settings.prizes[index].amount : 0, gold = index >= 0 && settings.prizes[index].gold }));
        }
        else
        {
            // The provider owns award recovery; never retry with a new offer or add currency here.
            try { provider.ResolveSpin(offer, response =>
                { if (this != null && !closing && ValidContext && response != null && !receivedResolution)
                    { receivedResolution = true; StartCoroutine(Spin(response)); } }); }
            catch (Exception error) { Debug.LogWarning("[Daily reward] Award status unknown: " + error.Message); }
            StartCoroutine(AllowPendingExit());
        }
    }
    private IEnumerator AllowPendingExit()
    {
        yield return new WaitForSecondsRealtime(8);
        if (waiting && !closing) { next.interactable = true; SetButtonLabel(next, "CONTINUE"); status.text = "Reward pending. Your results are available; do not spin again."; }
    }
    private IEnumerator Spin(DailyRewardResolution response)
    {
        if (!response.confirmed || response.prizeIndex < -1 || response.prizeIndex >= settings.prizes.Length ||
            response.prizeIndex >= 0 && (response.amount != settings.prizes[response.prizeIndex].amount || response.gold != settings.prizes[response.prizeIndex].gold))
        { waiting = false; next.interactable = true; SetButtonLabel(next, "CONTINUE"); status.text = "Reward status unresolved. No extra attempt will be made."; yield break; }
        ResetPresentation();
        int[] stop = response.prizeIndex >= 0 ? new[] { response.prizeIndex, response.prizeIndex, response.prizeIndex } : new[] { random.Next(6), 0, 0 };
        if (response.prizeIndex < 0) { stop[1] = (stop[0] + 1 + random.Next(5)) % 6; stop[2] = random.Next(6); }
        var sequences = new int[3][]; int[] cycles = new int[3], lastCycle = { -1, -1, -1 }; bool[] stopped = new bool[3];
        for (int i = 0; i < 3; i++)
        {
            cycles[i] = Mathf.Clamp(settings.reelCycles, 4, 20) + i;
            sequences[i] = new int[cycles[i] + 3];
            for (int k = 0; k < sequences[i].Length; k++) sequences[i][k] = random.Next(settings.prizes.Length);
            for (int k = 0; k < 3; k++) { int index = Array.FindIndex(settings.prizes, prize => prize.food == symbols[i,k].sprite); if (index >= 0) sequences[i][k] = index; }
            sequences[i][cycles[i] + 1] = stop[i];
        }
        if (reelAudio == null) { reelAudio = gameObject.AddComponent<AudioSource>(); reelAudio.playOnAwake = false; }
        float elapsed = 0, lastTick = -1, total = Mathf.Max(.3f, settings.spinSeconds), settle = Mathf.Max(.05f, settings.settleSeconds);
        while (elapsed < total + settle)
        {
            if (GameplaySfx.PresentationSuspended) { GameplaySfx.SetUiLoop(reelAudio, settings.spinLoop, settings.audioLevel, false); yield return null; continue; }
            elapsed += Time.unscaledDeltaTime;
            bool reduced = LevelOneUIAccessibility.ReducedMotion;
            GameplaySfx.SetUiLoop(reelAudio, settings.spinLoop, settings.audioLevel, !reduced && elapsed < total);
            for (int i = 0; i < 3; i++)
            {
                float end = Mathf.Max(.2f, total - (2 - i) * settings.reelStagger);
                float progress = reduced ? 1 : Mathf.Clamp01(elapsed / end);
                float distance = DailyRewardSlotMotion.Travel(progress) * cycles[i] * SymbolSpacing;
                int cycle = Mathf.Min(cycles[i], Mathf.FloorToInt(distance / SymbolSpacing));
                float offset = progress >= 1 ? 0 : distance - cycle * SymbolSpacing;
                for (int k = 0; k < 3; k++) SetSymbol(i, k, sequences[i][cycle + k], offset);
                if (progress >= 1)
                {
                    if (!stopped[i])
                    {
                        if (!reduced || i == 0) GameplaySfx.PlayUiClip(settings.reelTick, settings.audioLevel);
                        Burst(new Vector2((i - 1) * 214, 45), 2); stopped[i] = true;
                    }
                    if (!reduced) symbols[i,1].rectTransform.anchoredPosition += Vector2.up * DailyRewardSlotMotion.Settle((elapsed - end) / settle);
                }
                else if (i == 0 && cycle != lastCycle[i] && elapsed - lastTick > .08f)
                { GameplaySfx.PlayUiClip(settings.reelTick, settings.audioLevel * .32f); lastTick = elapsed; }
                lastCycle[i] = cycle;
            }
            if (reduced) break;
            yield return null;
        }
        GameplaySfx.SetUiLoop(reelAudio, settings.spinLoop, settings.audioLevel, false);
        yield return Celebrate(response);
        waiting = false; next.interactable = true; SetButtonLabel(next, "CONTINUE");
    }

    private void Update()
    {
        if (closing) return;
        UpdatePresentation();
        try { if (!ValidContext) { finished = null; next.interactable = true; Close(); return; } }
        catch { finished = null; next.interactable = true; Close(); return; }
        if (Input.GetKeyDown(KeyCode.Escape) && next.interactable) Close();
    }
    private void Close()
    {
        if (closing || next != null && !next.interactable) return;
        closing = true; group.interactable = false;
        StopAllCoroutines();
        GameplaySfx.SetUiLoop(reelAudio, settings.spinLoop, settings.audioLevel, false);
        if (!preview && !spun && !string.IsNullOrEmpty(offer)) { try { provider.Skip(offer); } catch { } }
        StartCoroutine(ExitThenClose());
    }
    private void OnDestroy() { if (reelAudio != null) reelAudio.Stop(); if (active == this) active = null; GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(gameObject, false); }
    private Image Image(string name, Transform parent, Vector2 size, Vector2 position, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.layer = 5; go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>(); image.color = color;
        image.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd"); image.type = UnityEngine.UI.Image.Type.Sliced;
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
        image.rectTransform.sizeDelta = size; image.rectTransform.anchoredPosition = position; return image;
    }
    private TMP_Text Text(string value, Transform parent, Vector2 size, Vector2 position, float fontSize)
    {
        var go = new GameObject(value, typeof(RectTransform), typeof(TextMeshProUGUI)); go.layer = 5; go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>(); text.font = settings.font; text.text = value; text.fontSize = fontSize;
        text.color = Ink; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f, .5f);
        text.rectTransform.sizeDelta = size; text.rectTransform.anchoredPosition = position; return text;
    }
    private Button Button(string label, Transform parent, Vector2 size, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        var image = Image(label, parent, size, position, Color.white); image.sprite = settings.buttonSprite; image.type = UnityEngine.UI.Image.Type.Sliced;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        Text(label, image.transform, size - new Vector2(20, 8), Vector2.zero, size.y < 50 ? 18 : 36);
        button.onClick.AddListener(action); GameplayButtonSound.Ensure(button); image.gameObject.AddComponent<UISubtlePressFeedback>(); return button;
    }
    private static void SetButtonLabel(Button button, string label) { button.GetComponentInChildren<TMP_Text>().text = label; }
}
