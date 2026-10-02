#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class CloudGateVerificationBuild
{
    public static void Run()
    {
        const string output = "G:/Unity Projects/DineIn/output/cloud-save-gate";
        string originalName = PlayerSettings.productName;
        try
        {
            // A separate product isolates player preferences AND persistent files from the user's careers.
            PlayerSettings.productName = "DineInCloudGateVerification";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                target = BuildTarget.StandaloneWindows64,
                locationPathName = output + "/Windows/DineInCloudGateVerification.exe",
                options = BuildOptions.Development,
                extraScriptingDefines = new[] { "DINEIN_CLOUD_GATE_TEST" }
            });
            File.WriteAllText(output + "/build-result.txt", report.summary.result + " errors=" + report.summary.totalErrors + " duration=" + report.summary.totalTime);
        }
        finally { PlayerSettings.productName = originalName; }
    }
}
#endif
