if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
var catalog=DineIn.Appearance.AppearanceCatalog.Load();
var report=new System.Collections.Generic.List<object>();
void Bind(GameObject root,bool player)
{
    if(root==null)return;
    var animator=root.GetComponentInChildren<Animator>(true);
    if(animator==null || animator.avatar==null || !animator.avatar.isHuman)throw new System.Exception(root.name+": no compatible Animator");
    var appearance=root.GetComponent<DineIn.Appearance.CharacterAppearance>();
    if(appearance==null)
    {
        appearance=root.AddComponent<DineIn.Appearance.CharacterAppearance>();
        var data=new SerializedObject(appearance);
        data.FindProperty("catalog").objectReferenceValue=catalog;
        data.FindProperty("animator").objectReferenceValue=animator;
        data.FindProperty("visualParent").objectReferenceValue=animator.transform;
        var renderers=animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if(renderers.Length==0)throw new System.Exception(root.name+": no original skinned visuals");
        var list=data.FindProperty("originalRenderers");list.arraySize=renderers.Length;
        for(int i=0;i<renderers.Length;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=renderers[i];
        var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        float scale=bounds.size.y/Mathf.Max(.001f,animator.transform.lossyScale.y)/2f;
        data.FindProperty("visualScale").vector3Value=Vector3.one*scale;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    var binding=root.GetComponent<DineIn.Appearance.PlayerAppearanceBinding>();
    if(player && binding==null)binding=root.AddComponent<DineIn.Appearance.PlayerAppearanceBinding>();
    if(binding!=null)binding.enabled=player;
    foreach(var legacy in root.GetComponentsInChildren<MonoBehaviour>(true))
        if(legacy is CharacterColorCustomizer || legacy is CharacterHatCustomizer || legacy is PhotonPlayerCustomizationApplier || !player && legacy is ApplyCustomizationOnSpawn)legacy.enabled=false;
    report.Add(new{root=root.name,player,avatar=animator.avatar.name,controller=animator.runtimeAnimatorController?.name});
}
foreach(var path in new[]{"Assets/_Project/Player/Manager.prefab","Assets/Resources/ManagerMultiplayer.prefab","Assets/Resources/Player.prefab"})
{
    var root=PrefabUtility.LoadPrefabContents(path);
    try{Bind(root,true);PrefabUtility.SaveAsPrefabAsset(root,path);}
    finally{PrefabUtility.UnloadPrefabContents(root);}
}
foreach(var path in new[]{"Assets/_Project/Scenes/RoleBased/Lobby1.unity","Assets/_Project/Scenes/RoleBased/Lobby2.unity","Assets/_Project/Scenes/RoleBased/Lobby1 Multiplayer.unity","Assets/_Project/Scenes/TutorialScenes/Lobby1Tutorial.unity","Assets/_Project/Scenes/RoleBased/Lobby2Tutorial.unity","Assets/_Project/Scenes/NewMenu/NewMainMenu.unity"})
{
    var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
    if(!opened && scene.isDirty)throw new System.Exception("Unsaved scene edits: "+path);
    if(opened)scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,UnityEditor.SceneManagement.OpenSceneMode.Additive);
    try
    {
        var objects=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        var staff=new System.Collections.Generic.HashSet<GameObject>();
        foreach(var roles in objects.Select(t=>t.GetComponent<RoleManager>()).Where(x=>x!=null))
        {staff.Add(roles.host);staff.Add(roles.waiter);staff.Add(roles.cashier);staff.Add(roles.busser);}
        foreach(var worker in objects.Select(t=>t.GetComponent<KitchenWorkerBot>()).Where(x=>x!=null))staff.Add(worker.gameObject);
        foreach(var authoring in objects.Select(t=>t.GetComponent<FastFoodLobbyAuthoring>()).Where(x=>x!=null))staff.Add(authoring.SecondCashier);
        foreach(var root in staff)Bind(root,false);
        foreach(var auth in objects.Select(t=>t.GetComponent<PlayFabAuthManager>()).Where(x=>x!=null))
            if(auth.GetComponent<DineIn.Appearance.PlayFabAppearanceAdapter>()==null)auth.gameObject.AddComponent<DineIn.Appearance.PlayFabAppearanceAdapter>();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    }
    finally{if(opened)UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
}
AssetDatabase.SaveAssets();return report;
