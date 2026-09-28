// Edit-mode only: bake the actual Humanoid-retargeted vertices for offline inspection.
if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.Exception("Edit Mode only");
var poses = new System.Collections.Generic.List<object>();
var report = new System.Collections.Generic.List<object>();
foreach (var body in new[]{"Male", "Female"})
{
    var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Player/Assets/Appearance/Models/"+body+".fbx"));
    root.hideFlags=HideFlags.HideAndDontSave;
    try
    {
        var animator=root.GetComponent<Animator>();
        animator.applyRootMotion=false;
        animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        var head=animator.GetBoneTransform(HumanBodyBones.Head);
        var hair=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Player/Assets/Appearance/Models/"+(body=="Male"?"M":"F")+"_Hair_01.fbx"));
        var bindRotation=hair.transform.rotation;
        hair.transform.SetParent(head,false);
        hair.transform.localPosition=Vector3.zero;
        hair.transform.localRotation=Quaternion.Inverse(head.rotation)*bindRotation;
        foreach(var clipName in new[]{"Idle", "Walking", "Running", "WalkingCarry", "CarryIdle"})
        {
            var path="Assets/_Project/Art/Animations/PlayerAnimation/"+clipName+(clipName=="Walking"?".fbx":".anim");
            var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
            animator.Rebind();
            var graph=UnityEngine.Playables.PlayableGraph.Create("CharacterAssetVerification");
            try
            {
                var playable=UnityEngine.Animations.AnimationClipPlayable.Create(graph,clip);
                var output=UnityEngine.Animations.AnimationPlayableOutput.Create(graph,"Pose",animator);
                UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output,playable);
                graph.Play(); graph.Evaluate(.35f);
                foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh=new Mesh();
                    try
                    {
                        renderer.BakeMesh(mesh);
                        var vertices=mesh.vertices.Select(v=>renderer.transform.TransformPoint(v)-root.transform.position).ToArray();
                        if(vertices.Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z))) throw new System.Exception(body+" non-finite pose");
                        poses.Add(new {body,clip=clipName,part=renderer.name,vertices=vertices.Select(v=>new[]{v.x,-v.z,v.y}).ToArray(),uv=mesh.uv.Select(v=>new[]{v.x,v.y}).ToArray(),triangles=mesh.triangles});
                    }
                    finally {UnityEngine.Object.DestroyImmediate(mesh);}
                }
                foreach(var filter in hair.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh=filter.sharedMesh;
                    poses.Add(new {body,clip=clipName,part="Hair",vertices=mesh.vertices.Select(v=>filter.transform.TransformPoint(v)-root.transform.position).Select(v=>new[]{v.x,-v.z,v.y}).ToArray(),uv=mesh.uv.Select(v=>new[]{v.x,v.y}).ToArray(),triangles=mesh.triangles});
                }
                report.Add(new {body,clip=clipName,avatarValid=animator.avatar.isValid,human=animator.isHuman});
            }
            finally {graph.Destroy();}
        }
    }
    finally {UnityEngine.Object.DestroyImmediate(root);}
}
System.IO.File.WriteAllText("Temp/CharacterAppearance/poses.json",Newtonsoft.Json.JsonConvert.SerializeObject(poses));
return report;
