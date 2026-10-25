using Echelon.Unity.UI.Battle;
using NUnit.Framework;
using UnityEngine;

namespace Echelon.Unity.Tests.BattleUi
{
    public sealed class NotificationAndEventTests : BattleUiTestFixture
    {
        [Test]
        public void FeedKeepsNewestEightAndFadesAfterTenSeconds()
        {
            var state = new NotificationFeedState();
            for (int i = 1; i <= 9; i++)
                state.Push(i.ToString(), Color.white);

            Assert.That(state.Entries, Has.Count.EqualTo(8));
            Assert.That(state.Entries[0].Text, Is.EqualTo("2"));
            Assert.That(state.Entries[7].Text, Is.EqualTo("9"));
            state.Advance(10f);
            Assert.That(state.Entries[0].Alpha, Is.EqualTo(1f));
            state.Advance(0.75f);
            Assert.That(state.Entries[0].Alpha, Is.EqualTo(0.5f).Within(0.001f));
            state.Advance(0.76f);
            Assert.That(state.Entries, Is.Empty);
        }

        [Test]
        public void FeedComponentSynchronizesLabelsAndAlpha()
        {
            NotificationFeed feed = NotificationFeed.Create(CanvasRect);
            feed.Push("첫 알림", BattleEventText.Info);
            feed.Push("둘째 알림", BattleEventText.Bad);
            Assert.That(feed.GetLabel(0).text, Is.EqualTo("첫 알림"));
            feed.Advance(10.75f);
            Assert.That(feed.GetLabel(0).color.a, Is.EqualTo(0.5f).Within(0.001f));
            feed.Advance(1f);
            Assert.That(feed.Count, Is.Zero);
        }

        [Test]
        public void EventMapperHandlesGlobalFriendlyAndEnemyResults()
        {
            BattleNotification point = BattleEventText.Describe(
                new BattleEventData { Type = "point_captured", Side = "red", Name = "다리" },
                "blue").Value;
            Assert.That(point.Text, Is.EqualTo("적: 다리 점령"));
            Assert.That(point.Color, Is.EqualTo(BattleEventText.Bad));

            BattleNotification ended = BattleEventText.Describe(
                new BattleEventData { Type = "battle_ended", Winner = "blue" },
                "blue").Value;
            Assert.That(ended.Text, Is.EqualTo("전투 종료 — 승리"));
            BattleNotification shotDown = BattleEventText.Describe(
                new BattleEventData { Type = "cas_shot_down", Side = "red" },
                "blue").Value;
            Assert.That(shotDown.Text, Is.EqualTo("아군 방공이 적 항공기를 격추"));
        }

        [Test]
        public void EventMapperSuppressesFollowTrafficAndFormatsOrders()
        {
            Assert.That(
                BattleEventText.Describe(
                    new BattleEventData
                    {
                        Type = "arrived",
                        Side = "blue",
                        HasUnit = true,
                        FollowsParent = true,
                        UnitName = "1소대"
                    },
                    "blue"),
                Is.Null);
            Assert.That(
                BattleEventText.Describe(
                    new BattleEventData
                    {
                        Type = "order_received",
                        Side = "blue",
                        HasUnit = true,
                        OrderTypeName = "FOLLOW",
                        UnitName = "1소대"
                    },
                    "blue"),
                Is.Null);

            BattleNotification arrived = BattleEventText.Describe(
                new BattleEventData
                {
                    Type = "arrived",
                    Side = "blue",
                    HasUnit = true,
                    UnitName = "1소대",
                    OrderType = BattleCommandType.AttackMove
                },
                "blue").Value;
            Assert.That(arrived.Text, Is.EqualTo("1소대: 목표 도착 (공격 이동)"));
            Assert.That(
                BattleEventText.Describe(
                    new BattleEventData { Type = "order_waiting_capacity", Side = "red" },
                    "blue"),
                Is.Null);
        }

        [Test]
        public void EventMapperFormatsCommsFireSupplyAndRefusal()
        {
            Assert.That(
                BattleEventText.Describe(
                    new BattleEventData
                    {
                        Type = "comms_changed",
                        Side = "blue",
                        UnitName = "2소대",
                        CommsState = BattleCommsState.Cut
                    },
                    "blue").Value.Text,
                Is.EqualTo("2소대: 통신 두절"));
            Assert.That(
                BattleEventText.Describe(
                    new BattleEventData
                    {
                        Type = "fire_mission",
                        Side = "blue",
                        Asset = BattleFireAsset.Artillery,
                        Phase = BattleFirePhase.Effect
                    },
                    "blue").Value.Text,
                Is.EqualTo("포병: 효력사 시작"));
            Assert.That(
                BattleEventText.Describe(
                    new BattleEventData
                    {
                        Type = "supply_dispatched",
                        Side = "blue",
                        UnitName = "보급소",
                        TargetUnitName = "3소대"
                    },
                    "blue").Value.Text,
                Is.EqualTo("보급소: 3소대 로 보급 출발"));
            Assert.That(
                BattleEventText.Describe(
                    new BattleEventData
                    {
                        Type = "order_refused",
                        Side = "blue",
                        UnitName = "공병",
                        Reason = "no_capacity"
                    },
                    "blue").Value.Text,
                Is.EqualTo("공병: 남은 지뢰·진지 자재가 없습니다"));
        }
    }
}
