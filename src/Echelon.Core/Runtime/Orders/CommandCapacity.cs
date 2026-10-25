using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public sealed class CommandCapacity
    {
        private readonly List<Order> _queue = new List<Order>();
        private double _tokens = -1.0;

        public IReadOnlyList<Order> Queue => _queue;
        public double RawTokens => _tokens;
        public int QueueCount => _queue.Count;

        public double CapacityNow(double maximum)
        {
            return _tokens < 0.0 ? maximum : _tokens;
        }

        public double EnsureCapacity(double maximum)
        {
            if (_tokens < 0.0)
            {
                _tokens = maximum;
            }

            return _tokens;
        }

        public bool Submit(Order order, double maximum)
        {
            EnsureCapacity(maximum);
            if (order.Cost > 0.0 && (_tokens < order.Cost || _queue.Count != 0))
            {
                order.State = OrderState.QUEUED;
                _queue.Add(order);
                return false;
            }

            _tokens -= order.Cost;
            return true;
        }

        public List<Order> RefillAndDequeue(double maximum, double deltaSeconds)
        {
            EnsureCapacity(maximum);
            _tokens = Math.Min(maximum, _tokens + maximum / 60.0 * deltaSeconds);
            List<Order> ready = new List<Order>();
            while (_queue.Count != 0)
            {
                Order order = _queue[0];
                if (order.State == OrderState.CANCELLED)
                {
                    _queue.RemoveAt(0);
                    continue;
                }

                if (_tokens < order.Cost)
                {
                    break;
                }

                _queue.RemoveAt(0);
                _tokens -= order.Cost;
                ready.Add(order);
            }

            return ready;
        }

        public bool TrySpend(double maximum, double amount)
        {
            EnsureCapacity(maximum);
            if (_tokens < amount)
            {
                return false;
            }

            _tokens -= amount;
            return true;
        }
    }
}
