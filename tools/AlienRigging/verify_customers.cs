// Edit-mode checks only. Export actual retargeted meshes for render_unity_poses.py.
var names = new[] { "OldAlien", "OrangeAlien", "PurpleAlien", "YellowAlien" };
var clips = new[] { "Idle", "Walking", "sitting" };
var report = new System.Text.StringBuilder();
var poses = new System.Collections.Generic.List<object>();
foreach (var name in names)
{
    var path = "Assets/_Project/Art/Models/Customer/AdditionalAliens/" + name + "/" + name + "Customer.prefab";
    var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
    try
    {
        var animator = root.GetComponentInChildren<Animator>();
        if (!animator.isHuman || !animator.avatar.isValid) throw new System.Exception(name + ": invalid avatar");
        if (root.GetComponentsInChildren<Animator>(true).Length != 1) throw new System.Exception(name + ": duplicate animator");
        if (root.GetComponent<UnityEngine.AI.NavMeshAgent>() == null || root.GetComponent<CapsuleCollider>() == null)
            throw new System.Exception(name + ": missing customer navigation/collider");
        var data = new UnityEditor.SerializedObject(root.GetComponent<CustomerAgent>());
        foreach (var field in new[]{"animator","trayCarryAnchor","trayLeftGrip","trayRightGrip"})
            if (data.FindProperty(field).objectReferenceValue == null) throw new System.Exception(name + ": missing " + field);
        foreach (var bone in new[]{HumanBodyBones.Hips,HumanBodyBones.Head,HumanBodyBones.LeftHand,HumanBodyBones.RightHand,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot})
            if (animator.GetBoneTransform(bone) == null) throw new System.Exception(name + ": missing " + bone);
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (var clipName in clips)
        {
            animator.Rebind();
            var graph = UnityEngine.Playables.PlayableGraph.Create("AlienClipCheck");
            var mesh = new Mesh();
            try
            {
                var clip = animator.runtimeAnimatorController.animationClips.First(c => c.name == clipName);
                var playable = UnityEngine.Animations.AnimationClipPlayable.Create(graph, clip);
                var output = UnityEngine.Animations.AnimationPlayableOutput.Create(graph, "Pose", animator);
                UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output, playable);
                graph.Play();
                graph.Evaluate(clipName == "sitting" ? 1.5f : .4f);
                var renderer = root.GetComponentInChildren<SkinnedMeshRenderer>();
                renderer.BakeMesh(mesh, false);
                if (mesh.vertices.Any(v => !float.IsFinite(v.x) || !float.IsFinite(v.y) || !float.IsFinite(v.z)))
                    throw new System.Exception(name + ": non-finite vertices in " + clipName);
                // BakeMesh includes inherited scale; apply only rotation for the offline preview.
                poses.Add(new { name, clip=clipName,
                    vertices=mesh.vertices.Select(v => renderer.transform.rotation*v).Select(v => new[]{v.x,-v.z,v.y}).ToArray(),
                    uv=mesh.uv.Select(v => new[]{v.x,v.y}).ToArray(), triangles=mesh.triangles });
                report.AppendLine(name + " / " + clipName + ": sampled, valid Humanoid and prefab references, finite deformed vertices");
            }
            finally { graph.Destroy(); UnityEngine.Object.DestroyImmediate(mesh); }
        }
    }
    finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
}
System.IO.File.WriteAllText(System.IO.Path.GetFullPath("../AlienRiggingWork/unity_verification.txt"), report.ToString());
System.IO.File.WriteAllText(System.IO.Path.GetFullPath("../AlienRiggingWork/poses.json"), Newtonsoft.Json.JsonConvert.SerializeObject(poses));
return report.ToString();
