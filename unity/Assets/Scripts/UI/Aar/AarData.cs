using System;
using System.Collections.Generic;

namespace Echelon.Unity.UI.Aar
{
    [Serializable]
    public sealed class AarData
    {
        public AarResultData Result { get; set; } = new AarResultData();
        public float DurationSeconds { get; set; }
        public float FirstContactSeconds { get; set; } = -1f;
        public float TickRate { get; set; } = 10f;
        public List<AarSideData> Sides { get; set; } = new List<AarSideData>();
        public List<AarControlPointData> Points { get; set; } = new List<AarControlPointData>();
        public List<AarTimelineEntry> Timeline { get; set; } = new List<AarTimelineEntry>();

        public AarSideData FindSide(string side)
        {
            if (Sides == null)
                return null;
            foreach (AarSideData candidate in Sides)
            {
                if (candidate != null && candidate.Side == side)
                    return candidate;
            }
            return null;
        }
    }

    [Serializable]
    public sealed class AarResultData
    {
        public string Winner { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    [Serializable]
    public sealed class AarSideData
    {
        public string Side { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public float VictoryPoints { get; set; }
        public float ValueRatio { get; set; } = 1f;
        public int LossesTotal { get; set; }
        public int Elements { get; set; }
        public List<AarLossData> Losses { get; set; } = new List<AarLossData>();
        public int Damaged { get; set; }
        public float OrderDelayMean { get; set; }
        public float OrderDelayMaximum { get; set; }
        public int Orders { get; set; }
        public int FireMissions { get; set; }
        public int CommsCuts { get; set; }
        public int Resupplies { get; set; }
        public int Shots { get; set; }
    }

    [Serializable]
    public sealed class AarLossData
    {
        public string ClassName { get; set; } = string.Empty;
        public int Count { get; set; }

        public AarLossData()
        {
        }

        public AarLossData(string className, int count)
        {
            ClassName = className ?? string.Empty;
            Count = count;
        }
    }

    [Serializable]
    public sealed class AarControlPointData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public float HeldBlueSeconds { get; set; }
        public float HeldRedSeconds { get; set; }
    }

    [Serializable]
    public sealed class AarTimelineEntry
    {
        public long Tick { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}
