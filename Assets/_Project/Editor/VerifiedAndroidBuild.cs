#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Reproducible Android test-build entry point. It uses the same five-scene
/// sequence as the authored Android Build Profile and always runs the stale
/// reference guard before Unity begins serialization.
/// </summary>
public static class VerifiedAndroidBuild
{
    private const string DefaultOutputPath =
        "Builds/Android/Thesis B TestBuilds/0.5.2-mobile-safe.apk";
    private const string ResultPath = "Logs/VerifiedAndroidBuild.result";

    private static readonly string[] AndroidScenes =
    {
        "Assets/Scenes/Bootstrap.unity",
        "Assets/_Project/Scenes/NewMenu/NewMainMenu.unity",
        "Assets/_Project/Scenes/NewMenu/NewGameMenu.unity",
        "Assets/_Project/Scenes/RoleBased/Lobby1.unity",
        "Assets/_Project/Scenes/RoleBased/RestockScene.unity"
    };

    [MenuItem("Tools/Dine In/Build Verified Android APK")]
    public static void BuildVerifiedApk()
    {
        string outputPath = ReadCommandLineValue("-dineInBuildOutput") ?? DefaultOutputPath;
        string absoluteOutputPath = Path.GetFullPath(outputPath);

        try
        {
            BuildReferenceIntegrityGuard.ValidateOrThrow();
            ValidateSceneList();

            string outputDirectory = Path.GetDirectoryName(absoluteOutputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            EditorUserBuildSettings.buildAppBundle = false;
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = AndroidScenes,
                locationPathName = absoluteOutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            Debug.Log(
                $"[VerifiedAndroidBuild] Building {AndroidScenes.Length} guarded scenes to {absoluteOutputPath}.");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            string result =
                $"Output={absoluteOutputPath}{Environment.NewLine}" +
                $"Result={summary.result}{Environment.NewLine}" +
                $"Errors={summary.totalErrors}{Environment.NewLine}" +
                $"Warnings={summary.totalWarnings}{Environment.NewLine}" +
                $"Size={summary.totalSize}{Environment.NewLine}" +
                $"Duration={summary.totalTime}{Environment.NewLine}";
            WriteResult(result);

            bool passed = summary.result == BuildResult.Succeeded &&
                          summary.totalErrors == 0 &&
                          File.Exists(absoluteOutputPath) &&
                          new FileInfo(absoluteOutputPath).Length > 0;
            if (!passed)
                throw new InvalidOperationException("Android build did not produce a valid APK.\n" + result);

            Debug.Log("[VerifiedAndroidBuild] PASS\n" + result);
            ExitBatchMode(0);
        }
        catch (Exception exception)
        {
            string failure =
                $"Output={absoluteOutputPath}{Environment.NewLine}" +
                $"Result=Failed{Environment.NewLine}" +
                $"Exception={exception}{Environment.NewLine}";
            WriteResult(failure);
            Debug.LogError("[VerifiedAndroidBuild] FAIL\n" + failure);
            ExitBatchMode(1);
            if (!Application.isBatchMode)
                throw;
        }
    }

    private static void ValidateSceneList()
    {
        string missing = AndroidScenes.FirstOrDefault(scene => !File.Exists(scene));
        if (!string.IsNullOrEmpty(missing))
            throw new FileNotFoundException("Android build scene is missing.", missing);

        if (AndroidScenes.Distinct(StringComparer.Ordinal).Count() != AndroidScenes.Length)
            throw new InvalidOperationException("Android build scene list contains a duplicate scene.");
    }

    private static string ReadCommandLineValue(string key)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private static void WriteResult(string contents)
    {
        string absoluteResultPath = Path.GetFullPath(ResultPath);
        string resultDirectory = Path.GetDirectoryName(absoluteResultPath);
        if (!string.IsNullOrEmpty(resultDirectory))
            Directory.CreateDirectory(resultDirectory);
        File.WriteAllText(absoluteResultPath, contents);
    }

    private static void ExitBatchMode(int code)
    {
        if (Application.isBatchMode)
            EditorApplication.Exit(code);
    }
}
#endif
