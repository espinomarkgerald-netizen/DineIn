using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>Renders saved kitchen prefabs without entering Play Mode or touching campaign data.</summary>
public static class FastFoodInteractionPreview
{
    public static string CaptureHud(int width=1920,int height=1080)
    {
        if(Application.isPlaying)throw new InvalidOperationException("Preview outside Play Mode.");
        var source=UnityEngine.Object.FindObjectsByType<FastFoodCookingView>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();
        var data=new SerializedObject(source);var authored=(Canvas)data.FindProperty("canvas").objectReferenceValue;
        var scene=EditorSceneManager.NewPreviewScene();var texture=new RenderTexture(width,height,24);
        var previous=RenderTexture.active;Texture2D image=null;
        try
        {
            var cameraObject=new GameObject("HUD preview camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraObject,scene);
            var camera=cameraObject.GetComponent<Camera>();camera.scene=scene;camera.targetTexture=texture;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.25f,.30f,.36f);
            var root=UnityEngine.Object.Instantiate(authored.gameObject);SceneManager.MoveGameObjectToScene(root,scene);root.SetActive(true);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            Transform Ref(string key)
            {
                var original=(Component)data.FindProperty(key).objectReferenceValue;
                var indices=new System.Collections.Generic.Stack<int>();
                for(var current=original.transform;current!=authored.transform;current=current.parent)indices.Push(current.GetSiblingIndex());
                var match=root.transform;while(indices.Count>0)match=match.GetChild(indices.Pop());return match;
            }
            Ref("selection").gameObject.SetActive(false);Ref("help").gameObject.SetActive(false);Ref("station").gameObject.SetActive(true);
            Ref("heading").parent.gameObject.SetActive(true);Ref("heading").GetComponent<TMPro.TMP_Text>().text="FRYER";
            Ref("progress").GetComponent<TMPro.TMP_Text>().text="Fried Chicken · 4/8 cooked";
            Ref("progressFill").GetComponent<UnityEngine.UI.Image>().fillAmount=.5f;
            Ref("noticeButtonLabel").GetComponent<TMPro.TMP_Text>().text="Restock (3)";
            Ref("notification").gameObject.SetActive(false);Ref("serve").gameObject.SetActive(false);Ref("tickets").gameObject.SetActive(false);
            Ref("feedback").parent.gameObject.SetActive(false);Ref("staffActivity").parent.gameObject.SetActive(false);
            Ref("discardBox").gameObject.SetActive(true);
            var cue=Ref("floatingCue").GetComponent<FastFoodCookingToast>();cue.gameObject.SetActive(true);cue.Show("Batch ready — nicely done!",MenuCatalog.Default.Products[0].sprite,3);
            cue.group.alpha=1;cue.motion.localScale=Vector3.one;
            foreach(var group in root.GetComponentsInChildren<CanvasGroup>(true))
                if(group!=cue.group&&group.gameObject.activeInHierarchy)group.alpha=1;
            var fade=Ref("stationFade");fade.gameObject.SetActive(false);
            Ref("hotbarContainer").gameObject.SetActive(false);
            Canvas.ForceUpdateCanvases();UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform);
            camera.Render();RenderTexture.active=texture;image=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);image.Apply();
            string path=System.IO.Path.GetFullPath("Temp/CozyHudPreview.png");System.IO.File.WriteAllBytes(path,image.EncodeToPNG());return path;
        }
        finally{RenderTexture.active=previous;EditorSceneManager.ClosePreviewScene(scene);UnityEngine.Object.DestroyImmediate(texture);if(image!=null)UnityEngine.Object.DestroyImmediate(image);}
    }
    public static string CaptureFoods()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Preview outside Play Mode.");
        var scene=EditorSceneManager.NewPreviewScene();
        var texture=new RenderTexture(1400,800,24);var previous=RenderTexture.active;Texture2D image=null;
        var disposableMeshes=new System.Collections.Generic.List<Mesh>();
        var previewOutlines=new System.Collections.Generic.List<Outline>();
        try
        {
            var cameraObject=new GameObject("Food preview camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraObject,scene);
            var camera=cameraObject.GetComponent<Camera>();camera.scene=scene;camera.targetTexture=texture;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.20f,.27f,.34f);
            camera.transform.position=new Vector3(0,7,-9);camera.transform.LookAt(Vector3.zero);camera.fieldOfView=38;
            var lightObject=new GameObject("Food preview light",typeof(Light));SceneManager.MoveGameObjectToScene(lightObject,scene);
            var light=lightObject.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1;light.transform.rotation=Quaternion.Euler(50,-35,0);
            var types=new[]{ItemTypeKitchen.Burger,ItemTypeKitchen.ChickenSandwich,ItemTypeKitchen.FishFilletSandwich,ItemTypeKitchen.Chicken,ItemTypeKitchen.ChickenNuggets,ItemTypeKitchen.Fries};
            for(int i=0;i<types.Length;i++)
            {
                var recipe=MenuCatalog.Default.Products.First(r=>r.kitchenItemType==types[i]);
                for(int row=0;row<2;row++)
                {
                    var root=UnityEngine.Object.Instantiate(row==0?recipe.kitchenServingPrefab:recipe.kitchenCookingPrefab);SceneManager.MoveGameObjectToScene(root,scene);root.SetActive(true);
                    foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
                    {filter.sharedMesh=UnityEngine.Object.Instantiate(filter.sharedMesh);disposableMeshes.Add(filter.sharedMesh);}
                    var renderers=root.GetComponentsInChildren<MeshRenderer>();var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                    root.transform.localScale*=1.25f/Mathf.Max(bounds.size.x,bounds.size.z);
                    bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                    root.transform.position+=new Vector3((i-2.5f)*1.65f,-bounds.min.y,(row-.5f)*2.2f)-new Vector3(bounds.center.x,0,bounds.center.z);
                    if(row==1)foreach(var renderer in renderers)
                    {
                        var materials=renderer.sharedMaterials;
                        for(int m=0;m<materials.Length;m++)if(materials[m].HasProperty("_CookingSurface")&&materials[m].GetFloat("_CookingSurface")>.5f)
                        {var properties=new MaterialPropertyBlock();properties.SetColor("_BaseColor",materials[m].GetColor("_RawColor"));renderer.SetPropertyBlock(properties,m);}
                    }
                    foreach(var outline in root.GetComponentsInChildren<Outline>())
                    {
                        // Exercise the real outline material setup on isolated mesh copies.
                        const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                        foreach(var method in new[]{"Awake","OnEnable","Update"})typeof(Outline).GetMethod(method,flags).Invoke(outline,null);
                        previewOutlines.Add(outline);
                    }
                }
            }
            camera.Render();RenderTexture.active=texture;image=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);image.Apply();
            string path=System.IO.Path.GetFullPath("Temp/CozyFoodPreview.png");System.IO.File.WriteAllBytes(path,image.EncodeToPNG());return path;
        }
        finally
        {
            RenderTexture.active=previous;
            foreach(var outline in previewOutlines)
            {
                const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                typeof(Outline).GetMethod("OnDisable",flags).Invoke(outline,null);
                foreach(var field in new[]{"outlineMaskMaterial","outlineFillMaterial"})
                {var reference=typeof(Outline).GetField(field,flags);UnityEngine.Object.DestroyImmediate(reference.GetValue(outline) as Material);reference.SetValue(outline,null);}
            }
            EditorSceneManager.ClosePreviewScene(scene);
            foreach(var mesh in disposableMeshes)UnityEngine.Object.DestroyImmediate(mesh);
            UnityEngine.Object.DestroyImmediate(texture);if(image!=null)UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
