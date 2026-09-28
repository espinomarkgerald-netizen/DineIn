// Run with Unity CLI eval_file; no scenes or existing models are modified.
var names = new[] { "OldAlien", "OrangeAlien", "PurpleAlien", "YellowAlien" };
var assetRoot = "Assets/_Project/Art/Models/Customer/AdditionalAliens";
var work = System.IO.Path.GetFullPath("../AlienRiggingWork/export");
foreach (var name in names)
{
    var folder = assetRoot + "/" + name;
    System.IO.Directory.CreateDirectory(folder);
    foreach (var ext in new[] { ".fbx", "Albedo.png" })
        System.IO.File.Copy(System.IO.Path.Combine(work, name, name + ext), folder + "/" + name + ext, true);
}
AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
var report = new System.Text.StringBuilder();
foreach (var name in names)
{
    var path = assetRoot + "/" + name + "/" + name + ".fbx";
    var importer = (ModelImporter)AssetImporter.GetAtPath(path);
    importer.animationType = ModelImporterAnimationType.Human;
    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
    importer.importAnimation = false;
    importer.optimizeGameObjects = false; // Head and hand bones are used by customer behavior.
    importer.SaveAndReimport();
    var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
    var animator = model.GetComponent<Animator>();
    if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
        throw new System.InvalidOperationException(name + " did not produce a valid Humanoid avatar.");
    report.AppendLine(name + ": valid Humanoid, " + model.GetComponentInChildren<SkinnedMeshRenderer>().bones.Length + " skinned bones");
}
return report.ToString();
