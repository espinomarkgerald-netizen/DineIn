// Restore the exact authored data reference removed in eef8d2f8. No bake or agent changes.
if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
const string path="Assets/_Project/Scenes/RoleBased/Lobby1.unity";
var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
if(!opened&&scene.isDirty)throw new System.Exception("Save Lobby1 edits before applying this repair.");
if(opened)scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,UnityEditor.SceneManagement.OpenSceneMode.Additive);
try
{
    var surface=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Unity.AI.Navigation.NavMeshSurface>(true)).Single(s=>s.name=="NavMesh");
    var data=AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>("Assets/_Project/Scenes/RoleBased/Lobby1/NavMesh-NavMesh.asset");
    if(data==null||!surface.isActiveAndEnabled)throw new System.Exception("Authored navigation data/surface mismatch");
    if(surface.navMeshData!=null&&surface.navMeshData!=data)throw new System.Exception("Surface has a different authored data assignment; inspect first");
    Undo.RecordObject(surface,"Restore Lobby1 navigation reference");surface.navMeshData=data;surface.AddData();
    EditorUtility.SetDirty(surface);UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    return new{surface=surface.name,data=AssetDatabase.GetAssetPath(data),agentType=surface.agentTypeID};
}
finally{if(opened)UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
