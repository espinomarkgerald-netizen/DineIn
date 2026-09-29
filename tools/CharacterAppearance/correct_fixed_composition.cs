if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/_Project/Scenes/NewMenu/NewGameMenu.unity");
if(!scene.IsValid()||!scene.isLoaded||scene.isDirty)throw new System.Exception("Open and save NewGameMenu first");
var ui=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DineIn.Appearance.AppearanceCustomizationPanel>(true)).Single();
var data=new SerializedObject(ui);var panel=((GameObject)data.FindProperty("panel").objectReferenceValue).transform;
void Fill(Transform t,Vector2 min,Vector2 max){var r=(RectTransform)t;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
Fill(panel.Find("Preview Rotation Area"),new Vector2(.075f,.17f),new Vector2(.465f,.91f));
Fill(panel.Find("Preview Hint"),new Vector2(.075f,.04f),new Vector2(.465f,.10f));
var hint=panel.Find("Preview Hint");var hintBackdrop=panel.Find("Preview Hint Backdrop");
if(hintBackdrop==null){hintBackdrop=new GameObject("Preview Hint Backdrop",typeof(RectTransform),typeof(UnityEngine.UI.Image)).transform;hintBackdrop.SetParent(panel,false);}
Fill(hintBackdrop,new Vector2(.16f,.04f),new Vector2(.38f,.10f));
hintBackdrop.GetComponent<UnityEngine.UI.Image>().color=new Color(.035f,.16f,.22f,.92f);
hintBackdrop.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;hintBackdrop.SetSiblingIndex(hint.GetSiblingIndex());
var right=panel.Find("Options Panel");Fill(right,new Vector2(.49f,.05f),new Vector2(.975f,.955f));
Fill(right.Find("Title"),new Vector2(.06f,.91f),new Vector2(.94f,.97f));
var tabs=data.FindProperty("categories");for(int i=0;i<tabs.arraySize;i++){var r=(RectTransform)((UnityEngine.UI.Button)tabs.GetArrayElementAtIndex(i).objectReferenceValue).transform;var min=r.anchorMin;var max=r.anchorMax;min.y=i<4?.84f:.775f;max.y=i<4?.90f:.83f;Fill(r,min,max);}
var scroll=(UnityEngine.UI.ScrollRect)data.FindProperty("scroll").objectReferenceValue;Fill(scroll.transform,new Vector2(.06f,.18f),new Vector2(.94f,.76f));
foreach(string name in new[]{"Category Divider","Footer Divider"}){var t=right.Find(name);if(t!=null)t.gameObject.SetActive(false);}
Fill(right.Find("Status"),new Vector2(.06f,.13f),new Vector2(.94f,.171f));
Fill(right.Find("Cancel"),new Vector2(.06f,.035f),new Vector2(.475f,.12f));Fill(right.Find("Apply"),new Vector2(.525f,.035f),new Vector2(.94f,.12f));
var layout=((RectTransform)data.FindProperty("options").objectReferenceValue).GetComponent<DineIn.Appearance.CosmeticOptionLayout>();
var ld=new SerializedObject(layout);ld.FindProperty("cardSize").vector2Value=new Vector2(134,176);ld.FindProperty("swatchSize").vector2Value=new Vector2(52,58);ld.FindProperty("spacing").vector2Value=new Vector2(12,14);ld.FindProperty("paletteColumns").intValue=4;ld.ApplyModifiedPropertiesWithoutUndo();
var template=(UnityEngine.UI.Button)data.FindProperty("optionTemplate").objectReferenceValue;
// One existing outer depth frame; the thumbnail and name strip provide the interior.
var surface=template.transform.Find("Preview Surface");if(surface!=null)surface.gameObject.SetActive(false);
template.GetComponent<UnityEngine.UI.Image>().color=new Color(.95f,.97f,.98f);
data.FindProperty("categoryPresentation").GetArrayElementAtIndex(3).FindPropertyRelative("thumbnailMargin").floatValue=1.04f;
data.ApplyModifiedPropertiesWithoutUndo();
var selector=(RestaurantSelector)data.FindProperty("restaurantSelector").objectReferenceValue;var sd=new SerializedObject(selector);
foreach(string n in new[]{"bodyReactions","headReactions","faceReactions"})sd.FindProperty(n).arraySize=0;
foreach(string n in new[]{"bodyReaction","headReaction","faceReaction","colorReaction"})sd.FindProperty(n).objectReferenceValue=null;
sd.ApplyModifiedPropertiesWithoutUndo();
var camera=(Camera)sd.FindProperty("menuCamera").objectReferenceValue;var follow=camera.GetComponent<CameraFollow>();
var fd=new SerializedObject(follow);var reference=fd.FindProperty("compositionReference");
if(reference.objectReferenceValue==null)
{
    // Author the camera once relative to the existing first world destination. No UI reference is used.
    float aspect=camera.aspect;camera.aspect=16f/9f;
    var point=selector.travelPoints[0];var v=camera.WorldToViewportPoint(point.position);
    camera.transform.position+=camera.transform.right*((v.x-.5f)*2*camera.orthographicSize*camera.aspect)+camera.transform.up*((v.y-.36f)*2*camera.orthographicSize);
    camera.aspect=aspect;selector.character.position=point.position;
    var direction=-camera.transform.forward;direction.y=0;selector.character.rotation=Quaternion.LookRotation(direction)*Quaternion.Euler(0,sd.FindProperty("facingOffset").floatValue,0);
    reference.objectReferenceValue=point;fd.ApplyModifiedPropertiesWithoutUndo();
}
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return "Fixed two-column cards; separate authored normal camera composition; no placeholder inspection clips.";
