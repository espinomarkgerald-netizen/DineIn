if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
var c=DineIn.Appearance.AppearanceCatalog.Load();
foreach(var option in c.bodies.Cast<DineIn.Appearance.AppearanceCatalog.Option>().Concat(c.outfits).Concat(c.faces).Concat(c.hairs).Concat(c.hats))option.thumbnail=null;
EditorUtility.SetDirty(c);
var path="Assets/_Project/Scenes/NewMenu/NewGameMenu.unity";
var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
if(!opened&&scene.isDirty)throw new System.Exception("Save current menu edits first");
if(opened)scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,UnityEditor.SceneManagement.OpenSceneMode.Additive);
try
{
    var panel=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DineIn.Appearance.AppearanceCustomizationPanel>(true)).Single();
    var so=new SerializedObject(panel);
    var content=(RectTransform)so.FindProperty("options").objectReferenceValue;
    var grid=content.GetComponent<UnityEngine.UI.GridLayoutGroup>();grid.cellSize=new Vector2(grid.cellSize.x,142);
    var template=(UnityEngine.UI.Button)so.FindProperty("optionTemplate").objectReferenceValue;
    template.transform.Find("Label").GetComponent<TMPro.TMP_Text>().fontSize=13;
    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
}
finally{if(opened)UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
AssetDatabase.SaveAssets();return "Responsive option cell height and thumbnail invalidation complete";
