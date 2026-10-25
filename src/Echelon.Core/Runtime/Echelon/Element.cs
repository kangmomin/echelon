using System;
using System.Collections.Generic;
using System.Globalization;

namespace Echelon.Core
{
    public enum ElementState
    {
        OK,
        DAMAGED,
        DESTROYED,
        ABANDONED
    }

    // 고정익 상태: 출격 준비 → 비행 → 귀환 → 재무장(지도 밖) → 출격 준비.
    public enum AirState
    {
        READY,
        AIRBORNE,
        RETURNING,
        REARMING
    }

    public sealed class Element
    {
        public const double TurnRateVehicle = Math.PI / 3.0;
        public const double TurnRateFoot = Math.PI;
        public const double TurnRateAir = Math.PI * 0.75;
        public const double TurnRatePlane = 0.4;
        public const double AltitudeLowM = 25.0;
        public const double AltitudeHighM = 90.0;

        public static readonly string[] StateNames = { "정상", "손상", "격파", "유기" };

        public int Id { get; set; }
        public ElementDef Def { get; set; } = null!;
        public string Side { get; set; } = "blue";
        public Platoon? Platoon { get; set; }
        public int IndexInPlatoon { get; set; }
        public SimVec2 Pos { get; set; } = SimVec2.Zero;
        public SimVec2 PrevPos { get; set; } = SimVec2.Zero;
        public double Heading { get; set; }
        public double PrevHeading { get; set; }
        public SimVec2 Velocity { get; set; } = SimVec2.Zero;
        public ElementState State { get; set; } = ElementState.OK;
        public string DamageKind { get; set; } = "";
        public double Hp { get; set; } = 100.0;
        public double Suppression { get; set; }
        public Dictionary<string, int> Ammo { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, double> Reload { get; set; } = new Dictionary<string, double>();
        public int TargetId { get; set; } = -1;
        public SimVec2 MoveTarget { get; set; } = SimVec2.Zero;
        public bool HasMoveTarget { get; set; }
        public double RestHeading { get; set; }
        public List<SimVec2> LocalPath { get; set; } = new List<SimVec2>();
        public int LocalPathIndex { get; set; }
        public GridPathfinder.PathRequest? PathRequest { get; set; }
        public int StuckTicks { get; set; }
        public int StragglerTicks { get; set; }
        public Element? MountedIn { get; set; }
        public List<Element> Passengers { get; set; } = new List<Element>();
        public bool OnMap { get; set; } = true;
        public int StationaryTicks { get; set; }
        public long LastFiredTick { get; set; } = -1000000;
        public long LastHitTick { get; set; } = -1000000;
        public int Kills { get; set; }
        public int KilledBy { get; set; } = -1;
        public double SupplyLeft { get; set; }
        public double RepairSec { get; set; }
        public long DestroyedTick { get; set; } = -1;
        public AirState Air { get; set; } = AirState.READY;
        public long AirReadyTick { get; set; }
        public double FuelSec { get; set; }
        public SimVec2 AirExit { get; set; } = SimVec2.Zero;

        public bool IsAlive()
        {
            return State == ElementState.OK || State == ElementState.DAMAGED;
        }

        public bool IsActive()
        {
            return IsAlive() && OnMap;
        }

        public bool IsMounted()
        {
            return MountedIn != null;
        }

        public bool IsInfantry()
        {
            return Def.ElementClass == "inf_squad";
        }

        public bool IsVehicle()
        {
            return !IsInfantry();
        }

        public bool IsAir()
        {
            return Def.ElementClass == "heli" || Def.ElementClass == "plane";
        }

        public bool IsFixedWing()
        {
            return Def.ElementClass == "plane";
        }

        public string TargetKind()
        {
            if (IsAir() && IsAlive())
            {
                return "air";
            }

            return IsInfantry() ? "infantry" : "vehicle";
        }

        public double AltitudeM()
        {
            if (!IsAir() || !IsAlive())
            {
                return 0.0;
            }

            if (IsFixedWing())
            {
                return Def.AltitudeM;
            }

            if (Platoon != null && Platoon.Sop.Altitude == "high")
            {
                return AltitudeHighM;
            }

            return AltitudeLowM;
        }

        public string Mobility()
        {
            if (IsAir())
            {
                return Terrain.MobilityAir;
            }

            return IsInfantry() ? Terrain.MobilityFoot : Terrain.MobilityVehicle;
        }

        public string ElementClass()
        {
            return Def.ElementClass;
        }

        public string DisplayName()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} #{1}", Def.DisplayName, Id);
        }

        public bool CanMove()
        {
            return IsAlive() && !(State == ElementState.DAMAGED && DamageKind == "mobility" && IsInfantry());
        }

        public double MaxSpeedMps(Terrain terrain)
        {
            int type = terrain.TypeAt(Pos);
            double kmh;
            if (type == (int)Terrain.Type.ROAD && IsVehicle())
            {
                kmh = Def.SpeedRoadKmh;
            }
            else
            {
                kmh = Def.SpeedOffroadKmh * terrain.SpeedFactor(type, Mobility());
            }

            if (State == ElementState.DAMAGED && DamageKind == "mobility")
            {
                kmh *= 0.5;
            }

            if (Suppression >= 50.0)
            {
                kmh *= 0.7;
            }

            return kmh / 3.6 * SimConst.MovementSpeedScale;
        }

        public double BaseSpeedMps()
        {
            double kmh = Def.SpeedOffroadKmh;
            if (State == ElementState.DAMAGED && DamageKind == "mobility")
            {
                kmh *= 0.5;
            }

            return kmh / 3.6 * SimConst.MovementSpeedScale;
        }

        public double TurnRate()
        {
            // 선회도 이동 속도와 같은 배율로 압축한다. 속도만 올리면 회전 반경이 커져 경유점 주위를 맴돈다.
            double rate = IsFixedWing() ? TurnRatePlane : IsAir() ? TurnRateAir : IsInfantry() ? TurnRateFoot : TurnRateVehicle;
            return rate * SimConst.MovementSpeedScale;
        }

        public void TurnToward(double targetHeading, double dt)
        {
            double difference = SimMath.AngleDifference(Heading, targetHeading);
            double maximumTurn = TurnRate() * dt;
            Heading = SimMath.Wrap(Heading + SimMath.Clamp(difference, -maximumTurn, maximumTurn), -Math.PI, Math.PI);
        }

        public void ClearLocalPath()
        {
            LocalPath = new List<SimVec2>();
            LocalPathIndex = 0;
            if (PathRequest != null)
            {
                PathRequest.Cancelled = true;
            }

            PathRequest = null;
        }

        public double Strength()
        {
            if (!IsAlive())
            {
                return 0.0;
            }

            if (IsInfantry())
            {
                return SimMath.Clamp(Hp / Math.Max(Def.Hp, 1.0), 0.0, 1.0);
            }

            return State == ElementState.DAMAGED ? 0.6 : 1.0;
        }
    }
}
