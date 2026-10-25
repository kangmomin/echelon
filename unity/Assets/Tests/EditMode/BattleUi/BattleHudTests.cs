using System.Collections.Generic;
using Echelon.Unity.UI.Battle;
using NUnit.Framework;
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

            Assert.That(
                BattleUiText.TopBar(state),
                Is.EqualTo("02:05   2.0x   승점 12 : 7   남은 07:54   전력 75% : 50%   " +
                           "일시정지   지휘 용량 1.5 / 4  (대기 2)"));
            state.Deploying = true;
            StringAssert.Contains("배치 단계", BattleUiText.TopBar(state));
            StringAssert.DoesNotContain("일시정지", BattleUiText.TopBar(state));
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

            string text = BattleUiText.Selection(new[] { unit });
            StringAssert.Contains("전투력 62%   사기 81   통신 두절", text);
            StringAssert.Contains("대형 쐐기   이동 방식 교대 약진   사격 중지", text);
            StringAssert.Contains("자세 지뢰 매설 중   임무 지뢰 매설", text);
            StringAssert.Contains("분리됨", text);
            StringAssert.Contains("지휘 공백", text);
            StringAssert.Contains("대형 전환 중", text);
            StringAssert.Contains("\uBCF4\uBCD1 탑승", text);
            StringAssert.Contains("탄약 44%", text);
            StringAssert.Contains("지뢰 지대 2개 남음, 진지 1/3", text);
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
        public void FireHotkeyCycleAndMissionTextMatchGodotSemantics()
        {
            BattleHud hud = BattleHud.Create(CanvasRect);
            hud.CycleFireMode(false);
            Assert.That(hud.FireAsset, Is.EqualTo(BattleFireAsset.Mortar));
            hud.CycleFireMode(false);
            Assert.That(hud.FireAsset, Is.EqualTo(BattleFireAsset.Artillery));
            hud.CycleFireMode(false);
            Assert.That(hud.FireAsset, Is.Null);

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
    }
}
