// In-place calibration of existing references. Does not create sockets or change task components.
if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
int calibrated=0;
var previewScene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Art/Animations/PlayerAnimation/CarryIdle.anim");
void Pose(Animator animator)
{
    animator.enabled=true;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
    var graph=UnityEngine.Playables.PlayableGraph.Create("CarrySocketCalibration");
    try
    {
        var playable=UnityEngine.Animations.AnimationClipPlayable.Create(graph,clip);var output=UnityEngine.Animations.AnimationPlayableOutput.Create(graph,"Pose",animator);
        UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output,playable);graph.Play();graph.Evaluate(.35f);
    }
    finally{graph.Destroy();}
}
void Calibrate(DineIn.Appearance.CharacterAppearance source)
{
    var sourceData=new SerializedObject(source);var entries=sourceData.FindProperty("attachmentPoints");if(entries.arraySize==0)return;
    var legacy=UnityEngine.Object.Instantiate(source.gameObject);var customized=UnityEngine.Object.Instantiate(source.gameObject);
    try
    {
        foreach(var root in new[]{legacy,customized}){UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,previewScene);root.SetActive(true);root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);}
        var oldData=new SerializedObject(legacy.GetComponent<DineIn.Appearance.CharacterAppearance>());var oldAnimator=(Animator)oldData.FindProperty("animator").objectReferenceValue;
        Pose(oldAnimator);
        var next=customized.GetComponent<DineIn.Appearance.CharacterAppearance>();next.Apply(next.Catalog.defaults);
        var animator=(Animator)new SerializedObject(next).FindProperty("animator").objectReferenceValue;Pose(animator);
        for(int i=0;i<entries.arraySize;i++)
        {
            var entry=entries.GetArrayElementAtIndex(i);var point=(Transform)oldData.FindProperty("attachmentPoints").GetArrayElementAtIndex(i).FindPropertyRelative("point").objectReferenceValue;
            var bone=animator.GetBoneTransform((HumanBodyBones)entry.FindPropertyRelative("bone").intValue);
            if(point==null||bone==null)throw new System.Exception("Missing socket/bone on "+source.name);
            var desiredPosition=customized.transform.TransformPoint(legacy.transform.InverseTransformPoint(point.position));
            var desiredRotation=customized.transform.rotation*Quaternion.Inverse(legacy.transform.rotation)*point.rotation;
            entry.FindPropertyRelative("hasCalibratedPose").boolValue=true;
            entry.FindPropertyRelative("customizedBonePosition").vector3Value=bone.InverseTransformPoint(desiredPosition);
            entry.FindPropertyRelative("customizedBoneRotation").quaternionValue=Quaternion.Inverse(bone.rotation)*desiredRotation;
            calibrated++;
        }
        sourceData.ApplyModifiedPropertiesWithoutUndo();
    }
    finally{UnityEngine.Object.DestroyImmediate(legacy);UnityEngine.Object.DestroyImmediate(customized);}
}
try
{
    foreach(var path in new[]{"Assets/_Project/Player/Manager.prefab","Assets/Resources/ManagerMultiplayer.prefab","Assets/Resources/Player.prefab"})
    {
        var root=PrefabUtility.LoadPrefabContents(path);
        try{Calibrate(root.GetComponent<DineIn.Appearance.CharacterAppearance>());PrefabUtility.SaveAsPrefabAsset(root,path);}
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    foreach(var path in new[]{"Assets/_Project/Scenes/RoleBased/Lobby1.unity","Assets/_Project/Scenes/RoleBased/Lobby2.unity","Assets/_Project/Scenes/RoleBased/Lobby1 Multiplayer.unity","Assets/_Project/Scenes/TutorialScenes/Lobby1Tutorial.unity","Assets/_Project/Scenes/RoleBased/Lobby2Tutorial.unity"})
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
        if(!opened&&scene.isDirty)throw new System.Exception("Save scene edits first: "+path);
        if(opened)scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,UnityEditor.SceneManagement.OpenSceneMode.Additive);
        try
        {
            foreach(var look in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DineIn.Appearance.CharacterAppearance>(true)))Calibrate(look);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }
        finally{if(opened)UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
    }
}
finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(previewScene);}
return new{calibrated,pose=clip.name};
