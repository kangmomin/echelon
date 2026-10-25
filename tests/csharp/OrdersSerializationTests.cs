using System;
using Echelon.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class OrdersSerializationTests
    {
        [Test]
        public void TypeAndStateNamesPreserveGodotOrdering()
        {
            Assert.That(Order.TypeNames, Is.EqualTo(Enum.GetNames(typeof(OrderType))));
            Assert.That(Order.StateNames, Is.EqualTo(Enum.GetNames(typeof(OrderState))));
            Assert.That((int)OrderType.LAY_MINES, Is.EqualTo(18));
            Assert.That((int)OrderState.CANCELLED, Is.EqualTo(5));
        }

        [Test]
        public void JsonLogRoundTripPreservesOriginalSchemaAndDeepClonesParams()
        {
            Order order = Order.Make(OrderType.ATTACK_MOVE, 9, new JObject
            {
                ["append"] = true,
                ["nested"] = new JObject { ["value"] = 1 }
            });
            order.Id = 42;
            order.TickIssued = 123;
            order.Issuer = "ai";
            order.IssuerUnitId = 7;
            order.Bypass = true;
            order.SetPos(new SimVec2(12.5f, -8.25f));
            order.SetFacing(1.75);
            order.State = OrderState.EXECUTING;
            order.ParentOrderId = 4;
            order.Cost = 2.0;
            order.BlockedSinceTick = 100;
            order.ReceivedTick = 120;
            order.Hops.Add(new OrderHop { FromUnitId = 7, ToUnitId = 9, ArriveTick = 999 });

            JObject data = order.ToJObject();
            Assert.That(data.Count, Is.EqualTo(8));
            Assert.That(data["params"], Is.Not.SameAs(order.Params));
            ((JObject)order.Params["nested"]!)["value"] = 99;
            Assert.That(((JObject)((JObject)data["params"]!)["nested"]!)["value"]!.Value<int>(), Is.EqualTo(1));

            Order restored = Order.FromJObject(JObject.Parse(data.ToString()));
            Assert.That(restored.Id, Is.EqualTo(42));
            Assert.That(restored.TickIssued, Is.EqualTo(123));
            Assert.That(restored.Issuer, Is.EqualTo("ai"));
            Assert.That(restored.IssuerUnitId, Is.EqualTo(7));
            Assert.That(restored.TargetUnitId, Is.EqualTo(9));
            Assert.That(restored.Type, Is.EqualTo(OrderType.ATTACK_MOVE));
            Assert.That(restored.Bypass, Is.True);
            Assert.That(restored.GetPos(), Is.EqualTo(new SimVec2(12.5f, -8.25f)));
            Assert.That(restored.GetFacing(), Is.EqualTo(1.75));
            Assert.That(restored.Params["append"]!.Value<bool>(), Is.True);
        }

        [Test]
        public void PositionFacingAndEtaAccessorsMatchOrderContract()
        {
            Order order = Order.Make(OrderType.MOVE, 3);
            order.TickIssued = 50;
            Assert.That(order.HasPos(), Is.False);
            Assert.That(order.GetPos(), Is.EqualTo(SimVec2.Zero));
            Assert.That(double.IsNaN(order.GetFacing()), Is.True);
            Assert.That(BitConverter.DoubleToInt64Bits(order.GetFacing()),
                Is.EqualTo(unchecked((long)0x7ff8000000000000UL)));
            Assert.That(order.EtaTick(), Is.EqualTo(50));

            order.SetPos(new SimVec2(100.0f, 200.0f));
            order.SetFacing(-0.5);
            order.Hops.Add(new OrderHop { ArriveTick = 150 });
            order.Hops.Add(new OrderHop { ArriveTick = 250 });
            Assert.That(order.HasPos(), Is.True);
            Assert.That(order.GetPos(), Is.EqualTo(new SimVec2(100.0f, 200.0f)));
            Assert.That(order.GetFacing(), Is.EqualTo(-0.5));
            Assert.That(order.EtaTick(), Is.EqualTo(250));
        }

        [Test]
        public void DeserializationUsesDefaultsTruncatesNumbersAndRejectsUnknownType()
        {
            Order restored = Order.FromJObject(new JObject
            {
                ["id"] = 12.9,
                ["tick"] = 99.8,
                ["type"] = "NOT_AN_ORDER",
                ["params"] = new JArray(1, 2),
                ["bypass"] = "true"
            });

            Assert.That(restored.Id, Is.EqualTo(12));
            Assert.That(restored.TickIssued, Is.EqualTo(99));
            Assert.That(restored.Type, Is.EqualTo(OrderType.MOVE));
            Assert.That(restored.Issuer, Is.EqualTo("player"));
            Assert.That(restored.IssuerUnitId, Is.EqualTo(-1));
            Assert.That(restored.TargetUnitId, Is.EqualTo(-1));
            Assert.That(restored.Params.Count, Is.EqualTo(0));
            Assert.That(restored.Bypass, Is.False);
        }

        [Test]
        public void MovementEngineerAndFinishedClassificationMatchesOriginalLists()
        {
            foreach (OrderType type in Enum.GetValues(typeof(OrderType)))
            {
                Order order = Order.Make(type, 1);
                bool movement = type == OrderType.MOVE || type == OrderType.FAST_MOVE ||
                    type == OrderType.ATTACK_MOVE || type == OrderType.SEIZE || type == OrderType.DEFEND ||
                    type == OrderType.WITHDRAW || type == OrderType.LAY_MINES || type == OrderType.CLEAR_MINES ||
                    type == OrderType.FORTIFY || type == OrderType.AIRDROP;
                bool engineer = type == OrderType.LAY_MINES || type == OrderType.CLEAR_MINES || type == OrderType.FORTIFY;
                Assert.That(order.IsMovement(), Is.EqualTo(movement), type.ToString());
                Assert.That(order.IsEngineer(), Is.EqualTo(engineer), type.ToString());
            }

            Order finished = Order.Make(OrderType.MOVE, 1);
            finished.State = OrderState.DONE;
            Assert.That(finished.IsFinished(), Is.True);
            finished.State = OrderState.CANCELLED;
            Assert.That(finished.IsFinished(), Is.True);
            finished.State = OrderState.RECEIVED;
            Assert.That(finished.IsFinished(), Is.False);
        }
    }
}
