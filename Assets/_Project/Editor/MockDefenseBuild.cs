#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class MockDefenseBuild
{
    static readonly string[] Scenes = {
        "Assets/Scenes/Bootstrap.unity",
        "Assets/_Project/Scenes/NewMenu/NewMainMenu.unity",
        "Assets/_Project/Scenes/NewMenu/NewGameMenu.unity",
        "Assets/_Project/Scenes/RoleBased/Lobby1.unity",
        "Assets/_Project/Scenes/TutorialScenes/Lobby1Tutorial.unity",
        "Assets/_Project/Scenes/RoleBased/RestockScene.unity",
        "Assets/_Project/Scenes/RoleBased/Lobby1 Multiplayer.unity",
        "Assets/_Project/Scenes/RoleBased/Lobby2.unity",
        "Assets/_Project/Scenes/RoleBased/Lobby2Tutorial.unity"
    };
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
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            foreach(var scene in Scenes) if(!File.Exists(scene))throw new FileNotFoundException(scene);
            BuildReferenceIntegrityGuard.ValidateOrThrow();
            if(android){PlayerSettings.Android.useCustomKeystore=false;EditorUserBuildSettings.buildAppBundle=false;}
            File.WriteAllText(resultPath,"Result=Building\nOutput="+path);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=Scenes,locationPathName=path,target=target,
                targetGroup=android?BuildTargetGroup.Android:BuildTargetGroup.Standalone,
                options=BuildOptions.None,
                extraScriptingDefines=android?Array.Empty<string>():new[]{"DINEIN_PRESENTATION_BUILD"}
            });
            var s=report.summary;
            File.WriteAllText(resultPath,$"Result={s.result}\nDevelopment={(s.options & BuildOptions.Development)!=0}\nOutput={path}\nErrors={s.totalErrors}\nWarnings={s.totalWarnings}\nDuration={s.totalTime}\n");
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
