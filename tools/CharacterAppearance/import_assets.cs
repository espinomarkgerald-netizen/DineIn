if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.Exception("Exit Play Mode before asset authoring.");
var folder = "Assets/_Project/Player/Assets/Appearance";
System.IO.Directory.CreateDirectory(folder + "/Models");
System.IO.Directory.CreateDirectory(folder + "/Textures");
System.IO.Directory.CreateDirectory("ArtSource/CharacterAppearance");
if (!System.IO.File.Exists("ArtSource/CharacterAppearance/Originals.zip"))
    System.IO.File.Copy("G:/Downloads/Player Customization&Employee.zip", "ArtSource/CharacterAppearance/Originals.zip");
foreach (var file in System.IO.Directory.GetFiles("Temp/CharacterAppearance/export", "*.fbx"))
    System.IO.File.Copy(file, folder + "/Models/" + System.IO.Path.GetFileName(file), true);
foreach (var file in System.IO.Directory.GetFiles("Temp/CharacterAppearance/source/Player/Textures", "*.png", System.IO.SearchOption.AllDirectories))
{
    var relative = file.Substring("Temp/CharacterAppearance/source/Player/Textures/".Length).Replace('\\', '/');
    var target = folder + "/Textures/" + relative;
    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));
    System.IO.File.Copy(file,target,true);
}
AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
var report = new System.Collections.Generic.List<object>();
foreach (var name in new[]{"Male", "Female"})
{
    var path=folder+"/Models/"+name+".fbx";
    var importer=(ModelImporter)AssetImporter.GetAtPath(path);
    importer.animationType=ModelImporterAnimationType.Human;
    importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
    importer.importAnimation=false; importer.optimizeGameObjects=false;
    importer.materialImportMode=ModelImporterMaterialImportMode.None;
    importer.SaveAndReimport();
    var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
    var animator=model.GetComponent<Animator>();
    if(animator?.avatar==null || !animator.avatar.isValid || !animator.avatar.isHuman)
        throw new System.Exception(name+": invalid Humanoid avatar");
    report.Add(new {name, valid=animator.avatar.isValid, human=animator.avatar.isHuman,
        renderers=model.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r=>new {r.name,vertices=r.sharedMesh.vertexCount,uv=r.sharedMesh.uv.Length}).ToArray()});
}
return report;
