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
    /// <summary>앱 화면 흐름: 메뉴 → 편성 → 전투(배치·교전) → AAR, 튜토리얼·샌드박스·가이드.</summary>
    public sealed class AppFlowController : MonoBehaviour
    {
        public static AppFlowController Instance { get; private set; }
        public static Func<IForceStore> ForceStoreProvider { get; set; } = UnityForceStore.Create;
        public DataCatalog Catalog { get; private set; }
        public BattleController Battle { get; private set; }

        private LaunchOptions _options;
        private ScenarioDef _scenario;
        private Canvas _screensCanvas;
        private GuideScreen _guide;
        private MonoBehaviour _screen;
        private bool _tutorial;

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
#else
            if (!Application.isBatchMode)
                Screen.SetResolution(1600, 900, false);
#endif
            _options = LaunchOptions.Parse(Environment.GetCommandLineArgs());
            if (_options.IsAutomated)
                AppAutomation.Attach(gameObject, _options, () => Battle);
            UiKit.DefaultFont = Resources.Load<TMP_FontAsset>("Fonts/NotoSansKR SDF");
            // 말줄임표와 HUD 기호를 먼저 동적 아틀라스에 올린다(처음 그릴 때 찾지 못하면 TMP 가 잘라내기로 바꾼다).
            UiKit.DefaultFont?.TryAddCharacters("…·—▼▶◀Ⅱ●○◐×");
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
            UiKit.EnsureEventSystem();
            string first = _options.Scene;
            if (first == "")
                first = _options.IsAutomated ? "sandbox" : "menu";
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
            if (_options.OpenGuide)
                ShowGuide();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            // 전투 중에는 BattleController 가 / 키를 처리한다.
            if (Battle == null && Keyboard.current != null && Keyboard.current.slashKey.wasPressedThisFrame)
            {
                if (_guide.IsOpen)
                    _guide.Close();
                else
                    ShowGuide();
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
            menu.QuitRequested += () => AppAutomation.Quit();
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
            var ai = _options.AiVsAi
                ? new List<string> { "blue", "red" }
                : new List<string> { Core.Battle.EnemyOf(playerSide) };
            Core.Battle battle = BattleSetup.Skirmish(Catalog, blue, red, _scenario.Id, -1, ai);
            battle.PlayerSide = playerSide;
            StartBattle(battle, _options.AutoStart);
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
            if (_options.AarOut != "")
                WriteText(_options.AarOut, JsonConvert.SerializeObject(Battle.Battle.Stats.Summary(), Formatting.Indented));
        }

        public void ShowGuide()
        {
            if (Battle != null)
                Battle.OpenGuide();
            else
                _guide.Open();
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
            ApplyDebugOptions(battle);
            Debug.Log($"[sim] battle start seed={battle.World.Rng.SeedValue}, elements={battle.Elements.Count}, units={battle.Units.Count}");
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

        private void CloseScreen()
        {
            if (_screen == null)
                return;
            if (_screen is AarScreen aar)
                aar.Close();
            else
                Destroy(_screen.gameObject);
            _screen = null;
        }

        private void ApplyDebugOptions(Core.Battle battle)
        {
            if (_options.NoFog)
                Battle.SetFog(false);
            if (_options.HasCamera)
            {
                Battle.RtsCamera.FocusOnSim(_options.CameraFocus);
                Battle.RtsCamera.Distance = _options.CameraDistance;
                Battle.RtsCamera.PitchDegrees = _options.CameraPitch;
                Battle.RtsCamera.YawDegrees = _options.CameraYaw;
                Battle.RtsCamera.EdgeScrollEnabled = false;
            }
            if (_options.RedAttack)
            {
                TaskForce red = battle.TaskForces["red"];
                for (int i = 0; i < red.Companies.Count; i++)
                {
                    Order order = Order.Make(OrderType.ATTACK_MOVE, red.Companies[i].Id);
                    order.SetPos(BattleViewAdapter.ToSim(_options.RedAttackPosition + new Vector2(i * 500.0f - 250.0f, 0.0f)));
                    battle.IssueOrder(order);
                }
            }
            if (_options.FireDebug)
            {
                battle.RunTicks(25);
                Dictionary<string, object> result = battle.Fires.Request(
                    battle.PlayerSide, (FireSupportSystem.Asset)_options.FireAsset, _options.FirePosition, _options.FireSmoke);
                Debug.Log("[fire] " + (result.TryGetValue("error", out object error) ? error : "요청됨"));
            }
            if (_options.SelectCompany >= 0)
                Battle.Commands.SelectCompany(_options.SelectCompany, false);
            if (_options.RunSeconds >= 0.0f)
            {
                int ticks = Mathf.CeilToInt(_options.RunSeconds * SimConst.TickRate);
                for (int tick = 0; tick < ticks && !battle.HasResult; tick++)
                    battle.StepTick();
            }
        }

        private static void WriteText(string path, string text)
        {
            string fullPath = AppAutomation.ResolveOutputPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, text);
        }
    }
}
