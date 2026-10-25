using System.Collections.Generic;
using Echelon.Unity.UI.Battle;
using NUnit.Framework;
using UnityEngine;

namespace Echelon.Unity.Tests.BattleUi
{
    public sealed class DeploymentAndDebugTests : BattleUiTestFixture
    {
        [Test]
        public void PlacementUsesGodotSpacingAndRightVector()
        {
            var units = new[]
            {
                new DeploymentUnitState(1, 0f),
                new DeploymentUnitState(2, 0f),
                new DeploymentUnitState(3, 0f)
            };
            IReadOnlyList<DeploymentPlacementIntent> intents = DeploymentPlacement.CreateIntents(
                units,
                new Vector2(1000f, 2000f),
                Mathf.PI * 0.5f);

            Assert.That(intents, Has.Count.EqualTo(3));
            Assert.That(intents[0].Position.x, Is.EqualTo(1000f).Within(0.001f));
            Assert.That(intents[0].Position.y, Is.EqualTo(2350f).Within(0.001f));
            Assert.That(intents[1].Position.y, Is.EqualTo(2000f).Within(0.001f));
            Assert.That(intents[2].Position.y, Is.EqualTo(1650f).Within(0.001f));
        }

        [Test]
        public void DeploymentPanelOnlyEmitsWhileDeploymentIsActive()
        {
            DeploymentPanel panel = DeploymentPanel.Create(CanvasRect);
            int starts = 0;
            IReadOnlyList<DeploymentPlacementIntent> placements = null;
            panel.StartRequested += () => starts++;
            panel.PlacementRequested += value => placements = value;
            var units = new[] { new DeploymentUnitState(7, 0.25f) };

            panel.RequestStart();
            panel.RequestPlacement(units, Vector2.zero);
            Assert.That(starts, Is.Zero);
            Assert.That(placements, Is.Null);
            panel.SetDeploymentState(true, false);
            panel.RequestStart();
            Assert.That(starts, Is.Zero);
            panel.SetDeploymentState(true);
            panel.RequestStart();
            panel.RequestPlacement(units, new Vector2(10f, 20f));
            Assert.That(starts, Is.EqualTo(1));
            Assert.That(placements, Has.Count.EqualTo(1));
            Assert.That(placements[0].HeadingRadians, Is.EqualTo(0.25f));
        }

        [Test]
        public void ZoneDataExposesRectangleCornersEntriesAndContainment()
        {
            var zone = new DeploymentZoneData
            {
                Zone = new Rect(100f, 200f, 400f, 300f),
                MapSize = new Vector2(2000f, 1000f),
                EntryPoints = new List<Vector2> { new Vector2(120f, 220f) }
            };
            Assert.That(zone.Corners, Has.Count.EqualTo(4));
            Assert.That(zone.Contains(new Vector2(150f, 250f)), Is.True);
            Assert.That(zone.Contains(new Vector2(50f, 250f)), Is.False);
            Assert.That(zone.LabelPosition, Is.EqualTo(new Vector2(300f, 500f)));
        }

        [Test]
        public void ZoneViewBuildsBorderFillAndEntryMarkers()
        {
            DeploymentZoneView view = DeploymentZoneView.Create(CanvasRect);
            view.SetScreenGeometry(
                new[]
                {
                    new Vector2(100f, 100f),
                    new Vector2(400f, 100f),
                    new Vector2(400f, 300f),
                    new Vector2(100f, 300f)
                },
                new[] { new Vector2(120f, 120f) },
                new Vector2(240f, 310f));

            Assert.That(view.PointCount, Is.EqualTo(4));
            Assert.That(view.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true), Has.Length.EqualTo(2));
            view.Clear();
            Assert.That(view.PointCount, Is.Zero);
            Assert.That(view.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true), Is.Empty);
        }

        [Test]
        public void DiagnosticsFormatAndHiddenOverlayBehaviorMatchGodot()
        {
            string text = BattleDebugText.Format(new BattleDiagnostics
            {
                FramesPerSecond = 60,
                Tick = 1234,
                TicksThisFrame = 2,
                AdvanceMicroseconds = 900,
                PeakTickMicroseconds = 450,
                ElementCount = 100,
                UnitCount = 20,
                PendingPathRequests = 3,
                ProcessedPathRequests = 50,
                CameraHeight = 800,
                SymbolLevel = 1
            });
            StringAssert.Contains("FPS 60   tick 1234   ticks/frame 2", text);
            StringAssert.Contains("경로 요청 대기 3 (누적 50)", text);
            StringAssert.Contains("높이 800 m   부호 단계 1", text);

            BattleDebugOverlay overlay = BattleDebugOverlay.Create(CanvasRect);
            overlay.SetLines(new[] { "숨김" });
            Assert.That(overlay.GetText(), Is.Empty);
            overlay.Toggle();
            overlay.SetLines(new[] { "표시" });
            Assert.That(overlay.IsVisible, Is.True);
            Assert.That(overlay.GetText(), Is.EqualTo("표시"));
        }
    }
}
