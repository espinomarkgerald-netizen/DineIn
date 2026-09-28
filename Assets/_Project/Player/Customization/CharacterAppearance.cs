using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DineIn.Appearance
{
    /// <summary>Instance-owned visuals only. Never reads another player's/global recipe.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10)] // Update stable sockets before trolley presentation's LateUpdate.
    public sealed class CharacterAppearance : MonoBehaviour
    {
        [SerializeField] private AppearanceCatalog catalog;
        [SerializeField] private Animator animator;
        [SerializeField] private Renderer[] originalRenderers;
        [SerializeField] private Transform visualParent;
        [SerializeField] private Vector3 visualScale = Vector3.one;
        [Tooltip("Gameplay adults share the catalog's body height; menu previews retain their own framing scale.")]
        [SerializeField] private bool normalizeAdultHeight;
        [System.Serializable] public sealed class AttachmentPoint
        {
            public Transform point;
            public HumanBodyBones bone;
            [Tooltip("Calibrated against the same carry pose on both avatars, not their unrelated bind-pose axes.")]
            public bool hasCalibratedPose;
            public Vector3 customizedBonePosition;
            public Quaternion customizedBoneRotation = Quaternion.identity;
            [System.NonSerialized] public Transform originalParent;
            [System.NonSerialized] public Transform followingBone;
            [System.NonSerialized] public Vector3 position, scale;
            [System.NonSerialized] public Quaternion rotation;
        }
        [Tooltip("Existing task anchors under the legacy skeleton. References are retained and restored with the legacy visual.")]
        [SerializeField] private AttachmentPoint[] attachmentPoints = System.Array.Empty<AttachmentPoint>();
        [Tooltip("Menu preview only; leave empty for gameplay characters.")]
        [SerializeField] private RuntimeAnimatorController previewController;
        private RuntimeAnimatorController originalController;
        private Avatar originalAvatar;
        private bool[] originalVisibility;
        private GameObject visual, hair, hat;
        private SkinnedMeshRenderer[] meshes;
        private Renderer[] hairRenderers;
        private Transform head;
        private AppearanceRecipe applied;
        private MaterialPropertyBlock block;
        private Quaternion attachmentRotation;
        public AppearanceCatalog Catalog => catalog != null ? catalog : catalog = AppearanceCatalog.Load();
        public Transform Head => head;
        public bool IsCustomized => applied != null;
        public bool TryGetVisualBounds(out Bounds bounds)
        {
            bounds = default;
            var visible = visual != null ? visual.GetComponentsInChildren<Renderer>() : originalRenderers;
            bool found = false;
            if (visible == null) return false;
            foreach (var renderer in visible)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer is ParticleSystemRenderer) continue;
                if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        private void Capture()
        {
            if (originalVisibility != null) return;
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (visualParent == null) visualParent = animator.transform;
            originalAvatar = animator.avatar;
            originalController = animator.runtimeAnimatorController;
            foreach (var attachment in attachmentPoints)
            {
                if (attachment.point == null) continue;
                attachment.originalParent = attachment.point.parent;
                attachment.position = attachment.point.localPosition; attachment.rotation = attachment.point.localRotation; attachment.scale = attachment.point.localScale;
            }
            if (originalRenderers == null || originalRenderers.Length == 0)
                originalRenderers = animator.GetComponentsInChildren<Renderer>(true).Where(r => r is SkinnedMeshRenderer || r is MeshRenderer).ToArray();
            originalVisibility = originalRenderers.Select(r => r != null && r.enabled).ToArray();
        }
        public void Apply(AppearanceRecipe recipe)
        {
            Capture();
            if (recipe == null) { RestoreOriginal(); return; }
            var next = Catalog.Validate(recipe);
            if (next.SameAs(applied)) return;
            bool creating = visual == null;
            if (previewController != null) animator.runtimeAnimatorController = previewController;
            if (creating)
            {
                var model = AppearanceCatalog.Find(Catalog.bodies, Catalog.defaults.bodyId).model;
                visual = Instantiate(model, visualParent, false);
                visual.name = "Customized Visual";
                visual.transform.localScale = visualScale;
                if (normalizeAdultHeight)
                {
                    var inherited = visualParent.lossyScale;
                    float size = Catalog.adultHeight / Mathf.Max(.001f, Catalog.modelHeight);
                    visual.transform.localScale = new Vector3(size / Mathf.Max(.001f, Mathf.Abs(inherited.x)),
                        size / Mathf.Max(.001f, Mathf.Abs(inherited.y)), size / Mathf.Max(.001f, Mathf.Abs(inherited.z)));
                }
                var importedAnimator = visual.GetComponent<Animator>();
                var avatar = importedAnimator.avatar;
                importedAnimator.enabled = false;
                if (Application.isPlaying) Destroy(importedAnimator); else DestroyImmediate(importedAnimator);
                meshes = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                RebindPreservingAnimation(avatar);
                head = animator.GetBoneTransform(HumanBodyBones.Head);
                attachmentRotation = Quaternion.Inverse(head.rotation) * visual.transform.rotation;
                BindAttachmentPoints();
            }
            else if (!visual.activeSelf)
            {
                visual.SetActive(true);
                RebindPreservingAnimation(AppearanceCatalog.Find(Catalog.bodies, Catalog.defaults.bodyId).model.GetComponent<Animator>().avatar);
                BindAttachmentPoints();
            }
            foreach (var r in originalRenderers) if (r != null) r.enabled = false;
            if (creating || applied == null || applied.bodyId != next.bodyId)
            {
                var source = AppearanceCatalog.Find(Catalog.bodies, next.bodyId).model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var target in meshes)
                {
                    var match = source.First(r => r.name == target.name);
                    target.sharedMesh = match.sharedMesh;
                    target.localBounds = match.localBounds;
                }
            }
            if (creating || applied == null || applied.skinId != next.skinId || applied.faceId != next.faceId || applied.outfitId != next.outfitId)
            {
                var skin = AppearanceCatalog.Find(Catalog.skins, next.skinId).color;
                foreach (var r in meshes)
                {
                    bool isSkin = r.name == "BodyHead" || r.name == "Ears" || r.name == "Hands";
                    r.sharedMaterial = isSkin ? Catalog.skinMaterial : r.name == "Face" ?
                        AppearanceCatalog.Find(Catalog.faces, next.faceId).material : AppearanceCatalog.Find(Catalog.outfits, next.outfitId).material;
                    Tint(r, isSkin ? skin : Color.white);
                }
            }
            var hatOption = AppearanceCatalog.Find(Catalog.hats, next.hatId);
            if (creating || applied == null || applied.hairId != next.hairId)
                Replace(ref hair, AppearanceCatalog.Find(Catalog.hairs, next.hairId), true);
            if (creating || applied == null || applied.hatId != next.hatId || applied.hairId != next.hairId)
            {
                Replace(ref hat, hatOption, false);
                if (hat != null && (hair == null || hatOption.HidesHair(next.hairId)))
                    hat.transform.localPosition += attachmentRotation * hatOption.bareHeadPositionOffset;
            }
            if (hair != null)
            {
                hair.SetActive(!hatOption.HidesHair(next.hairId));
                if (creating || applied == null || applied.hairId != next.hairId || applied.hairColorId != next.hairColorId)
                    foreach (var r in hairRenderers) Tint(r, AppearanceCatalog.Find(Catalog.hairColors, next.hairColorId).color);
            }
            applied = next.Copy();
        }
        private void Replace(ref GameObject current, AppearanceCatalog.Attachment option, bool isHair)
        {
            if (current != null)
            {
                current.SetActive(false);
                if (Application.isPlaying) Destroy(current); else DestroyImmediate(current);
            }
            current = option.prefab == null ? null : Instantiate(option.prefab, head, false);
            if (current == null) return;
            // Preserve the asset's imported basis; catalog offsets are relative to that basis.
            current.transform.localPosition = attachmentRotation * (option.position + option.prefab.transform.localPosition);
            current.transform.localRotation = attachmentRotation * Quaternion.Euler(option.eulerAngles) * option.prefab.transform.localRotation;
            current.transform.localScale = Vector3.Scale(option.scale, option.prefab.transform.localScale);
            if (isHair)
            {
                hairRenderers = current.GetComponentsInChildren<Renderer>(true);
                foreach (var r in hairRenderers) r.sharedMaterial = Catalog.hairMaterial;
            }
        }
        private void Tint(Renderer renderer, Color color)
        {
            block ??= new MaterialPropertyBlock();
            block.Clear(); block.SetColor("_BaseColor", color); block.SetColor("_Color", color); renderer.SetPropertyBlock(block);
        }
        private void BindAttachmentPoints()
        {
            foreach (var attachment in attachmentPoints)
            {
                if (attachment.point == null) continue;
                // These are the real gameplay sockets, not disposable cosmetic children.
                // Keep their references and world scale, including any currently held props.
                attachment.point.SetParent(transform, true);
                attachment.followingBone = attachment.hasCalibratedPose ? animator.GetBoneTransform(attachment.bone) : null;
            }
            UpdateAttachmentPoints();
        }
        private void LateUpdate()
        {
            if (applied != null) UpdateAttachmentPoints();
        }
        private void UpdateAttachmentPoints()
        {
            foreach (var attachment in attachmentPoints)
            {
                if (attachment.point == null || attachment.followingBone == null) continue;
                attachment.point.SetPositionAndRotation(
                    attachment.followingBone.TransformPoint(attachment.customizedBonePosition),
                    attachment.followingBone.rotation * attachment.customizedBoneRotation);
            }
        }
        private void RebindPreservingAnimation(Avatar avatar)
        {
            if (!animator.isInitialized || animator.runtimeAnimatorController == null) { animator.avatar = avatar; animator.Rebind(); return; }
            var parameters = animator.parameters;
            var floats = parameters.Where(p => p.type == AnimatorControllerParameterType.Float).ToDictionary(p => p.nameHash, p => animator.GetFloat(p.nameHash));
            var ints = parameters.Where(p => p.type == AnimatorControllerParameterType.Int).ToDictionary(p => p.nameHash, p => animator.GetInteger(p.nameHash));
            var bools = parameters.Where(p => p.type == AnimatorControllerParameterType.Bool).ToDictionary(p => p.nameHash, p => animator.GetBool(p.nameHash));
            var states = Enumerable.Range(0, animator.layerCount).Select(i => animator.GetCurrentAnimatorStateInfo(i)).ToArray();
            var weights = Enumerable.Range(0, animator.layerCount).Select(i => animator.GetLayerWeight(i)).ToArray();
            animator.avatar = avatar; animator.Rebind();
            foreach (var pair in floats) animator.SetFloat(pair.Key, pair.Value);
            foreach (var pair in ints) animator.SetInteger(pair.Key, pair.Value);
            foreach (var pair in bools) animator.SetBool(pair.Key, pair.Value);
            for (int i = 0; i < states.Length; i++)
            {
                animator.SetLayerWeight(i, weights[i]);
                if (animator.HasState(i, states[i].fullPathHash)) animator.Play(states[i].fullPathHash, i, states[i].normalizedTime);
            }
        }
        public void RestoreOriginal()
        {
            if (originalVisibility == null || applied == null) return;
            if (visual != null) visual.SetActive(false);
            foreach (var attachment in attachmentPoints)
            {
                if (attachment.point == null) continue;
                attachment.followingBone = null;
                attachment.point.SetParent(attachment.originalParent, false);
                attachment.point.localPosition = attachment.position; attachment.point.localRotation = attachment.rotation; attachment.point.localScale = attachment.scale;
            }
            for (int i = 0; i < originalRenderers.Length; i++) if (originalRenderers[i] != null) originalRenderers[i].enabled = originalVisibility[i];
            if (previewController != null) animator.runtimeAnimatorController = originalController;
            RebindPreservingAnimation(originalAvatar); applied = null;
        }
    }
}
