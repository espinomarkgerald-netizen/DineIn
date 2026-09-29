using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DineIn.Appearance
{
    public sealed class AppearanceCustomizationPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private CharacterAppearance preview;
        [SerializeField] private PlayerAppearanceBinding binding;
        [SerializeField] private Button openButton, applyButton, cancelButton;
        [SerializeField] private Button[] categories;
        [SerializeField] private Button optionTemplate;
        [SerializeField] private RectTransform options;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private TMP_Text status;
        [SerializeField] private Selectable[] menuControls;
        [SerializeField] private GameObject[] menuPresentation = System.Array.Empty<GameObject>();
        [SerializeField] private Camera previewCamera;
        [SerializeField] private CameraFollow cameraFollow;
        [SerializeField] private RestaurantSelector restaurantSelector;
        [SerializeField, Range(.3f, .8f)] private float previewScreenHeight = .65f;
        [SerializeField] private RectTransform previewRegion;
        [SerializeField] private Sprite selectedTabSprite;
        public enum FramingMode { FullBody, ColorSwatch, Head, HeadThreeQuarter, TorsoThreeQuarter, IsolatedHeadwear }
        [System.Serializable] public sealed class CategoryPresentation
        {
            public FramingMode thumbnailMode;
            public Vector3 thumbnailEuler = new(0, 180, 0);
            [Min(.1f)] public float thumbnailMargin = 1.12f;
            public Color thumbnailBackground = new(.84f, .87f, .91f, 1);
            [Range(0, 1)] public float previewCenter = .5f;
            [Range(.25f, 1.5f)] public float previewHeight = 1f;
        }
        [Header("Category presentation (Body, Skin, Face, Hair, Hair Color, Outfit, Hats)")]
        [SerializeField] private CategoryPresentation[] categoryPresentation = System.Array.Empty<CategoryPresentation>();
        [SerializeField, Min(.05f)] private float framingSeconds = .4f;
        [SerializeField] private Vector2 previewViewportCenter = new(.225f, .53f);
        public CategoryPresentation Presentation(int index) => categoryPresentation != null && index >= 0 && index < categoryPresentation.Length ? categoryPresentation[index] : null;
        private readonly List<GameObject> cells = new();
        private AppearanceRecipe draft;
        private string draftAccount;
        private bool[] controlsEnabled;
        private bool[] controlsVisible;
        private bool[] presentationVisible;
        private Sprite[] categorySprites;
        private bool editing, followWasEnabled, cameraCaptured;
        private int category;
        private Vector3 cameraPosition;
        private Vector3 lastPreviewPosition;
        private CosmeticOptionLayout grid;
        private float gridWidth = -1;
        private float gridHeight = -1;
        private readonly Vector3[] regionCorners = new Vector3[4];
        private float cameraSize, cameraFov;
        private Bounds framingBounds;
        private bool framingReady, transitioning;
        private int framingCategory;
        private Vector3 frameFrom, frameTo;
        private float lensFrom, lensTo, frameElapsed;

        private void Awake()
        {
            openButton.onClick.AddListener(Open);
            applyButton.onClick.AddListener(Apply);
            cancelButton.onClick.AddListener(Cancel);
            for (int i = 0; i < categories.Length; i++)
            { int index = i; categories[i].onClick.AddListener(() => SelectCategory(index)); }
            categorySprites = categories.Select(c => c.GetComponent<Image>().sprite).ToArray();
            panel.SetActive(false); optionTemplate.gameObject.SetActive(false);
            grid = options.GetComponent<CosmeticOptionLayout>();
        }
        private void OnEnable() => PlayerCustomizationData.ProfileChanged += Cancel;
        private void OnDisable() { PlayerCustomizationData.ProfileChanged -= Cancel; Cancel(); }
        private void Update()
        {
            if (!editing) return;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true) Cancel();
#else
            if (Input.GetKeyDown(KeyCode.Escape)) Cancel();
#endif
            if (editing && !panel.activeInHierarchy) Cancel();
            if (editing) ResizeGrid();
        }
        private void LateUpdate()
        {
            if (!editing || previewCamera == null || preview == null) return;
            // Camera stays attached to the same preview. Travel is suspended by the selector.
            var delta = preview.transform.position - lastPreviewPosition;
            previewCamera.transform.position += delta;
            frameFrom += delta; frameTo += delta; framingBounds.center += delta;
            lastPreviewPosition = preview.transform.position;
            AdvanceFraming(Time.unscaledDeltaTime);
        }
        public void Open()
        {
            if (editing) return;
            draftAccount = PlayerCustomizationData.AccountId;
            draft = preview.Catalog.Validate(PlayerCustomizationData.CommittedAppearance);
            editing = true; binding.Previewing = true;
            lastPreviewPosition = preview.transform.position;
            controlsEnabled = menuControls.Select(c => c != null && c.interactable).ToArray();
            controlsVisible = menuControls.Select(c => c != null && c.gameObject.activeSelf).ToArray();
            presentationVisible = menuPresentation.Select(g => g != null && g.activeSelf).ToArray();
            foreach (var item in menuPresentation) if (item != null) item.SetActive(false);
            foreach (var control in menuControls) if (control != null) { control.interactable = false; control.gameObject.SetActive(false); }
            panel.SetActive(true); panel.transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases(); ResizeGrid();
            status.text = "Changes save when you Apply.";
            preview.Apply(draft);
            framingReady = preview.TryGetBodyBounds(out framingBounds);
            framingCategory = 0;
            if (previewCamera != null)
            {
                cameraPosition = previewCamera.transform.position;
                cameraSize = previewCamera.orthographicSize; cameraFov = previewCamera.fieldOfView;
                cameraCaptured = true;
                followWasEnabled = cameraFollow != null && cameraFollow.enabled;
                if (cameraFollow != null) cameraFollow.enabled = false;
                FramePreview();
            }
            SelectCategory(0);
            if (restaurantSelector != null) restaurantSelector.BeginCustomization();
        }
        public void Apply()
        {
            if (!editing) return;
            if (draftAccount != PlayerCustomizationData.AccountId) { Cancel(); return; }
            if (!PlayerCustomizationData.CommitAppearance(draft, preview.Catalog))
            { status.text = "Could not save locally. Your draft is still here; please try Apply again."; return; }
            Close(true);
        }
        public void Cancel() { if (editing) Close(); }
        private void Close(bool applied = false)
        {
            editing = false; draft = null; transitioning = framingReady = false;
            if (binding != null) { binding.Previewing = false; binding.Refresh(); }
            for (int i = 0; i < menuPresentation.Length; i++) if (menuPresentation[i] != null) menuPresentation[i].SetActive(presentationVisible[i]);
            for (int i = 0; i < menuControls.Length; i++) if (menuControls[i] != null)
            { menuControls[i].interactable = controlsEnabled[i]; menuControls[i].gameObject.SetActive(controlsVisible[i]); }
            if (previewCamera != null)
            { previewCamera.transform.position = cameraPosition; previewCamera.orthographicSize = cameraSize; previewCamera.fieldOfView = cameraFov; }
            if (cameraFollow != null) cameraFollow.enabled = followWasEnabled;
            cameraCaptured = false;
            if (panel != null) panel.SetActive(false);
            if (restaurantSelector != null && restaurantSelector.isActiveAndEnabled) restaurantSelector.EndCustomization(applied);
        }
        private void FramePreview()
        {
            if (previewCamera == null || preview == null) return;
            if (!framingReady) framingReady = preview.TryGetBodyBounds(out framingBounds);
            if (!framingReady) return;
            // Cache the opening silhouette: breathing, rotation and similar hairstyles must not pump the camera.
            var bounds = framingBounds;
            var presentation = Presentation(framingCategory);
            if (presentation != null)
            {
                bounds.center = framingBounds.center + Vector3.up * framingBounds.size.y * (presentation.previewCenter - .5f);
                float height = framingBounds.size.y * presentation.previewHeight;
                if (framingCategory == 6 && draft != null)
                {
                    var hat = AppearanceCatalog.Find(preview.Catalog.hats, draft.hatId);
                    float extra = hat != null ? hat.previewHeadroom * framingBounds.size.y : 0;
                    height += extra; bounds.center += Vector3.up * extra * .5f;
                }
                bounds.size = new Vector3(Mathf.Min(framingBounds.size.x, height), height, Mathf.Min(framingBounds.size.z, height));
            }
            var extents = bounds.extents;
            float Project(Vector3 axis) => Mathf.Abs(axis.x) * extents.x + Mathf.Abs(axis.y) * extents.y + Mathf.Abs(axis.z) * extents.z;
            Vector2 center = previewViewportCenter;
            float regionWidth = .39f;
            if (previewRegion != null)
            {
                previewRegion.GetWorldCorners(regionCorners);
                var canvas = previewRegion.GetComponentInParent<Canvas>();
                Vector3 ToViewport(Vector3 p) => canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? previewCamera.ScreenToViewportPoint(RectTransformUtility.WorldToScreenPoint(null, p))
                    : previewCamera.WorldToViewportPoint(p);
                var min = ToViewport(regionCorners[0]); var max = ToViewport(regionCorners[2]);
                center = (min + max) * .5f;
                regionWidth = Mathf.Max(.1f, (max.x - min.x) * .88f);
            }
            float halfHeight = Mathf.Max(Project(previewCamera.transform.up) / previewScreenHeight,
                Project(previewCamera.transform.right) / (Mathf.Max(.1f, previewCamera.aspect) * regionWidth));
            float depth = Mathf.Max(1, Vector3.Dot(framingBounds.center - cameraPosition, previewCamera.transform.forward));
            halfHeight *= 1.08f;
            float desiredLens = previewCamera.orthographic ? halfHeight : Mathf.Clamp(2 * Mathf.Atan(halfHeight / depth) * Mathf.Rad2Deg, 15, 65);
            if (!previewCamera.orthographic) halfHeight = Mathf.Tan(desiredLens * Mathf.Deg2Rad * .5f) * depth;
            var desiredPosition = bounds.center - previewCamera.transform.forward * depth
                - previewCamera.transform.right * ((center.x - .5f) * 2 * halfHeight * previewCamera.aspect)
                - previewCamera.transform.up * ((center.y - .5f) * 2 * halfHeight);
            if (transitioning && Vector3.SqrMagnitude(desiredPosition - frameTo) < .0001f && Mathf.Abs(desiredLens - lensTo) < .001f) return;
            frameFrom = previewCamera.transform.position; frameTo = desiredPosition;
            lensFrom = previewCamera.orthographic ? previewCamera.orthographicSize : previewCamera.fieldOfView;
            lensTo = desiredLens; frameElapsed = 0; transitioning = true;
        }
        private void AdvanceFraming(float deltaTime)
        {
            if (!transitioning || previewCamera == null) return;
            frameElapsed += Mathf.Max(0, deltaTime);
            float t = Mathf.Clamp01(frameElapsed / Mathf.Max(.05f, framingSeconds));
            float eased = t * t * (3 - 2 * t);
            previewCamera.transform.position = Vector3.Lerp(frameFrom, frameTo, eased);
            float lens = Mathf.Lerp(lensFrom, lensTo, eased);
            if (previewCamera.orthographic) previewCamera.orthographicSize = lens; else previewCamera.fieldOfView = lens;
            transitioning = t < 1;
        }
        private void ResizeGrid()
        {
            if (grid == null || scroll.viewport == null) return;
            float width = scroll.viewport.rect.width;
            float height = scroll.viewport.rect.height;
            if (Mathf.Abs(width - gridWidth) < .5f && Mathf.Abs(height - gridHeight) < .5f) return;
            gridWidth = width; gridHeight = height;
            grid.SetPalette(category == 1 || category == 4); grid.Refresh();
            LayoutRebuilder.ForceRebuildLayoutImmediate(options);
            if (editing && cameraCaptured) FramePreview();
        }
        public void BeginPreviewDrag() { if (editing && restaurantSelector != null) restaurantSelector.InterruptPreviewPose(); }
        public void RotatePreview(float pixels)
        {
            if (editing) { BeginPreviewDrag(); preview.transform.Rotate(Vector3.up, -pixels * .3f, Space.World); }
        }
        private string SelectedId() => category switch
        { 0 => draft.bodyId, 1 => draft.skinId, 2 => draft.faceId, 3 => draft.hairId, 4 => draft.hairColorId, 5 => draft.outfitId, _ => draft.hatId };
        private void SelectCategory(int index)
        {
            if (restaurantSelector != null) restaurantSelector.InterruptPreviewPose();
            category = index;
            if (index != 1 && index != 4) framingCategory = index;
            for (int i = 0; i < categories.Length; i++)
            {
                categories[i].interactable = true;
                categories[i].GetComponent<Image>().sprite = i == category && selectedTabSprite != null ? selectedTabSprite : categorySprites[i];
                // Both depth-border sprites have light centers; keep their labels readable.
                categories[i].GetComponentInChildren<TMP_Text>().color = new Color(.04f, .24f, .31f);
            }
            gridWidth = -1; ResizeGrid();
            RebuildOptions();
            if (cameraCaptured) FramePreview();
        }
        private void RebuildOptions()
        {
            foreach (var cell in cells)
            {
                cell.SetActive(false);
                if (Application.isPlaying) Destroy(cell); else DestroyImmediate(cell);
            }
            cells.Clear();
            var c = preview.Catalog;
            IEnumerable<AppearanceCatalog.Option> available = category switch
            { 0 => c.bodies, 1 => c.skins, 2 => c.faces, 3 => c.hairs, 4 => c.hairColors, 5 => c.outfits, _ => c.hats };
            foreach (var option in available.Where(o => o != null && (category == 0 || o.Fits(draft.bodyId))))
            {
                var button = Instantiate(optionTemplate, options); button.gameObject.SetActive(true); cells.Add(button.gameObject);
                button.name = option.id;
                var label = button.transform.Find("Label").GetComponent<TMP_Text>(); label.text = option.label;
                label.gameObject.SetActive(option is not AppearanceCatalog.Palette);
                var nameBand = button.transform.Find("Name Band");
                if (nameBand != null) nameBand.gameObject.SetActive(option is not AppearanceCatalog.Palette);
                var icon = button.transform.Find("Icon").GetComponent<Image>();
                icon.sprite = option.thumbnail;
                icon.type = Image.Type.Simple;
                icon.preserveAspect = option is not AppearanceCatalog.Palette;
                icon.color = option is AppearanceCatalog.Palette palette ? palette.color : Color.white;
                icon.enabled = icon.sprite != null || option is AppearanceCatalog.Palette;
                if (option is not AppearanceCatalog.Palette)
                {
                    // Single-line choices give more of the card to the image; outfits keep two readable lines.
                    float nameHeight = option.label != null && option.label.Contains("\n") ? 35 : 22;
                    label.rectTransform.offsetMax = new Vector2(label.rectTransform.offsetMax.x, 9 + nameHeight);
                    if (nameBand != null) ((RectTransform)nameBand).offsetMax = new Vector2(-6, 10 + nameHeight);
                    icon.rectTransform.offsetMin = new Vector2(6, 11 + nameHeight);
                    ((RectTransform)button.transform.Find("Selected")).anchoredPosition = new Vector2(-15, 18 + nameHeight);
                }
                if (option is AppearanceCatalog.Palette)
                {
                    icon.rectTransform.anchorMin = new Vector2(.16f, .18f); icon.rectTransform.anchorMax = new Vector2(.84f, .86f);
                    icon.rectTransform.offsetMin = icon.rectTransform.offsetMax = Vector2.zero;
                    var check = (RectTransform)button.transform.Find("Selected");
                    check.anchorMin = check.anchorMax = new Vector2(.8f, .24f);
                    check.anchoredPosition = Vector2.zero; check.sizeDelta = new Vector2(16, 16);
                }
                button.transform.Find("Selected").gameObject.SetActive(option.id == SelectedId());
                var border = button.transform.Find("Selection Border");
                if (border != null) border.gameObject.SetActive(option.id == SelectedId());
                string id = option.id; button.onClick.AddListener(() => Choose(id));
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(options); scroll.verticalNormalizedPosition = 1;
        }
        private void Choose(string id)
        {
            if (!editing) return;
            if (SelectedId() == id) return;
            switch (category)
            {
                case 0: draft.bodyId = id; break; case 1: draft.skinId = id; break;
                case 2: draft.faceId = id; break; case 3: draft.hairId = id; break;
                case 4: draft.hairColorId = id; break; case 5: draft.outfitId = id; break;
                default: draft.hatId = id; break;
            }
            float scrollPosition = scroll.verticalNormalizedPosition;
            draft = preview.Catalog.Validate(draft); preview.Apply(draft); RebuildOptions();
            scroll.verticalNormalizedPosition = scrollPosition;
            if (category == 0 || category == 6) FramePreview();
            if (restaurantSelector != null) restaurantSelector.ReactToSelection(category);
        }
    }
}
