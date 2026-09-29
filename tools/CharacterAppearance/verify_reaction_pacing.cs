if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
int assertions=0;void Check(bool c,string m){assertions++;if(!c)throw new System.Exception(m);}
var g=new GameObject("Reaction timing test");var selector=g.AddComponent<RestaurantSelector>();
var a=new AnimationClip();var b=new AnimationClip();
try
{
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    void Set(string n,object v)=>typeof(RestaurantSelector).GetField(n,flags).SetValue(selector,v);
    object Get(string n)=>typeof(RestaurantSelector).GetField(n,flags).GetValue(selector);
    void Tick(float t)=>typeof(RestaurantSelector).GetMethod("AdvanceSelectionReaction",flags).Invoke(selector,new object[]{t});
    Set("customizing",true);Set("bodyReactions",new[]{a,b});Set("headReactions",new[]{a,b});
    selector.ReactToSelection(0);Tick(.2f);selector.ReactToSelection(3);Tick(.2f);selector.ReactToSelection(5);
    Check((AnimationClip)Get("lastReaction")==null,"Reacted during rapid browsing");Tick(.5f);Check((int)Get("pendingCategory")==5,"Latest choice lost");Tick(.5f);
    Check((AnimationClip)Get("lastReaction")==a,"Settled choice did not react");Check((int)Get("pendingCategory")==-1,"Reaction queued");
    selector.ReactToSelection(3);Tick(1);Check((AnimationClip)Get("lastReaction")==a,"Cooldown failed");Tick(10);Check((int)Get("pendingCategory")==-1,"Suppressed choice retained");
    selector.ReactToSelection(3);Tick(1);Check((AnimationClip)Get("lastReaction")==b,"Variation immediately repeated");
    Tick(10);selector.ReactToSelection(4);Tick(1);Check((AnimationClip)Get("lastReaction")==b,"Color swatch played full reaction");
    selector.ReactToSelection(0);selector.InterruptPreviewPose();Tick(2);Check((int)Get("pendingCategory")==-1,"Drag left pending reaction");
    selector.ReactToSelection(0);selector.EndCustomization(false);Tick(2);Check((int)Get("pendingCategory")==-1,"Close left pending reaction");
}
finally{UnityEngine.Object.DestroyImmediate(g);UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);}
return new{assertions,scope="Pure Edit-mode timing; no runtime clip playback or saves"};
