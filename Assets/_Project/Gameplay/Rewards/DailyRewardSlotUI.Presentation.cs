using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class DailyRewardSlotUI
{
    private const float SymbolSpacing = 176;
    private RectTransform safeRoot, layoutRoot, panelRoot, banner;
    private Image backdrop, rewardIcon;
    private Image bannerPlate;
    private Sprite currentCurrencySprite;
    private readonly CanvasGroup[] winRims = new CanvasGroup[3];
    private CanvasGroup bannerGroup;
    private Vector2 lastScreen;
    private Rect lastSafe;
    private float layoutScale = 1;
    private sealed class Spark
    {
        public Image image;
        public Vector2 start, velocity;
        public float age, life, angle, gravity;
    }
    private Spark[] sparks;

    private void Build()
    {
        backdrop = Image("Backdrop", transform, new Vector2(1600, 900), Vector2.zero, new Color(0, .04f, .08f, 0));
        backdrop.rectTransform.anchorMin = Vector2.zero; backdrop.rectTransform.anchorMax = Vector2.one; backdrop.rectTransform.sizeDelta = Vector2.zero;
        safeRoot = new GameObject("Safe Area", typeof(RectTransform)).GetComponent<RectTransform>(); safeRoot.SetParent(transform, false);
        layoutRoot = new GameObject("Popup Layout", typeof(RectTransform)).GetComponent<RectTransform>(); layoutRoot.SetParent(safeRoot, false);
        layoutRoot.anchorMin = layoutRoot.anchorMax = new Vector2(.5f, .5f); layoutRoot.sizeDelta = new Vector2(settings.popupWidth, settings.popupHeight);
        var paper = Image("Reward Panel", layoutRoot, new Vector2(800, 630), Vector2.zero, Color.clear);
        panelRoot = paper.rectTransform;
        Image("Opaque Matte Fill", panelRoot, new Vector2(760, 590), Vector2.zero, new Color(.84f, .95f, 1));
        var rim = Image("Authored Rounded Rim", panelRoot, new Vector2(800, 630), Vector2.zero, Color.white);
        rim.sprite = settings.panelSprite; rim.type = UnityEngine.UI.Image.Type.Sliced; rim.fillCenter = false; rim.raycastTarget = false;
        group = layoutRoot.gameObject.AddComponent<CanvasGroup>(); group.interactable = false;
        Text("DAILY REWARD", panelRoot, new Vector2(700, 70), new Vector2(0, 239), 49);
        var subtitle = Image("Subtitle Band", panelRoot, new Vector2(650, 35), new Vector2(0, 185), new Color(.69f, .88f, .98f));
        Text("One free spin after a successful game day", subtitle.transform, new Vector2(630, 32), Vector2.zero, 22);
        var frame = Image("Reel Surround", panelRoot, new Vector2(660, 234), new Vector2(0, 45), new Color(.08f, .39f, .63f));
        var innerFrame = Image("Blue Rim", frame.transform, new Vector2(650, 224), Vector2.zero, new Color(.22f, .66f, .85f));
        for (int i = 0; i < 3; i++)
        {
            var reelBorder = Image("Window Border " + i, innerFrame.transform, new Vector2(204, 214), new Vector2((i - 1) * 214, 0), Ink);
            var reel = Image("Cream Window " + i, reelBorder.transform, new Vector2(196, 206), Vector2.zero, new Color(1, .98f, .89f));
            reel.gameObject.AddComponent<RectMask2D>(); reels[i] = reel.rectTransform;
            for (int k = 0; k < 3; k++)
            {
                var icon = Image("Symbol " + k, reel.transform, new Vector2(164, 164), new Vector2(0, (k - 1) * SymbolSpacing), Color.white);
                icon.type = UnityEngine.UI.Image.Type.Simple; icon.preserveAspect = true; icon.raycastTarget = false; symbols[i, k] = icon;
                SetSymbol(i, k, (i + k) % settings.prizes.Length, 0);
            }
            // A steady warm rim at rest; its alpha animates only for a confirmed win.
            var rimRoot = new GameObject("Border-only Outcome Rim " + i, typeof(RectTransform), typeof(CanvasGroup));
            rimRoot.transform.SetParent(reelBorder.transform, false);
            var rimRect = rimRoot.GetComponent<RectTransform>(); rimRect.anchorMin = rimRect.anchorMax = new Vector2(.5f, .5f); rimRect.sizeDelta = Vector2.zero;
            winRims[i] = rimRoot.GetComponent<CanvasGroup>(); winRims[i].alpha = 0; winRims[i].blocksRaycasts = false;
            BorderBar(rimRoot.transform, new Vector2(206, 3), new Vector2(0, 106));
            BorderBar(rimRoot.transform, new Vector2(206, 3), new Vector2(0, -106));
            BorderBar(rimRoot.transform, new Vector2(3, 212), new Vector2(-102, 0));
            BorderBar(rimRoot.transform, new Vector2(3, 212), new Vector2(102, 0));
        }
        var statusPlate = Image("Reward Banner", panelRoot, new Vector2(650, 62), new Vector2(0, -109), new Color(1, .98f, .89f));
        bannerPlate = statusPlate;
        banner = statusPlate.rectTransform; bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
        status = Text("MATCH 3 TO WIN", banner, new Vector2(590, 54), Vector2.zero, 30);
        rewardIcon = Image("Reward Currency", banner, Vector2.one * settings.rewardIconSize, Vector2.zero, Color.white);
        rewardIcon.type = UnityEngine.UI.Image.Type.Simple; rewardIcon.preserveAspect = true; rewardIcon.raycastTarget = false; rewardIcon.gameObject.SetActive(false);

        spin = Button("SPIN", panelRoot, new Vector2(350, 64), new Vector2(-128, -184), StartSpin);
        next = Button("SKIP", panelRoot, new Vector2(230, 64), new Vector2(178, -184), Close);
        var spinLabel = spin.GetComponentInChildren<TMP_Text>(); spinLabel.color = Color.white;
        var prizes = Image("Prize Strip", panelRoot, new Vector2(665, 36), new Vector2(0, -242), new Color(.75f, .9f, .98f));
        CurrencyLabel("Cash prize", prizes.transform, settings.cashIcon, "100–1,000 CASH", new Vector2(-157, 0), new Vector2(48, 34));
        CurrencyLabel("GC prize", prizes.transform, settings.goldCoinIcon, "10–50 GC", new Vector2(167, 0), new Vector2(34, 34));
        int cashWeight = 0, goldWeight = 0;
        foreach (var prize in settings.prizes) { if (prize.gold) goldWeight += prize.weight; else cashWeight += prize.weight; }
        float total = DailyRewardSlotOdds.Total(System.Array.ConvertAll(settings.prizes, prize => prize.weight), settings.noPrizeWeight);
        Text($"Cash {100 * cashWeight / total:0.#}%  •  GC {100 * goldWeight / total:0.#}%  •  No prize {100 * settings.noPrizeWeight / total:0.#}%", panelRoot, new Vector2(680, 25), new Vector2(0, -274), 18);
        if (preview)
        {
            var debug = Image("Separate Developer Strip", layoutRoot, new Vector2(745, 71), new Vector2(0, -370), new Color(.85f, .94f, .98f));
            Text("PREVIEW ONLY • No currency or attempts consumed", debug.transform, new Vector2(710, 23), new Vector2(0, 20), 17);
            Button("TEST CASH", debug.transform, new Vector2(218, 31), new Vector2(-236, -15), () => SetForced(2));
            Button("TEST GC", debug.transform, new Vector2(218, 31), new Vector2(0, -15), () => SetForced(5));
            Button("TEST NO MATCH", debug.transform, new Vector2(218, 31), new Vector2(236, -15), () => SetForced(-1));
        }
        sparks = new Spark[Mathf.Clamp(settings.particleCount, 0, 24)];
        for (int i = 0; i < sparks.Length; i++)
        {
            var image = Image("Pooled Spark " + i, panelRoot, new Vector2(18, 18), Vector2.zero, Color.white);
            image.sprite = settings.particleSprite; image.type = UnityEngine.UI.Image.Type.Simple; image.preserveAspect = true; image.raycastTarget = false; image.gameObject.SetActive(false);
            sparks[i] = new Spark { image = image };
        }
        LayoutPopup();
    }
    private void BorderBar(Transform parent, Vector2 size, Vector2 point)
    {
        var bar = Image("Highlight Edge", parent, size, point, new Color(1, .78f, .2f));
        bar.sprite = null; bar.type = UnityEngine.UI.Image.Type.Simple; bar.raycastTarget = false;
    }
    private void CurrencyLabel(string name, Transform parent, Sprite sprite, string label, Vector2 center, Vector2 iconSize)
    {
        var root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); root.SetParent(parent, false);
        root.anchorMin = root.anchorMax = new Vector2(.5f, .5f); root.anchoredPosition = center;
        var text = Text(label, root, new Vector2(240, 35), Vector2.zero, 23);
        float width = Mathf.Min(240, text.GetPreferredValues(label).x);
        text.rectTransform.sizeDelta = new Vector2(width, 35); text.rectTransform.anchoredPosition = new Vector2((iconSize.x + settings.iconTextGap) * .5f, 0);
        var icon = Image("Currency", root, iconSize, new Vector2(-(width + settings.iconTextGap) * .5f, 0), Color.white);
        icon.sprite = sprite; icon.type = UnityEngine.UI.Image.Type.Simple; icon.preserveAspect = true; icon.raycastTarget = false;
    }
    private void ArrangeReward(string finalText)
    {
        float size = Mathf.Clamp(settings.rewardIconSize, 40, 80), gap = settings.iconTextGap;
        float textWidth = Mathf.Min(570 - size - gap, status.GetPreferredValues(finalText).x);
        status.rectTransform.sizeDelta = new Vector2(textWidth, 54);
        status.rectTransform.anchoredPosition = new Vector2((size + gap) * .5f, 0);
        rewardIcon.rectTransform.sizeDelta = Vector2.one * size;
        rewardIcon.rectTransform.anchoredPosition = new Vector2(-(textWidth + gap) * .5f, 0);
    }
    private void LayoutPopup()
    {
        lastScreen = new Vector2(Screen.width, Screen.height); lastSafe = Screen.safeArea;
        safeRoot.anchorMin = new Vector2(lastSafe.xMin / Mathf.Max(1, Screen.width), lastSafe.yMin / Mathf.Max(1, Screen.height));
        safeRoot.anchorMax = new Vector2(lastSafe.xMax / Mathf.Max(1, Screen.width), lastSafe.yMax / Mathf.Max(1, Screen.height)); safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
        Canvas.ForceUpdateCanvases();
        float preferred = Mathf.Min(settings.popupWidth / 800f, settings.popupHeight / 630f);
        layoutScale = preferred * DailyRewardSlotMotion.Fit(840 * preferred, (preview ? 735 : 650) * preferred, safeRoot.rect.width, safeRoot.rect.height, settings.maximumSafeWidth, settings.maximumSafeHeight);
        layoutRoot.localScale = Vector3.one * layoutScale;
        layoutRoot.anchoredPosition = preview ? new Vector2(0, 28 * layoutScale) : Vector2.zero;
    }
    private void SetSymbol(int reel, int cell, int index, float offset)
    {
        var prize = settings.prizes[index]; var image = symbols[reel, cell];
        if (image.sprite != prize.food) image.sprite = prize.food;
        image.rectTransform.sizeDelta = Vector2.one * (164 * prize.symbolScale);
        image.rectTransform.anchoredPosition = new Vector2(0, (cell - 1) * SymbolSpacing - offset) + prize.symbolOffset;
    }
    private void ResetPresentation()
    {
        foreach (var rim in winRims) rim.alpha = 0;
        foreach (var spark in sparks) spark.image.gameObject.SetActive(false);
        for (int i = 0; i < 3; i++) { symbols[i, 1].transform.localScale = Vector3.one; symbols[i, 1].color = Color.white; }
        rewardIcon.gameObject.SetActive(false); banner.localScale = Vector3.one; bannerGroup.alpha = 1;
        status.rectTransform.anchoredPosition = Vector2.zero; status.rectTransform.sizeDelta = new Vector2(590, 54);
        rewardIcon.transform.localScale = Vector3.one; rewardIcon.transform.localRotation = Quaternion.identity;
        bannerPlate.color = new Color(1, .98f, .89f);
    }
    private void Burst(Vector2 position, int count, bool positive = true, bool currency = false)
    {
        if (LevelOneUIAccessibility.ReducedMotion) return;
        foreach (var spark in sparks)
        {
            if (count == 0) break;
            if (spark.image.gameObject.activeSelf) continue;
            count--; spark.start = position; spark.age = 0; spark.life = settings.particleLifetime;
            spark.velocity = positive ? new Vector2((float)(random.NextDouble() * 280 - 140), (float)(random.NextDouble() * 150 + 110)) : new Vector2((float)(random.NextDouble() * 65 - 32), (float)(-random.NextDouble() * 40 - 20));
            spark.gravity = positive ? 130 : 35; spark.angle = currency ? random.Next(-25, 25) : random.Next(-100, 100);
            spark.image.sprite = currency ? currentCurrencySprite : positive ? settings.particleSprite : Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
            spark.image.rectTransform.sizeDelta = Vector2.one * (currency ? 32 : positive ? 18 : 9);
            spark.image.color = currency ? Color.white : positive ? new Color(1, .8f + (float)random.NextDouble() * .17f, .35f, 1) : new Color(.42f, .7f, .82f, .65f);
            spark.image.rectTransform.anchoredPosition = position; spark.image.gameObject.SetActive(true);
        }
    }
    private void UpdatePresentation()
    {
        if (lastScreen.x != Screen.width || lastScreen.y != Screen.height || lastSafe != Screen.safeArea) LayoutPopup();
        if (GameplaySfx.PresentationSuspended) return;
        foreach (var spark in sparks)
        {
            if (!spark.image.gameObject.activeSelf) continue;
            if (LevelOneUIAccessibility.ReducedMotion) { spark.image.gameObject.SetActive(false); continue; }
            spark.age += Time.unscaledDeltaTime; float t = spark.age / Mathf.Max(.1f, spark.life);
            if (t >= 1) { spark.image.gameObject.SetActive(false); continue; }
            spark.image.rectTransform.anchoredPosition = spark.start + spark.velocity * spark.age + Vector2.down * (spark.gravity * spark.age * spark.age);
            spark.image.rectTransform.localRotation = Quaternion.Euler(0, 0, spark.angle * t);
            spark.image.transform.localScale = Vector3.one * (Mathf.Sin(t * Mathf.PI) * .8f + .2f);
            var color = spark.image.color; color.a = (1 - t) * (1 - t); spark.image.color = color;
        }
    }
    private IEnumerator Celebrate(DailyRewardResolution response)
    {
        bool won = response.prizeIndex >= 0;
        var restingPositions = new Vector2[3];
        for (int i = 0; i < 3; i++) restingPositions[i] = symbols[i, 1].rectTransform.anchoredPosition;
        currentCurrencySprite = response.gold ? settings.goldCoinIcon : settings.cashIcon;
        rewardIcon.sprite = currentCurrencySprite; rewardIcon.color = Color.white;
        rewardIcon.gameObject.SetActive(won);
        string finalText = !won ? "NO MATCH • See you next game day!" : (preview ? "PREVIEW  " : "YOU WON  ") + (response.gold ? response.amount + " GC" : "+₱" + response.amount.ToString("N0"));
        if (won) ArrangeReward(finalText);
        foreach (var rim in winRims) foreach (var bar in rim.GetComponentsInChildren<Image>()) bar.color = won ? new Color(1, .78f, .2f) : new Color(.42f, .7f, .85f);
        bannerPlate.color = !won ? new Color(.84f, .92f, .97f) : response.gold ? new Color(1, .96f, .8f) : new Color(.9f, .98f, .87f);
        Burst(new Vector2(0, 10), won ? Mathf.Max(0, sparks.Length - 6) : 5, won);
        GameplaySfx.Play(won ? response.gold ? GameplaySfx.Cue.Excellent : GameplaySfx.Cue.Success : GameplaySfx.Cue.Info);
        bool secondBurst = false;
        float elapsed = 0, duration = LevelOneUIAccessibility.ReducedMotion ? .2f : Mathf.Max(.15f, won ? settings.celebrationSeconds : settings.lossDuration);
        while (elapsed < duration)
        {
            if (GameplaySfx.PresentationSuspended) { yield return null; continue; }
            elapsed += Time.unscaledDeltaTime; float t = Mathf.Clamp01(elapsed / duration);
            bannerGroup.alpha = Mathf.Min(1, t * 5);
            if (won && !secondBurst && t >= .38f)
            { secondBurst = true; Burst(new Vector2(0, -58), 6, true, true); GameplaySfx.PlayUiClip(settings.currencyWinSound, settings.audioLevel * .7f); }
            if (!LevelOneUIAccessibility.ReducedMotion)
            {
                banner.localScale = Vector3.one * (1 + Mathf.Sin(t * Mathf.PI) * (won ? .045f : 0));
                rewardIcon.transform.localScale = Vector3.one * (1 + Mathf.Sin(t * Mathf.PI) * .14f);
                rewardIcon.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 2) * 4);
                for (int i = 0; i < 3; i++)
                {
                    symbols[i, 1].transform.localScale = Vector3.one * (1 + (won ? Mathf.Sin(t * Mathf.PI) * .065f : 0));
                    if (!won) symbols[i, 1].rectTransform.anchoredPosition = restingPositions[i] + Vector2.down * (Mathf.Sin(t * Mathf.PI) * settings.lossDipPixels);
                }
            }
            foreach (var rim in winRims) rim.alpha = won ? LevelOneUIAccessibility.ReducedMotion ? .9f : .7f + .3f * Mathf.Sin(t * Mathf.PI) : LevelOneUIAccessibility.ReducedMotion ? 0 : Mathf.Sin(t * Mathf.PI) * .55f;
            int amount = LevelOneUIAccessibility.ReducedMotion ? response.amount : Mathf.RoundToInt(response.amount * (1 - Mathf.Pow(1 - t, 3)));
            status.text = !won ? "NO MATCH • See you next game day!" : (preview ? "PREVIEW  " : "YOU WON  ") + (response.gold ? amount + " GC" : "+₱" + amount.ToString("N0"));
            yield return null;
        }
        bannerGroup.alpha = 1; banner.localScale = Vector3.one;
        rewardIcon.transform.localScale = Vector3.one; rewardIcon.transform.localRotation = Quaternion.identity;
        foreach (var rim in winRims) rim.alpha = won ? .85f : 0;
        for (int i = 0; i < 3; i++) symbols[i, 1].transform.localScale = Vector3.one;
        for (int i = 0; i < 3; i++) symbols[i, 1].rectTransform.anchoredPosition = restingPositions[i];
        if (!preview && won) PlayFabWalletManager.Instance?.RefreshWallet();
    }
    private IEnumerator Entrance()
    {
        group.alpha = 0; group.interactable = false;
        float duration = Mathf.Max(.05f, settings.entranceSeconds);
        for (float time = 0; time < duration; time += Time.unscaledDeltaTime)
        {
            float t = Mathf.Clamp01(time / duration); group.alpha = t;
            backdrop.color = new Color(0, .04f, .08f, settings.backdropOpacity * t);
            panelRoot.localScale = Vector3.one * (LevelOneUIAccessibility.ReducedMotion ? 1 : Mathf.Lerp(.96f, 1, 1 - Mathf.Pow(1 - t, 3)));
            yield return null;
        }
        group.alpha = 1; panelRoot.localScale = Vector3.one; group.interactable = true;
        backdrop.color = new Color(0, .04f, .08f, settings.backdropOpacity);
    }
    private IEnumerator ExitThenClose()
    {
        float initial = group.alpha, dim = backdrop.color.a, duration = Mathf.Max(.05f, settings.closeSeconds);
        for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = 1 - Mathf.Clamp01(elapsed / duration); group.alpha = initial * t;
            backdrop.color = new Color(0, .04f, .08f, dim * t); yield return null;
        }
        var callback = finished; finished = null;
        GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(gameObject, false);
        active = null; Destroy(gameObject); callback?.Invoke();
    }
}
