#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class AssemblerGuidanceCheck
{
    [MenuItem("Dine In/Fast Food/Validate Center Guidance")]
    public static void Validate()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Use Edit Mode.");
        var source=UnityEngine.Object.FindObjectsByType<FastFoodCookingToast>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();
        var root=new GameObject("Guidance check",typeof(RectTransform));
        root.SetActive(false);
        var copy=UnityEngine.Object.Instantiate(source,root.transform);
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var elapsed=typeof(FastFoodCookingToast).GetField("elapsed",flags);
        var update=typeof(FastFoodCookingToast).GetMethod("Advance",flags);
        void Check(bool pass,string message){if(!pass)throw new InvalidOperationException("[Guidance check] "+message);}
        try
        {
            foreach(var size in new[]{new Vector2(1920,1080),new Vector2(1920,1200),new Vector2(2340,1080)})
            foreach(var message in new[]{"Try the highlighted area","Next batch soon"})
            {
                ((RectTransform)root.transform).sizeDelta=size;
                copy.Clear();copy.Show(message,source.icon.sprite,message=="Next batch soon"?2:1);
                Check(copy.motion.anchorMin==new Vector2(.5f,.5f) && copy.motion.anchorMax==copy.motion.anchorMin,"Center anchors.");
                Check(copy.motion.anchoredPosition==Vector2.zero && copy.motion.localScale.x<1,"Initial position/pop.");
                copy.Show(message,source.icon.sprite,message=="Next batch soon"?2:1);
                var queue=typeof(FastFoodCookingToast).GetField("pending",flags).GetValue(copy);
                Check((int)queue.GetType().GetProperty("Count").GetValue(queue)==0,"Duplicate queued.");
                elapsed.SetValue(copy,.5f);update.Invoke(copy,new object[]{0f});
                Check(copy.group.alpha==1 && Mathf.Abs(copy.motion.localScale.x-1)<.001f,"Hold.");
                elapsed.SetValue(copy,copy.popupPopDuration+copy.popupHoldDuration+copy.popupFadeDuration*.5f);
                update.Invoke(copy,new object[]{0f});
                Check(copy.group.alpha>0 && copy.group.alpha<1,"Fade.");
                if(!LevelOneUIAccessibility.ReducedMotion)Check(copy.motion.anchoredPosition.y>0 && copy.motion.anchoredPosition.y<=copy.popupUpwardTravel,"Upward travel.");
                elapsed.SetValue(copy,10f);update.Invoke(copy,new object[]{0f});
                Check(copy.group.alpha==0,"Hidden.");
            }
            Debug.Log("[Guidance check] PASS: center anchors at 16:9, 16:10 and Android landscape; soft pop, hold, upward fade, hide and duplicate suppression. Isolated component check.");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
}
#endif
