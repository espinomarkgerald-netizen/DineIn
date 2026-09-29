// Scene presentation only. Run in Edit Mode; preserves all backend and gameplay bindings.
if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
const string path="Assets/_Project/Scenes/NewMenu/NewGameMenu.unity";
var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
if(!scene.IsValid()||!scene.isLoaded)throw new System.Exception("Open NewGameMenu first");
if(scene.isDirty)throw new System.Exception("Save scene edits first");
var ui=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DineIn.Appearance.AppearanceCustomizationPanel>(true)).Single();
var data=new SerializedObject(ui);var panel=((GameObject)data.FindProperty("panel").objectReferenceValue).transform;
if(((RectTransform)data.FindProperty("options").objectReferenceValue).GetComponent<DineIn.Appearance.CosmeticOptionLayout>()!=null && typeof(DineIn.Appearance.CosmeticOptionLayout).GetField("fewOptionSize",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)==null)
    throw new System.Exception("Superseded by correct_fixed_composition.cs; do not restore count-fitted cards.");
void Fill(Transform t,Vector2 min,Vector2 max){var r=(RectTransform)t;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
RectTransform Child(Transform parent,string name){var t=parent.Find(name);if(t!=null)return (RectTransform)t;var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);return (RectTransform)g.transform;}
UnityEngine.UI.Image Image(Transform parent,string name,Vector2 min,Vector2 max,Color color){var r=Child(parent,name);Fill(r,min,max);var i=r.GetComponent<UnityEngine.UI.Image>();if(i==null)i=r.gameObject.AddComponent<UnityEngine.UI.Image>();i.color=color;i.raycastTarget=false;return i;}
var navy=new Color(.04f,.24f,.31f);var blue=new Color(.12f,.61f,.78f);var green=new Color(.05f,.72f,.45f);
var region=panel.Find("Preview Rotation Area");Fill(region,new Vector2(.075f,.15f),new Vector2(.51f,.91f));
Fill(panel.Find("Preview Hint"),new Vector2(.075f,.07f),new Vector2(.51f,.13f));
data.FindProperty("previewRegion").objectReferenceValue=region;
data.FindProperty("framingSeconds").floatValue=.7f;data.FindProperty("previewScreenHeight").floatValue=.63f;
var right=panel.Find("Options Panel");Fill(right,new Vector2(.535f,.055f),new Vector2(.975f,.955f));
Fill(right.Find("Title"),new Vector2(.06f,.905f),new Vector2(.94f,.97f));right.Find("Title").GetComponent<TMPro.TMP_Text>().text="CUSTOMIZE";
var tabs=data.FindProperty("categories");for(int i=0;i<tabs.arraySize;i++){var r=(RectTransform)((UnityEngine.UI.Button)tabs.GetArrayElementAtIndex(i).objectReferenceValue).transform;var min=r.anchorMin;var max=r.anchorMax;min.y=i<4?.825f:.755f;max.y=i<4?.89f:.815f;Fill(r,min,max);}
var scroll=(UnityEngine.UI.ScrollRect)data.FindProperty("scroll").objectReferenceValue;
Fill(scroll.transform,new Vector2(.065f,.24f),new Vector2(.935f,.73f));
Image(right,"Category Divider",new Vector2(.07f,.738f),new Vector2(.93f,.742f),new Color(.55f,.69f,.75f));
Image(right,"Footer Divider",new Vector2(.07f,.232f),new Vector2(.93f,.236f),new Color(.55f,.69f,.75f));
var status=(TMPro.TMP_Text)data.FindProperty("status").objectReferenceValue;
Fill(status.transform,new Vector2(.06f,.177f),new Vector2(.94f,.229f));status.text="Changes save when you Apply.";status.color=navy;status.fontSize=12;
var options=(RectTransform)data.FindProperty("options").objectReferenceValue;
var old=options.GetComponent<UnityEngine.UI.GridLayoutGroup>();if(old!=null)UnityEngine.Object.DestroyImmediate(old);
var layout=options.GetComponent<DineIn.Appearance.CosmeticOptionLayout>();if(layout==null)layout=options.gameObject.AddComponent<DineIn.Appearance.CosmeticOptionLayout>();
var ld=new SerializedObject(layout);ld.FindProperty("viewport").objectReferenceValue=scroll.viewport;
ld.FindProperty("cardSize").vector2Value=new Vector2(110,120);ld.FindProperty("fewOptionSize").vector2Value=new Vector2(146,184);ld.FindProperty("swatchSize").vector2Value=new Vector2(52,58);ld.ApplyModifiedPropertiesWithoutUndo();
layout.padding=new RectOffset(4,4,6,6);
var template=(UnityEngine.UI.Button)data.FindProperty("optionTemplate").objectReferenceValue;
var frame=template.GetComponent<UnityEngine.UI.Image>();frame.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/MainMenu/NewDesign/UI Elements/PNG/Grey/Double/button_square_depth_flat.png");frame.type=UnityEngine.UI.Image.Type.Sliced;frame.pixelsPerUnitMultiplier=1;
var colors=template.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.9f,.97f,1);colors.pressedColor=new Color(.76f,.89f,.94f);colors.fadeDuration=.1f;template.colors=colors;
var surface=Image(template.transform,"Preview Surface",new Vector2(.04f,.07f),new Vector2(.96f,.97f),new Color(.92f,.95f,.96f));surface.transform.SetAsFirstSibling();
var band=Image(template.transform,"Name Band",new Vector2(.045f,.075f),new Vector2(.955f,.31f),new Color(.83f,.9f,.92f));band.transform.SetSiblingIndex(1);
var icon=template.transform.Find("Icon");Fill(icon,new Vector2(.055f,.32f),new Vector2(.945f,.95f));icon.GetComponent<UnityEngine.UI.Image>().preserveAspect=true;
var label=template.transform.Find("Label").GetComponent<TMPro.TMP_Text>();Fill(label.transform,new Vector2(.065f,.08f),new Vector2(.935f,.31f));label.fontSize=11;label.margin=new Vector4(1,0,1,0);label.color=navy;label.transform.SetAsLastSibling();
// Keep the name strip readable as the card adapts, rather than scaling text space with its height.
void BottomStrip(RectTransform r,float bottom,float top){r.anchorMin=Vector2.zero;r.anchorMax=new Vector2(1,0);r.offsetMin=new Vector2(6,bottom);r.offsetMax=new Vector2(-6,top);}
BottomStrip(band.rectTransform,8,45);BottomStrip(label.rectTransform,9,44);
var iconRect=(RectTransform)icon;iconRect.anchorMin=Vector2.zero;iconRect.anchorMax=Vector2.one;iconRect.offsetMin=new Vector2(6,46);iconRect.offsetMax=new Vector2(-6,-6);
icon.GetComponent<UnityEngine.UI.Image>().type=UnityEngine.UI.Image.Type.Simple;
var border=Child(template.transform,"Selection Border");Fill(border,Vector2.zero,Vector2.one);
foreach(var edge in new[]{("Top",new Vector2(.02f,.975f),new Vector2(.98f,.99f)),("Bottom",new Vector2(.02f,.055f),new Vector2(.98f,.07f)),("Left",new Vector2(.02f,.055f),new Vector2(.035f,.99f)),("Right",new Vector2(.965f,.055f),new Vector2(.98f,.99f))})Image(border,edge.Item1,edge.Item2,edge.Item3,green);
border.gameObject.SetActive(false);
var check=(RectTransform)template.transform.Find("Selected");check.anchorMin=check.anchorMax=new Vector2(.87f,.37f);check.pivot=new Vector2(.5f,.5f);check.anchoredPosition=Vector2.zero;check.sizeDelta=new Vector2(19,19);check.SetAsLastSibling();
check.anchorMin=check.anchorMax=new Vector2(1,0);check.anchoredPosition=new Vector2(-15,53);
template.gameObject.SetActive(false);
var selector=new SerializedObject(data.FindProperty("restaurantSelector").objectReferenceValue);selector.FindProperty("selectionSettleSeconds").floatValue=.9f;selector.FindProperty("inspectionCooldownSeconds").floatValue=9;selector.FindProperty("colorReaction").objectReferenceValue=null;selector.ApplyModifiedPropertiesWithoutUndo();
data.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return "Authored preview region, centered adaptive content, lighter cards and fixed footer in existing scene.";
