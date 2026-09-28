// Temporary Edit-mode instances only. No runtime AI, movement, saves, or network operations.
if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
var results=new System.Collections.Generic.List<object>();var catalog=DineIn.Appearance.AppearanceCatalog.Load();
var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/RoleBased/Lobby2.unity");
try
{
    var staff=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DineIn.Appearance.CharacterAppearance>(true)).Where(a=>a.GetComponent<DineIn.Appearance.PlayerAppearanceBinding>()==null);
    var sources=new System.Collections.Generic.List<GameObject>(staff.Select(s=>s.gameObject));
    foreach(var path in new[]{"Assets/_Project/Player/Manager.prefab","Assets/Resources/ManagerMultiplayer.prefab","Assets/Resources/Player.prefab"})sources.Add(AssetDatabase.LoadAssetAtPath<GameObject>(path));
    foreach(var source in sources)
    {
        var root=UnityEngine.Object.Instantiate(source);root.hideFlags=HideFlags.HideAndDontSave;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);root.SetActive(true);
        try
        {
            var look=root.GetComponent<DineIn.Appearance.CharacterAppearance>();var data=new SerializedObject(look);
            var animator=(Animator)data.FindProperty("animator").objectReferenceValue;animator.enabled=true;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var avatar=animator.avatar;var controller=animator.runtimeAnimatorController;bool rootMotion=animator.applyRootMotion;
            var scale=root.transform.localScale;var rotation=root.transform.localRotation;
            var preserved=root.GetComponentsInChildren<Component>(true).Where(c=>c!=null).ToArray();
            var anchorArray=data.FindProperty("attachmentPoints");
            var anchors=new System.Collections.Generic.List<(Transform point,Transform parent,Vector3 pos,Quaternion rot,Vector3 scale)>();
            for(int i=0;i<anchorArray.arraySize;i++){var point=(Transform)anchorArray.GetArrayElementAtIndex(i).FindPropertyRelative("point").objectReferenceValue;anchors.Add((point,point.parent,point.localPosition,point.localRotation,point.localScale));}
            foreach(var body in catalog.bodies)
            {
                look.Apply(body.defaults);
                if(animator.runtimeAnimatorController!=controller||animator.applyRootMotion!=rootMotion||root.transform.localScale!=scale||root.transform.localRotation!=rotation||preserved.Any(c=>c==null))throw new System.Exception("Changed gameplay structure: "+source.name);
                var visual=animator.transform.Find("Customized Visual");var head=animator.GetBoneTransform(HumanBodyBones.Head);
                if(visual==null||head==null||!head.IsChildOf(visual))throw new System.Exception("Avatar failed to bind derived skeleton");
                foreach(var anchor in anchors)if(!anchor.point.IsChildOf(visual))throw new System.Exception("Task anchor left under old skeleton");
                foreach(var pose in new[]{"Idle","Walking","Running","WalkingCarry","CarryIdle"})
                {
                    var graph=UnityEngine.Playables.PlayableGraph.Create("AppearanceCompatibility");
                    try
                    {
                        string path="Assets/_Project/Art/Animations/PlayerAnimation/"+pose+(pose=="Walking"?".fbx":".anim");
                        var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(x=>!x.name.StartsWith("__preview__"));
                        var playable=UnityEngine.Animations.AnimationClipPlayable.Create(graph,clip);var output=UnityEngine.Animations.AnimationPlayableOutput.Create(graph,"Pose",animator);
                        UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output,playable);graph.Play();graph.Evaluate(.35f);
                        foreach(var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {
                            var mesh=new Mesh();try{renderer.BakeMesh(mesh);if(mesh.vertices.Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)))throw new System.Exception("Nonfinite skin pose");}finally{UnityEngine.Object.DestroyImmediate(mesh);}
                        }
                    }
                    finally{graph.Destroy();}
                }
            }
            look.RestoreOriginal();
            if(animator.avatar!=avatar)throw new System.Exception("Original avatar not restored");
            foreach(var a in anchors)if(a.point.parent!=a.parent||a.point.localPosition!=a.pos||a.point.localRotation!=a.rot||a.point.localScale!=a.scale)throw new System.Exception("Original attachment transform not restored");
            results.Add(new{source=source.name,bodies=2,posesPerBody=5,anchors=anchors.Count,gameplayReferencesPreserved=true});
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
}
finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
return results;
