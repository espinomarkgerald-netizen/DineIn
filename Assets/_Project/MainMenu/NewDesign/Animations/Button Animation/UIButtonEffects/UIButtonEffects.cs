using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace UI.Effects
{
    public enum UIAnimationPreset
    {
        KeycapPress,  // Sinks down like a mechanical switch
        RefreshSpin,  // Full 360° spin rotation on click + wobble hover
        ExitShrink,   // Tilt hover + shrink & fade on click
        SubtleLift,   // Floating lift on hover + snappy bounce
        Heartbeat,    // Continuous breathing/pulsing
        JiggleWiggle, // Wobble side-to-side
        ExplodePop,   // Pop scale bounce
        PulseGlow,    // Elastic squish & pop expansion
        SlideNudge,   // Directional nudge & punch
        FlipCard,     // 360-degree Y-axis flip
        RubberBand    // Dynamic vertical/horizontal stretch
    }

    [RequireComponent(typeof(AudioSource))]
    public class UIButtonEffects : MonoBehaviour
    {
        [System.Serializable]
        public class ButtonConfig
        {
            [Header("Button Target")]
            public string label = "Button Entry";
            public Button targetButton;
            public UIAnimationPreset preset = UIAnimationPreset.KeycapPress;

            [Header("Timing Customization")]
            [Tooltip("Multiplier to speed up/slow down animations")]
            public float speedMultiplier = 1.0f;

            [Header("Audio Customization")]
            public AudioClip hoverSFX;
            public AudioClip clickSFX;

            [HideInInspector] public RectTransform rectTransform;
            [HideInInspector] public Vector3 initialScale;
            [HideInInspector] public Vector2 initialAnchoredPosition;
            [HideInInspector] public Quaternion initialRotation;
            [HideInInspector] public CanvasGroup canvasGroup;
            [HideInInspector] public Coroutine activeCoroutine;
            [HideInInspector] public bool isHovering;
        }

        [Header("Audio Settings")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField, Range(0.8f, 1.2f)] private float minPitch = 0.95f;
        [SerializeField, Range(0.8f, 1.2f)] private float maxPitch = 1.05f;

        [Header("Button List")]
        [SerializeField] private List<ButtonConfig> buttonEntries = new List<ButtonConfig>();

        private void Awake()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            InitializeButtons();
        }

        private void OnEnable()
        {
            foreach (var entry in buttonEntries)
            {
                if (entry.targetButton != null)
                {
                    StopEntryCoroutine(entry);
                    ResetToInitialImmediate(entry);
                }
            }
        }

        private void OnDisable()
        {
            foreach (var entry in buttonEntries)
            {
                StopEntryCoroutine(entry);
            }
        }

        private void InitializeButtons()
        {
            foreach (var entry in buttonEntries)
            {
                if (entry.targetButton == null) continue;

                if (entry.speedMultiplier <= 0f) 
                    entry.speedMultiplier = 1.0f;

                entry.rectTransform = entry.targetButton.GetComponent<RectTransform>();
                if (entry.rectTransform == null) continue;

                entry.initialScale = entry.rectTransform.localScale;
                entry.initialAnchoredPosition = entry.rectTransform.anchoredPosition;
                entry.initialRotation = entry.rectTransform.localRotation;

                if (!entry.targetButton.TryGetComponent<CanvasGroup>(out entry.canvasGroup))
                {
                    entry.canvasGroup = entry.targetButton.gameObject.AddComponent<CanvasGroup>();
                }

                EventTrigger trigger = entry.targetButton.gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                    trigger = entry.targetButton.gameObject.AddComponent<EventTrigger>();

                trigger.triggers.Clear();

                AddEventTriggerListener(trigger, EventTriggerType.PointerEnter, (data) => OnPointerEnter(entry));
                AddEventTriggerListener(trigger, EventTriggerType.PointerExit, (data) => OnPointerExit(entry));
                AddEventTriggerListener(trigger, EventTriggerType.PointerDown, (data) => OnPointerDown(entry));
            }
        }

        #region Pointer Events

        private void OnPointerEnter(ButtonConfig entry)
        {
            if (!entry.targetButton.interactable) return;

            entry.isHovering = true;
            PlaySFX(entry.hoverSFX);
            StartPresetAnimation(entry, isClick: false);
        }

        private void OnPointerExit(ButtonConfig entry)
        {
            if (!entry.targetButton.interactable) return;

            entry.isHovering = false;
            StartAnimationCoroutine(entry, AnimateToResetState(entry, 0.15f / entry.speedMultiplier));
        }

        private void OnPointerDown(ButtonConfig entry)
        {
            if (!entry.targetButton.interactable) return;

            PlaySFX(entry.clickSFX);
            StartPresetAnimation(entry, isClick: true);
        }

        #endregion

        #region Animation Engine

        private void StartPresetAnimation(ButtonConfig entry, bool isClick)
        {
            StopEntryCoroutine(entry);

            IEnumerator routine = isClick 
                ? GetClickRoutine(entry) 
                : GetHoverRoutine(entry);

            if (routine != null)
                StartAnimationCoroutine(entry, routine);
        }

        private IEnumerator GetHoverRoutine(ButtonConfig entry)
        {
            float speed = entry.speedMultiplier;

            switch (entry.preset)
            {
                case UIAnimationPreset.KeycapPress:
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, entry.initialScale * 1.08f, entry.initialRotation, 1f, 0.1f / speed);
                    break;

                case UIAnimationPreset.RefreshSpin:
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, entry.initialScale * 1.05f, Quaternion.Euler(0, 0, 20f), 1f, 0.12f / speed);
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, entry.initialScale * 1.05f, Quaternion.Euler(0, 0, -20f), 1f, 0.12f / speed);
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, entry.initialScale * 1.05f, entry.initialRotation, 1f, 0.1f / speed);
                    break;

                case UIAnimationPreset.ExitShrink:
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, entry.initialScale * 1.1f, Quaternion.Euler(0, 0, -8f), 1f, 0.12f / speed);
                    break;

                case UIAnimationPreset.SubtleLift:
                    Vector2 liftPos = entry.initialAnchoredPosition + new Vector2(0, 10f);
                    yield return TransformLerp(entry, liftPos, entry.initialScale * 1.04f, entry.initialRotation, 1f, 0.12f / speed);
                    break;

                case UIAnimationPreset.Heartbeat:
                    while (entry.isHovering)
                    {
                        yield return TransformLerp(entry, entry.initialAnchoredPosition, entry.initialScale * 1.12f, entry.initialRotation, 1f, 0.2f / speed);
                        yield return TransformLerp(entry, entry.initialAnchoredPosition, entry.initialScale, entry.initialRotation, 1f, 0.2f / speed);
                    }
                    break;

                case UIAnimationPreset.JiggleWiggle:
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, entry.initialScale, Quaternion.Euler(0, 0, 10f), 1f, 0.08f / speed);
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, entry.initialScale, Quaternion.Euler(0, 0, -10f), 1f, 0.08f / speed);
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, entry.initialScale, entry.initialRotation, 1f, 0.08f / speed);
                    break;

                case UIAnimationPreset.ExplodePop:
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, entry.initialScale * 1.12f, entry.initialRotation, 1f, 0.1f / speed);
                    break;

                case UIAnimationPreset.PulseGlow:
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, entry.initialScale * 1.06f, entry.initialRotation, 1f, 0.15f / speed);
                    break;

                case UIAnimationPreset.SlideNudge:
                    Vector2 nudgePos = entry.initialAnchoredPosition + new Vector2(12f, 0f);
                    yield return TransformLerp(entry, nudgePos, entry.initialScale, entry.initialRotation, 1f, 0.1f / speed);
                    break;

                case UIAnimationPreset.FlipCard:
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, entry.initialScale, Quaternion.Euler(0, 30f, 0), 1f, 0.12f / speed);
                    break;

                case UIAnimationPreset.RubberBand:
                    Vector3 stretchHover = new Vector3(entry.initialScale.x * 0.9f, entry.initialScale.y * 1.12f, entry.initialScale.z);
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, stretchHover, entry.initialRotation, 1f, 0.1f / speed);
                    break;
            }
        }

        private IEnumerator GetClickRoutine(ButtonConfig entry)
        {
            float speed = entry.speedMultiplier;

            switch (entry.preset)
            {
                case UIAnimationPreset.KeycapPress:
                    Vector2 pressedPos = entry.initialAnchoredPosition + new Vector2(0, -8f);
                    Vector3 pressedScale = new Vector3(entry.initialScale.x * 1.05f, entry.initialScale.y * 0.82f, entry.initialScale.z);
                    
                    yield return TransformLerp(entry, pressedPos, pressedScale, entry.initialRotation, 1f, 0.06f / speed);
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, entry.initialScale, entry.initialRotation, 1f, 0.12f / speed, EaseOutBounce);
                    break;

                case UIAnimationPreset.RefreshSpin:
                    // FULL 360-DEGREE ROTATION SPIN
                    float spinDuration = 0.45f / speed;
                    float elapsed = 0f;
                    Vector3 baseScale = entry.initialScale;
                    Quaternion baseRot = entry.initialRotation;

                    while (elapsed < spinDuration)
                    {
                        elapsed += LevelOneUIAccessibility.ScaledAnimationDeltaTime;
                        float rawT = Mathf.Clamp01(elapsed / spinDuration);
                        float easedT = EaseInOutCubic(rawT);

                        // Complete 360 Degree Spin
                        float zAngle = Mathf.Lerp(0f, -360f, easedT);
                        entry.rectTransform.localRotation = baseRot * Quaternion.Euler(0, 0, zAngle);

                        // Subtle scale dip during spin
                        float scaleDip = 1.0f - (Mathf.Sin(rawT * Mathf.PI) * 0.15f);
                        entry.rectTransform.localScale = baseScale * scaleDip;

                        yield return null;
                    }

                    entry.rectTransform.localRotation = baseRot;
                    entry.rectTransform.localScale = baseScale;
                    break;

                case UIAnimationPreset.ExitShrink:
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, Vector3.zero, Quaternion.Euler(0, 0, -30f), 0f, 0.18f / speed, EaseInBack);
                    break;

                case UIAnimationPreset.SubtleLift:
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, entry.initialScale * 0.92f, entry.initialRotation, 1f, 0.08f / speed);
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, entry.initialScale, entry.initialRotation, 1f, 0.15f / speed, EaseOutBounce);
                    break;

                case UIAnimationPreset.Heartbeat:
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, entry.initialScale * 1.25f, entry.initialRotation, 1f, 0.08f / speed);
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, entry.initialScale, entry.initialRotation, 1f, 0.12f / speed);
                    break;

                case UIAnimationPreset.JiggleWiggle:
                    yield return TransformLerp(entry, entry.initialAnchoredPosition + new Vector2(-8f, 0), entry.initialScale, Quaternion.Euler(0, 0, 14f), 1f, 0.05f / speed);
                    yield return TransformLerp(entry, entry.initialAnchoredPosition + new Vector2(8f, 0), entry.initialScale, Quaternion.Euler(0, 0, -14f), 1f, 0.05f / speed);
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, entry.initialScale, entry.initialRotation, 1f, 0.1f / speed);
                    break;

                case UIAnimationPreset.ExplodePop:
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, entry.initialScale * 1.3f, entry.initialRotation, 1f, 0.08f / speed);
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, entry.initialScale, entry.initialRotation, 1f, 0.15f / speed, EaseOutBounce);
                    break;

                case UIAnimationPreset.PulseGlow:
                    Vector3 Squish = new Vector3(entry.initialScale.x * 1.2f, entry.initialScale.y * 0.8f, entry.initialScale.z);
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, Squish, entry.initialRotation, 1f, 0.06f / speed);
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, entry.initialScale * 1.15f, entry.initialRotation, 1f, 0.08f / speed);
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, entry.initialScale, entry.initialRotation, 1f, 0.12f / speed, EaseOutBounce);
                    break;

                case UIAnimationPreset.SlideNudge:
                    Vector2 punchPos = entry.initialAnchoredPosition + new Vector2(25f, 0f);
                    yield return TransformLerp(entry, punchPos, entry.initialScale, entry.initialRotation, 1f, 0.06f / speed);
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, entry.initialScale, entry.initialRotation, 1f, 0.12f / speed, EaseOutBounce);
                    break;

                case UIAnimationPreset.FlipCard:
                    float flipDuration = 0.35f / speed;
                    float flipElapsed = 0f;
                    while (flipElapsed < flipDuration)
                    {
                        flipElapsed += LevelOneUIAccessibility.ScaledAnimationDeltaTime;
                        float t = flipElapsed / flipDuration;
                        float yAngle = Mathf.Lerp(0f, 360f, EaseInOutCubic(t));
                        entry.rectTransform.localRotation = Quaternion.Euler(0, yAngle, 0);
                        yield return null;
                    }
                    entry.rectTransform.localRotation = entry.initialRotation;
                    break;

                case UIAnimationPreset.RubberBand:
                    Vector3 stretchX = new Vector3(entry.initialScale.x * 1.3f, entry.initialScale.y * 0.7f, entry.initialScale.z);
                    Vector3 stretchY = new Vector3(entry.initialScale.x * 0.75f, entry.initialScale.y * 1.25f, entry.initialScale.z);

                    yield return TransformLerp(entry, entry.initialAnchoredPosition, stretchX, entry.initialRotation, 1f, 0.06f / speed);
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, stretchY, entry.initialRotation, 1f, 0.06f / speed);
                    yield return TransformLerp(entry, entry.initialAnchoredPosition, entry.initialScale, entry.initialRotation, 1f, 0.1f / speed, EaseOutBounce);
                    break;
            }

            if (entry.isHovering && entry.preset != UIAnimationPreset.ExitShrink)
                StartAnimationCoroutine(entry, GetHoverRoutine(entry));
        }

        #endregion

        #region Lerp Utilities & Math Easing

        private IEnumerator TransformLerp(ButtonConfig entry, Vector2 targetPos, Vector3 targetScale, Quaternion targetRot, float targetAlpha, float duration, Func<float, float> easeFunc = null)
        {
            RectTransform rect = entry.rectTransform;
            Vector2 startPos = rect.anchoredPosition;
            Vector3 startScale = rect.localScale;
            Quaternion startRot = rect.localRotation;
            float startAlpha = entry.canvasGroup.alpha;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += LevelOneUIAccessibility.ScaledAnimationDeltaTime;
                float rawT = Mathf.Clamp01(elapsed / duration);
                float t = easeFunc != null ? easeFunc(rawT) : EaseOutQuad(rawT);

                rect.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, t);
                rect.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
                rect.localRotation = Quaternion.LerpUnclamped(startRot, targetRot, t);
                entry.canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

                yield return null;
            }

            rect.anchoredPosition = targetPos;
            rect.localScale = targetScale;
            rect.localRotation = targetRot;
            entry.canvasGroup.alpha = targetAlpha;
        }

        private IEnumerator AnimateToResetState(ButtonConfig entry, float duration)
        {
            yield return TransformLerp(entry, entry.initialAnchoredPosition, entry.initialScale, entry.initialRotation, 1f, duration);
        }

        private void ResetToInitialImmediate(ButtonConfig entry)
        {
            if (entry.rectTransform == null) return;

            entry.rectTransform.anchoredPosition = entry.initialAnchoredPosition;
            entry.rectTransform.localScale = entry.initialScale;
            entry.rectTransform.localRotation = entry.initialRotation;

            if (entry.canvasGroup != null)
            {
                entry.canvasGroup.alpha = 1f;
                entry.canvasGroup.blocksRaycasts = true;
            }

            entry.isHovering = false;
        }

        private void StartAnimationCoroutine(ButtonConfig entry, IEnumerator routine)
        {
            StopEntryCoroutine(entry);
            entry.activeCoroutine = StartCoroutine(routine);
        }

        private void StopEntryCoroutine(ButtonConfig entry)
        {
            if (entry.activeCoroutine != null)
            {
                StopCoroutine(entry.activeCoroutine);
                entry.activeCoroutine = null;
            }
        }

        private float EaseOutQuad(float t) => t * (2f - t);
        private float EaseInOutCubic(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        private float EaseInBack(float t) => 2.70158f * t * t * t - 1.70158f * t * t;
        private float EaseOutBounce(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;

            if (t < 1 / d1) return n1 * t * t;
            if (t < 2 / d1) return n1 * (t -= 1.5f / d1) * t + 0.75f;
            if (t < 2.5 / d1) return n1 * (t -= 2.25f / d1) * t + 0.9375f;
            return n1 * (t -= 2.625f / d1) * t + 0.984375f;
        }

        #endregion

        #region Helpers & Audio

        private void PlaySFX(AudioClip clip)
        {
            if (clip == null || audioSource == null) return;

            audioSource.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
            audioSource.PlayOneShot(clip);
        }

        private void AddEventTriggerListener(EventTrigger trigger, EventTriggerType eventType, Action<BaseEventData> callback)
        {
            EventTrigger.Entry entry = new EventTrigger.Entry { eventID = eventType };
            entry.callback.AddListener((data) => callback(data));
            trigger.triggers.Add(entry);
        }

        #endregion
    }
}
