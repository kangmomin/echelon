using System;
using System.Collections.Generic;
using System.IO;
using Echelon.Core;
using Echelon.Unity.Platform;
using Echelon.Unity.UI.Aar;
using Echelon.Unity.UI.Builder;
using Echelon.Unity.UI.Common;
using Echelon.Unity.UI.Guide;
using Echelon.Unity.UI.MainMenu;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Echelon.Unity.App
{
    public sealed class AppFlowController : MonoBehaviour
    {
        public static AppFlowController Instance { get; private set; }
        public static Func<IForceStore> ForceStoreProvider { get; set; } = UnityForceStore.Create;
        public DataCatalog Catalog { get; private set; }
        public BattleController Battle { get; private set; }

        private const float SmokeTimeoutSec = 60.0f;
        private const int ScreenshotDelayFrames = 30;

        private ScenarioDef _scenario;
        private Canvas _screensCanvas;
        private GuideScreen _guide;
        private MonoBehaviour _screen;
        private bool _guideWasPaused;
        private bool _guideCameraInputEnabled;
        private bool _guideBattleInputEnabled;
        private bool _captureInProgress;
        private bool _tutorial;
        private string _sceneArg = "";
        private bool _autoStart;
        private bool _aiVsAi;
        private string _aarOut = "";
        private int _smokeTicks = -1;
        private float _smokeElapsed;
        private string _screenshotPath = "";
        private int _screenshotFrames = ScreenshotDelayFrames;
        private int _seriesCount = 1;
        private int _seriesInterval = 1;
        private int _seriesDone;
        private int _frames;
        private bool _noFog;
        private bool _hasCamera;
        private Vector2 _cameraFocus;
        private float _cameraDistance;
        private float _cameraPitch;
        private float _cameraYaw;
        private bool _redAttack;
        private Vector2 _redAttackPos;
        private bool _fireDebug;
        private int _fireAsset;
        private SimVec2 _firePos;
        private bool _fireSmoke;
        private float _runSec = -1.0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Application.targetFrameRate = 60;
#if UNITY_WEBGL
            InputSystem.settings.supportedDevices =
                new UnityEngine.InputSystem.Utilities.ReadOnlyArray<string>(new[] { "Keyboard", "Mouse" });
#endif
#if !UNITY_WEBGL
            if (!Application.isBatchMode)
                Screen.SetResolution(1600, 900, false);
#endif
            ParseCommandLine();
            Application.logMessageReceived += OnLogMessage;
            UiKit.DefaultFont = Resources.Load<TMP_FontAsset>("Fonts/NotoSansKR SDF");
            Catalog = DataLoader.LoadCatalog(new UnityCatalogSource());
            foreach (string error in Catalog.Errors)
                Debug.LogError("[data] " + error);
            foreach (string warning in Catalog.Warnings)
                Debug.LogWarning("[data] " + warning);
            Debug.Log("[data] " + Catalog.Summary());
            if (!Catalog.IsValid())
            {
                enabled = false;
                return;
            }
            _scenario = Catalog.GetScenario("skirmish_01");
            _screensCanvas = UiKit.CreateCanvas("Screens", 10);
            _guide = GuideScreen.Create(_screensCanvas.transform);
            _guide.Opened += OnGuideOpened;
            _guide.Closed += OnGuideClosed;
            UiKit.EnsureEventSystem();
            string first = _sceneArg;
            if (first == "")
                first = _smokeTicks >= 0 || _screenshotPath != "" ? "sandbox" : "menu";
            switch (first)
            {
                case "builder":
                    ShowBuilder();
                    break;
                case "skirmish":
                    StartSkirmish(Catalog.DefaultForce("US"));
                    break;
                case "sandbox":
                    StartSandbox();
                    break;
                case "tutorial":
                    StartTutorial();
                    break;
                default:
                    ShowMenu();
                    break;
            }
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLogMessage;
            if (Instance == this)
                Instance = null;
        }

        private void ClearScreens()
        {
            if (_screen != null)
            {
                Destroy(_screen.gameObject);
                _screen = null;
            }
            if (Battle != null)
            {
                Battle.Dispose();
                Destroy(Battle.gameObject);
                Battle = null;
            }
        }

        public void ShowMenu()
        {
            _tutorial = false;
            ClearScreens();
            MainMenu menu = MainMenu.Create(_screensCanvas.transform);
            menu.TutorialRequested += StartTutorial;
            menu.SkirmishRequested += ShowBuilder;
            menu.SandboxRequested += StartSandbox;
            menu.GuideRequested += ShowGuide;
            menu.QuitRequested += () => Quit();
            _screen = menu;
        }

        public void ShowBuilder()
        {
            ShowBuilder(null);
        }

        public void ShowBuilder(ForceSpec initial)
        {
            _tutorial = false;
            ClearScreens();
            ForceBuilder builder = ForceBuilder.Create(
                _screensCanvas.transform, Catalog, _scenario, ForceStoreProvider(), initial);
            builder.BackRequested += ShowMenu;
            builder.StartRequested += StartSkirmish;
            _screen = builder;
        }

        public void StartSkirmish(ForceSpec player)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));
            _tutorial = false;
            string enemyFaction = player.Faction == "US" ? "RU" : "US";
            ForceSpec enemy = Catalog.DefaultForce(enemyFaction);
            string playerSide = "blue";
            foreach (KeyValuePair<string, ScenarioDef.SideSetup> pair in _scenario.Sides)
            {
                if (pair.Value.Faction == player.Faction)
                    playerSide = pair.Key;
            }
            ForceSpec blue = playerSide == "blue" ? player : enemy;
            ForceSpec red = playerSide == "blue" ? enemy : player;
            var ai = new List<string> { Core.Battle.EnemyOf(playerSide) };
            if (_aiVsAi)
                ai = new List<string> { "blue", "red" };
            Core.Battle battle = BattleSetup.Skirmish(Catalog, blue, red, _scenario.Id, -1, ai);
            battle.PlayerSide = playerSide;
            StartBattle(battle, _autoStart);
        }

        public void StartSandbox()
        {
            _tutorial = false;
            StartBattle(BattleSetup.Sandbox(Catalog));
        }

        public void StartTutorial()
        {
            _tutorial = true;
            StartBattle(BattleSetup.Tutorial(Catalog), false, true);
        }

        private void StartBattle(Core.Battle battle, bool autoBegin = false, bool tutorial = false)
        {
            ClearScreens();
            var battleObject = new GameObject("Battle");
            Battle = battleObject.AddComponent<BattleController>();
            Battle.Finished += ShowAar;
            Battle.MenuRequested += ShowMenu;
            Battle.Setup(battle);
            if (tutorial)
                Battle.StartTutorial();
            else if (autoBegin)
                Battle.StartBattle();
            ApplyDebugArguments(battle);
            Debug.Log($"[sim] battle start seed={battle.World.Rng.SeedValue}, elements={battle.Elements.Count}, units={battle.Units.Count}");
        }

        public void ShowAar(AarData data)
        {
            if (_tutorial || Battle == null)
                return;
            if (_screen != null)
                Destroy(_screen.gameObject);
            AarScreen screen = AarScreen.Create(_screensCanvas.transform, data, Battle.Battle.PlayerSide);
            screen.MenuRequested += ShowMenu;
            screen.ViewBattlefieldRequested += CloseScreen;
            _screen = screen;
            Debug.Log($"[aar] winner={data.Result.Winner} duration={data.DurationSeconds:F1}s");
            if (_aarOut != "")
            {
                string text = JsonConvert.SerializeObject(Battle.Battle.Stats.Summary(), Formatting.Indented);
                WriteText(_aarOut, text);
            }
        }

        public void ShowGuide()
        {
            if (Battle != null)
                Battle.OpenGuide();
            else
                _guide.Open();
        }

        private void CloseScreen()
        {
            if (_screen != null)
            {
                if (_screen is AarScreen aar)
                    aar.Close();
                else
                    Destroy(_screen.gameObject);
                _screen = null;
            }
        }

        private void OnGuideOpened()
        {
            _guideWasPaused = Battle != null && Battle.Battle.World.Paused;
            if (Battle != null)
            {
                _guideCameraInputEnabled = Battle.RtsCamera.InputEnabled;
                _guideBattleInputEnabled = Battle.Input.enabled;
                Battle.RtsCamera.InputEnabled = false;
                Battle.Input.SetGuideOpenFromView(true);
                Battle.Input.enabled = false;
                Battle.Battle.World.SetPaused(true);
            }
        }

        private void OnGuideClosed()
        {
            if (Battle != null)
            {
                Battle.Input.SetGuideOpenFromView(false);
                Battle.Input.enabled = _guideBattleInputEnabled;
                Battle.RtsCamera.InputEnabled = _guideCameraInputEnabled;
            }
            if (!_guideWasPaused && Battle != null && !Battle.Battle.Deploying && !Battle.Battle.HasResult)
                Battle.Battle.World.SetPaused(false);
        }

        private void Update()
        {
            if (Battle == null && Keyboard.current != null && Keyboard.current.slashKey.wasPressedThisFrame)
            {
                if (_guide.IsOpen)
                    _guide.Close();
                else
                    ShowGuide();
            }
            _frames++;
            if (_smokeTicks >= 0 && Battle != null)
            {
                _smokeElapsed += Time.deltaTime;
                if (Battle.Battle.World.Tick >= _smokeTicks)
                {
                    Debug.Log($"[smoke] OK tick={Battle.Battle.World.Tick} sim={Battle.Battle.World.GetSimTimeSec():F1}s real={_smokeElapsed:F2}s hash={Battle.Battle.World.StateHash()}");
                    Quit(0);
                }
                else if (_smokeElapsed > SmokeTimeoutSec)
                {
                    Debug.LogError($"[smoke] FAIL timeout tick={Battle.Battle.World.Tick}");
                    Quit(2);
                }
            }
            if (_screenshotPath != "" && !_captureInProgress && _frames >= _screenshotFrames &&
                (_frames - _screenshotFrames) % _seriesInterval == 0)
            {
                string path = _screenshotPath;
                if (_seriesCount > 1)
                    path = Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path) + "_" + _seriesDone + ".png");
                _seriesDone++;
                StartCoroutine(CaptureScreenshot(path));
            }
        }

        private System.Collections.IEnumerator CaptureScreenshot(string path)
        {
            _captureInProgress = true;
            yield return new WaitForEndOfFrame();
            string output = ResolveOutputPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");
            if (File.Exists(output))
                File.Delete(output);
            ScreenCapture.CaptureScreenshot(output);
            bool saved = false;
            for (int frame = 0; frame < 120; frame++)
            {
                yield return new WaitForEndOfFrame();
                if (File.Exists(output) && new FileInfo(output).Length > 0)
                {
                    saved = true;
                    break;
                }
            }
            _captureInProgress = false;
            if (!saved)
            {
                Debug.LogError("[screenshot] FAIL " + output);
                Quit(2);
                yield break;
            }
            Debug.Log("[screenshot] OK " + output);
            if (_seriesDone >= _seriesCount)
                Quit(0);
        }

        private void ApplyDebugArguments(Core.Battle battle)
        {
            if (_noFog)
                Battle.SetFog(false);
            if (_hasCamera)
            {
                Battle.RtsCamera.FocusOnSim(_cameraFocus);
                Battle.RtsCamera.Distance = _cameraDistance;
                Battle.RtsCamera.PitchDegrees = _cameraPitch;
                Battle.RtsCamera.YawDegrees = _cameraYaw;
                Battle.RtsCamera.EdgeScrollEnabled = false;
            }
            if (_redAttack)
            {
                TaskForce red = battle.TaskForces["red"];
                for (int i = 0; i < red.Companies.Count; i++)
                {
                    Order order = Order.Make(OrderType.ATTACK_MOVE, red.Companies[i].Id);
                    order.SetPos(BattleViewAdapter.ToSim(
                        _redAttackPos + new Vector2(i * 500.0f - 250.0f, 0.0f)));
                    battle.IssueOrder(order);
                }
            }
            if (_fireDebug)
            {
                battle.RunTicks(25);
                Dictionary<string, object> result = battle.Fires.Request(
                    battle.PlayerSide, (FireSupportSystem.Asset)_fireAsset, _firePos, _fireSmoke);
                Debug.Log("[fire] " + (result.TryGetValue("error", out object error) ? error : "요청됨"));
            }
            if (_runSec >= 0.0f)
            {
                int ticks = Mathf.CeilToInt(_runSec * SimConst.TickRate);
                for (int tick = 0; tick < ticks && !battle.HasResult; tick++)
                    battle.StepTick();
            }
        }

        private void ParseCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            foreach (string arg in args)
            {
                string value = arg.Contains("=") ? arg.Substring(arg.IndexOf('=') + 1) : "";
                if (arg.StartsWith("--smoke-ticks="))
                    _smokeTicks = int.Parse(value);
                else if (arg.StartsWith("--screenshot="))
                    _screenshotPath = value;
                else if (arg.StartsWith("--screenshot-frames="))
                    _screenshotFrames = int.Parse(value);
                else if (arg.StartsWith("--screenshot-series="))
                {
                    string[] parts = value.Split(',');
                    _seriesCount = Math.Max(1, int.Parse(parts[0]));
                    _seriesInterval = Math.Max(1, parts.Length > 1 ? int.Parse(parts[1]) : 1);
                }
                else if (arg.StartsWith("--scene="))
                    _sceneArg = value;
                else if (arg == "--sandbox")
                    _sceneArg = "sandbox";
                else if (arg == "--start")
                    _autoStart = true;
                else if (arg == "--ai-vs-ai")
                    _aiVsAi = true;
                else if (arg == "--nofog")
                    _noFog = true;
                else if (arg.StartsWith("--aar-out="))
                    _aarOut = value;
                else if (arg.StartsWith("--run-sec="))
                    _runSec = float.Parse(value);
                else if (arg.StartsWith("--camera="))
                {
                    string[] parts = value.Split(',');
                    if (parts.Length >= 3)
                    {
                        _hasCamera = true;
                        _cameraFocus = new Vector2(float.Parse(parts[0]), float.Parse(parts[1]));
                        _cameraDistance = float.Parse(parts[2]);
                        _cameraPitch = parts.Length > 3 ? float.Parse(parts[3]) : 55.0f;
                        _cameraYaw = parts.Length > 4 ? float.Parse(parts[4]) : 0.0f;
                    }
                }
                else if (arg.StartsWith("--red-attack="))
                {
                    string[] parts = value.Split(',');
                    if (parts.Length >= 2)
                    {
                        _redAttack = true;
                        _redAttackPos = new Vector2(float.Parse(parts[0]), float.Parse(parts[1]));
                    }
                }
                else if (arg.StartsWith("--fire="))
                {
                    string[] parts = value.Split(',');
                    if (parts.Length >= 3)
                    {
                        _fireDebug = true;
                        _fireAsset = int.Parse(parts[0]);
                        _firePos = new SimVec2(float.Parse(parts[1]), float.Parse(parts[2]));
                        _fireSmoke = parts.Length > 3;
                    }
                }
            }
        }

        private static string ResolveOutputPath(string path)
        {
            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);
            return Application.isEditor
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", path))
                : Path.GetFullPath(path);
        }

        private static void WriteText(string path, string text)
        {
            string fullPath = ResolveOutputPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, text);
        }

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                return;
            if (_smokeTicks >= 0 || _screenshotPath != "")
                Quit(2);
        }

        private static void Quit(int exitCode = 0)
        {
            Application.Quit(exitCode);
#if UNITY_EDITOR
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(exitCode);
            else
                UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
