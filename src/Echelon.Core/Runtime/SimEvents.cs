using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public sealed class SimEvents
    {
        public const int MaxQueue = 20000;
        public const int KeepAfterTrim = 10000;

        private List<SimEvent> _queue = new List<SimEvent>();
        private readonly List<Action<SimEvent>> _listeners = new List<Action<SimEvent>>();

        public int PendingCount => _queue.Count;

        public void EmitEvent(string type, long tick, Dictionary<string, object>? payload = null)
        {
            SimEvent simEvent = new SimEvent(type, tick, payload ?? new Dictionary<string, object>());
            _queue.Add(simEvent);
            if (_queue.Count > MaxQueue)
            {
                _queue = _queue.GetRange(_queue.Count - KeepAfterTrim, KeepAfterTrim);
            }

            for (int i = 0; i < _listeners.Count; i++)
            {
                _listeners[i](simEvent);
            }
        }

        public void AddListener(Action<SimEvent> listener)
        {
            if (listener == null)
            {
                throw new ArgumentNullException(nameof(listener));
            }

            _listeners.Add(listener);
        }

        public void ClearListeners()
        {
            _listeners.Clear();
        }

        public List<SimEvent> Drain()
        {
            List<SimEvent> drained = _queue;
            _queue = new List<SimEvent>();
            return drained;
        }
    }
}
