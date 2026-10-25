using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Echelon.Unity.App
{
    /// <summary>
    /// 명령줄로 켜는 자동 검증: N틱 스모크, 스크린샷(연속 촬영 포함), 오류 로그 시 실패 종료.
    /// 종료 코드 0 = 성공, 2 = 실패.
    /// </summary>
    internal sealed class AppAutomation : MonoBehaviour
    {
        private const float SmokeTimeoutSec = 60f;
        private const int ScreenshotSaveWaitFrames = 120;

        private LaunchOptions _options;
        private Func<BattleController> _battle;
        private float _smokeElapsed;
        private int _frames;
        private int _seriesDone;
        private bool _captureInProgress;

        public static AppAutomation Attach(GameObject host, LaunchOptions options, Func<BattleController> battle)
        {
            var automation = host.AddComponent<AppAutomation>();
            automation._options = options;
            automation._battle = battle;
            Application.logMessageReceived += automation.OnLogMessage;
            return automation;
        }

        public static string ResolveOutputPath(string path)
        {
            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);
            return Application.isEditor
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", path))
                : Path.GetFullPath(path);
        }

        public static void Quit(int exitCode = 0)
        {
            Application.Quit(exitCode);
#if UNITY_EDITOR
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(exitCode);
            else
                UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLogMessage;
        }

        private void Update()
        {
            _frames++;
            BattleController battle = _battle();
            if (_options.SmokeTicks >= 0 && battle != null)
            {
                _smokeElapsed += Time.deltaTime;
                var world = battle.Battle.World;
                if (world.Tick >= _options.SmokeTicks)
                {
                    Debug.Log($"[smoke] OK tick={world.Tick} sim={world.GetSimTimeSec():F1}s real={_smokeElapsed:F2}s hash={world.StateHash()}");
                    Quit(0);
                }
                else if (_smokeElapsed > SmokeTimeoutSec)
                {
                    Debug.LogError($"[smoke] FAIL timeout tick={world.Tick}");
                    Quit(2);
                }
            }
            if (_options.ScreenshotPath != "" && !_captureInProgress && _frames >= _options.ScreenshotFrames
                && (_frames - _options.ScreenshotFrames) % _options.SeriesInterval == 0)
            {
                string path = _options.ScreenshotPath;
                if (_options.SeriesCount > 1)
                {
                    path = Path.Combine(Path.GetDirectoryName(path) ?? "",
                        Path.GetFileNameWithoutExtension(path) + "_" + _seriesDone + ".png");
                }
                _seriesDone++;
                StartCoroutine(CaptureScreenshot(path));
            }
        }

        private IEnumerator CaptureScreenshot(string path)
        {
            _captureInProgress = true;
            yield return new WaitForEndOfFrame();
            string output = ResolveOutputPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");
            if (File.Exists(output))
                File.Delete(output);
            ScreenCapture.CaptureScreenshot(output);
            // CaptureScreenshot 은 비동기라 파일이 생길 때까지 기다린다.
            bool saved = false;
            for (int frame = 0; frame < ScreenshotSaveWaitFrames && !saved; frame++)
            {
                yield return new WaitForEndOfFrame();
                saved = File.Exists(output) && new FileInfo(output).Length > 0;
            }
            _captureInProgress = false;
            if (!saved)
            {
                Debug.LogError("[screenshot] FAIL " + output);
                Quit(2);
                yield break;
            }
            Debug.Log("[screenshot] OK " + output);
            if (_seriesDone >= _options.SeriesCount)
                Quit(0);
        }

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                Quit(2);
        }
    }
}
