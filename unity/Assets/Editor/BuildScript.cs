// One-command builds so the agent never has to touch the Unity GUI.
//
//   Unity.exe -batchmode -quit -projectPath . -executeMethod Mesruiyet.EditorTools.BuildScript.Windows
//
// Exits non-zero on failure so the loop script can stop early.

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Mesruiyet.EditorTools
{
    public static class BuildScript
    {
        const string OutDir = "../agent/build";

        [MenuItem("Meşruiyet/Build Windows (agent)")]
        public static void Windows() => Run(BuildTarget.StandaloneWindows64, "Mesruiyet.exe");

        [MenuItem("Meşruiyet/Build Linux (agent)")]
        public static void Linux() => Run(BuildTarget.StandaloneLinux64, "Mesruiyet.x86_64");

        [MenuItem("Meşruiyet/Build WebGL")]
        public static void Web() => Run(BuildTarget.WebGL, "");

        // The shipping web build: Brotli with the JS decompression fallback, so ANY static
        // host serves it without Content-Encoding configuration — the lobby Worker's assets
        // directory included. No Development flag: a fraction of the size, no AgentBridge.
        [MenuItem("Meşruiyet/Build WebGL (release)")]
        public static void WebRelease()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            Run(BuildTarget.WebGL, "", release: true, outSub: "WebGLRelease");
        }

        static void Run(BuildTarget target, string exeName, bool release = false, string outSub = null)
        {
            // Idempotent: creates the boot scene, the UI panel asset and the player settings
            // the agent loop needs, so a fresh clone builds without anyone opening the GUI.
            ProjectSetup.Ensure();

            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Fail("No scenes enabled in Build Settings. Bootstrap builds the world from " +
                     "code, but Unity still needs one empty scene registered.");
                return;
            }

            var sub = outSub ?? target.ToString();
            var path = Path.Combine(OutDir, sub, exeName);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));

            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                target = target,
                locationPathName = path,
                // Development build keeps AgentBridge compiled in and gives us a real stack
                // trace in player.log — the agent loop depends on both. Release drops both.
                options = release ? BuildOptions.None
                                  : BuildOptions.Development | BuildOptions.AllowDebugging
            };

            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;

            Debug.Log($"[Build] {s.result} · {s.totalSize / (1024 * 1024)} MB · " +
                      $"{s.totalTime.TotalSeconds:F1}s · {path}");

            if (s.result != BuildResult.Succeeded)
                Fail($"Build failed with {s.totalErrors} error(s).");
        }

        static void Fail(string msg)
        {
            Debug.LogError("[Build] " + msg);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
