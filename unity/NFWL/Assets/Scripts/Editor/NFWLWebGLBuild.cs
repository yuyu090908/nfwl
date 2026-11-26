using System.IO;
using UnityEditor;
using UnityEngine;

namespace NFWL.EditorTools
{
    /// <summary>
    /// NFWL > Build WebGL: builds the match for the web platform into unity/NFWL/Builds/NFWL/.
    /// Copy (or upload to the CDN) the "Build" folder: see web/README.md, "Unity WebGL build".
    /// Brotli with decompression fallback: files end in .unityweb and load on any static host,
    /// even one that cannot send Content-Encoding headers.
    /// </summary>
    static class NFWLWebGLBuild
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string Output = "Builds/NFWL";

        [MenuItem("NFWL/Build WebGL")]
        public static void Build()
        {
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("NFWL", "Assets/Scenes/Main.unity がありません。メニュー NFWL > Open Main Scene で作成してください。", "OK");
                return;
            }
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                EditorUtility.DisplayDialog("NFWL", "WebGL Build Support がインストールされていません。Unity Hub > Installs > 6000.6.9f1 > Add modules で追加してください。", "OK");
                return;
            }
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.runInBackground = false;
            PlayerSettings.companyName = "NFWL";
            PlayerSettings.productName = "NFWL";

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[NFWL] WebGL build {report.summary.result}: {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalTime}");
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorUtility.RevealInFinder(Path.Combine(Output, "Build"));
        }
    }
}
