#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static partial class AlmanacContentAuthoring
{
    private const string Root = "Assets/_Project/UI/Almanac";
    private const string EntriesPath = "Assets/Resources/Almanac";
    private static readonly List<AlmanacEntryData> authored = new List<AlmanacEntryData>();
    private static List<AlmanacEntryData> legacy;

    [MenuItem("Dine In/Almanac/Populate Audited Content")]
    public static void Populate()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before authoring.");
        EnsureFolder(EntriesPath);EnsureFolder(Root+"/Thumbnails");authored.Clear();
        legacy=AssetDatabase.FindAssets("t:AlmanacEntryData",new[]{Root+"/Data"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<AlmanacEntryData>).ToList();
        PopulatePeople();PopulateObjects();
        var catalog=AssetDatabase.LoadAssetAtPath<AlmanacCatalog>(Root+"/AlmanacCatalog.asset");
        if(catalog==null){catalog=ScriptableObject.CreateInstance<AlmanacCatalog>();AssetDatabase.CreateAsset(catalog,Root+"/AlmanacCatalog.asset");}
        catalog.entries=authored.Distinct().ToArray();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        Debug.Log("[Almanac] Authored "+catalog.entries.Length+" verified entries.");
    }
    private static AlmanacEntryData Entry(string id,AlmanacCategory category,string name,string subtitle,string description,string notes,string source,string iconPath=null,string prefabPath=null,AlmanacPreviewKind kind=AlmanacPreviewKind.Image,string[] related=null)
    {
        var entry=AssetDatabase.LoadAssetAtPath<AlmanacEntryData>(EntriesPath+"/"+id+".asset");
        if(entry==null)entry=legacy?.FirstOrDefault(e=>e!=null&&(e.entryId==id || (string.IsNullOrEmpty(e.entryId)&&e.entryName==name)));
        if(entry==null){entry=ScriptableObject.CreateInstance<AlmanacEntryData>();AssetDatabase.CreateAsset(entry,EntriesPath+"/"+id+".asset");}
        entry.entryId=id;entry.category=category;entry.entryName=name;entry.subTitle=subtitle;entry.description=description;entry.gameplayNotes=notes;entry.sourceNotes=source;entry.relatedIds=related??Array.Empty<string>();entry.previewKind=kind;
        if(!string.IsNullOrEmpty(iconPath))entry.icon=AssetDatabase.LoadAssetAtPath<Sprite>(iconPath)??AssetDatabase.LoadAllAssetsAtPath(iconPath).OfType<Sprite>().FirstOrDefault();
        if(!string.IsNullOrEmpty(prefabPath))entry.previewPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if(!authored.Contains(entry))authored.Add(entry);EditorUtility.SetDirty(entry);return entry;
    }
    [MenuItem("Dine In/Almanac/Install in Game Menu")]
    public static void Install()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.name!="NewGameMenu")throw new InvalidOperationException("Open NewGameMenu in Edit Mode.");
        if(UnityEngine.Object.FindFirstObjectByType<AlmanacMenu>()!=null)throw new InvalidOperationException("Almanac already installed; edit its authored layout.");
        var catalog=AssetDatabase.LoadAssetAtPath<AlmanacCatalog>(Root+"/AlmanacCatalog.asset");if(catalog==null)throw new InvalidOperationException("Populate the catalog first.");
        var menuCanvas=GameObject.Find("GameCanvas");var menuGroup=menuCanvas.GetComponent<CanvasGroup>();if(menuGroup==null)menuGroup=Undo.AddComponent<CanvasGroup>(menuCanvas);
        var root=new GameObject("Dine In Almanac");Undo.RegisterCreatedObjectUndo(root,"Install Almanac");
        var stage=root.AddComponent<AlmanacPreviewStage>();var controller=root.AddComponent<AlmanacMenu>();
        var button=new GameObject("AlmanacButton",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));Undo.RegisterCreatedObjectUndo(button,"Install Almanac button");button.transform.SetParent(menuCanvas.transform,false);button.transform.SetSiblingIndex(2);
        var rect=button.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(84,-13);rect.sizeDelta=new Vector2(129,44);
        var image=button.GetComponent<UnityEngine.UI.Image>();image.color=new Color(.97f,.91f,.72f);var uiButton=button.GetComponent<UnityEngine.UI.Button>();uiButton.targetGraphic=image;
        var outline=button.AddComponent<UnityEngine.UI.Outline>();outline.effectColor=new Color(.15f,.25f,.26f);outline.effectDistance=new Vector2(2,-2);
        var icon=new GameObject("Newspaper",typeof(RectTransform),typeof(UnityEngine.UI.Image));icon.transform.SetParent(button.transform,false);var ir=icon.GetComponent<RectTransform>();ir.anchorMin=new Vector2(0,0);ir.anchorMax=new Vector2(0,1);ir.offsetMin=new Vector2(6,5);ir.offsetMax=new Vector2(42,-5);
        var ii=icon.GetComponent<UnityEngine.UI.Image>();ii.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Icons/GameIcons/HUD/NewspaperIcon.png");ii.preserveAspect=true;ii.raycastTarget=false;
        var text=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));text.transform.SetParent(button.transform,false);var tr=text.GetComponent<RectTransform>();tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=new Vector2(44,3);tr.offsetMax=new Vector2(-5,-3);
        var headingFont=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/UI/Assets/Fonts/Anton/Anton-Regular SDF.asset");var bodyFont=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/UI/Assets/Fonts/Open_Sans/OpenSans-VariableFont_wdth,wght SDF.asset");
        var label=text.GetComponent<TextMeshProUGUI>();label.font=headingFont;label.fontSize=21;label.text="ALMANAC";label.alignment=TextAlignmentOptions.Center;label.color=new Color(.09f,.20f,.24f);label.raycastTarget=false;
        var so=new SerializedObject(controller);so.FindProperty("catalog").objectReferenceValue=catalog;so.FindProperty("menuGroup").objectReferenceValue=menuGroup;so.FindProperty("openButton").objectReferenceValue=uiButton;so.FindProperty("previewStage").objectReferenceValue=stage;so.FindProperty("headlineFont").objectReferenceValue=headingFont;so.FindProperty("bodyFont").objectReferenceValue=bodyFont;
        so.FindProperty("pageSound").objectReferenceValue=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Restaurant/CookingAssets/Feedback/Soft Placement.wav");
        var mixer=AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>("Assets/_Project/MainMenu/NewDesign/Audio/Mixer Controller/SFX.mixer");if(mixer!=null)so.FindProperty("soundMixer").objectReferenceValue=mixer.FindMatchingGroups("").FirstOrDefault();
        so.ApplyModifiedProperties();controller.BuildLayout();
        var customization=UnityEngine.Object.FindFirstObjectByType<DineIn.Appearance.AppearanceCustomizationPanel>();
        if(customization!=null){var cs=new SerializedObject(customization);var controls=cs.FindProperty("menuControls");int size=controls.arraySize;controls.InsertArrayElementAtIndex(size);controls.GetArrayElementAtIndex(size).objectReferenceValue=uiButton;cs.ApplyModifiedProperties();}
        var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);var layer=tags.FindProperty("layers").GetArrayElementAtIndex(30);if(string.IsNullOrEmpty(layer.stringValue)){layer.stringValue="AlmanacPreview";tags.ApplyModifiedProperties();}
        var mainCamera=UnityEngine.Object.FindFirstObjectByType<CameraFollow>().GetComponent<Camera>();Undo.RecordObject(mainCamera,"Isolate Almanac previews");mainCamera.cullingMask &= ~(1<<30);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("[Almanac] Installed newspaper reader in NewGameMenu.");
    }
    public static string ValidateCatalog()
    {
        var catalog=AssetDatabase.LoadAssetAtPath<AlmanacCatalog>(Root+"/AlmanacCatalog.asset");var entries=catalog.LoadEntries();var errors=new List<string>();var ids=new HashSet<string>(entries.Select(e=>e.entryId));
        foreach(var entry in entries)
        {
            if(string.IsNullOrWhiteSpace(entry.sourceNotes))errors.Add(entry.entryId+": missing evidence");
            if(entry.icon==null)errors.Add(entry.entryId+": missing thumbnail");
            if(entry.previewKind!=AlmanacPreviewKind.Image && entry.previewPrefab==null)errors.Add(entry.entryId+": missing model");
            foreach(var id in entry.relatedIds)if(!ids.Contains(id))errors.Add(entry.entryId+": broken link "+id);
        }
        return "Entries: "+entries.Count+"\n"+string.Join("\n",errors);
    }
    public static string GenerateThumbnails(int maxCount=12,int start=0,bool replace=false)
    {
        var catalog=AssetDatabase.LoadAssetAtPath<AlmanacCatalog>(Root+"/AlmanacCatalog.asset");var pending=catalog.LoadEntries().Where(e=>e.previewPrefab!=null && (e.icon==null || (replace && (e.category==AlmanacCategory.Staff || AssetDatabase.GetAssetPath(e.icon).StartsWith(Root+"/Thumbnails/"))))).Skip(start).Take(maxCount).ToArray();
        var go=new GameObject("Almanac thumbnail authoring");var stage=go.AddComponent<AlmanacPreviewStage>();var active=RenderTexture.active;
        try{foreach(var entry in pending)
        {
            var preview=UnityEngine.Object.Instantiate(entry);if(preview.previewKind==AlmanacPreviewKind.Image)preview.previewKind=AlmanacPreviewKind.Model;bool shown=stage.Show(preview);UnityEngine.Object.DestroyImmediate(preview);if(!shown)continue;
            var camera=Resources.FindObjectsOfTypeAll<Camera>().First(c=>c.name=="Preview Camera" && c.targetTexture==stage.Texture);camera.Render();RenderTexture.active=(RenderTexture)stage.Texture;
            var texture=new Texture2D(stage.Texture.width,stage.Texture.height,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);texture.Apply();
            string path=Root+"/Thumbnails/"+entry.entryId+".png";File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
            entry.icon=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(entry.category==AlmanacCategory.Restaurants)entry.previewKind=AlmanacPreviewKind.Image;EditorUtility.SetDirty(entry);
        }}finally{RenderTexture.active=active;stage.Release();UnityEngine.Object.DestroyImmediate(go);}AssetDatabase.SaveAssets();return "Rendered "+pending.Length+" thumbnails";
    }
    private static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;int slash=path.LastIndexOf('/');EnsureFolder(path.Substring(0,slash));AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));}
}
#endif
