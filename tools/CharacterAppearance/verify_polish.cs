if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
int assertions=0;void Check(bool condition,string message){assertions++;if(!condition)throw new System.Exception(message);}
var catalog=DineIn.Appearance.AppearanceCatalog.Load();
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var posedHeights=new System.Collections.Generic.Dictionary<string,float>();
var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/RoleBased/Lobby2.unity");
try
{
    var sources=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DineIn.Appearance.CharacterAppearance>(true)).Select(a=>a.gameObject).ToList();
    sources.Add(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ManagerMultiplayer.prefab"));sources.Add(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Player.prefab"));
    foreach(var source in sources)
    {
        var root=UnityEngine.Object.Instantiate(source);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);root.SetActive(true);
        try
        {
            var look=root.GetComponent<DineIn.Appearance.CharacterAppearance>();var data=new SerializedObject(look);var animator=(Animator)data.FindProperty("animator").objectReferenceValue;
            var scale=root.transform.localScale;var controller=animator.runtimeAnimatorController;var motion=animator.applyRootMotion;
            var originals=data.FindProperty("originalRenderers");var list=Enumerable.Range(0,originals.arraySize).Select(i=>(Renderer)originals.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();var visibility=list.Select(r=>r.enabled).ToArray();
            foreach(var body in catalog.bodies)
            {
                look.Apply(body.defaults);var visual=animator.transform.Find("Customized Visual");
                Check(Mathf.Abs(visual.lossyScale.y*catalog.modelHeight-catalog.adultHeight)<.001f,"Inconsistent body height: "+source.name);
                Check(root.transform.localScale==scale && animator.runtimeAnimatorController==controller && animator.applyRootMotion==motion,"Changed gameplay root/controller: "+source.name);
                Check(list.All(r=>!r.enabled),"Legacy visual still rendering: "+source.name);
                foreach(var socket in animator.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="headSocket"))Check(socket.GetComponentsInChildren<MeshRenderer>(true).All(r=>!r.enabled),"Duplicate legacy hat: "+source.name);
                var graph=UnityEngine.Playables.PlayableGraph.Create("AdultScaleCheck");animator.enabled=true;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                try
                {
                    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Art/Animations/PlayerAnimation/Idle.anim");
                    var playable=UnityEngine.Animations.AnimationClipPlayable.Create(graph,clip);var output=UnityEngine.Animations.AnimationPlayableOutput.Create(graph,"Pose",animator);
                    UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output,playable);graph.Play();graph.Evaluate(.2f);
                    Check(Mathf.Abs(visual.lossyScale.y*catalog.modelHeight-catalog.adultHeight)<.001f,"Animation overwrote visual scale: "+source.name+" "+visual.lossyScale);
                    float low=float.MaxValue,high=float.MinValue;var mesh=new Mesh();
                    try{foreach(var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>()){renderer.BakeMesh(mesh);foreach(var v in mesh.vertices){float y=renderer.transform.TransformPoint(v).y;low=Mathf.Min(low,y);high=Mathf.Max(high,y);}}}
                    finally{UnityEngine.Object.DestroyImmediate(mesh);}
                    float height=high-low;
                    if(!posedHeights.ContainsKey(body.id))posedHeights[body.id]=height;
                    Check(Mathf.Abs(posedHeights[body.id]-height)<.02f,"Retargeted adult pose has inconsistent height: "+source.name+" "+height+" expected "+posedHeights[body.id]);
                }
                finally{graph.Destroy();}
            }
            look.RestoreOriginal();Check(list.Select((r,i)=>r.enabled==visibility[i]).All(x=>x),"Legacy visibility restoration: "+source.name);
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
    var test=new GameObject("Hat compatibility checks");test.AddComponent<Animator>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(test,scene);
    try
    {
        var look=test.AddComponent<DineIn.Appearance.CharacterAppearance>();
        foreach(var hair in catalog.hairs)
        {
            var recipe=catalog.bodies.First(b=>hair.Fits(b.id)).defaults.Copy();recipe.hairId=hair.id;
            foreach(var hat in catalog.hats)
            {
                recipe.hatId=hat.id;var original=recipe.Copy();look.Apply(recipe);
                Check(recipe.SameAs(original),"Visual fitting mutated saved selection");
                var visualHair=(GameObject)typeof(DineIn.Appearance.CharacterAppearance).GetField("hair",flags).GetValue(look);
                Check(hair.prefab==null?visualHair==null:visualHair!=null && visualHair.activeSelf==!hat.HidesHair(hair.id),"Hair visibility mismatch");
            }
            recipe.hatId="none";look.Apply(recipe);var restored=(GameObject)typeof(DineIn.Appearance.CharacterAppearance).GetField("hair",flags).GetValue(look);
            Check(hair.prefab==null || restored.activeSelf,"Hair failed to restore after removing hat");
        }
    }
    finally{UnityEngine.Object.DestroyImmediate(test);}
}
finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
Check(catalog.hats.All(h=>!h.hidesHair),"Blanket full-hide headwear rule");
var ponytail=DineIn.Appearance.AppearanceCatalog.Find(catalog.hairs,"female-hair-03");
Check(ponytail.prefab.GetComponentsInChildren<MeshRenderer>().Length==1,"Fallback hairstyle is separable; inspect partial hiding instead");
return new{assertions,posedHeights,hairChoices=catalog.hairs.Length,headwearChoices=catalog.hats.Length,scope="Temporary Edit-mode instances only; no saves or network calls"};
