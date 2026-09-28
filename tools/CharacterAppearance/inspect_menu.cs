var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/NewMenu/NewGameMenu.unity");
try
{
    var objects=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
    return new {
        roots=scene.GetRootGameObjects().Select(g=>g.name).ToArray(),
        buttons=objects.Select(t=>t.GetComponent<UnityEngine.UI.Button>()).Where(b=>b!=null).Select(b=>new{b.name,parent=b.transform.parent.name,text=b.GetComponentInChildren<TMPro.TMP_Text>(true)?.text, sprite=AssetDatabase.GetAssetPath(b.GetComponent<UnityEngine.UI.Image>()?.sprite),font=AssetDatabase.GetAssetPath(b.GetComponentInChildren<TMPro.TMP_Text>(true)?.font),rect=((RectTransform)b.transform).rect.ToString()}).ToArray(),
        animators=objects.Select(t=>t.GetComponent<Animator>()).Where(a=>a!=null).Select(a=>new{a.name,path=AnimationUtility.CalculateTransformPath(a.transform,null),pos=a.transform.position.ToString(),rot=a.transform.localEulerAngles.ToString(),scale=a.transform.localScale.ToString(),controller=AssetDatabase.GetAssetPath(a.runtimeAnimatorController)}).ToArray(),
        scripts=objects.SelectMany(t=>t.GetComponents<MonoBehaviour>()).Where(b=>b!=null).Select(b=>b.GetType().Name).Distinct().ToArray()
    };
}
finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
