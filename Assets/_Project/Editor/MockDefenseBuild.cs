#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class MockDefenseBuild
{
    [MenuItem("Dine In/Build/Mock Defense Android")]
    public static void Android() => Build(BuildTarget.Android);
    [MenuItem("Dine In/Build/Mock Defense Windows")]
    public static void Windows() => Build(BuildTarget.StandaloneWindows64);
    static void Build(BuildTarget target)
    {
        bool android=target==BuildTarget.Android;
        string name="Thesis B Mock Def Build " + (android?"Android":"Windows");
        string path=Path.GetFullPath("Builds/Mock Defense/"+name+"/"+name+(android?".apk":".exe"));
        string resultPath="Logs/"+(android?"MockDefenseAndroid":"MockDefenseWindows")+".result";
        bool customKey=PlayerSettings.Android.useCustomKeystore;
        bool bundle=EditorUserBuildSettings.buildAppBundle;
        try
        {
            var profile = BuildProfile.GetActiveBuildProfile();
            if (profile != null && new SerializedObject(profile).FindProperty("m_BuildTarget").intValue != (int)target)
                throw new Exception("Select the matching Windows or Android build profile before building.");
            var scenes = profile != null && new SerializedObject(profile).FindProperty("m_OverrideGlobalSceneList").boolValue
                ? profile.scenes : EditorBuildSettings.scenes;
            var paths = scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (paths.Length == 0) throw new Exception("The selected build profile has no enabled scenes.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            foreach(var scene in paths) if(!File.Exists(scene))throw new FileNotFoundException(scene);
            BuildReferenceIntegrityGuard.ValidateOrThrow();
            if(android){PlayerSettings.Android.useCustomKeystore=false;EditorUserBuildSettings.buildAppBundle=false;}
            File.WriteAllText(resultPath,"Result=Building\nOutput="+path);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=paths,locationPathName=path,target=target,
                targetGroup=android?BuildTargetGroup.Android:BuildTargetGroup.Standalone,
                options=BuildOptions.None,
                extraScriptingDefines=android?Array.Empty<string>():new[]{"DINEIN_PRESENTATION_BUILD"}
            });
            var s=report.summary;
            File.WriteAllText(resultPath,$"Result={s.result}\nDevelopment={(s.options & BuildOptions.Development)!=0}\nOutput={path}\nErrors={s.totalErrors}\nWarnings={s.totalWarnings}\nDuration={s.totalTime}\n");
            File.AppendAllText(resultPath, "Profile=" + (profile != null ? AssetDatabase.GetAssetPath(profile) : "Global") + "\nScenes=" + string.Join(";", paths) + "\n");
            using (var fingerprint = IncrementalHash.CreateHash(HashAlgorithmName.SHA256)) {
                foreach (var source in Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal)) {
                    fingerprint.AppendData(System.Text.Encoding.UTF8.GetBytes(source));
                    fingerprint.AppendData(File.ReadAllBytes(source));
                }
                File.AppendAllText(resultPath, "SourceSHA256=" + BitConverter.ToString(fingerprint.GetHashAndReset()).Replace("-", "").ToLowerInvariant() + "\n");
            }
            if (s.result == BuildResult.Succeeded)
            {
                using var hash = SHA256.Create();
                var files = android ? new[] { path } : Directory.GetFiles(Path.GetDirectoryName(path), "*", SearchOption.AllDirectories).OrderBy(p => p).ToArray();
                File.WriteAllLines(resultPath + ".sha256", files.Select(file => {
                    using var input = File.OpenRead(file);
                    return BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "").ToLowerInvariant() + "  " + file;
                }));
            }
            if(s.result!=BuildResult.Succeeded)throw new Exception(name+" failed. See "+resultPath);
            Debug.Log("[MockDefenseBuild] Built "+path);
        }
        catch(Exception e){File.AppendAllText(resultPath,"\nException="+e);throw;}
        finally
        {
            if(android){PlayerSettings.Android.useCustomKeystore=customKey;EditorUserBuildSettings.buildAppBundle=bundle;}
        }
    }
}
#endif
