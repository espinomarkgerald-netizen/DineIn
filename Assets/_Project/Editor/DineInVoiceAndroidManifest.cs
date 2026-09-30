#if UNITY_EDITOR && UNITY_ANDROID && DINEIN_PHOTON_VOICE
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEditor.Build;

// Edits only Unity's generated Gradle manifest, never the authored project manifest.
// Microphone permission is requested by MultiplayerVoiceController on multiplayer entry.
public sealed class DineInVoiceAndroidManifest : IPostGenerateGradleAndroidProject
{
    private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
    public int callbackOrder => 100;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
        if (!File.Exists(manifestPath))
            throw new BuildFailedException("Dine In voice could not find the generated Android manifest.");
        var document = new XmlDocument();
        document.Load(manifestPath);
        var root = document.DocumentElement;
        var application = root?.SelectSingleNode("application") as XmlElement;
        if (root == null || application == null)
            throw new BuildFailedException("Dine In voice requires a valid Android application manifest.");

        FindOrCreate(document, root, "uses-permission", "android.permission.RECORD_AUDIO");
        var microphone = FindOrCreate(document, root, "uses-feature", "android.hardware.microphone");
        microphone.SetAttribute("required", AndroidNamespace, "false");
        var skip = FindOrCreate(document, application, "meta-data", "unityplayer.SkipPermissionsDialog");
        skip.SetAttribute("value", AndroidNamespace, "true");
        document.Save(manifestPath);
    }

    private static XmlElement FindOrCreate(XmlDocument document, XmlElement parent, string tag, string name)
    {
        foreach (XmlNode node in parent.ChildNodes)
            if (node is XmlElement element && element.Name == tag && element.GetAttribute("name", AndroidNamespace) == name)
                return element;
        var created = document.CreateElement(tag);
        created.SetAttribute("name", AndroidNamespace, name);
        parent.AppendChild(created);
        return created;
    }
}
#endif