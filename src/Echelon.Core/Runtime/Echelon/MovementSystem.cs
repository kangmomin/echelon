using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public sealed class MovementSystem : SimSystem
    {
        public const double ArriveRadiusM = 6.0;
        public const double SlowRadiusM = 40.0;
        public const double SeparationRadiusM = 20.0;
        public const double PlatoonWaitM = 150.0;
        public const double CompanyWaitM = 300.0;
        public const int StragglerTicks = 600;
        public const double PlatoonColumnSpacingM = 25.0;
        public const double CompanyColumnSpacingM = 160.0;
        public const double TrailingVehicleGapM = 60.0;
        public const double ChokeLookaheadM = 200.0;
        public const double OverwatchLeadM = 100.0;
        public const double CatchUpMult = 1.15;
        public const double SettledErrorM = 50.0;
        public const int SettleTimeoutTicks = 1200;
        public const int LineCheckInterval = 20;
        public const int StuckTicksBeforePath = 10;
        public const double SlideProgressMin = 0.25;
        public const double WaypointReachM = 8.0;
        public const double WaypointReachTightM = 2.0;
        public const double FollowRepathM = 80.0;
        public const int BriefingTimeoutTicks = 300;
        public const int FollowRepathInterval = 10;
        public const int RestAssignInterval = 10;
        public const double RestErrorM = 15.0;

        private readonly Dictionary<int, SimVec2> _companyTargets = new Dictionary<int, SimVec2>();
        private readonly Dictionary<int, List<Element>> _membersCache = new Dictionary<int, List<Element>>();
        private readonly Dictionary<int, double> _speedCache = new Dictionary<int, double>();
        private readonly Dictionary<int, List<Platoon>> _platoonsCache = new Dictionary<int, List<Platoon>>();
        private readonly Dictionary<int, CompanyRest> _companyRest = new Dictionary<int, CompanyRest>();

        public MovementSystem(Battle battle)
        {
            Battle = battle ?? throw new ArgumentNullException(nameof(battle));
        }

        public Battle Battle { get; }

        public override string GetSystemName()
        {
            return "movement";
        }

        public override void Step(SimWorld world)
        {
            double dt = SimConst.TickDt;
            foreach (Element e in Battle.Elements)
            {
                e.PrevPos = e.Pos;
                e.PrevHeading = e.Heading;
            }

            Battle.Pathfinder.Process();
            _companyTargets.Clear();
            _membersCache.Clear();
            _speedCache.Clear();
            _platoonsCache.Clear();
            foreach (string side in Battle.Sides)
            {
                if (!Battle.TaskForces.TryGetValue(side, out TaskForce? tf) || tf == null)
                {
                    continue;
                }

                foreach (Unit unit in tf.AllUnits())
                {
                    if (!unit.OnMap || unit.Level == UnitLevel.TaskForce)
                    {
                        continue;
                    }

                    if (unit.Level == UnitLevel.Company)
                    {
                        UpdateCompany((Company)unit, world.Tick, dt);
                    }
                    else
                    {
                        UpdatePlatoon((Platoon)unit, world.Tick, dt);
                    }
                }
            }

            foreach (Element e in Battle.Elements)
            {
                if (e.IsActive() && !e.IsMounted())
                {
                    MoveElement(e, world.Tick, dt);
                }
            }

            foreach (Element e in Battle.Elements)
            {
                if (e.IsMounted() && e.MountedIn != null)
                {
                    e.Pos = e.MountedIn.Pos;
                    e.Heading = e.MountedIn.Heading;
                }
            }

            Battle.RebuildSpatial();
        }

        public override long StateHash()
        {
            long hash = 0;
            foreach (Element e in Battle.Elements)
            {
                hash = SimHash.Append(hash, BitConverter.SingleToInt32Bits(e.Pos.X));
                hash = SimHash.Append(hash, BitConverter.SingleToInt32Bits(e.Pos.Y));
                hash = SimHash.Append(hash, BitConverter.DoubleToInt64Bits(e.Heading));
            }

            return hash;
        }

        private List<Element> Members(Platoon p)
        {
            if (!_membersCache.TryGetValue(p.Id, out List<Element>? cached))
            {
                cached = p.FormationMembers();
                _membersCache[p.Id] = cached;
            }

            return new List<Element>(cached);
        }

        private double PlatoonSpeed(Platoon p)
        {
            if (!_speedCache.TryGetValue(p.Id, out double speed))
            {
                double best = double.PositiveInfinity;
                foreach (Element e in Members(p))
                {
                    best = Math.Min(best, e.BaseSpeedMps());
                }

                speed = double.IsPositiveInfinity(best) ? 0.0 : best;
                _speedCache[p.Id] = speed;
            }

            return speed;
        }

        private List<Platoon> Platoons(Company c)
        {
            if (!_platoonsCache.TryGetValue(c.Id, out List<Platoon>? cached))
            {
                cached = c.FormationPlatoons();
                _platoonsCache[c.Id] = cached;
            }

            return new List<Platoon>(cached);
        }

        private double CompanySpeed(Company c)
        {
            if (_speedCache.TryGetValue(c.Id, out double cached))
            {
                return cached;
            }

            double best = double.PositiveInfinity;
            foreach (Platoon p in Platoons(c))
            {
                double speed = PlatoonSpeed(p);
                if (speed > 0.0)
                {
                    best = Math.Min(best, speed);
                }
            }

            if (c.Hq != null && !c.Hq.IsDestroyed())
            {
                double speed = PlatoonSpeed(c.Hq);
                if (speed > 0.0)
                {
                    best = Math.Min(best, speed);
                }
            }

            double result = double.IsPositiveInfinity(best) ? 0.0 : best;
            _speedCache[c.Id] = result;
            return result;
        }

        private void UpdateCompany(Company c, long tick, double dt)
        {
            PollPath(c);
            double speed = CompanySpeed(c) * c.Formation!.GetMod("speed", 1.0);
            if (c.IsReforming(tick))
            {
                speed *= 0.5;
            }

            if (c.ColumnMode && Battle.Terrain.TypeAt(c.Guide.Pos) == (int)Terrain.Type.ROAD)
            {
                speed = RoadMarchSpeed(c, CompanySpeed(c));
            }

            if (c.FormationError > CompanyWaitM || tick < c.ContactHaltUntil)
            {
                speed = 0.0;
            }

            if (c.Mission != null && c.Mission.ReceivedTick >= 0 &&
                tick - c.Mission.ReceivedTick < BriefingTimeoutTicks)
            {
                foreach (Platoon p in Platoons(c))
                {
                    if (p.AwaitingFollow)
                    {
                        speed = 0.0;
                        break;
                    }
                }
            }

            if (c.IsRouted(tick) || c.IsLeaderless(tick))
            {
                speed = Math.Min(speed, CompanySpeed(c) * 0.5);
            }

            c.GuideSpeedLimit = speed;
            double moved = AdvanceGuide(c, dt, speed);
            (float X, float Y, float Z) restKey = (c.Guide.Pos.X, c.Guide.Pos.Y, (float)c.Guide.Heading);
            int platoonCount = Platoons(c).Count;
            if (_companyRest.TryGetValue(c.Id, out CompanyRest? saved) &&
                saved.Key == restKey && saved.N == platoonCount && c.FormationError < RestErrorM &&
                (tick + c.Id) % RestAssignInterval != 0)
            {
                foreach (KeyValuePair<int, SimVec2> pair in saved.Targets)
                {
                    _companyTargets[pair.Key] = pair.Value;
                }

                return;
            }

            List<Platoon> platoons = Platoons(c);
            if (c.Guide.IsMoving() && (OnRoadAhead(c.Guide) || ChokeAhead(c.Guide)))
            {
                c.ColumnHoldM = Math.Max(c.ColumnHoldM,
                    (platoons.Count + 2) * CompanyColumnSpacingM + 100.0);
            }

            c.ColumnHoldM = Math.Max(0.0, c.ColumnHoldM - moved);
            c.ColumnMode = c.Guide.IsMoving() && (c.ColumnHoldM > 0.0 || c.Formation.Id == "co_column");
            string terrainName = Battle.Terrain.TypeNameAt(c.Guide.Pos);
            List<SimVec2> slots = FormationSolver.WorldSlots(c.Formation!, platoons.Count, c.Guide.Pos,
                c.Guide.Heading, terrainName);
            double worst = 0.0;
            int columnIndex = 0;
            for (int i = 0; i < platoons.Count; ++i)
            {
                Platoon p = platoons[i];
                SimVec2 target;
                if (c.ColumnMode)
                {
                    target = c.Guide.TrailPointBehind(columnIndex * CompanyColumnSpacingM);
                    ++columnIndex;
                    if (i == 0)
                    {
                        ++columnIndex;
                    }
                }
                else
                {
                    target = slots[i];
                    if (!ReachableStraight(c.Guide.Pos, target, Terrain.MobilityVehicle))
                    {
                        target = c.Guide.TrailPointBehind(
                            Math.Abs(FormationSolver.WorldToLocal(target, c.Guide.Pos, c.Guide.Heading).Y) + i * 30.0);
                    }
                }

                target = Battle.Terrain.NearestPassable(target, OrderExecutor.PathMobility(p));
                _companyTargets[p.Id] = target;
                if (!p.IsDestroyed() && !p.AwaitingFollow && p.CommsState != Unit.Comms.Cut)
                {
                    worst = Math.Max(worst, Math.Max(p.Guide.Pos.DistanceTo(target), p.FormationError));
                }
            }

            if (c.Hq != null && c.Hq.OnMap)
            {
                SimVec2 hqTarget;
                if (c.ColumnMode)
                {
                    hqTarget = c.Guide.TrailPointBehind(CompanyColumnSpacingM);
                }
                else
                {
                    hqTarget = FormationSolver.LocalToWorld(new SimVec2(0.0, -ForceFactory.HqOffsetBehindM),
                        c.Guide.Pos, c.Guide.Heading);
                    if (!ReachableStraight(c.Guide.Pos, hqTarget, Terrain.MobilityVehicle))
                    {
                        hqTarget = c.Guide.TrailPointBehind(ForceFactory.HqOffsetBehindM);
                    }
                }

                _companyTargets[c.Hq.Id] = Battle.Terrain.NearestPassable(hqTarget, Terrain.MobilityVehicle);
            }

            c.FormationError = worst;
            CheckSettled(c);
            CompanyRest rest = new CompanyRest { Key = restKey, N = platoonCount };
            foreach (Platoon p in platoons)
            {
                rest.Targets.Add(new KeyValuePair<int, SimVec2>(p.Id, _companyTargets[p.Id]));
            }

            if (c.Hq != null && _companyTargets.TryGetValue(c.Hq.Id, out SimVec2 savedHqTarget))
            {
                rest.Targets.Add(new KeyValuePair<int, SimVec2>(c.Hq.Id, savedHqTarget));
            }

            _companyRest[c.Id] = rest;
        }

        private void UpdatePlatoon(Platoon p, long tick, double dt)
        {
            if (p.IsDestroyed())
            {
                return;
            }

            double baseSpeed = PlatoonSpeed(p);
            double speed = baseSpeed * p.Formation!.GetMod("speed", 1.0);
            if (p.IsReforming(tick))
            {
                speed *= 0.5;
            }

            if (p.Sop.Technique == "bounding")
            {
                speed *= 0.5;
            }

            if (p.IsRouted(tick))
            {
                speed = baseSpeed;
            }

            bool waiting = p.FormationError > PlatoonWaitM;
            if (p.FollowsParent() && _companyTargets.ContainsKey(p.Id))
            {
                Company c = p.Company()!;
                SimVec2 target = _companyTargets[p.Id];
                double face = c.Guide.Heading;
                if (!c.Guide.IsMoving() && !double.IsNaN(c.DefendFacing))
                {
                    face = c.DefendFacing;
                }

                double catchUp = baseSpeed * CatchUpMult;
                if (p.Guide.Pos.DistanceTo(target) < 40.0)
                {
                    catchUp = Math.Max(c.Guide.Speed, baseSpeed * 0.5);
                }

                if (waiting || p.AwaitingFollow)
                {
                    catchUp = 0.0;
                }

                p.GuideSpeedLimit = catchUp;
                SimVec2 before = p.Guide.Pos;
                FollowTarget(p, target, dt, catchUp, face, tick);
                if (c.ColumnMode)
                {
                    p.ColumnHoldM = Math.Max(p.ColumnHoldM,
                        (p.Elements.Count + 1) * PlatoonColumnSpacingM + 50.0);
                }

                p.ColumnHoldM = Math.Max(0.0, p.ColumnHoldM - before.DistanceTo(p.Guide.Pos));
                p.ColumnMode = c.ColumnMode || p.ColumnHoldM > 0.0;
                if (!c.Guide.IsMoving())
                {
                    p.Posture = c.Posture;
                    p.DefendFacing = c.DefendFacing;
                }
            }
            else
            {
                PollPath(p);
                if (p.Guide.IsMoving() && Battle.Terrain.TypeAt(p.Guide.Pos) == (int)Terrain.Type.ROAD &&
                    OnRoadAhead(p.Guide))
                {
                    speed = RoadMarchSpeed(p, baseSpeed);
                }

                if (waiting || tick < p.ContactHaltUntil)
                {
                    speed = 0.0;
                }

                p.GuideSpeedLimit = speed;
                double moved = AdvanceGuide(p, dt, speed);
                if (p.Guide.IsMoving() && (OnRoadAhead(p.Guide) || ChokeAhead(p.Guide)))
                {
                    p.ColumnHoldM = Math.Max(p.ColumnHoldM,
                        (p.Elements.Count + 1) * PlatoonColumnSpacingM + 50.0);
                }

                p.ColumnHoldM = Math.Max(0.0, p.ColumnHoldM - moved);
                p.ColumnMode = p.Guide.IsMoving() && (p.ColumnHoldM > 0.0 || p.Formation.Id == "column");
            }

            (float X, float Y, float Z) key = (p.Guide.Pos.X, p.Guide.Pos.Y, (float)p.Guide.Heading);
            int members = Members(p).Count;
            bool unchanged = key == p.LastAssignKey && members == p.LastAssignMembers && p.FormationError < RestErrorM;
            if (unchanged && (tick + p.Id) % RestAssignInterval != 0)
            {
                return;
            }

            p.LastAssignKey = key;
            p.LastAssignMembers = members;
            AssignElementTargets(p);
        }

        private void AssignElementTargets(Platoon p)
        {
            List<Element> members = Members(p);
            Guide guide = p.Guide;
            double rest = guide.Heading;
            if (p.Posture == "defending" && !double.IsNaN(p.DefendFacing))
            {
                rest = p.DefendFacing;
            }

            List<SimVec2> targets = new List<SimVec2>(members.Count);
            targets.AddRange(new SimVec2[members.Count]);
            if (p.ColumnMode)
            {
                for (int i = 0; i < members.Count; ++i)
                {
                    targets[i] = guide.TrailPointBehind(i * PlatoonColumnSpacingM);
                }
            }
            else
            {
                string terrainName = Battle.Terrain.TypeNameAt(guide.Pos);
                FormationDef formation = p.Formation!;
                if ((terrainName == "forest" || terrainName == "urban") && formation.Id == "line")
                {
                    formation = Battle.Catalog.GetFormation("wedge")!;
                }

                List<SimVec2> slots = FormationSolver.WorldSlots(formation, members.Count, guide.Pos,
                    guide.Heading, terrainName);
                if (p.Sop.Technique == "overwatch" && guide.IsMoving() && slots.Count > 1)
                {
                    slots[0] += FormationSolver.ForwardVector(guide.Heading) * OverwatchLeadM;
                }

                for (int i = 0; i < members.Count; ++i)
                {
                    SimVec2 target = slots[i];
                    if (!ReachableStraight(guide.Pos, target, members[i].Mobility()))
                    {
                        target = guide.TrailPointBehind(i * PlatoonColumnSpacingM);
                    }

                    targets[i] = target;
                }
            }

            double worst = 0.0;
            for (int i = 0; i < members.Count; ++i)
            {
                Element e = members[i];
                e.MoveTarget = Battle.Terrain.NearestPassable(targets[i], e.Mobility(), 10);
                e.HasMoveTarget = true;
                e.RestHeading = rest;
                double distance = e.Pos.DistanceTo(e.MoveTarget);
                if (distance > PlatoonWaitM)
                {
                    ++e.StragglerTicks;
                }
                else if (distance < 50.0)
                {
                    e.StragglerTicks = 0;
                }

                if (e.StragglerTicks < StragglerTicks)
                {
                    worst = Math.Max(worst, distance);
                }
            }

            List<Element> trailing = p.TrailingVehicles();
            for (int i = 0; i < trailing.Count; ++i)
            {
                Element vehicle = trailing[i];
                SimVec2 anchor = i < members.Count ? members[i].MoveTarget : guide.Pos;
                SimVec2 target = anchor - FormationSolver.ForwardVector(guide.Heading) * TrailingVehicleGapM;
                vehicle.MoveTarget = Battle.Terrain.NearestPassable(target, Terrain.MobilityVehicle, 10);
                vehicle.HasMoveTarget = true;
                vehicle.RestHeading = rest;
            }

            p.FormationError = worst;
            CheckSettled(p);
        }

        private void MoveElement(Element e, long tick, double dt)
        {
            if (!e.HasMoveTarget || !e.CanMove())
            {
                e.Velocity = SimVec2.Zero;
                ++e.StationaryTicks;
                return;
            }

            Terrain terrain = Battle.Terrain;
            string mobility = e.Mobility();
            SimVec2 goal = e.MoveTarget;
            if (e.PathRequest != null && e.PathRequest.Done)
            {
                if (e.PathRequest.Ok)
                {
                    e.LocalPath = new List<SimVec2>(e.PathRequest.Points);
                    e.LocalPathIndex = 1;
                }

                e.PathRequest = null;
            }

            SimVec2 steer = goal;
            if (e.LocalPath.Count > 0)
            {
                if (e.LocalPath[e.LocalPath.Count - 1].DistanceTo(goal) > 60.0)
                {
                    e.ClearLocalPath();
                }
                else
                {
                    while (e.LocalPathIndex < e.LocalPath.Count)
                    {
                        double reach = e.Pos.DistanceTo(e.LocalPath[e.LocalPathIndex]);
                        if (reach >= WaypointReachM)
                        {
                            break;
                        }

                        // 모퉁이 경유점은 다음 경유점이 곧장 보일 때만 일찍 넘긴다. 그렇지 않으면 장애물 모서리를
                        // 잘라 들어가 다리·숲 가장자리에 걸린다(빠른 차량일수록 심하다).
                        bool hasNext = e.LocalPathIndex + 1 < e.LocalPath.Count;
                        if (hasNext && reach >= WaypointReachTightM &&
                            !terrain.LinePassable(e.Pos, e.LocalPath[e.LocalPathIndex + 1], mobility))
                        {
                            break;
                        }

                        ++e.LocalPathIndex;
                    }

                    if (e.LocalPathIndex >= e.LocalPath.Count)
                    {
                        e.ClearLocalPath();
                    }
                    else
                    {
                        steer = e.LocalPath[e.LocalPathIndex];
                    }
                }
            }

            SimVec2 toSteer = steer - e.Pos;
            double distance = toSteer.Length();
            if (distance < ArriveRadiusM && steer == goal)
            {
                e.Velocity = SimVec2.Zero;
                e.TurnToward(e.RestHeading, dt);
                ++e.StationaryTicks;
                e.StuckTicks = 0;
                return;
            }

            double maxSpeed = e.MaxSpeedMps(terrain);
            double desired = maxSpeed;
            if (steer == goal && distance < SlowRadiusM)
            {
                desired = Math.Max(maxSpeed * distance / SlowRadiusM, 1.0);
            }

            SimVec2 direction = toSteer / distance;
            SimVec2 separation = Separation(e);
            if (separation != SimVec2.Zero)
            {
                direction = (direction + separation).Normalized();
            }

            e.TurnToward(FormationSolver.HeadingFromVector(direction), dt);
            if (e.IsVehicle() &&
                Math.Abs(SimMath.AngleDifference(e.Heading, FormationSolver.HeadingFromVector(direction))) > Math.PI / 3.0)
            {
                desired *= 0.3;
            }

            SimVec2 deltaStep = direction * desired * dt;
            SimVec2 next = terrain.ClampToMap(e.Pos + deltaStep);
            bool moved = false;
            if (terrain.IsPassable(next, mobility))
            {
                e.Pos = next;
                moved = true;
            }
            else
            {
                SimVec2 slideX = terrain.ClampToMap(e.Pos + new SimVec2(deltaStep.X, 0.0));
                SimVec2 slideY = terrain.ClampToMap(e.Pos + new SimVec2(0.0, deltaStep.Y));
                if (Math.Abs(deltaStep.X) > Math.Abs(deltaStep.Y) && terrain.IsPassable(slideX, mobility))
                {
                    e.Pos = slideX;
                    moved = true;
                }
                else if (terrain.IsPassable(slideY, mobility))
                {
                    e.Pos = slideY;
                    moved = true;
                }
                else if (terrain.IsPassable(slideX, mobility))
                {
                    e.Pos = slideX;
                    moved = true;
                }
            }

            if (moved)
            {
                // 장애물 가장자리를 따라 조금씩만 미끄러지면 움직이긴 해도 갇힌 것이다. 진척이 의도의 1/4 미만이면
                // 막힘으로 세어 경로를 다시 찾게 한다(그러지 않으면 강둑을 따라 영원히 미끄러진다).
                double intended = deltaStep.Length();
                if (intended > 1e-6 && (e.Pos - e.PrevPos).Length() < intended * SlideProgressMin)
                {
                    ++e.StuckTicks;
                }
                else
                {
                    e.StuckTicks = 0;
                }

                e.StationaryTicks = 0;
            }
            else
            {
                ++e.StuckTicks;
                ++e.StationaryTicks;
            }

            e.Velocity = (e.Pos - e.PrevPos) / dt;
            bool needPath = e.StuckTicks > StuckTicksBeforePath;
            if (!needPath && e.LocalPath.Count == 0 && (tick + e.Id) % LineCheckInterval == 0 &&
                distance > SlowRadiusM)
            {
                needPath = !terrain.LinePassable(e.Pos, goal, mobility);
            }

            if (needPath && e.PathRequest == null && e.LocalPath.Count == 0)
            {
                e.PathRequest = Battle.Pathfinder.Request(e.Pos, goal, mobility, e.Side);
                e.StuckTicks = 0;
            }
        }

        private SimVec2 Separation(Element e)
        {
            SimVec2 push = SimVec2.Zero;
            foreach (Element other in Battle.Spatial.Query(e.Pos, SeparationRadiusM))
            {
                if (ReferenceEquals(other, e) || other.Side != e.Side || other.IsMounted() || !other.IsActive() ||
                    other.IsAir() != e.IsAir())
                {
                    continue;
                }

                SimVec2 away = e.Pos - other.Pos;
                double distance = away.Length();
                if (distance < 0.01)
                {
                    away = new SimVec2(1.0, 0.0).Rotated(e.Id);
                    distance = 0.01;
                }

                if (distance < SeparationRadiusM)
                {
                    push += away / distance * (1.0 - distance / SeparationRadiusM);
                }
            }

            return push * 0.8;
        }

        private void PollPath(Unit unit)
        {
            GridPathfinder.PathRequest? request = unit.Guide.Pending;
            if (request == null || !request.Done)
            {
                return;
            }

            unit.Guide.Pending = null;
            if (request.Ok)
            {
                unit.Guide.SetPath(new List<SimVec2>(request.Points));
            }
            else
            {
                if (unit.Mission != null)
                {
                    unit.Mission.State = OrderState.CANCELLED;
                    unit.Mission = null;
                }

                unit.Posture = "idle";
            }
        }

        private double AdvanceGuide(Unit unit, double dt, double speed)
        {
            bool wasMoving = unit.Guide.IsMoving();
            double moved = unit.Guide.Advance(dt, speed);
            if (wasMoving && !unit.Guide.IsMoving())
            {
                unit.SettlingSinceTick = Battle.World.Tick;
            }

            if (!unit.Guide.IsMoving() && !double.IsNaN(unit.Guide.FinalFacing))
            {
                unit.Guide.TurnToward(unit.Guide.FinalFacing, dt);
            }

            return moved;
        }

        private void FollowTarget(Platoon p, SimVec2 target, double dt, double speed, double face, long tick)
        {
            string mobility = OrderExecutor.PathMobility(p);
            if (ReachableStraight(p.Guide.Pos, target, mobility))
            {
                if (p.Guide.IsMoving() || p.Guide.Pending != null)
                {
                    p.Guide.ClearPath();
                }

                p.Guide.MoveToward(target, dt, speed, face);
                return;
            }

            GridPathfinder.PathRequest? request = p.Guide.Pending;
            if (request != null && request.Done)
            {
                p.Guide.Pending = null;
                if (request.Ok)
                {
                    p.Guide.SetPath(new List<SimVec2>(request.Points));
                }
            }

            bool stale = p.Guide.HasDestination && p.Guide.Destination.DistanceTo(target) > FollowRepathM;
            if (p.Guide.Pending == null && (!p.Guide.IsMoving() || stale) &&
                (tick + p.Id) % FollowRepathInterval == 0)
            {
                p.Guide.Pending = Battle.Pathfinder.Request(p.Guide.Pos, target, mobility, p.Side);
            }

            p.Guide.Advance(dt, speed);
        }

        private void CheckSettled(Unit unit)
        {
            if (unit.SettlingSinceTick < 0 || unit.Guide.IsMoving() || Battle.World.Tick <= unit.SettlingSinceTick)
            {
                return;
            }

            if (unit.FormationError <= SettledErrorM ||
                Battle.World.Tick - unit.SettlingSinceTick >= SettleTimeoutTicks)
            {
                unit.SettlingSinceTick = -1;
                Battle.Executor.OnArrival(unit);
            }
        }

        private bool ReachableStraight(SimVec2 a, SimVec2 b, string mobility)
        {
            return Battle.Terrain.IsPassable(b, mobility) && Battle.Terrain.LinePassable(a, b, mobility);
        }

        private bool OnRoadAhead(Guide guide)
        {
            if (!guide.IsMoving() || Battle.Terrain.TypeAt(guide.Pos) != (int)Terrain.Type.ROAD)
            {
                return false;
            }

            foreach (SimVec2 point in guide.PointsAhead(60.0, 20.0))
            {
                if (Battle.Terrain.TypeAt(point) != (int)Terrain.Type.ROAD)
                {
                    return false;
                }
            }

            return true;
        }

        private bool ChokeAhead(Guide guide)
        {
            foreach (SimVec2 point in guide.PointsAhead(ChokeLookaheadM, 20.0))
            {
                if (Battle.Terrain.IsBridge(point))
                {
                    return true;
                }
            }

            return false;
        }

        private double RoadMarchSpeed(Unit unit, double baseSpeed)
        {
            double bonus = unit.Formation != null ? unit.Formation.GetMod("road_speed", 1.2) : 1.2;
            return Math.Min(baseSpeed * bonus, baseSpeed * RoadSpeedRatio(unit) * 0.9);
        }

        private double RoadSpeedRatio(Unit unit)
        {
            double ratio = double.PositiveInfinity;
            foreach (Element e in unit.AliveElements())
            {
                if (e.IsMounted())
                {
                    continue;
                }

                ratio = Math.Min(ratio, e.Def.SpeedRoadKmh / Math.Max(e.Def.SpeedOffroadKmh, 1.0));
            }

            return double.IsPositiveInfinity(ratio) ? 1.0 : ratio;
        }

        private sealed class CompanyRest
        {
            internal (float X, float Y, float Z) Key { get; set; }
            internal int N { get; set; }
            internal List<KeyValuePair<int, SimVec2>> Targets { get; } =
                new List<KeyValuePair<int, SimVec2>>();
        }
    }
}
