using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public sealed class SimEvent
    {
        public SimEvent(string type, long tick, Dictionary<string, object> payload)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Tick = tick;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        public string Type { get; }
        public long Tick { get; }
        public Dictionary<string, object> Payload { get; }
    }
}
