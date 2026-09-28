if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
int total=0;
void Bind(DineIn.Appearance.CharacterAppearance look)
{
    var data=new SerializedObject(look);var array=data.FindProperty("attachmentPoints");if(array.arraySize>0)return;
    var animator=(Animator)data.FindProperty("animator").objectReferenceValue;
    var bones=new System.Collections.Generic.Dictionary<Transform,HumanBodyBones>();
    for(int i=0;i<(int)HumanBodyBones.LastBone;i++){var bone=animator.GetBoneTransform((HumanBodyBones)i);if(bone!=null)bones[bone]=(HumanBodyBones)i;}
    var points=new System.Collections.Generic.Dictionary<Transform,HumanBodyBones>();
    foreach(var component in look.GetComponents<MonoBehaviour>().Where(x=>x is WaiterHands||x is BusserHands))
    {
        var source=new SerializedObject(component);var iterator=source.GetIterator();
        while(iterator.NextVisible(true))if(iterator.propertyType==SerializedPropertyType.ObjectReference&&iterator.objectReferenceValue is Transform point&&point!=look.transform)
        {
            for(var parent=point.parent;parent!=null&&parent!=look.transform;parent=parent.parent)
                if(bones.TryGetValue(parent,out var bone)){points[point]=bone;break;}
        }
    }
    array.arraySize=points.Count;int index=0;
    foreach(var pair in points){var item=array.GetArrayElementAtIndex(index++);item.FindPropertyRelative("point").objectReferenceValue=pair.Key;item.FindPropertyRelative("bone").intValue=(int)pair.Value;}
    data.ApplyModifiedPropertiesWithoutUndo();total+=points.Count;
}
foreach(var path in new[]{"Assets/_Project/Player/Manager.prefab","Assets/Resources/ManagerMultiplayer.prefab","Assets/Resources/Player.prefab"})
{
    var root=PrefabUtility.LoadPrefabContents(path);try{Bind(root.GetComponent<DineIn.Appearance.CharacterAppearance>());PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
}
foreach(var path in new[]{"Assets/_Project/Scenes/RoleBased/Lobby1.unity","Assets/_Project/Scenes/RoleBased/Lobby2.unity","Assets/_Project/Scenes/RoleBased/Lobby1 Multiplayer.unity","Assets/_Project/Scenes/TutorialScenes/Lobby1Tutorial.unity","Assets/_Project/Scenes/RoleBased/Lobby2Tutorial.unity"})
{
    var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
    if(!opened&&scene.isDirty)throw new System.Exception("Save scene edits first: "+path);
    if(opened)scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,UnityEditor.SceneManagement.OpenSceneMode.Additive);
    try{foreach(var look in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DineIn.Appearance.CharacterAppearance>(true)))Bind(look);UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);}
    finally{if(opened)UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
}
return new{authoredExistingTaskAnchors=total};
