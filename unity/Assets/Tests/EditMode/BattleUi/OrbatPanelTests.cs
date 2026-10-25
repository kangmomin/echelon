using System.Collections.Generic;
using Echelon.Unity.UI.Battle;
using NUnit.Framework;

namespace Echelon.Unity.Tests.BattleUi
{
    public sealed class OrbatPanelTests : BattleUiTestFixture
    {
        [Test]
        public void TreeShowsHierarchyStrengthCommsAndMission()
        {
            OrbatPanel panel = OrbatPanel.Create(CanvasRect);
            panel.SetRoot(SampleTree());

            Assert.That(panel.VisibleRowCount, Is.EqualTo(5));
            Assert.That(panel.GetStrengthLabel(1).text, Is.Empty);
            Assert.That(panel.GetCommsLabel(1).text, Is.Empty);
            Assert.That(panel.GetStrengthLabel(2).text, Is.EqualTo("75%"));
            Assert.That(panel.GetCommsLabel(2).text, Is.EqualTo("◐"));
            StringAssert.Contains("A중대  · 방어", panel.GetRowButton(2).GetComponentInChildren<TMPro.TextMeshProUGUI>().text);
            StringAssert.Contains("1소대  · 증원 대기", panel.GetRowButton(4).GetComponentInChildren<TMPro.TextMeshProUGUI>().text);
        }

        [Test]
        public void SelectionSupportsAdditiveIntentAndExternalSynchronization()
        {
            OrbatPanel panel = OrbatPanel.Create(CanvasRect);
            panel.SetRoot(SampleTree());
            var intents = new List<OrbatSelectionIntent>();
            panel.SelectionRequested += intents.Add;

            panel.SelectUnit(1, false);
            Assert.That(intents, Has.Count.EqualTo(0));
            panel.SelectUnit(2, false);
            panel.SelectUnit(4, true);
            panel.SelectUnit(4, true);

            Assert.That(intents, Has.Count.EqualTo(3));
            Assert.That(intents[1].SelectedUnitIds, Is.EqualTo(new[] { 2, 4 }));
            Assert.That(panel.SelectedUnitIds, Is.EqualTo(new[] { 2, 4 }));
            panel.ShowSelection(new[] { 5 });
            Assert.That(panel.SelectedUnitIds, Is.EqualTo(new[] { 5 }));
            Assert.That(intents, Has.Count.EqualTo(3));
        }

        [Test]
        public void FoldAndRefreshPreserveSelectionAndFocusIsSeparate()
        {
            OrbatPanel panel = OrbatPanel.Create(CanvasRect);
            panel.SetRoot(SampleTree());
            panel.SelectUnit(4, false);
            int focused = 0;
            panel.FocusRequested += id => focused = id;

            panel.FocusUnit(4);
            panel.ToggleExpanded(2);
            Assert.That(focused, Is.EqualTo(4));
            Assert.That(panel.VisibleRowCount, Is.EqualTo(3));
            Assert.That(panel.SelectedUnitIds, Is.EqualTo(new[] { 4 }));
            panel.SetRoot(SampleTree());
            Assert.That(panel.VisibleRowCount, Is.EqualTo(3));
            panel.ToggleExpanded(2);
            Assert.That(panel.VisibleRowCount, Is.EqualTo(5));
        }

        [Test]
        public void StatusPrecedenceMatchesGodot()
        {
            var unit = new BattleUnitView
            {
                OnMap = false,
                Destroyed = true,
                Leaderless = true,
                Routed = true,
                AwaitingFollow = true,
                Detached = true,
                OrderPending = true,
                MissionType = BattleCommandType.Move,
                Posture = "moving"
            };
            Assert.That(BattleUiText.OrbatStatus(unit), Is.EqualTo("증원 대기"));
            unit.OnMap = true;
            Assert.That(BattleUiText.OrbatStatus(unit), Is.EqualTo("전멸"));
            unit.Destroyed = false;
            Assert.That(BattleUiText.OrbatStatus(unit), Is.EqualTo("지휘 공백"));
            unit.Leaderless = false;
            Assert.That(BattleUiText.OrbatStatus(unit), Is.EqualTo("패주"));
            unit.Routed = false;
            Assert.That(BattleUiText.OrbatStatus(unit), Is.EqualTo("명령 대기"));
        }

        private static OrbatNodeData SampleTree()
        {
            return new OrbatNodeData(
                new BattleUnitView
                {
                    Id = 1,
                    Level = BattleUnitLevel.TaskForce,
                    DisplayName = "제1대대"
                },
                new OrbatNodeData(
                    new BattleUnitView
                    {
                        Id = 2,
                        Level = BattleUnitLevel.Company,
                        DisplayName = "A중대",
                        Strength = 0.75f,
                        Comms = BattleCommsState.Poor,
                        Posture = "defending"
                    },
                    new OrbatNodeData(new BattleUnitView
                    {
                        Id = 3,
                        DisplayName = "중대본부",
                        ShortName = "본부"
                    }),
                    new OrbatNodeData(new BattleUnitView
                    {
                        Id = 4,
                        DisplayName = "1소대 정식명",
                        ShortName = "1소대",
                        OnMap = false
                    })),
                new OrbatNodeData(new BattleUnitView
                {
                    Id = 5,
                    DisplayName = "대대 직할 정찰",
                    ShortName = "정찰"
                }));
        }
    }
}
