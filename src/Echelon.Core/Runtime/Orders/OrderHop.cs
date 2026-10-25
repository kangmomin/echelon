namespace Echelon.Core
{
    public sealed class OrderHop
    {
        public int FromUnitId { get; set; }
        public int ToUnitId { get; set; }
        public double BaseSeconds { get; set; }
        public long StartTick { get; set; } = -1;
        public long ArriveTick { get; set; } = -1;
        public int Relays { get; set; }
    }
}
