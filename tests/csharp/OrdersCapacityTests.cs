using System.Collections.Generic;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class OrdersCapacityTests
    {
        [Test]
        public void SixMissionTokensAreImmediateAndLaterOrdersWaitFifo()
        {
            CommandCapacity capacity = new CommandCapacity();
            List<Order> orders = new List<Order>();
            for (int i = 0; i < 8; ++i)
            {
                Order order = Order.Make(OrderType.MOVE, i + 1);
                order.Cost = OrderSystem.CostMission;
                orders.Add(order);
                bool immediate = capacity.Submit(order, 6.0);
                Assert.That(immediate, Is.EqualTo(i < 6));
            }

            Assert.That(orders[6].State, Is.EqualTo(OrderState.QUEUED));
            Assert.That(capacity.QueueCount, Is.EqualTo(2));
            Assert.That(capacity.RawTokens, Is.EqualTo(0.0).Within(1e-9));

            List<Order> ready = new List<Order>();
            for (int i = 0; i < 110; ++i)
            {
                ready.AddRange(capacity.RefillAndDequeue(6.0, SimConst.TickDt));
            }

            Assert.That(ready, Is.EqualTo(new[] { orders[6] }));
            Assert.That(capacity.Queue, Is.EqualTo(new[] { orders[7] }));
        }

        [Test]
        public void QueueIsHeadOfLineBlockingEvenWhenLaterOrderWouldFit()
        {
            CommandCapacity capacity = new CommandCapacity();
            Assert.That(capacity.TrySpend(6.0, 6.0), Is.True);
            Order head = Order.Make(OrderType.MOVE, 1);
            head.Cost = 2.0;
            Order tail = Order.Make(OrderType.SET_SOP, 2);
            tail.Cost = 0.5;
            Assert.That(capacity.Submit(head, 6.0), Is.False);
            Assert.That(capacity.Submit(tail, 6.0), Is.False);

            Assert.That(capacity.RefillAndDequeue(6.0, 10.0), Is.Empty);
            Assert.That(capacity.QueueCount, Is.EqualTo(2));
            Assert.That(capacity.RefillAndDequeue(6.0, 10.0), Is.EqualTo(new[] { head }));
            Assert.That(capacity.Queue, Is.EqualTo(new[] { tail }));
            Assert.That(capacity.RefillAndDequeue(6.0, 10.0), Is.EqualTo(new[] { tail }));
        }

        [Test]
        public void ZeroCostOrdersBypassQueueAndCancelledQueueEntriesAreDropped()
        {
            CommandCapacity capacity = new CommandCapacity();
            Assert.That(capacity.TrySpend(6.0, 6.0), Is.True);
            Order waiting = Order.Make(OrderType.MOVE, 1);
            waiting.Cost = 1.0;
            Assert.That(capacity.Submit(waiting, 6.0), Is.False);

            Order free = Order.Make(OrderType.CANCEL, 2);
            free.Cost = OrderSystem.CostFree;
            Assert.That(capacity.Submit(free, 6.0), Is.True);
            Assert.That(capacity.QueueCount, Is.EqualTo(1));
            Assert.That(capacity.RawTokens, Is.EqualTo(0.0).Within(1e-9));

            waiting.State = OrderState.CANCELLED;
            Assert.That(capacity.RefillAndDequeue(6.0, 0.0), Is.Empty);
            Assert.That(capacity.QueueCount, Is.EqualTo(0));
        }

        [Test]
        public void SpendingFailureIsAtomicAndCapacityReductionClampsTokens()
        {
            CommandCapacity capacity = new CommandCapacity();
            Assert.That(capacity.CapacityNow(6.0), Is.EqualTo(6.0));
            Assert.That(capacity.TrySpend(6.0, 2.0), Is.True);
            Assert.That(capacity.TrySpend(6.0, 5.0), Is.False);
            Assert.That(capacity.RawTokens, Is.EqualTo(4.0).Within(1e-9));

            capacity.RefillAndDequeue(3.0, 0.0);
            Assert.That(capacity.RawTokens, Is.EqualTo(3.0).Within(1e-9));
            capacity.RefillAndDequeue(6.0, 60.0);
            Assert.That(capacity.RawTokens, Is.EqualTo(6.0).Within(1e-9));
        }

        [Test]
        public void DelayRoundingUsesGodotHalfAwayFromZeroTicks()
        {
            Assert.That(OrderSystem.SecondsToTicks(15.0), Is.EqualTo(150));
            Assert.That(OrderSystem.SecondsToTicks(1.25), Is.EqualTo(13));
            Assert.That(OrderSystem.SecondsToTicks(1.35), Is.EqualTo(14));
            Assert.That(OrderSystem.SecondsToTicks(0.04), Is.EqualTo(0));
        }
    }
}
