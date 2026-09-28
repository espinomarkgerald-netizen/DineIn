if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
var report=new System.Collections.Generic.List<object>();
foreach(var path in new[]{"Assets/_Project/Scenes/RoleBased/Lobby1.unity","Assets/_Project/Scenes/RoleBased/Lobby2.unity"})
{
    var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(path);
    var handles=new System.Collections.Generic.List<UnityEngine.AI.NavMeshDataInstance>();
    try
    {
        var roots=scene.GetRootGameObjects();var surfaces=roots.SelectMany(g=>g.GetComponentsInChildren<Unity.AI.Navigation.NavMeshSurface>(true)).Where(s=>s.isActiveAndEnabled&&s.navMeshData!=null).ToArray();
        foreach(var surface in surfaces)handles.Add(UnityEngine.AI.NavMesh.AddNavMeshData(surface.navMeshData,surface.transform.position,surface.transform.rotation));
        var manager=roots.SelectMany(g=>g.GetComponentsInChildren<ManagerPlayer>(true)).Single();var agent=manager.GetComponent<UnityEngine.AI.NavMeshAgent>();
        var filter=new UnityEngine.AI.NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
        bool sourceFound=UnityEngine.AI.NavMesh.SamplePosition(manager.transform.position,out var start,3,filter);
        if(!sourceFound)throw new System.Exception(path+" Manager spawn has no authored walkable point within the existing 3-unit search");
        var targets=roots.SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)).OfType<IInteractable>().Where(t=>t.StandPoint!=null).Take(8).ToArray();
        var checks=new System.Collections.Generic.List<string>();
        foreach(var target in targets)
        {
            var position=target.StandPoint.position;position.y=start.position.y;var navPath=new UnityEngine.AI.NavMeshPath();
            bool sampled=UnityEngine.AI.NavMesh.SamplePosition(position,out var end,3,filter);
            bool complete=sampled&&UnityEngine.AI.NavMesh.CalculatePath(start.position,end.position,filter,navPath)&&navPath.status==UnityEngine.AI.NavMeshPathStatus.PathComplete;
            checks.Add(target.GetType().Name+"="+complete);
        }
        if(!checks.Any(c=>c.EndsWith("=True")))throw new System.Exception("No sampled task point connected to Manager in "+path);
        report.Add(new{path,surfaces=surfaces.Select(s=>AssetDatabase.GetAssetPath(s.navMeshData)).ToArray(),spawn=manager.transform.position.ToString(),sample=start.position.ToString(),checks});
    }
    finally{foreach(var handle in handles)handle.Remove();UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
}
return report;
