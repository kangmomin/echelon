namespace Echelon.Core
{
    public sealed class CommsLink
    {
        public bool Ok { get; set; }
        public double Quality { get; set; }
        public int Relays { get; set; }
    }

    public readonly struct CommsEdge
    {
        public CommsEdge(int toElementId, double quality)
        {
            ToElementId = toElementId;
            Quality = quality;
        }

        public int ToElementId { get; }
        public double Quality { get; }
    }
}
