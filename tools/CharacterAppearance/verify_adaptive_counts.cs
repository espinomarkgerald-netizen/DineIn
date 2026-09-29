if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
int assertions=0;void Check(bool c,string m){assertions++;if(!c)throw new System.Exception(m);}
var root=new GameObject("Adaptive layout checks",typeof(RectTransform));
try
{
    var viewport=(RectTransform)root.transform;viewport.sizeDelta=new Vector2(306,240);
    foreach(int count in new[]{2,3,4,5,6,11})
    {
        var content=new GameObject("Content",typeof(RectTransform),typeof(DineIn.Appearance.CosmeticOptionLayout));content.transform.SetParent(viewport,false);
        try
        {
            var r=(RectTransform)content.transform;r.sizeDelta=new Vector2(306,400);
            var layout=content.GetComponent<DineIn.Appearance.CosmeticOptionLayout>();var d=new SerializedObject(layout);d.FindProperty("viewport").objectReferenceValue=viewport;d.ApplyModifiedPropertiesWithoutUndo();
            for(int i=0;i<count;i++){var cell=new GameObject("Cell",typeof(RectTransform));cell.transform.SetParent(r,false);}
            layout.CalculateLayoutInputHorizontal();layout.CalculateLayoutInputVertical();r.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,layout.preferredHeight);layout.SetLayoutHorizontal();layout.SetLayoutVertical();
            Check(layout.Columns==2,"Unexpected column count: "+count);
            Check(layout.CellSize==new Vector2(138,174),"Card resized based on count");
            for(int row=0;row<count;row+=layout.Columns)
            {
                var first=(RectTransform)r.GetChild(row);var last=(RectTransform)r.GetChild(Mathf.Min(count,row+layout.Columns)-1);
                float left=first.anchoredPosition.x-first.rect.width*first.pivot.x;
                float right=last.anchoredPosition.x+last.rect.width*(1-last.pivot.x);
                Check(Mathf.Abs(left+right-r.rect.width)<.01f,"Uncentered incomplete row: "+count);
            }
            if(count<=2)Check(layout.preferredHeight<=viewport.rect.height+.01f,"Two-option list unnecessarily scrolls");
            else Check(layout.preferredHeight>viewport.rect.height,"Long list cannot scroll");
        }
        finally{UnityEngine.Object.DestroyImmediate(content);}
    }
}
finally{UnityEngine.Object.DestroyImmediate(root);}
return new{assertions,counts="2, 3, 4, 5, 6, 11"};
