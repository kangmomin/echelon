using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Echelon.Unity.Editor
{
    public static class EchelonBuild
    {
        public static void BuildWindows()
        {
            Build("windows/Echelon.exe", BuildTarget.StandaloneWindows64);
        }

        public static void BuildLinux()
        {
            Build("linux/Echelon.x86_64", BuildTarget.StandaloneLinux64);
        }

        public static void BuildWeb()
        {
            Build("web", BuildTarget.WebGL);
        }

        public static void BuildAll()
        {
            BuildWindows();
            BuildLinux();
            BuildWeb();
        }

        private static void Build(string relativeOutput, BuildTarget target)
        {
            EchelonAssetSetup.SetupAll();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "build", "unity", relativeOutput));
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Select(scene => scene.path).ToArray(),
                locationPathName = output,
                target = target,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Unity build failed: " + target + " (" + report.summary.result + ")");
            Debug.Log("[unity-build] OK " + target + " -> " + output);
        }
    }
}
