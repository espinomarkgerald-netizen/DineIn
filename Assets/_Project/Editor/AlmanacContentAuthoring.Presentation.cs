#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static partial class AlmanacContentAuthoring
{
    [MenuItem("Dine In/Almanac/Apply Presentation Polish")]
    public static void PolishPresentation()
    {
        var menu = UnityEngine.Object.FindFirstObjectByType<AlmanacMenu>();
        if (EditorApplication.isPlayingOrWillChangePlaymode || menu == null || menu.gameObject.scene.name != "NewGameMenu")
            throw new InvalidOperationException("Open NewGameMenu in Edit Mode.");
        menu.Close();
        var right = menu.GetComponentsInChildren<RectTransform>(true).First(r => r.name == "Feature page");
        var photo = right.Find("Animated photograph").GetComponent<RectTransform>();
        var variants = right.Find("Uniform editions").GetComponent<ScrollRect>();
        var related = right.Find("Related stories");
        if (related != null) UnityEngine.Object.DestroyImmediate(related.gameObject);
        var labelTransform = right.Find("Variant caption");
        if (labelTransform == null)
        {
            labelTransform = new GameObject("Variant caption", typeof(RectTransform), typeof(TextMeshProUGUI)).transform;
            labelTransform.SetParent(right, false);
        }
        var label = labelTransform.GetComponent<TMP_Text>();
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/UI/Assets/Fonts/Anton/Anton-Regular SDF.asset");
        label.fontSize = 15; label.text = "UNIFORM / RESTAURANT"; label.color = new Color(.09f,.20f,.24f); label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.TopLeft;
        SetArticleRect(label.rectTransform, Vector2.zero, new Vector2(.46f,0), new Vector2(0,52), new Vector2(0,74));
        SetArticleRect(variants.GetComponent<RectTransform>(), Vector2.zero, new Vector2(.46f,0), Vector2.zero, new Vector2(0,48));
        SetArticleRect(photo, Vector2.zero, new Vector2(.46f,1), Vector2.zero, new Vector2(0,-75));
        photo.GetComponent<UnityEngine.UI.Image>().color = new Color(.76f,.84f,.81f);
        SetArticleRect(right.Find("Article").GetComponent<RectTransform>(), new Vector2(.50f,0), Vector2.one, Vector2.zero, new Vector2(0,-75));
        var so = new SerializedObject(menu);
        so.FindProperty("photoFrame").objectReferenceValue = photo;
        so.FindProperty("variantScroll").objectReferenceValue = variants;
        so.FindProperty("variantLabel").objectReferenceValue = label;
        so.ApplyModifiedProperties();
        var stage = new SerializedObject(menu.GetComponent<AlmanacPreviewStage>());
        stage.FindProperty("textureSize").intValue = 1024;
        stage.FindProperty("mobileTextureSize").intValue = 768;
        stage.ApplyModifiedProperties();
        variants.gameObject.SetActive(false); label.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        EditorSceneManager.SaveScene(menu.gameObject.scene);
    }

    public static string GenerateRestaurantArtwork()
    {
        var entries = AssetDatabase.LoadAssetAtPath<AlmanacCatalog>(Root+"/AlmanacCatalog.asset").LoadEntries()
            .Where(e => e.category == AlmanacCategory.Restaurants).ToArray();
        var owner = new GameObject("Almanac restaurant artwork");
        var stage = owner.AddComponent<AlmanacPreviewStage>();
        var previous = RenderTexture.active;
        try
        {
            foreach (var entry in entries)
            {
                var sample = UnityEngine.Object.Instantiate(entry);
                sample.previewKind = AlmanacPreviewKind.Model; sample.previewEuler = Vector3.zero;
                bool shown = stage.Show(sample); UnityEngine.Object.DestroyImmediate(sample);
                if (!shown) throw new InvalidOperationException("Missing restaurant visual: "+entry.entryId);
                var camera = Resources.FindObjectsOfTypeAll<Camera>().Single(c=>c.name=="Preview Camera"&&c.targetTexture==stage.Texture);
                var renderers = camera.transform.parent.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
                var bounds = renderers[0].bounds;
                foreach(var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                camera.transform.rotation = Quaternion.Euler(35,-38,0);
                camera.transform.position = bounds.center-camera.transform.forward*6;
                var wide = new RenderTexture(1536,864,24,RenderTextureFormat.ARGB32) { antiAliasing=4, filterMode=FilterMode.Bilinear };
                try
                {
                    camera.targetTexture = wide; camera.aspect = 16f/9f;
                    float minX=float.MaxValue,minY=float.MaxValue,maxX=float.MinValue,maxY=float.MinValue;
                    foreach(var renderer in renderers)
                    {
                        if(!renderer.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh==null)continue;
                        foreach(var vertex in filter.sharedMesh.vertices)
                        {
                            var point=camera.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertex));
                            minX=Mathf.Min(minX,point.x);maxX=Mathf.Max(maxX,point.x);minY=Mathf.Min(minY,point.y);maxY=Mathf.Max(maxY,point.y);
                        }
                    }
                    camera.transform.position+=camera.transform.right*((minX+maxX)*.5f)+camera.transform.up*((minY+maxY)*.5f);
                    camera.orthographicSize=Mathf.Max((maxY-minY)*.5f,(maxX-minX)*.5f/camera.aspect)/.94f;
                    camera.Render();RenderTexture.active=wide;
                    var texture=new Texture2D(1536,864,TextureFormat.RGBA32,false);
                    texture.ReadPixels(new Rect(0,0,1536,864),0,0);texture.Apply();
                    string path=Root+"/Thumbnails/"+entry.entryId+".png";
                    System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
                    AssetDatabase.ImportAsset(path);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                    importer.maxTextureSize=2048;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
                    entry.icon=AssetDatabase.LoadAssetAtPath<Sprite>(path);entry.previewKind=AlmanacPreviewKind.Image;EditorUtility.SetDirty(entry);
                }
                finally{camera.targetTexture=null;RenderTexture.active=previous;wide.Release();UnityEngine.Object.DestroyImmediate(wide);}
            }
        }
        finally{RenderTexture.active=previous;stage.Release();UnityEngine.Object.DestroyImmediate(owner);}
        AssetDatabase.SaveAssets();return "Rendered "+entries.Length+" actual restaurant panoramas";
    }
    private static void SetArticleRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min; rect.anchorMax = max; rect.pivot = Vector2.one * .5f;
        rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
    }
}
#endif
