if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/NewMenu/NewGameMenu.unity");var report=new System.Collections.Generic.List<string>();
try
{
    var objects=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();var ui=objects.Select(t=>t.GetComponent<DineIn.Appearance.AppearanceCustomizationPanel>()).Single(c=>c!=null);
    var data=new SerializedObject(ui);var camera=(Camera)data.FindProperty("previewCamera").objectReferenceValue;camera.scene=scene;camera.enabled=false;
    var canvas=ui.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
    canvas.GetComponent<UnityEngine.UI.CanvasScaler>().referenceResolution /= 1.15f;
    foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1024,768)})
    {
        var rt=new RenderTexture(size.x,size.y,24);rt.Create();camera.targetTexture=rt;camera.aspect=(float)size.x/size.y;
        try
        {
            Canvas.ForceUpdateCanvases();camera.Render();
            foreach(var t in canvas.GetComponentsInChildren<RectTransform>(true).Where(t=>t.parent==canvas.transform||t.IsChildOf(canvas.transform.Find("WalletUIController"))))
            {
                var corners=new Vector3[4];t.GetWorldCorners(corners);var a=camera.WorldToViewportPoint(corners[0]);var b=camera.WorldToViewportPoint(corners[2]);
                report.Add(size.x+" "+t.name+" active="+t.gameObject.activeInHierarchy+" parent="+t.parent.name+" rect="+a.ToString("F3")+".."+b.ToString("F3")+" scale="+t.localScale+" anchor="+t.anchorMin+".."+t.anchorMax+" pos="+t.anchoredPosition+" size="+t.sizeDelta);
            }
            var previous=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
            try{texture.ReadPixels(new Rect(0,0,size.x,size.y),0,0);texture.Apply();System.IO.File.WriteAllBytes("Temp/CharacterAppearance/normal-menu-"+size.x+".png",texture.EncodeToPNG());}
            finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(texture);}
        }
        finally{camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
    }
}
finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
return report;
