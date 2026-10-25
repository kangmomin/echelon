using System.Collections.Generic;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class SimEventsTests
    {
        [Test]
        public void ListenersAreSynchronousAndOrdered()
        {
            SimEvents events = new SimEvents();
            List<string> calls = new List<string>();
            events.AddListener(value => calls.Add($"first:{value.Type}:{events.PendingCount}"));
            events.AddListener(value => calls.Add($"second:{value.Type}:{events.PendingCount}"));
            events.EmitEvent("one", 4);
            events.EmitEvent("two", 5);
            Assert.That(calls, Is.EqualTo(new[] { "first:one:1", "second:one:1", "first:two:2", "second:two:2" }));
            List<SimEvent> drained = events.Drain();
            Assert.That(drained.Count, Is.EqualTo(2));
            Assert.That(drained[0].Type, Is.EqualTo("one"));
            Assert.That(drained[0].Tick, Is.EqualTo(4));
            Assert.That(drained[1].Type, Is.EqualTo("two"));
            Assert.That(drained[1].Tick, Is.EqualTo(5));
        }

        [Test]
        public void QueueTrimsFromTwentyThousandToNewestTenThousand()
        {
            SimEvents events = new SimEvents();
            for (int i = 0; i <= SimEvents.MaxQueue; i++)
            {
                events.EmitEvent("tick", i);
            }

            Assert.That(events.PendingCount, Is.EqualTo(SimEvents.KeepAfterTrim));
            List<SimEvent> drained = events.Drain();
            Assert.That(drained[0].Tick, Is.EqualTo(10001));
            Assert.That(drained[drained.Count - 1].Tick, Is.EqualTo(20000));
        }

        [Test]
        public void DrainDetachesQueueAndClearListenersStopsNotifications()
        {
            SimEvents events = new SimEvents();
            int calls = 0;
            events.AddListener(_ => calls++);
            events.EmitEvent("before", 1);
            List<SimEvent> first = events.Drain();
            events.EmitEvent("after", 2);
            Assert.That(first.Count, Is.EqualTo(1));
            Assert.That(events.PendingCount, Is.EqualTo(1));
            events.ClearListeners();
            events.EmitEvent("silent", 3);
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void PayloadIsSharedWithQueuedEventAndListeners()
        {
            SimEvents events = new SimEvents();
            Dictionary<string, object> payload = new Dictionary<string, object> { ["value"] = 7 };
            SimEvent observed = null!;
            events.AddListener(value => observed = value);
            events.EmitEvent("payload", 9, payload);
            SimEvent queued = events.Drain()[0];
            Assert.That(observed, Is.SameAs(queued));
            Assert.That(queued.Payload, Is.SameAs(payload));
            Assert.That(queued.Payload["value"], Is.EqualTo(7));
        }
    }
}
