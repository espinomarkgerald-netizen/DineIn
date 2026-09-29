using System;
using System.Linq;
using UnityEngine;

namespace DineIn.Appearance
{
    [CreateAssetMenu(menuName = "Dine In/Appearance Catalog")]
    public sealed class AppearanceCatalog : ScriptableObject
    {
        [Serializable] public class Option
        {
            public string id, label;
            public Sprite thumbnail;
            [Tooltip("Empty means compatible with every body.")] public string[] bodies = Array.Empty<string>();
            public bool Fits(string body) => bodies == null || bodies.Length == 0 || Array.IndexOf(bodies, body) >= 0;
        }
        [Serializable] public sealed class Body : Option
        {
            public GameObject model;
            public AppearanceRecipe defaults;
            [Tooltip("Optional derived skin mesh with sleeve-compatible weights. Same geometry, UVs and bind poses as Hands.")]
            public Mesh sleeveSkinMesh;
        }
        [Serializable] public sealed class HairFit
        {
            public string hairId;
            public Vector3 position, eulerAngles, scale = Vector3.one;
        }
        [Serializable] public sealed class Palette : Option { public Color color = Color.white; }
        [Serializable] public sealed class Surface : Option { public Material material; }
        [Serializable] public sealed class Attachment : Option
        {
            public GameObject prefab;
            public Vector3 position, eulerAngles, scale = Vector3.one;
            public bool hidesHair;
            [Tooltip("Only these hairstyles need the full-hide fallback. Non-covering accessories leave this empty.")]
            public string[] incompatibleHairIds = Array.Empty<string>();
            [Tooltip("Headwear fit offset when bald or using the full-hide fallback.")]
            public Vector3 bareHeadPositionOffset;
            [Tooltip("Additional menu framing height relative to body height; zero for no hat.")]
            [Range(0, .5f)] public float previewHeadroom;
            [Tooltip("Visual-only hairstyle fit while this hat is worn. Removing it restores the authored hair transform.")]
            public HairFit[] hairFits = Array.Empty<HairFit>();
            public bool HidesHair(string hairId) => hidesHair || Array.IndexOf(incompatibleHairIds ?? Array.Empty<string>(), hairId) >= 0;
        }

        public Body[] bodies = Array.Empty<Body>();
        public Palette[] skins = Array.Empty<Palette>(), hairColors = Array.Empty<Palette>();
        public Surface[] faces = Array.Empty<Surface>(), outfits = Array.Empty<Surface>();
        public Attachment[] hairs = Array.Empty<Attachment>(), hats = Array.Empty<Attachment>();
        public Material skinMaterial, hairMaterial;
        [Tooltip("World-space adult height for gameplay visuals, excluding hair and hats. Does not scale agents or gameplay roots.")]
        [Min(.1f)] public float adultHeight = 3.81f;
        [Tooltip("Authored rest-pose body height in the shared derived models.")]
        [Min(.1f)] public float modelHeight = 2f;
        public AppearanceRecipe defaults;
        private static AppearanceCatalog cached;
        public static AppearanceCatalog Load() => cached != null ? cached : cached = Resources.Load<AppearanceCatalog>("AppearanceCatalog");

        public static T Find<T>(T[] options, string id) where T : Option => options?.FirstOrDefault(x => x != null && x.id == id);
        private static string Resolve<T>(T[] options, string id, string fallback, string body) where T : Option
        {
            var item = Find(options, id);
            if (item != null && item.Fits(body)) return item.id;
            item = Find(options, fallback);
            return item != null && item.Fits(body) ? item.id : options?.FirstOrDefault(x => x != null && x.Fits(body))?.id;
        }
        public AppearanceRecipe Validate(AppearanceRecipe input)
        {
            var r = (input ?? defaults ?? new AppearanceRecipe()).Copy();
            r.version = 2;
            var body = Find(bodies, r.bodyId) ?? Find(bodies, defaults?.bodyId) ?? bodies.FirstOrDefault();
            if (body == null) throw new InvalidOperationException("Appearance catalog has no bodies.");
            r.bodyId = body.id;
            var d = body.defaults ?? defaults ?? new AppearanceRecipe();
            r.skinId = Resolve(skins, r.skinId, d.skinId, r.bodyId);
            r.faceId = Resolve(faces, r.faceId, d.faceId, r.bodyId);
            r.hairId = Resolve(hairs, r.hairId, d.hairId, r.bodyId);
            r.hairColorId = Resolve(hairColors, r.hairColorId, d.hairColorId, r.bodyId);
            r.outfitId = Resolve(outfits, r.outfitId, d.outfitId, r.bodyId);
            r.hatId = Resolve(hats, r.hatId, d.hatId, r.bodyId);
            return r;
        }

        public AppearanceRecipe GenerateEmployee(string employeeId)
        {
            // FNV-1a + local PRNG: stable across sessions, never consumes UnityEngine.Random.
            uint state = 2166136261;
            foreach (char c in employeeId ?? "") state = unchecked((state ^ c) * 16777619);
            string Pick<T>(T[] entries, string body = null) where T : Option
            {
                var pool = entries.Where(x => x != null && (body == null || x.Fits(body))).ToArray();
                state = unchecked(state * 1664525 + 1013904223);
                return pool.Length == 0 ? null : pool[state % (uint)pool.Length].id;
            }
            var r = new AppearanceRecipe { bodyId = Pick(bodies) };
            r.skinId = Pick(skins); r.faceId = Pick(faces, r.bodyId);
            r.hairId = Pick(hairs, r.bodyId); r.hairColorId = Pick(hairColors);
            r.outfitId = Find(bodies, r.bodyId).defaults.outfitId; r.hatId = "none";
            return Validate(r);
        }
    }
}
