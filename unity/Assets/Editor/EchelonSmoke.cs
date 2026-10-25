using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Echelon.Unity.Editor
{
    public static class EchelonSmoke
    {
        private const double TimeoutSeconds = 90.0;
        private static double _startedAt;

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Boot.unity");
            _startedAt = EditorApplication.timeSinceStartup;
            EditorApplication.update += Watchdog;
            Debug.Log("[unity-smoke] entering Boot play mode");
            EditorApplication.EnterPlaymode();
        }

        private static void Watchdog()
        {
            if (EditorApplication.timeSinceStartup - _startedAt <= TimeoutSeconds)
                return;
            EditorApplication.update -= Watchdog;
            Debug.LogError("[unity-smoke] FAIL editor play mode timeout");
            EditorApplication.Exit(2);
        }
    }
}
