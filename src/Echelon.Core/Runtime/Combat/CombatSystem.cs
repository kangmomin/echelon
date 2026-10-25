using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public sealed class CombatSystem : SimSystem
    {
        public const int TargetInterval = 10;
        public const int LosTopChecks = 3;
        public const double MovingShooter = 0.7;
        public const double MovingTarget = 0.85;
        public const double SuppressedAccuracy = 0.7;
        public const double ReformingAccuracy = 0.75;
        public const double BoundingBonus = 1.15;
        public const int DugInTicks = 600;
        public const double DugInCover = 0.7;
        public const double KillProbability = 0.7;
        public const double MarginalProbability = 0.5;
        public const double Overmatch = 1.1;
        public const double Undermatch = 0.9;
        public const double InfantryCasualtyPerDamage = 0.25;
        public const double HeSplashCasualtyPerDamage = 0.06;
        public const double MissSuppression = 0.4;
        public const double VehicleSuppression = 0.5;
        public const double ThreatRadiusM = 100.0;
        public const double ThreatAmount = 1.0;
        public const int ReturnFireTicks = 100;
        public const double MaskRangeM = 250.0;
        public const double MaskHalfWidthM = 7.0;
        public const double MaskedScore = 0.2;
        public const double LongRangeStartM = 1000.0;
        public const double LongRangeEndM = 3000.0;
        public const double LongRangeMultiplier = 0.4;
        public const double NonAaVsAir = 0.3;
        // 고정익은 플레어·채프와 급기동으로 유도 미사일(지대공·공대공)을 절반쯤 따돌린다. 이게 없으면 지도 전체를
        // 덮는 Tor 하나가 출격하자마자 편대를 떨어뜨린다(D-050).
        public const double FixedWingEvasion = 0.5;

        private static readonly Dictionary<string, double> SkillAccuracy = new Dictionary<string, double>
        {
            ["recruit"] = 0.85,
            ["regular"] = 1.0,
            ["veteran"] = 1.1,
            ["elite"] = 1.2
        };
        private static readonly Dictionary<int, double> TargetCoverAccuracy = new Dictionary<int, double>
        {
            [(int)Terrain.Type.FOREST] = 0.75,
            [(int)Terrain.Type.URBAN] = 0.7
        };
        private static readonly Dictionary<int, double> InfantryCover = new Dictionary<int, double>
        {
            [(int)Terrain.Type.FOREST] = 0.6,
            [(int)Terrain.Type.URBAN] = 0.5
        };

        private readonly ICombatBattle _battle;
        private readonly ICombatMorale _morale;

        public CombatSystem(ICombatBattle battle, ICombatMorale? morale = null)
        {
            _battle = battle ?? throw new ArgumentNullException(nameof(battle));
            _morale = morale ?? battle.Morale as ICombatMorale ??
                throw new ArgumentException("Battle morale must implement ICombatMorale.", nameof(battle));
        }

        public int ShotsFired { get; private set; }
        public int Hits { get; private set; }
        public int Kills { get; private set; }

        public override string GetSystemName()
        {
            return "combat";
        }

        public override long StateHash()
        {
            long hash = SimHash.Append(SimHash.Initial, ShotsFired);
            hash = SimHash.Append(hash, Hits);
            return SimHash.Append(hash, Kills);
        }

        public override void Step(SimWorld world)
        {
            double dt = SimConst.TickDt;
            long tick = world.Tick;
            foreach (Element element in _battle.Elements)
            {
                if (!element.IsActive() || element.IsMounted())
                {
                    continue;
                }

                foreach (string weaponId in new List<string>(element.Reload.Keys))
                {
                    element.Reload[weaponId] = Math.Max(0.0, element.Reload[weaponId] - dt);
                }

                if ((element.Id + tick) % TargetInterval == 0)
                {
                    element.TargetId = ChooseTarget(element, tick);
                }

                if (element.TargetId >= 0)
                {
                    TryFire(element, tick);
                }
            }
        }

        public int ChooseTarget(Element element, long tick)
        {
            Platoon? platoon = element.Platoon;
            if (platoon == null ||
                (platoon.Sop.Roe == "hold_fire" && tick - platoon.LastHitTick > ReturnFireTicks))
            {
                return -1;
            }

            List<ScoredTarget> scored = new List<ScoredTarget>();
            foreach (KeyValuePair<int, long> contact in platoon.Contacts)
            {
                if (tick - contact.Value > DetectionSystem.ContactMemoryTicks)
                {
                    continue;
                }

                Element? target = _battle.GetElement(contact.Key);
                if (target == null || !target.IsActive() || target.IsMounted())
                {
                    continue;
                }

                if (platoon.Sop.Roe == "designated" && target.IsInfantry())
                {
                    continue;
                }

                double distance = element.Pos.DistanceTo(target.Pos);
                double score = Score(element, target, distance);
                if (score > 0.0 && IsMasked(element, target))
                {
                    score *= MaskedScore;
                }

                if (score > 0.0)
                {
                    scored.Add(new ScoredTarget(score, target.Id, target));
                }
            }

            if (scored.Count == 0)
            {
                return -1;
            }

            scored.Sort((left, right) =>
            {
                int comparison = right.Score.CompareTo(left.Score);
                return comparison != 0 ? comparison : left.TargetId.CompareTo(right.TargetId);
            });
            int count = Math.Min(scored.Count, LosTopChecks);
            for (int i = 0; i < count; ++i)
            {
                Element target = scored[i].Target;
                if (Los.Between(_battle.Terrain, _battle.Smokes, tick, element, target))
                {
                    return target.Id;
                }
            }

            return -1;
        }

        public double HitChance(Element shooter, WeaponDef weapon, Element target, double distance, long tick)
        {
            double probability = weapon.AccuracyAt(distance);
            Platoon? platoon = shooter.Platoon;
            if (platoon != null)
            {
                probability *= SkillAccuracy.TryGetValue(platoon.Skill, out double skill) ? skill : 1.0;
                if (platoon.IsReforming(tick))
                {
                    probability *= ReformingAccuracy;
                }

                if (platoon.Sop.Technique == "bounding" && shooter.Velocity.Length() < 0.5)
                {
                    probability *= BoundingBonus;
                }
            }

            if (shooter.Velocity.Length() > 1.0)
            {
                probability *= MovingShooter;
            }

            if (target.Velocity.Length() > 1.0)
            {
                probability *= MovingTarget;
            }

            if (shooter.Suppression >= 50.0)
            {
                probability *= SuppressedAccuracy;
            }

            if (!target.IsAir())
            {
                probability *= TargetCoverAccuracy.TryGetValue(_battle.Terrain.TypeAt(target.Pos), out double cover)
                    ? cover
                    : 1.0;
                probability *= PositionFactor(target);
            }

            probability *= SimMath.Clamp(Math.Sqrt(target.Def.Size), 0.6, 1.2);
            probability *= LongRangeFactor(distance);
            if (target.IsAir() && weapon.WeaponType != "SAM" && weapon.WeaponType != "AAM" &&
                shooter.Def.ElementClass != "aa")
            {
                probability *= NonAaVsAir;
            }

            if (target.IsFixedWing() && (weapon.WeaponType == "SAM" || weapon.WeaponType == "AAM"))
            {
                probability *= FixedWingEvasion;
            }

            return SimMath.Clamp(probability, 0.01, 0.98);
        }

        public bool IsMasked(Element shooter, Element target)
        {
            if (shooter.Platoon == null || shooter.IsInfantry() || shooter.IsAir())
            {
                return false;
            }

            SimVec2 toTarget = target.Pos - shooter.Pos;
            double distance = toTarget.Length();
            if (distance < 1.0)
            {
                return false;
            }

            SimVec2 direction = toTarget / distance;
            double reach = Math.Min(distance - 5.0, MaskRangeM);
            foreach (Element other in shooter.Platoon.Elements)
            {
                if (other == shooter || !other.IsActive() || other.IsMounted() || other.IsInfantry() || other.IsAir())
                {
                    continue;
                }

                SimVec2 relative = other.Pos - shooter.Pos;
                double along = relative.Dot(direction);
                if (along <= 5.0 || along > reach)
                {
                    continue;
                }

                if (Math.Abs(relative.Cross(direction)) < MaskHalfWidthM)
                {
                    return true;
                }
            }

            return false;
        }

        public void IndirectImpact(SimVec2 position, WeaponDef weapon, string firingSide, Element? source, long tick)
        {
            double radius = Math.Max(weapon.HeRadiusM, 1.0);
            SimRng rng = _battle.World.Rng;
            foreach (Element target in _battle.ElementsNear(position, radius * 2.0))
            {
                if (target.Side == firingSide || target.IsMounted() || !target.IsAlive() || target.IsAir())
                {
                    continue;
                }

                double distance = target.Pos.DistanceTo(position);
                Suppress(target, weapon.Suppression * SimMath.Clamp(1.0 - distance / (radius * 2.0), 0.0, 1.0));
                target.LastHitTick = tick;
                if (target.Platoon != null)
                {
                    target.Platoon.LastHitTick = tick;
                    target.Platoon.LastShelledTick = tick;
                    target.Platoon.LastShellPos = position;
                }

                if (distance > radius)
                {
                    continue;
                }

                double effect = (1.0 - distance / radius) * ArtyVulnerability(target);
                if (target.IsInfantry())
                {
                    ResolveInfantryHit(source, weapon, target, tick,
                        weapon.Damage * HeSplashCasualtyPerDamage * 2.0 * effect);
                    continue;
                }

                switch (ClassifyPenetration(weapon.Penetration, target.Def.GetArmor("top")))
                {
                    case "overmatch":
                        if (rng.Chance(effect * KillProbability))
                        {
                            Destroy(target, source, tick);
                        }
                        else if (rng.Chance(effect))
                        {
                            Damage(target, source, tick);
                        }

                        break;
                    case "marginal":
                        if (rng.Chance(effect * MarginalProbability))
                        {
                            Damage(target, source, tick);
                        }

                        break;
                }
            }
        }

        public double ArtyVulnerability(Element element)
        {
            double vulnerability = 1.0;
            if (element.Platoon != null && element.Platoon.Formation != null)
            {
                vulnerability *= element.Platoon.Formation.GetMod("arty_vuln", 1.0);
            }

            vulnerability *= TargetCoverAccuracy.TryGetValue(_battle.Terrain.TypeAt(element.Pos), out double cover)
                ? cover
                : 1.0;
            if (element.StationaryTicks > DugInTicks)
            {
                vulnerability *= DugInCover;
            }

            return vulnerability * PositionFactor(element);
        }

        public void Destroy(Element target, Element? killer, long tick)
        {
            if (!target.IsAlive())
            {
                return;
            }

            target.State = ElementState.DESTROYED;
            target.DestroyedTick = tick;
            target.Velocity = SimVec2.Zero;
            target.TargetId = -1;
            ++Kills;
            if (killer != null)
            {
                target.KilledBy = killer.Id;
                ++killer.Kills;
            }

            if (target.IsAir())
            {
                // 날고 있는 헬기·수송기가 떨어지면 탄 보병은 빠져나오지 못한다.
                List<Element> riders = new List<Element>(target.Passengers);
                target.Passengers.Clear();
                foreach (Element squad in riders)
                {
                    squad.MountedIn = null;
                    Destroy(squad, killer, tick);
                }
            }

            foreach (Element squad in target.Passengers)
            {
                squad.MountedIn = null;
                squad.Hp = Math.Floor(squad.Hp * 0.5);
                if (squad.Hp <= 0.0)
                {
                    squad.State = ElementState.DESTROYED;
                    squad.DestroyedTick = tick;
                }
                else
                {
                    squad.Pos = _battle.Terrain.NearestPassable(
                        target.Pos + FormationSolver.RightVector(target.Heading) * 10.0,
                        Terrain.MobilityFoot);
                    squad.Suppression = 80.0;
                }
            }

            target.Passengers.Clear();
            if (target.Platoon != null && target.Platoon.HasSquads())
            {
                target.Platoon.Mounted = false;
            }

            _battle.Pathfinder.AddThreat(target.Side, target.Pos, ThreatRadiusM, ThreatAmount * 2.0);
            _morale.OnElementLost(target);
            _battle.World.Events.EmitEvent("element_destroyed", tick, new Dictionary<string, object>
            {
                ["element"] = target.Id,
                ["side"] = target.Side,
                ["killer"] = killer != null ? killer.Id : -1,
                ["pos"] = target.Pos,
                ["unit"] = target.Platoon != null ? target.Platoon.Id : -1
            });
        }

        public void Damage(Element target, Element? attacker, long tick)
        {
            if (target.State == ElementState.DAMAGED)
            {
                if (_battle.World.Rng.Chance(0.5))
                {
                    Destroy(target, attacker, tick);
                }

                return;
            }

            target.State = ElementState.DAMAGED;
            target.DamageKind = _battle.World.Rng.Chance(0.5) ? "mobility" : "firepower";
            _battle.Pathfinder.AddThreat(target.Side, target.Pos, ThreatRadiusM, ThreatAmount);
            _morale.OnElementDamaged(target);
            _battle.World.Events.EmitEvent("element_damaged", tick, new Dictionary<string, object>
            {
                ["element"] = target.Id,
                ["side"] = target.Side,
                ["kind"] = target.DamageKind,
                ["unit"] = target.Platoon != null ? target.Platoon.Id : -1
            });
        }

        public static double LongRangeFactor(double distance)
        {
            return SimMath.Lerp(1.0, LongRangeMultiplier,
                SimMath.Clamp((distance - LongRangeStartM) / (LongRangeEndM - LongRangeStartM), 0.0, 1.0));
        }

        public static string HitFacing(SimVec2 shooterPosition, Element target, bool topAttack)
        {
            if (topAttack)
            {
                return "top";
            }

            double relative = Math.Abs(SimMath.AngleDifference(
                target.Heading,
                FormationSolver.HeadingFromVector(shooterPosition - target.Pos)));
            if (relative <= Math.PI / 4.0)
            {
                return "front";
            }

            return relative >= Math.PI * 0.75 ? "rear" : "side";
        }

        public static string ClassifyPenetration(double penetration, double armor)
        {
            double ratio = penetration / Math.Max(armor, 1.0);
            if (ratio > Overmatch)
            {
                return "overmatch";
            }

            return ratio >= Undermatch ? "marginal" : "bounce";
        }

        private double Score(Element shooter, Element target, double distance)
        {
            double best = 0.0;
            foreach (string weaponId in shooter.Def.Weapons)
            {
                WeaponDef? weapon = _battle.Catalog.GetWeapon(weaponId);
                if (!Usable(shooter, weapon, target, distance))
                {
                    continue;
                }

                double effectiveness = weapon!.AccuracyAt(distance);
                if (target.IsAir())
                {
                    effectiveness *= SimMath.Clamp(
                        weapon.PenetrationAt(distance) / Math.Max(target.Def.GetArmor("side"), 1.0), 0.05, 1.5) * 1.5;
                }
                else if (target.IsVehicle())
                {
                    effectiveness *= SimMath.Clamp(
                        weapon.PenetrationAt(distance) / Math.Max(target.Def.GetArmor("front"), 1.0), 0.05, 1.5);
                }
                else
                {
                    effectiveness *= weapon.Damage * 0.1;
                }

                best = Math.Max(best, effectiveness);
            }

            if (best <= 0.0)
            {
                return 0.0;
            }

            double value = target.Def.Cost / 50.0;
            if (target.Def.ElementClass == "tank" || target.IsAir())
            {
                value *= 1.5;
            }

            return best * value / (1.0 + distance / 1000.0);
        }

        // 빠르고 높이 나는 고정익은 지대공·공대공 미사일, 방공 차량, 다른 항공기만 잡는다. 전차·장갑차 기관포와
        // 기관총으로는 쏠 수 없다(헬기는 기존대로 0.3 배로 쏠 수 있다).
        public static bool CanEngageFixedWing(Element shooter, WeaponDef weapon)
        {
            return weapon.WeaponType == "SAM" || weapon.WeaponType == "AAM" || shooter.Def.ElementClass == "aa" ||
                shooter.IsAir();
        }

        private bool Usable(Element shooter, WeaponDef? weapon, Element target, double distance)
        {
            if (weapon == null || weapon.Indirect)
            {
                return false;
            }

            if (!shooter.Ammo.TryGetValue(weapon.Id, out int ammunition) || ammunition < 1)
            {
                return false;
            }

            if (target.IsFixedWing() && !CanEngageFixedWing(shooter, weapon))
            {
                return false;
            }

            if (!weapon.CanTarget(target.TargetKind()) || distance < weapon.RangeMinM || distance > weapon.RangeMaxM)
            {
                return false;
            }

            return shooter.State != ElementState.DAMAGED || shooter.DamageKind != "firepower" || weapon.WeaponType == "MG";
        }

        private void TryFire(Element shooter, long tick)
        {
            Element? target = _battle.GetElement(shooter.TargetId);
            if (target == null || !target.IsActive() || target.IsMounted())
            {
                shooter.TargetId = -1;
                return;
            }

            double distance = shooter.Pos.DistanceTo(target.Pos);
            bool moving = shooter.Velocity.Length() > 1.0;
            if (IsMasked(shooter, target))
            {
                return;
            }

            foreach (string weaponId in shooter.Def.Weapons)
            {
                WeaponDef? weapon = _battle.Catalog.GetWeapon(weaponId);
                if (!Usable(shooter, weapon, target, distance))
                {
                    continue;
                }

                if (shooter.Reload.TryGetValue(weapon!.Id, out double reload) && reload > 0.0)
                {
                    continue;
                }

                if (moving && !weapon.FireOnMove)
                {
                    continue;
                }

                Fire(shooter, weapon, target, distance, tick);
                if (!target.IsAlive())
                {
                    shooter.TargetId = -1;
                    return;
                }
            }
        }

        private void Fire(Element shooter, WeaponDef weapon, Element target, double distance, long tick)
        {
            int ammunition = shooter.Ammo.TryGetValue(weapon.Id, out int left) ? left : 0;
            shooter.Ammo[weapon.Id] = Math.Max(0, ammunition - weapon.Burst);
            double interval = weapon.IntervalSec();
            if (shooter.Suppression >= 50.0)
            {
                interval *= 1.25;
            }

            shooter.Reload[weapon.Id] = interval;
            shooter.LastFiredTick = tick;
            ++ShotsFired;
            double probability = HitChance(shooter, weapon, target, distance, tick);
            bool hit = _battle.World.Rng.Chance(probability);
            _battle.World.Events.EmitEvent("shot", tick, new Dictionary<string, object>
            {
                ["from"] = shooter.Id,
                ["to"] = target.Id,
                ["weapon"] = weapon.WeaponType,
                ["hit"] = hit,
                ["fp"] = shooter.Pos,
                ["tp"] = target.Pos
            });
            MarkUnderFire(target, tick);
            if (!hit)
            {
                Suppress(target, weapon.Suppression * MissSuppression);
                return;
            }

            ++Hits;
            if (target.IsVehicle())
            {
                ResolveVehicleHit(shooter, weapon, target, distance, tick);
            }
            else
            {
                ResolveInfantryHit(shooter, weapon, target, tick, weapon.Damage * InfantryCasualtyPerDamage);
            }

            if (weapon.HeRadiusM > 0.0)
            {
                HeSplash(shooter, weapon, target.Pos, target, tick);
            }
        }

        private void ResolveVehicleHit(Element shooter, WeaponDef weapon, Element target, double distance, long tick)
        {
            string facing = HitFacing(shooter.Pos, target, weapon.TopAttack);
            Suppress(target, weapon.Suppression * VehicleSuppression);
            SimRng rng = _battle.World.Rng;
            switch (ClassifyPenetration(weapon.PenetrationAt(distance), target.Def.GetArmor(facing)))
            {
                case "overmatch":
                    if (rng.Chance(KillProbability))
                    {
                        Destroy(target, shooter, tick);
                    }
                    else
                    {
                        Damage(target, shooter, tick);
                    }

                    break;
                case "marginal":
                    if (rng.Chance(MarginalProbability))
                    {
                        Damage(target, shooter, tick);
                    }

                    break;
            }
        }

        private void ResolveInfantryHit(Element? attacker, WeaponDef weapon, Element target, long tick, double expected)
        {
            Suppress(target, weapon.Suppression);
            double cover = InfantryCover.TryGetValue(_battle.Terrain.TypeAt(target.Pos), out double terrainCover)
                ? terrainCover
                : 1.0;
            if (target.StationaryTicks > DugInTicks)
            {
                cover *= DugInCover;
            }

            cover *= PositionFactor(target);
            double adjusted = expected * cover;
            double floor = Math.Floor(adjusted);
            int casualties = (int)floor + (_battle.World.Rng.Chance(adjusted - floor) ? 1 : 0);
            if (casualties <= 0)
            {
                return;
            }

            target.Hp = Math.Max(0.0, target.Hp - casualties);
            _morale.OnCasualties(target.Platoon, casualties);
            if (target.Hp <= 0.0)
            {
                Destroy(target, attacker, tick);
            }
        }

        private void HeSplash(Element shooter, WeaponDef weapon, SimVec2 center, Element directTarget, long tick)
        {
            foreach (Element target in _battle.ElementsNear(center, weapon.HeRadiusM))
            {
                if (target == directTarget || target.Side == shooter.Side || target.IsMounted() || target.IsAir())
                {
                    continue;
                }

                Suppress(target, weapon.Suppression * 0.5);
                if (target.IsInfantry())
                {
                    ResolveInfantryHit(shooter, weapon, target, tick, weapon.Damage * HeSplashCasualtyPerDamage);
                }
            }
        }

        private double PositionFactor(Element element)
        {
            return _battle.Engineers is ICombatEngineers engineers ? engineers.PositionFactor(element) : 1.0;
        }

        private static void Suppress(Element target, double amount)
        {
            target.Suppression = Math.Min(100.0, target.Suppression + amount);
        }

        private static void MarkUnderFire(Element target, long tick)
        {
            target.LastHitTick = tick;
            if (target.Platoon != null)
            {
                target.Platoon.LastHitTick = tick;
                target.Platoon.LastContactTick = tick;
            }
        }

        private readonly struct ScoredTarget
        {
            public ScoredTarget(double score, int targetId, Element target)
            {
                Score = score;
                TargetId = targetId;
                Target = target;
            }

            public double Score { get; }
            public int TargetId { get; }
            public Element Target { get; }
        }
    }
}
