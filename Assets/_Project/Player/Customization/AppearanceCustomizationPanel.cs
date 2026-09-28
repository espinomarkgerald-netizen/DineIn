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
        [SerializeField] private Vector2 optionCardSize = new(126, 138);
        [SerializeField] private Vector2 colorSwatchSize = new(58, 64);
        [SerializeField] private Sprite selectedTabSprite;
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
        private UnityEngine.UI.GridLayoutGroup grid;
        private float gridWidth = -1;
        private float cameraSize, cameraFov;

        private void Awake()
        {
            openButton.onClick.AddListener(Open);
            applyButton.onClick.AddListener(Apply);
            cancelButton.onClick.AddListener(Cancel);
            for (int i = 0; i < categories.Length; i++)
            { int index = i; categories[i].onClick.AddListener(() => SelectCategory(index)); }
            categorySprites = categories.Select(c => c.GetComponent<Image>().sprite).ToArray();
            panel.SetActive(false); optionTemplate.gameObject.SetActive(false);
            grid = options.GetComponent<UnityEngine.UI.GridLayoutGroup>();
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
            previewCamera.transform.position += preview.transform.position - lastPreviewPosition;
            lastPreviewPosition = preview.transform.position;
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
            status.text = "Changes are saved only when you Apply.";
            preview.Apply(draft);
            if (previewCamera != null)
            {
                cameraPosition = previewCamera.transform.position;
                cameraSize = previewCamera.orthographicSize; cameraFov = previewCamera.fieldOfView;
                cameraCaptured = true;
                followWasEnabled = cameraFollow != null && cameraFollow.enabled;
                if (cameraFollow != null) cameraFollow.enabled = false;
                FramePreview();
            }
            if (restaurantSelector != null) restaurantSelector.BeginCustomization();
            SelectCategory(0);
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
            editing = false; draft = null;
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
            if (previewCamera == null || !preview.TryGetVisualBounds(out var bounds)) return;
            // Stable full-body framing, including current headwear, independent of screen resolution.
            var extents = bounds.extents;
            float Project(Vector3 axis) => Mathf.Abs(axis.x) * extents.x + Mathf.Abs(axis.y) * extents.y + Mathf.Abs(axis.z) * extents.z;
            float halfHeight = Mathf.Max(Project(previewCamera.transform.up) / previewScreenHeight,
                Project(previewCamera.transform.right) / (Mathf.Max(.1f, previewCamera.aspect) * .39f));
            float depth = previewCamera.WorldToViewportPoint(bounds.center).z;
            if (previewCamera.orthographic) previewCamera.orthographicSize = halfHeight * 1.08f;
            else previewCamera.fieldOfView = Mathf.Clamp(2 * Mathf.Atan(halfHeight * 1.08f / Mathf.Max(1, depth)) * Mathf.Rad2Deg, 15, 65);
            previewCamera.transform.position += bounds.center - previewCamera.ViewportToWorldPoint(new Vector3(.225f, .53f, depth));
        }
        private void ResizeGrid()
        {
            if (grid == null || scroll.viewport == null) return;
            float width = scroll.viewport.rect.width;
            if (Mathf.Abs(width - gridWidth) < .5f) return;
            gridWidth = width;
            Vector2 size = category == 1 || category == 4 ? colorSwatchSize : optionCardSize;
            int columns = Mathf.Max(1, Mathf.FloorToInt((width - grid.padding.horizontal + grid.spacing.x) / (size.x + grid.spacing.x)));
            grid.constraintCount = columns;
            grid.cellSize = new Vector2(Mathf.Min(size.x, Mathf.Max(1, width - grid.padding.horizontal)), size.y);
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
            category = index;
            for (int i = 0; i < categories.Length; i++)
            {
                categories[i].interactable = true;
                categories[i].GetComponent<Image>().sprite = i == category && selectedTabSprite != null ? selectedTabSprite : categorySprites[i];
                categories[i].GetComponentInChildren<TMP_Text>().color = i == category ? Color.white : new Color(.04f, .24f, .31f);
            }
            gridWidth = -1; ResizeGrid();
            RebuildOptions();
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
                var icon = button.transform.Find("Icon").GetComponent<Image>();
                icon.sprite = option.thumbnail;
                icon.color = option is AppearanceCatalog.Palette palette ? palette.color : Color.white;
                icon.enabled = icon.sprite != null || option is AppearanceCatalog.Palette;
                if (option is AppearanceCatalog.Palette)
                {
                    icon.rectTransform.anchorMin = new Vector2(.16f, .18f); icon.rectTransform.anchorMax = new Vector2(.84f, .86f);
                    icon.rectTransform.offsetMin = icon.rectTransform.offsetMax = Vector2.zero;
                }
                button.transform.Find("Selected").gameObject.SetActive(option.id == SelectedId());
                string id = option.id; button.onClick.AddListener(() => Choose(id));
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(options); scroll.verticalNormalizedPosition = 1;
        }
        private void Choose(string id)
        {
            if (!editing) return;
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
            FramePreview();
            if (restaurantSelector != null) restaurantSelector.ReactToSelection(category);
        }
    }
}
