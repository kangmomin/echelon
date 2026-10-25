using System.Collections.Generic;
using Echelon.Unity.UI.Aar;
using NUnit.Framework;

namespace Echelon.Unity.Tests.BattleUi
{
    public sealed class AarTests : BattleUiTestFixture
    {
        [Test]
        public void ViewModelMapsAllStatisticsPointsFactorsAndTimeline()
        {
            AarViewData view = AarViewModel.Build(SampleData(), "blue");

            Assert.That(view.Title, Is.EqualTo("사후 검토 — 승리"));
            StringAssert.Contains("판정: 시간 종료(승점)", view.Summary);
            StringAssert.Contains("전투 시간 10:00", view.Summary);
            StringAssert.Contains("첫 교전 00:12", view.Summary);
            StringAssert.Contains("승점 12   ·   남은 전투력 75%", view.SidesText);
            StringAssert.Contains("손실 3 / 20  (전차 2, \uBCF4\uBCD1 \uBD84\uB300 1)   손상 4", view.SidesText);
            StringAssert.Contains("명령 8건 — 평균 지연 6초, 최대 14초", view.SidesText);
            StringAssert.Contains("화력 요청 4   ·   통신 두절 3회   ·   보급 2회", view.SidesText);
            StringAssert.Contains("사격 120발", view.SidesText);
            StringAssert.Contains("다리 — 아군  (청 05:00, 적 02:00 보유)", view.ControlPointsText);
            StringAssert.Contains("손실 교환비 3 : 5", view.FactorsText);
            StringAssert.Contains("명령 지연이 아군 쪽이 8초 짧았다", view.FactorsText);
            StringAssert.Contains("통신 두절이 3회", view.FactorsText);
            StringAssert.Contains("화력 요청 4 : 2", view.FactorsText);
            StringAssert.Contains("00:30", view.TimelineText);
            StringAssert.Contains("청군: 다리 점령", view.TimelineText);
        }

        [Test]
        public void DrawAndRedPlayerResultsAreMappedFromPlayerPerspective()
        {
            AarData draw = SampleData();
            draw.Result.Winner = string.Empty;
            Assert.That(AarViewModel.Build(draw, "blue").Title, Is.EqualTo("사후 검토 — 무승부"));
            AarData loss = SampleData();
            Assert.That(AarViewModel.Build(loss, "red").Title, Is.EqualTo("사후 검토 — 패배"));
            StringAssert.Contains("중립", AarViewModel.Build(SampleData(), "red").ControlPointsText);
        }

        [Test]
        public void ScreenRendersDataAndButtonsOnlyRaiseNavigationRequests()
        {
            AarScreen screen = AarScreen.Create(CanvasRect, SampleData(), "blue");
            int battlefield = 0;
            int menu = 0;
            screen.ViewBattlefieldRequested += () => battlefield++;
            screen.MenuRequested += () => menu++;

            Assert.That(screen.TitleLabel.text, Is.EqualTo("사후 검토 — 승리"));
            StringAssert.Contains("손실 교환비", screen.LeftLabel.text);
            StringAssert.Contains("청군: 다리 점령", screen.TimelineLabel.text);
            screen.ViewBattlefieldButton.onClick.Invoke();
            screen.MenuButton.onClick.Invoke();
            Assert.That(battlefield, Is.EqualTo(1));
            Assert.That(menu, Is.EqualTo(1));
            Assert.That(screen.gameObject.activeSelf, Is.True);
        }

        private static AarData SampleData()
        {
            return new AarData
            {
                Result = new AarResultData { Winner = "blue", Reason = "time" },
                DurationSeconds = 600f,
                FirstContactSeconds = 12.2f,
                Sides = new List<AarSideData>
                {
                    new AarSideData
                    {
                        Side = "blue",
                        Name = "제1대대",
                        VictoryPoints = 12f,
                        ValueRatio = 0.75f,
                        LossesTotal = 3,
                        Elements = 20,
                        Losses = new List<AarLossData>
                        {
                            new AarLossData("tank", 2),
                            new AarLossData("inf_squad", 1)
                        },
                        Damaged = 4,
                        Orders = 8,
                        OrderDelayMean = 6f,
                        OrderDelayMaximum = 14f,
                        FireMissions = 4,
                        CommsCuts = 3,
                        Resupplies = 2,
                        Shots = 120
                    },
                    new AarSideData
                    {
                        Side = "red",
                        Name = "제2대대",
                        VictoryPoints = 7f,
                        ValueRatio = 0.5f,
                        LossesTotal = 5,
                        Elements = 18,
                        Orders = 9,
                        OrderDelayMean = 14f,
                        OrderDelayMaximum = 25f,
                        FireMissions = 2,
                        CommsCuts = 1,
                        Resupplies = 1,
                        Shots = 90
                    }
                },
                Points = new List<AarControlPointData>
                {
                    new AarControlPointData
                    {
                        Name = "다리",
                        Owner = "blue",
                        HeldBlueSeconds = 300f,
                        HeldRedSeconds = 120f
                    },
                    new AarControlPointData { Name = "언덕", Owner = string.Empty }
                },
                Timeline = new List<AarTimelineEntry>
                {
                    new AarTimelineEntry { Tick = 300, Side = "blue", Text = "청군: 다리 점령" }
                }
            };
        }
    }
}
