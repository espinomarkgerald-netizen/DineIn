var results=new System.Collections.Generic.List<object>();
results.Add(new {playing=EditorApplication.isPlaying,changing=EditorApplication.isPlayingOrWillChangePlaymode});
if(EditorApplication.isPlayingOrWillChangePlaymode)return results;
string Path(Transform t)=>t.parent==null?t.name:Path(t.parent)+"/"+t.name;
foreach(var path in new[]{"Assets/_Project/Scenes/RoleBased/Lobby2.unity","Assets/_Project/Scenes/NewMenu/NewGameMenu.unity"})
{
    var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(path);
    try
    {
        foreach(var look in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DineIn.Appearance.CharacterAppearance>(true)))
        {
            var data=new SerializedObject(look);var a=(Animator)data.FindProperty("animator").objectReferenceValue;
            var originals=data.FindProperty("originalRenderers");
            var listed=Enumerable.Range(0,originals.arraySize).Select(i=>originals.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
            results.Add(new{scene=path,root=Path(look.transform),rootScale=look.transform.lossyScale.ToString(),animatorScale=a.transform.lossyScale.ToString(),visualScale=data.FindProperty("visualScale").vector3Value.ToString(),renderers=a.GetComponentsInChildren<Renderer>(true).Select(r=>new{path=Path(r.transform),type=r.GetType().Name,enabled=r.enabled,listed=listed.Contains(r),height=r.bounds.size.y}).ToArray()});
        }
        if(path.Contains("NewGameMenu"))results.Add(new{scalers=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UnityEngine.UI.CanvasScaler>(true)).Select(s=>new{s.name,mode=s.uiScaleMode.ToString(),reference=s.referenceResolution.ToString()}).ToArray()});
    }
    finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
}
results.Add(new{clips=AssetDatabase.FindAssets("t:AnimationClip",new[]{"Assets/_Project","Assets/Resources"}).Select(AssetDatabase.GUIDToAssetPath).Distinct().SelectMany(p=>AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).Select(c=>new{path=p,c.name,c.length,c.humanMotion})).ToArray()});
return results;
