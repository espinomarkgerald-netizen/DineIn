if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
var report=new System.Collections.Generic.List<object>();
foreach(var path in new[]{"Assets/_Project/Scenes/RoleBased/Lobby1.unity","Assets/_Project/Scenes/RoleBased/Lobby2.unity","Assets/_Project/Scenes/RoleBased/Lobby1 Multiplayer.unity","Assets/_Project/Scenes/TutorialScenes/Lobby1Tutorial.unity","Assets/_Project/Scenes/RoleBased/Lobby2Tutorial.unity","Assets/_Project/Scenes/NewMenu/NewGameMenu.unity","Assets/_Project/Scenes/NewMenu/NewMainMenu.unity"})
{
    var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(path);
    try
    {
        var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)).Where(x=>x!=null).ToArray();
        var appearances=all.OfType<DineIn.Appearance.CharacterAppearance>().ToArray();
        foreach(var appearance in appearances)
        {
            var data=new SerializedObject(appearance);
            if(data.FindProperty("animator").objectReferenceValue==null||data.FindProperty("originalRenderers").arraySize==0)throw new System.Exception("Missing appearance binding in "+path+"/"+appearance.name);
            var anchors=new System.Collections.Generic.List<string>();
            foreach(var component in appearance.GetComponents<MonoBehaviour>().Where(x=>x!=null))
            {
                var serialized=new SerializedObject(component);var iterator=serialized.GetIterator();
                while(iterator.NextVisible(true))if(iterator.propertyType==SerializedPropertyType.ObjectReference&&iterator.objectReferenceValue is Transform anchor&&anchor!=appearance.transform)
                    anchors.Add(component.GetType().Name+"."+iterator.propertyPath+"="+AnimationUtility.CalculateTransformPath(anchor,appearance.transform));
            }
            report.Add(new{scene=scene.name,worker=appearance.name,scale=data.FindProperty("visualScale").vector3Value.ToString(),anchors});
        }
        if(scene.name=="NewGameMenu")
        {
            var panel=all.OfType<DineIn.Appearance.AppearanceCustomizationPanel>().Single();var so=new SerializedObject(panel);
            foreach(var field in new[]{"panel","preview","binding","openButton","applyButton","cancelButton","optionTemplate","options","scroll","status","previewCamera","cameraFollow"})
                if(so.FindProperty(field).objectReferenceValue==null)throw new System.Exception("Missing menu field "+field);
            if(so.FindProperty("categories").arraySize!=7)throw new System.Exception("Expected seven categories");
            if(all.OfType<UnityEngine.EventSystems.EventSystem>().Count()!=1||all.OfType<UnityEngine.UI.GraphicRaycaster>().Count()==0)throw new System.Exception("Menu input components missing");
        }
        if(scene.name=="NewMainMenu"&&all.OfType<DineIn.Appearance.PlayFabAppearanceAdapter>().Count()!=1)throw new System.Exception("Expected one auth appearance adapter");
    }
    finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
}
return report;
