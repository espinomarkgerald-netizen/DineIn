if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
var report=new System.Collections.Generic.List<string>();
var reference=new System.Collections.Generic.Dictionary<string,(Vector3 position,Quaternion rotation)>();int assertions=0;
var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/RoleBased/Lobby2.unity");
try
{
    foreach(var source in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DineIn.Appearance.CharacterAppearance>(true)).Where(a=>a.name=="Waiter"||a.name=="Busser"||a.name=="Manager"))
    foreach(var body in DineIn.Appearance.AppearanceCatalog.Load().bodies.Select(b=>b.id).Prepend("legacy"))
    {
        bool custom=body!="legacy";
        var root=UnityEngine.Object.Instantiate(source.gameObject);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);root.SetActive(true);root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        try
        {
            var look=root.GetComponent<DineIn.Appearance.CharacterAppearance>();var data=new SerializedObject(look);var animator=(Animator)data.FindProperty("animator").objectReferenceValue;animator.enabled=true;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            if(custom)look.Apply(look.Catalog.bodies.Single(b=>b.id==body).defaults);
            foreach(var pose in new[]{"Idle","CarryIdle","WalkingCarry"})
            {
                var graph=UnityEngine.Playables.PlayableGraph.Create("SocketInspection");
                try
                {
                    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Art/Animations/PlayerAnimation/"+pose+".anim");
                    var playable=UnityEngine.Animations.AnimationClipPlayable.Create(graph,clip);var output=UnityEngine.Animations.AnimationPlayableOutput.Create(graph,"Pose",animator);UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output,playable);graph.Play();graph.Evaluate(.35f);
                    if(custom)typeof(DineIn.Appearance.CharacterAppearance).GetMethod("LateUpdate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(look,null);
                    foreach(var p in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="TrayHolder"||t.name=="TrolleyGripPoint"||t.name=="BillHolder"))
                    {
                        var position=root.transform.InverseTransformPoint(p.position);var rotation=Quaternion.Inverse(root.transform.rotation)*p.rotation;
                        string key=source.name+"/"+pose+"/"+p.name;
                        if(!custom)reference[key]=(position,rotation);
                        else
                        {
                            assertions++;
                            if(p.parent!=root.transform)throw new System.Exception("Socket owned by cosmetic geometry: "+key);
                            if(p.name!="BillHolder"&&position.z<=0)throw new System.Exception("Carry socket behind actor: "+key);
                            if(pose=="CarryIdle"&&(Vector3.Distance(reference[key].position,position)>.001f||Quaternion.Angle(reference[key].rotation,rotation)>.1f))throw new System.Exception("Calibrated carry pose mismatch: "+key);
                        }
                        report.Add(source.name+" "+body+" "+pose+" "+p.name+" local="+position.ToString("F3")+" euler="+rotation.eulerAngles);
                    }
                }
                finally{graph.Destroy();}
            }
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
}
finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
return new{assertions,report};
