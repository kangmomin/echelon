using System.Collections.Generic;
using Echelon.Unity.UI.Battle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Echelon.Unity.Tests.BattleUi
{
    public sealed class BattleHudTests : BattleUiTestFixture
    {
        [Test]
        public void TopBarMapsTimeScoreForcePauseAndCapacity()
        {
            var state = new BattleHudState
            {
                SimulationSeconds = 125.9f,
                Speed = 2f,
                PlayerVictoryPoints = 12f,
                EnemyVictoryPoints = 7f,
                TimeLimitSeconds = 600f,
                PlayerForceRatio = 0.75f,
                EnemyForceRatio = 0.5f,
                Paused = true,
                CommandCapacityUsed = 1.5f,
                CommandCapacityMaximum = 4f,
                CommandCapacityQueue = 2
            };

            Assert.That(BattleUiText.Clock((int)state.SimulationSeconds), Is.EqualTo("02:05"));
            Assert.That(BattleUiText.Speed(state.Speed), Is.EqualTo("2×"));
            Assert.That(BattleUiText.Remaining(state), Is.EqualTo("07:54"));
            Assert.That(BattleUiText.Percent(state.PlayerForceRatio), Is.EqualTo("75%"));
            Assert.That(BattleUiText.Phase(state), Is.EqualTo("일시정지"));
            Assert.That(BattleUiText.Capacity(state), Is.EqualTo("1.5 / 4  (대기 2)"));
            state.Deploying = true;
            Assert.That(BattleUiText.Phase(state), Is.EqualTo("배치 단계"));

            BattleHud hud = BattleHud.Create(CanvasRect);
            hud.Render(state);
            Assert.That(hud.PauseButton.interactable, Is.False);
        }

        [Test]
        public void SelectionTextIncludesCommandAndEngineerStatus()
        {
            var unit = new BattleUnitView
            {
                DisplayName = "공병 1소대",
                Level = BattleUnitLevel.Platoon,
                Kind = "engineer",
                Strength = 0.62f,
                Morale = 81f,
                Comms = BattleCommsState.Cut,
                FormationId = "wedge",
                Technique = BattleSop.Bounding,
                RulesOfEngagement = BattleSop.HoldFire,
                Posture = "laying",
                MissionType = BattleCommandType.LayMines,
                Detached = true,
                Leaderless = true,
                Reforming = true,
                HasSquads = true,
                Mounted = true,
                AmmoRatio = 0.44f,
                MinesLeft = 2,
                PositionsBuilt = 1,
                PositionsCapacity = 3
            };

            Assert.That(BattleUiText.UnitSetup(unit), Is.EqualTo("쐐기 · 교대 약진 · 사격 중지"));
            Assert.That(BattleUiText.UnitActivity(unit), Is.EqualTo("지뢰 매설 중 · 임무 지뢰 매설"));
            var tags = BattleUiText.UnitTags(unit).ConvertAll(tag => tag.Text);
            CollectionAssert.AreEqual(
                new[] { "통신 두절", "분리됨", "지휘 공백", "대형 전환 중", "보병 탑승", "지뢰 2 · 진지 1/3" },
                tags);
            Assert.That(BattleUiText.SelectionTitle(new[] { unit, unit }), Is.EqualTo("공병 1소대  외 1"));
        }

        [Test]
        public void CommandFormationFireAndTimeButtonsEmitIntents()
        {
            BattleHud hud = BattleHud.Create(CanvasRect);
            hud.SetSelection(new[] { new BattleUnitView { Id = 4, DisplayName = "1소대" } });
            BattleCommandType? order = null;
            BattleCommandIntent immediate = null;
            string formation = null;
            BattleFireAsset? fire = null;
            bool smoke = false;
            int speed = 0;
            bool pause = false;
            hud.OrderModeSelected += value => order = value;
            hud.ImmediateOrderRequested += value => immediate = value;
            hud.FormationSelected += value => formation = value;
            hud.FireModeSelected += (asset, useSmoke) => { fire = asset; smoke = useSmoke; };
            hud.SpeedChangeRequested += value => speed = value;
            hud.PauseToggleRequested += () => pause = true;

            hud.GetOrderButton(BattleCommandType.AttackMove).onClick.Invoke();
            FindButton(hud, "Command_Overwatch").onClick.Invoke();
            hud.GetFormationButton("wedge").onClick.Invoke();
            hud.GetFireButton(BattleFireAsset.Artillery, true).onClick.Invoke();
            FindButton(hud, "SpeedDown").onClick.Invoke();
            FindButton(hud, "Pause").onClick.Invoke();

            Assert.That(order, Is.EqualTo(BattleCommandType.AttackMove));
            Assert.That(hud.OrderMode, Is.EqualTo(BattleCommandType.AttackMove));
            Assert.That(immediate.Type, Is.EqualTo(BattleCommandType.SetSop));
            Assert.That(immediate.Technique, Is.EqualTo(BattleSop.Overwatch));
            Assert.That(formation, Is.EqualTo("wedge"));
            Assert.That(fire, Is.EqualTo(BattleFireAsset.Artillery));
            Assert.That(smoke, Is.True);
            Assert.That(speed, Is.EqualTo(-1));
            Assert.That(pause, Is.True);
        }

        [Test]
        public void SelectionControlsFormationSetAndSpecialCommands()
        {
            BattleHud hud = BattleHud.Create(CanvasRect);
            Assert.That(hud.CommandPanel.gameObject.activeSelf, Is.False);
            hud.SetSelection(new[]
            {
                new BattleUnitView { Id = 1, Level = BattleUnitLevel.Company, DisplayName = "A중대" }
            });
            Assert.That(hud.GetFormationButton("co_box"), Is.Not.Null);
            Assert.That(hud.GetFormationButton("column"), Is.Null);

            hud.SetSelection(new[]
            {
                new BattleUnitView { Id = 2, Kind = "engineer", DisplayName = "공병" }
            });
            Assert.That(hud.GetOrderButton(BattleCommandType.LayMines).gameObject.activeSelf, Is.True);
            Assert.That(FindButton(hud, "Command_Altitude").gameObject.activeSelf, Is.False);

            hud.SetSelection(new[]
            {
                new BattleUnitView { Id = 3, Kind = "aviation", DisplayName = "헬기" }
            });
            Assert.That(hud.GetOrderButton(BattleCommandType.LayMines).gameObject.activeSelf, Is.False);
            Assert.That(FindButton(hud, "Command_Altitude").gameObject.activeSelf, Is.True);
        }

        [Test]
        public void FireModeDisplayAndMissionText()
        {
            BattleHud hud = BattleHud.Create(CanvasRect);
            hud.SetFireMode(BattleFireAsset.Artillery, true);
            Assert.That(hud.FireAsset, Is.EqualTo(BattleFireAsset.Artillery));
            Assert.That(hud.FireSmoke, Is.True);
            Assert.That(FindLabel(hud, "FireHint").gameObject.activeSelf, Is.True);
            hud.SetFireMode(null, false);
            Assert.That(hud.FireAsset, Is.Null);
            Assert.That(FindLabel(hud, "FireHint").gameObject.activeSelf, Is.False);

            hud.SetFireSupport(new BattleFireSupportState
            {
                ArtilleryMissions = 3,
                CasSorties = 2,
                ActiveMissions = new List<BattleFireMissionView>
                {
                    new BattleFireMissionView
                    {
                        Asset = BattleFireAsset.Artillery,
                        Smoke = true,
                        Phase = BattleFirePhase.Processing,
                        SecondsRemaining = 42.2f
                    },
                    new BattleFireMissionView
                    {
                        Asset = BattleFireAsset.Mortar,
                        Phase = BattleFirePhase.Effect,
                        RoundsLeft = 5
                    }
                }
            });
            StringAssert.Contains("남은 포병 임무 3   항공 출격 2", hud.FireLabel.text);
            StringAssert.Contains("포병 연막 — 처리 중 42초", hud.FireLabel.text);
            StringAssert.Contains("박격포 — 효력사 (남은 5발)", hud.FireLabel.text);
        }

        private static TMPro.TextMeshProUGUI FindLabel(Component root, string name)
        {
            foreach (TMPro.TextMeshProUGUI label in root.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
            {
                if (label.name == name)
                    return label;
            }
            Assert.Fail("Label not found: " + name);
            return null;
        }
    }
}
