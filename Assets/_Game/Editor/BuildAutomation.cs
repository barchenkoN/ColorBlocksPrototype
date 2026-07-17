using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ColorBlocks.Editor
{
    public static class BuildAutomation
    {
        private const string ScenePath = "Assets/_Game/Scenes/Game.unity";
        private const string AndroidManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
        private const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";

        [MenuItem("Color Blocks/Build/Android Development APK")]
        public static void BuildAndroidDevelopment()
        {
            string output = Path.GetFullPath("Builds/Android/ColorBlocksPrototype.apk");
            Build(BuildTarget.Android, output, BuildOptions.Development);
        }

        [MenuItem("Color Blocks/Build/Export iOS Xcode Project")]
        public static void ExportIosProject()
        {
            string output = Path.GetFullPath("Builds/iOS");
            Build(BuildTarget.iOS, output, BuildOptions.None);
        }

        private static void Build(BuildTarget target, string output, BuildOptions options)
        {
            ValidatePlatformConfiguration(target);
            string directory = target == BuildTarget.iOS ? output : Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            BuildPlayerOptions buildOptions = new()
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = target,
                options = options
            };
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"{target} build failed: {report.summary.result}, {report.summary.totalErrors} errors.");
            }

            Debug.Log($"{target} build succeeded: {output} ({report.summary.totalSize / 1048576f:0.0} MB).");
        }

        private static void ValidatePlatformConfiguration(BuildTarget target)
        {
            if (target != BuildTarget.Android) return;
            string projectSettings = Path.GetFullPath(ProjectSettingsPath);
            if (!File.Exists(projectSettings) ||
                File.ReadAllText(projectSettings).IndexOf("useCustomMainManifest: 1", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException(
                    "Android build requires Custom Main Manifest so VIBRATE permission is merged.");
            }

            string manifest = Path.GetFullPath(AndroidManifestPath);
            if (!File.Exists(manifest) ||
                File.ReadAllText(manifest).IndexOf("android.permission.VIBRATE", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException(
                    $"Android haptics permission is missing from {AndroidManifestPath}.");
            }
        }
    }
}
