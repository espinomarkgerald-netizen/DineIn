if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
var catalog=DineIn.Appearance.AppearanceCatalog.Load();
foreach(var hat in catalog.hats.Where(h=>h.prefab!=null))hat.thumbnail=null;
EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
return "Invalidated only the three changed headwear thumbnails; render_options.cs regenerates them.";
