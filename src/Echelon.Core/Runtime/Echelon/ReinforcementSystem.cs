using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public interface IReinforcementBattle
    {
        SimWorld World { get; }
        Terrain Terrain { get; }
        TaskForce? GetTaskForce(string side);
        void PlaceElements(Platoon platoon);
    }

    public sealed class ReinforcementSystem : SimSystem
    {
        public static readonly double[] WaveSec = { 0.0, 600.0, 1200.0 };
        public const double EntryInsetM = 120.0;
        public const double ColumnGapM = 90.0;

        private static readonly string[] SideNames = { "blue", "red" };
        private readonly IReinforcementBattle _battle;

        public ReinforcementSystem(IReinforcementBattle battle)
        {
            _battle = battle ?? throw new ArgumentNullException(nameof(battle));
        }

        public Dictionary<string, int> ArrivedWaves { get; } = new Dictionary<string, int>
        {
            ["blue"] = 0,
            ["red"] = 0
        };

        public override string GetSystemName()
        {
            return "reinforcement";
        }

        public override long StateHash()
        {
            long hash = SimHash.Append(SimHash.Initial, ArrivedWaves["blue"]);
            return SimHash.Append(hash, ArrivedWaves["red"]);
        }

        public static int WaveTick(int wave)
        {
            int index = Math.Max(0, Math.Min(wave, WaveSec.Length - 1));
            return (int)Math.Round(WaveSec[index] * SimConst.TickRate, MidpointRounding.AwayFromZero);
        }

        public override void Step(SimWorld world)
        {
            if (world.Tick % SimConst.TickRate != 0)
            {
                return;
            }

            foreach (string side in SideNames)
            {
                int next = ArrivedWaves[side] + 1;
                if (next >= WaveSec.Length || world.Tick < WaveTick(next))
                {
                    continue;
                }

                ArrivedWaves[side] = next;
                BringIn(side, next, world.Tick);
            }
        }

        public void BringIn(string side, int wave, long tick)
        {
            TaskForce? taskForce = _battle.GetTaskForce(side);
            if (taskForce == null)
            {
                return;
            }

            SimVec2 inward = (_battle.Terrain.SizeM * 0.5 - taskForce.EntryPos).Normalized();
            double heading = FormationSolver.HeadingFromVector(inward);
            int slot = 0;
            foreach (Company company in taskForce.Companies)
            {
                if (company.OnMap || company.Phase != wave)
                {
                    continue;
                }

                SimVec2 position = EntryPoint(taskForce, inward, slot);
                slot += 3;
                company.OnMap = true;
                company.Guide.Reset(position, heading);
                foreach (Unit child in company.ChildrenUnits())
                {
                    if (child is Platoon platoon && platoon.Phase == wave)
                    {
                        Place(platoon, position, heading);
                    }
                }

                Announce(company, tick);
            }

            foreach (Platoon platoon in taskForce.AllPlatoons())
            {
                if (platoon.OnMap || platoon.Phase != wave)
                {
                    continue;
                }

                SimVec2 position = EntryPoint(taskForce, inward, slot);
                ++slot;
                Place(platoon, position, heading);
                Announce(platoon, tick);
            }
        }

        private SimVec2 EntryPoint(TaskForce taskForce, SimVec2 inward, int slot)
        {
            SimVec2 position = taskForce.EntryPos + inward * (EntryInsetM + slot * ColumnGapM);
            return _battle.Terrain.NearestPassable(
                _battle.Terrain.ClampToMap(position, 30.0), Terrain.MobilityVehicle);
        }

        private void Place(Platoon platoon, SimVec2 position, double heading)
        {
            platoon.OnMap = true;
            foreach (Element element in platoon.Elements)
            {
                element.OnMap = true;
            }

            platoon.Guide.Reset(position, heading);
            _battle.PlaceElements(platoon);
            platoon.Posture = "idle";
        }

        private void Announce(Unit unit, long tick)
        {
            _battle.World.Events.EmitEvent("reinforcement_arrived", tick, new Dictionary<string, object>
            {
                ["unit"] = unit.Id,
                ["side"] = unit.Side,
                ["pos"] = unit.Guide.Pos
            });
        }
    }
}
