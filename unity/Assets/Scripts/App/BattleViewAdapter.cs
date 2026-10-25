using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Echelon.Core;
using Echelon.Unity.Presentation;
using Echelon.Unity.Presentation.Elements;
using Echelon.Unity.Presentation.Fx;
using Echelon.Unity.Presentation.Symbols;
using Echelon.Unity.Presentation.Terrain;
using Echelon.Unity.UI.Aar;
using Echelon.Unity.UI.Battle;
using Echelon.Unity.UI.Minimap;
using Echelon.Unity.UI.Tutorial;
using UnityEngine;
using CoreTerrain = Echelon.Core.Terrain;
using PresentationTerrainType = Echelon.Unity.Presentation.Terrain.TerrainType;

namespace Echelon.Unity.App
{
    public static class BattleViewAdapter
    {
        // 고정익을 실제 고도(300~1200 m)로 그리면 카메라 위로 빠진다. 화면에서는 60 m + 고도의 8% 로 눌러 그린다.
        public static float VisualAltitude(Element element)
        {
            return VisualAltitude(element, element.AltitudeM());
        }

        public static float VisualAltitude(Element element, double altitude)
        {
            return element.IsFixedWing() ? (float)(60.0 + altitude * 0.08) : (float)altitude;
        }

        public static TerrainMeshData CreateTerrainData(Battle battle)
        {
            RequireBattle(battle);
            CoreTerrain terrain = battle.Terrain;
            int count = terrain.Width * terrain.HeightCells;
            var types = new PresentationTerrainType[count];
            for (int i = 0; i < count; i++)
                types[i] = (PresentationTerrainType)terrain.Types[i];
            return new TerrainMeshData(
                terrain.Width,
                terrain.HeightCells,
                (float)terrain.CellM,
                ToVector(terrain.SizeM),
                terrain.Heights,
                types,
                Features(battle.Map.Roads),
                Features(battle.Map.Rivers),
                Features(battle.Map.Forests),
                Features(battle.Map.Towns),
                battle.Map.HeightSeed);
        }

        public static float GroundHeight(Battle battle, Vector2 position)
        {
            RequireBattle(battle);
            return (float)battle.Terrain.GroundHeight(ToSim(position));
        }

        public static MinimapTerrainSample SampleMinimapTerrain(Battle battle, Vector2 position)
        {
            RequireBattle(battle);
            SimVec2 point = ToSim(position);
            return new MinimapTerrainSample(
                (MinimapTerrainType)battle.Terrain.TypeAt(point),
                (float)battle.Terrain.GroundHeight(point));
        }

        public static List<ElementVisualState> CreateElementStates(Battle battle)
        {
            RequireBattle(battle);
            var states = new List<ElementVisualState>(battle.Elements.Count);
            long tick = battle.World.Tick;
            foreach (Element element in battle.Elements)
            {
                long lastSeen = ElementVisualState.NoTick;
                if (element.Side == battle.PlayerSide)
                {
                    lastSeen = tick;
                }
                else if (battle.Detection.Seen.TryGetValue(battle.PlayerSide, out Dictionary<int, long> seen)
                         && seen.TryGetValue(element.Id, out long seenTick))
                {
                    lastSeen = seenTick;
                }

                Vector2 position = ToVector(element.Pos);
                states.Add(new ElementVisualState
                {
                    Id = element.Id,
                    ElementId = element.Def.Id,
                    Side = element.Side,
                    ElementClass = element.Def.ElementClass,
                    Loadout = element.Platoon != null ? element.Platoon.Loadout : string.Empty,
                    PreviousPosition = ToVector(element.PrevPos),
                    Position = position,
                    PreviousHeading = (float)element.PrevHeading,
                    Heading = (float)element.Heading,
                    Velocity = ToVector(element.Velocity),
                    Hp = (float)element.Hp,
                    State = (ElementVisualCondition)(int)element.State,
                    IsMounted = element.IsMounted(),
                    OnMap = element.OnMap,
                    TargetId = element.TargetId,
                    Altitude = VisualAltitude(element),
                    TerrainHeight = GroundHeight(battle, position),
                    TerrainNormal = TerrainNormal(battle, position),
                    LastFiredTick = TickToInt(element.LastFiredTick),
                    LastSeenTick = TickToInt(lastSeen)
                });
            }
            return states;
        }

        public static BattlefieldSymbolSnapshot CreateSymbolSnapshot(
            Battle battle,
            IEnumerable<Unit> selection,
            bool fogEnabled,
            float interpolationAlpha = 0f,
            bool includeDeployment = true)
        {
            RequireBattle(battle);
            var snapshot = new BattlefieldSymbolSnapshot
            {
                PlayerSide = battle.PlayerSide,
                FogEnabled = fogEnabled,
                IsDeploying = includeDeployment && battle.Deploying,
                MapSize = ToVector(battle.Terrain.SizeM)
            };
            if (selection != null)
            {
                foreach (Unit unit in selection)
                {
                    if (unit != null)
                        snapshot.SelectedUnitIds.Add(Id(unit.Id));
                }
            }

            foreach (string side in Battle.Sides)
            {
                TaskForce taskForce = battle.GetTaskForce(side);
                if (taskForce == null)
                    continue;
                foreach (Unit unit in taskForce.AllUnits())
                {
                    if (!unit.OnMap || unit.Level == UnitLevel.TaskForce || unit.IsDestroyed())
                        continue;
                    SymbolUnitSnapshot unitSnapshot = CreateSymbolUnit(
                        battle, unit, fogEnabled, interpolationAlpha);
                    if (unitSnapshot != null)
                        snapshot.Units.Add(unitSnapshot);
                }
            }

            AddControlPoints(battle, snapshot.ControlPoints);
            if (battle.Engineers is EngineerSystem engineers)
            {
                foreach (EngineerSystem.Minefield minefield in engineers.KnownMinefields(battle.PlayerSide))
                {
                    snapshot.KnownMinefields.Add(new SymbolMinefieldSnapshot
                    {
                        Side = minefield.Side,
                        Position = ToVector(minefield.Pos),
                        RadiusMeters = (float)minefield.Radius,
                        Charges = minefield.Charges
                    });
                }
                foreach (EngineerSystem.FieldPosition position in engineers.Positions)
                {
                    if (position.Side != battle.PlayerSide)
                        continue;
                    snapshot.Fortifications.Add(new SymbolFortificationSnapshot
                    {
                        Side = position.Side,
                        Position = ToVector(position.Pos),
                        RadiusMeters = (float)position.Radius
                    });
                }
            }

            if (includeDeployment)
                snapshot.Deployment = CreateDeploymentSnapshot(battle);
            return snapshot;
        }

        public static MinimapSnapshot CreateMinimapSnapshot(
            Battle battle,
            bool fogEnabled,
            RtsCameraController camera = null)
        {
            RequireBattle(battle);
            var snapshot = new MinimapSnapshot
            {
                PlayerSide = battle.PlayerSide,
                FogEnabled = fogEnabled,
                MapSize = ToVector(battle.Terrain.SizeM)
            };
            AddControlPoints(battle, snapshot.ControlPoints);
            long tick = battle.World.Tick;
            foreach (Element element in battle.Elements)
            {
                if (!element.OnMap || element.IsMounted() || !element.IsAlive())
                    continue;
                Vector2 position = ToVector(element.Pos);
                SymbolContactState contact = SymbolContactState.Current;
                if (element.Side != battle.PlayerSide && fogEnabled)
                {
                    if (!battle.Detection.SeenRecently(battle.PlayerSide, element.Id, tick))
                    {
                        if (!TryKnownReport(battle, element.Id, out position, out long reportTick))
                            continue;
                        contact = ReportContact(tick, reportTick);
                    }
                }
                snapshot.Contacts.Add(new MinimapContactSnapshot
                {
                    Side = element.Side,
                    Position = position,
                    OnMap = element.OnMap,
                    IsMounted = element.IsMounted(),
                    IsAlive = element.IsAlive(),
                    Contact = contact
                });
            }
            if (camera != null)
            {
                snapshot.Camera = new MinimapCameraSnapshot
                {
                    Visible = true,
                    Focus = camera.SimFocus,
                    Distance = camera.Distance
                };
            }
            return snapshot;
        }

        public static DeploymentZoneData CreateDeploymentZone(Battle battle)
        {
            RequireBattle(battle);
            var data = new DeploymentZoneData { MapSize = ToVector(battle.Terrain.SizeM) };
            if (!battle.Scenario.Sides.TryGetValue(battle.PlayerSide, out ScenarioDef.SideSetup setup))
                return data;
            MapRect zone = setup.DeployZone;
            data.Zone = new Rect(zone.Position.X, zone.Position.Y, zone.Size.X, zone.Size.Y);
            foreach (SimVec2 entry in setup.EntryPoints)
                data.EntryPoints.Add(ToVector(battle.Terrain.ClampToMap(entry, 20.0)));
            return data;
        }

        public static BattleHudState CreateHudState(Battle battle)
        {
            RequireBattle(battle);
            string enemy = Battle.EnemyOf(battle.PlayerSide);
            var state = new BattleHudState
            {
                SimulationSeconds = (float)battle.World.GetSimTimeSec(),
                Speed = (float)battle.World.GetSpeed(),
                HasScoring = battle.Scoring != null,
                TimeLimitSeconds = (float)(battle.Scenario.TimeLimitMin * 60.0),
                Deploying = battle.Deploying,
                Paused = battle.World.Paused
            };
            if (battle.Scoring != null)
            {
                state.PlayerVictoryPoints = (float)battle.Scoring.Vp[battle.PlayerSide];
                state.EnemyVictoryPoints = (float)battle.Scoring.Vp[enemy];
                state.PlayerForceRatio = (float)battle.Scoring.ValueRatio(battle.PlayerSide);
                state.EnemyForceRatio = (float)battle.Scoring.ValueRatio(enemy);
            }
            if (battle.OrderSystem != null)
            {
                state.CommandCapacityUsed = (float)battle.OrderSystem.CapacityNow(battle.PlayerSide);
                state.CommandCapacityMaximum = (float)battle.OrderSystem.CapacityMax(battle.PlayerSide);
                state.CommandCapacityQueue = battle.OrderSystem.CapacityQueueSize(battle.PlayerSide);
            }
            return state;
        }

        public static BattleFireSupportState CreateFireSupportState(Battle battle)
        {
            RequireBattle(battle);
            var state = new BattleFireSupportState();
            TaskForce taskForce = battle.GetTaskForce(battle.PlayerSide);
            if (taskForce != null)
            {
                state.ArtilleryMissions = taskForce.ArtilleryMissions;
                state.CasSorties = taskForce.CasSorties;
                foreach (Platoon platoon in taskForce.AllPlatoons())
                {
                    if (platoon.Kind == "artillery" && platoon.OnMap && !platoon.IsDestroyed())
                        ++state.ArtilleryBatteries;
                }
            }
            foreach (FireSupportSystem.Mission mission in battle.Fires.ActiveMissions(battle.PlayerSide))
            {
                state.ActiveMissions.Add(new BattleFireMissionView
                {
                    Label = mission.Label(),
                    Asset = (BattleFireAsset)(int)mission.Asset,
                    Smoke = mission.Smoke,
                    Phase = (BattleFirePhase)(int)mission.Phase,
                    SecondsRemaining = Mathf.Max(0f, (mission.ReadyTick - battle.World.Tick) / (float)SimConst.TickRate),
                    RoundsLeft = mission.RoundsLeft
                });
            }
            return state;
        }

        public static BattleUnitView CreateUnitView(Battle battle, Unit unit)
        {
            RequireBattle(battle);
            if (unit == null)
                throw new ArgumentNullException(nameof(unit));
            long tick = battle.World.Tick;
            var view = new BattleUnitView
            {
                Id = unit.Id,
                DisplayName = unit.DisplayName,
                ShortName = unit.ShortName,
                Kind = unit.Kind,
                Level = (BattleUnitLevel)(int)unit.Level,
                Strength = (float)unit.Strength(),
                Morale = (float)unit.Morale,
                Comms = (BattleCommsState)(int)unit.CommsState,
                FormationId = unit.Formation != null ? unit.Formation.Id : string.Empty,
                FormationName = unit.Formation != null ? unit.Formation.DisplayName : string.Empty,
                Technique = unit.Sop.Technique,
                RulesOfEngagement = unit.Sop.Roe,
                Posture = unit.Posture,
                MissionType = unit.Mission != null && !unit.Mission.IsFinished()
                    ? (BattleCommandType)(int)unit.Mission.Type
                    : (BattleCommandType?)null,
                Detached = unit.Detached,
                Leaderless = unit.IsLeaderless(tick),
                Reforming = unit.IsReforming(tick),
                Routed = unit.Routed || unit.IsRouted(tick),
                AwaitingFollow = unit.AwaitingFollow,
                OrderPending = battle.OrderSystem != null && battle.OrderSystem.PendingForUnit(unit.Id).Count > 0,
                Destroyed = unit.IsDestroyed(),
                OnMap = unit.OnMap,
                AmmoRatio = (float)battle.AmmoRatio(unit),
                Altitude = unit.Sop.Altitude
            };
            if (unit is Platoon platoon)
            {
                view.HasSquads = platoon.HasSquads();
                view.Mounted = platoon.Mounted;
                view.MinesLeft = platoon.MinesLeft;
                view.PositionsBuilt = platoon.PositionsBuilt;
                view.PositionsCapacity = EngineerSystem.PositionsPerPlatoon;
                if (platoon.IsAirWing)
                {
                    view.AirStatus = AirStatusText(platoon, tick);
                    view.CarriesCargo = AirOpsSystem.CarriesSupply(platoon);
                }
            }
            else if (unit.Kind == "fixed_wing")
            {
                foreach (Unit child in unit.ChildrenUnits())
                {
                    if (child is Platoon flight && flight.IsAirWing && AirOpsSystem.CarriesSupply(flight))
                        view.CarriesCargo = true;
                }
            }
            return view;
        }

        /// <summary>고정익 편대 상태: 비행(남은 연료) · 귀환 중 · 출격 가능 · 출격 준비(남은 시간).</summary>
        public static string AirStatusText(Platoon flight, long tick)
        {
            double fuel = double.MaxValue;
            long readyAt = long.MaxValue;
            bool airborne = false;
            bool returning = false;
            bool ready = false;
            foreach (Element plane in flight.Elements)
            {
                if (!plane.IsAlive())
                    continue;
                switch (plane.Air)
                {
                    case AirState.AIRBORNE:
                        if (plane.OnMap)
                        {
                            airborne = true;
                            fuel = Math.Min(fuel, plane.FuelSec);
                        }
                        break;
                    case AirState.RETURNING:
                        returning = true;
                        break;
                    case AirState.READY:
                        ready = true;
                        break;
                    case AirState.REARMING:
                        readyAt = Math.Min(readyAt, plane.AirReadyTick);
                        break;
                }
            }
            if (airborne)
                return string.Format(CultureInfo.InvariantCulture, "비행 · 연료 {0:0}초", fuel);
            if (returning)
                return "귀환 중";
            if (ready)
                return "출격 가능";
            if (readyAt != long.MaxValue)
                return string.Format(CultureInfo.InvariantCulture, "출격 준비 {0:0}초",
                    Math.Max(0.0, (readyAt - tick) / (double)SimConst.TickRate));
            return string.Empty;
        }

        public static OrbatNodeData CreateOrbat(Battle battle)
        {
            RequireBattle(battle);
            TaskForce taskForce = battle.GetTaskForce(battle.PlayerSide);
            return taskForce == null ? null : CreateOrbatNode(battle, taskForce);
        }

        public static FxEvent CreateFxEvent(Battle battle, SimEvent simEvent, bool fogEnabled)
        {
            RequireBattle(battle);
            if (simEvent == null)
                throw new ArgumentNullException(nameof(simEvent));
            Dictionary<string, object> payload = simEvent.Payload;
            var fx = new FxEvent
            {
                Type = simEvent.Type,
                Tick = TickToInt(simEvent.Tick),
                Position = GroundVector(Vector(payload, "pos"))
            };
            switch (simEvent.Type)
            {
                case FxEventType.Shot:
                {
                    Element source = battle.GetElement(Int(payload, "from", -1));
                    Element target = battle.GetElement(Int(payload, "to", -1));
                    fx.SourceId = source != null ? source.Id : Int(payload, "from", -1);
                    fx.TargetId = target != null ? target.Id : Int(payload, "to", -1);
                    fx.SourcePosition = GroundVector(Vector(payload, "fp"));
                    fx.TargetPosition = GroundVector(Vector(payload, "tp"));
                    fx.Weapon = Text(payload, "weapon");
                    fx.Hit = Bool(payload, "hit");
                    fx.SourceVisible = source != null && IsVisible(battle, source, fogEnabled);
                    fx.TargetVisible = target != null && IsVisible(battle, target, fogEnabled);
                    fx.SourceIsInfantry = source != null && source.IsInfantry();
                    fx.TargetIsInfantry = target != null && target.IsInfantry();
                    fx.SourceAltitude = source != null ? VisualAltitude(source) : 0f;
                    fx.TargetAltitude = target != null ? VisualAltitude(target) : 0f;
                    fx.SourceSide = source != null ? source.Side : string.Empty;
                    return fx;
                }
                case FxEventType.ElementDestroyed:
                {
                    Element destroyed = battle.GetElement(Int(payload, "element", -1));
                    fx.SourceId = destroyed != null ? destroyed.Id : -1;
                    fx.DestroyedIsHelicopter = destroyed != null && destroyed.IsAir();
                    fx.DestroyedHasPlatoon = destroyed != null && destroyed.Platoon != null;
                    fx.DestroyedAltitude = destroyed != null && destroyed.IsFixedWing()
                        ? VisualAltitude(destroyed, destroyed.Def.AltitudeM)
                        : destroyed != null && destroyed.IsAir()
                        ? destroyed.Platoon != null && destroyed.Platoon.Sop.Altitude == "high"
                            ? (float)Element.AltitudeHighM
                            : (float)Element.AltitudeLowM
                        : 0f;
                    return fx;
                }
                case FxEventType.Shell:
                    fx.Asset = Int(payload, "asset", 1);
                    fx.Smoke = Bool(payload, "smoke");
                    return fx;
                case FxEventType.IndirectFire:
                {
                    Element source = battle.GetElement(Int(payload, "from", -1));
                    fx.SourceId = source != null ? source.Id : -1;
                    fx.SourceVisible = source != null && IsVisible(battle, source, fogEnabled);
                    fx.SourceIsMortar = source != null &&
                        (source.Def.ElementClass == "mortar" || source.Def.ElementClass == "inf_squad");
                    return fx;
                }
                case FxEventType.SamLaunch:
                {
                    Element source = battle.GetElement(Int(payload, "from", -1));
                    fx.SourceId = source != null ? source.Id : -1;
                    fx.SourceVisible = source != null && IsVisible(battle, source, fogEnabled);
                    fx.TargetPosition = GroundVector(Vector(payload, "to"));
                    return fx;
                }
                case FxEventType.MineStrike:
                case FxEventType.CasShotDown:
                    return fx;
                default:
                    return null;
            }
        }

        public static BattleEventData CreateEventData(Battle battle, SimEvent simEvent)
        {
            RequireBattle(battle);
            if (simEvent == null)
                throw new ArgumentNullException(nameof(simEvent));
            Dictionary<string, object> payload = simEvent.Payload;
            Unit unit = battle.GetUnit(Int(payload, "unit", -1));
            Unit target = battle.GetUnit(Int(payload, "target", -1));
            Element element = battle.GetElement(Int(payload, "element", -1));
            string side = Text(payload, "side");
            if (string.IsNullOrEmpty(side))
                side = unit != null ? unit.Side : element != null ? element.Side : string.Empty;
            Order order = battle.GetOrder(Int(payload, "order", -1));
            int orderType = order != null ? (int)order.Type : Int(payload, "type", 0);
            string orderTypeName = order != null ? order.TypeName() : Text(payload, "type");
            if (payload.TryGetValue("type", out object rawType) && rawType is string typeName)
            {
                int index = Array.IndexOf(Order.TypeNames, typeName);
                if (index >= 0)
                    orderType = index;
            }
            return new BattleEventData
            {
                Type = simEvent.Type,
                Side = side,
                UnitName = unit != null ? unit.Label() : string.Empty,
                TargetUnitName = target != null ? target.Label() : string.Empty,
                ElementName = element != null ? element.Def.DisplayName : string.Empty,
                Name = Text(payload, "name"),
                Winner = Text(payload, "winner"),
                Reason = Text(payload, "reason"),
                OrderTypeName = orderTypeName,
                HasUnit = unit != null,
                AwaitingFollow = unit != null && unit.AwaitingFollow,
                FollowsParent = unit != null && unit.FollowsParent(),
                None = Bool(payload, "none"),
                VictoryPoints = VictoryPoints(payload, side),
                OrderType = (BattleCommandType)orderType,
                CommsState = (BattleCommsState)Int(payload, "state", 0),
                Seconds = (float)Number(payload, "seconds"),
                Phase = (BattleFirePhase)Int(payload, "phase", 0),
                Asset = (BattleFireAsset)Int(payload, "asset", 0)
            };
        }

        public static AarData CreateAar(IReadOnlyDictionary<string, object> summary)
        {
            if (summary == null)
                throw new ArgumentNullException(nameof(summary));
            var data = new AarData
            {
                DurationSeconds = (float)Number(Member(summary, "duration_sec")),
                FirstContactSeconds = (float)Number(Member(summary, "first_contact_sec"), -1.0),
                TickRate = SimConst.TickRate
            };
            object result = Member(summary, "result");
            data.Result = new AarResultData
            {
                Winner = Text(Member(result, "winner")),
                Reason = Text(Member(result, "reason"))
            };
            object sides = Member(summary, "sides");
            foreach (string side in Battle.Sides)
            {
                object source = Member(sides, side);
                var sideData = new AarSideData
                {
                    Side = side,
                    Name = Text(Member(source, "name")),
                    VictoryPoints = (float)Number(Member(source, "vp")),
                    ValueRatio = (float)Number(Member(source, "value_ratio"), 1.0),
                    LossesTotal = Int(Member(source, "losses_total"), 0),
                    Elements = Int(Member(source, "elements"), 0),
                    Damaged = Int(Member(source, "damaged"), 0),
                    OrderDelayMean = (float)Number(Member(source, "order_delay_mean")),
                    OrderDelayMaximum = (float)Number(Member(source, "order_delay_max")),
                    Orders = Int(Member(source, "orders"), 0),
                    FireMissions = Int(Member(source, "fire_missions"), 0),
                    CommsCuts = Int(Member(source, "comms_cuts"), 0),
                    Resupplies = Int(Member(source, "resupplies"), 0),
                    Shots = Int(Member(source, "shots"), 0)
                };
                if (Member(source, "losses") is IDictionary losses)
                {
                    foreach (DictionaryEntry loss in losses)
                    {
                        sideData.Losses.Add(new AarLossData(
                            Convert.ToString(loss.Key, CultureInfo.InvariantCulture),
                            Convert.ToInt32(loss.Value, CultureInfo.InvariantCulture)));
                    }
                }
                data.Sides.Add(sideData);
            }
            foreach (object point in Items(Member(summary, "points")))
            {
                data.Points.Add(new AarControlPointData
                {
                    Id = Text(Member(point, "id")),
                    Name = Text(Member(point, "name")),
                    Owner = Text(Member(point, "owner")),
                    HeldBlueSeconds = (float)Number(Member(point, "held_blue_sec")),
                    HeldRedSeconds = (float)Number(Member(point, "held_red_sec"))
                });
            }
            foreach (object entry in Items(Member(summary, "timeline")))
            {
                data.Timeline.Add(new AarTimelineEntry
                {
                    Tick = Long(Member(entry, "tick"), 0),
                    Type = Text(Member(entry, "type")),
                    Side = Text(Member(entry, "side")),
                    Text = Text(Member(entry, "text"))
                });
            }
            return data;
        }

        public static TutorialState CreateTutorialState(Battle battle)
        {
            RequireBattle(battle);
            var state = new TutorialState { PlayerSide = battle.PlayerSide };
            TaskForce player = battle.GetTaskForce(battle.PlayerSide);
            if (player != null && player.Companies.Count > 0)
                state.CompanyUnitId = Id(player.Companies[0].Id);
            TaskForce enemy = battle.GetTaskForce(Battle.EnemyOf(battle.PlayerSide));
            if (enemy != null)
            {
                Platoon found = null;
                foreach (Platoon platoon in enemy.Organic)
                {
                    found = platoon;
                    if (!platoon.OnMap)
                        break;
                }
                if (found == null && enemy.AllPlatoons().Count > 0)
                    found = enemy.AllPlatoons()[0];
                if (found != null)
                    state.EnemyUnitId = Id(found.Id);
            }
            UpdateTutorialState(battle, state, null, null, null);
            return state;
        }

        public static void UpdateTutorialState(
            Battle battle,
            TutorialState state,
            IEnumerable<Unit> selection,
            IEnumerable<int> controlGroup1,
            RtsCameraController camera)
        {
            RequireBattle(battle);
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            state.PlayerSide = battle.PlayerSide;
            state.IsPaused = battle.World.Paused;
            state.TimeSpeed = (float)battle.World.GetSpeed();
            state.HasBattleResult = battle.HasResult;
            if (camera != null)
            {
                state.CameraFocus = camera.SimFocus;
                state.CameraDistance = camera.Distance;
            }

            state.Selection.Clear();
            if (selection != null)
            {
                foreach (Unit unit in selection)
                {
                    if (unit != null)
                        state.Selection.Add(new TutorialSelectedUnit(Id(unit.Id), unit.Parent != null ? Id(unit.Parent.Id) : string.Empty));
                }
            }
            state.ControlGroup1.Clear();
            if (controlGroup1 != null)
            {
                foreach (int id in controlGroup1)
                    state.ControlGroup1.Add(Id(id));
            }

            Unit company = battle.GetUnit(ParseId(state.CompanyUnitId));
            state.CompanyPosition = company != null ? ToVector(company.Centroid()) : Vector2.zero;
            state.CompanyFormationId = company != null && company.Formation != null ? company.Formation.Id : string.Empty;
            state.CompanyRoe = company != null ? company.Sop.Roe : string.Empty;
            state.CompanyElements.Clear();
            if (company != null)
            {
                foreach (Element element in company.AllElements())
                    state.CompanyElements.Add(new TutorialElementState(element.IsAlive(), ToVector(element.Velocity)));
            }

            Unit enemy = battle.GetUnit(ParseId(state.EnemyUnitId));
            state.EnemyOnMap = enemy != null && enemy.OnMap;
            state.EnemyIsDestroyed = enemy == null || enemy.IsDestroyed();
            state.EnemyElements.Clear();
            if (enemy != null)
            {
                foreach (Element element in enemy.AllElements())
                    state.EnemyElements.Add(new TutorialElementState(element.IsAlive(), ToVector(element.Velocity)));
            }
        }

        public static Vector2 ToVector(SimVec2 value)
        {
            return new Vector2(value.X, value.Y);
        }

        public static SimVec2 ToSim(Vector2 value)
        {
            return new SimVec2(value.x, value.y);
        }

        private static SymbolUnitSnapshot CreateSymbolUnit(
            Battle battle,
            Unit unit,
            bool fogEnabled,
            float alpha)
        {
            bool hostile = unit.Side != battle.PlayerSide;
            Vector2 position = UnitPosition(unit, alpha);
            SymbolContactState contact = SymbolContactState.Current;
            if (hostile && fogEnabled && !TryEnemyUnitPosition(battle, unit, out position, out contact))
                return null;
            var snapshot = new SymbolUnitSnapshot
            {
                Id = Id(unit.Id),
                ParentId = unit.Parent != null ? Id(unit.Parent.Id) : string.Empty,
                Side = unit.Side,
                Branch = unit.Kind,
                Label = unit.Label(),
                Level = (SymbolUnitLevel)(int)unit.Level,
                OnMap = unit.OnMap,
                IsDestroyed = unit.IsDestroyed(),
                Detached = unit.Detached,
                FollowsParent = unit.FollowsParent(),
                Position = position,
                GuidePosition = hostile && fogEnabled ? position : ToVector(unit.Guide.Pos),
                Altitude = contact == SymbolContactState.Current ? UnitAltitude(unit) : 0f,
                Strength = hostile ? 1f : (float)unit.Strength(),
                Comms = (SymbolCommsState)(int)unit.CommsState,
                Contact = contact
            };
            if (unit is Platoon platoon)
            {
                Element leader = platoon.Leader();
                foreach (Element element in platoon.Elements)
                {
                    SymbolElementSnapshot elementSnapshot = CreateSymbolElement(
                        battle, platoon, element, leader, fogEnabled, alpha);
                    if (elementSnapshot != null)
                        snapshot.Elements.Add(elementSnapshot);
                }
            }
            if (!hostile)
                AddMovementAndOrders(battle, unit, snapshot);
            return snapshot;
        }

        private static SymbolElementSnapshot CreateSymbolElement(
            Battle battle,
            Platoon platoon,
            Element element,
            Element leader,
            bool fogEnabled,
            float alpha)
        {
            if (!element.OnMap || element.IsMounted())
                return null;
            bool hostile = element.Side != battle.PlayerSide;
            bool current = !hostile || !fogEnabled ||
                (element.IsAlive() && battle.Detection.SeenRecently(battle.PlayerSide, element.Id, battle.World.Tick));
            Vector2 position = Vector2.Lerp(ToVector(element.PrevPos), ToVector(element.Pos), Mathf.Clamp01(alpha));
            float heading = Mathf.LerpAngle(
                (float)element.PrevHeading * Mathf.Rad2Deg,
                (float)element.Heading * Mathf.Rad2Deg,
                Mathf.Clamp01(alpha)) * Mathf.Deg2Rad;
            SymbolContactState contact = SymbolContactState.Current;
            if (hostile && fogEnabled && !current)
            {
                if (!TryKnownReport(battle, element.Id, out position, out long reportTick))
                    return null;
                contact = ReportContact(battle.World.Tick, reportTick);
                heading = 0f;
            }
            return new SymbolElementSnapshot
            {
                Id = Id(element.Id),
                OnMap = element.OnMap,
                IsMounted = element.IsMounted(),
                IsAlive = element.IsAlive(),
                IsInfantry = element.IsInfantry(),
                IsLeader = ReferenceEquals(element, leader),
                Position = position,
                Forward = new Vector2(Mathf.Sin(heading), Mathf.Cos(heading)),
                Altitude = contact == SymbolContactState.Current && element.IsAlive() ? VisualAltitude(element) : 0f,
                Contact = contact
            };
        }

        /// <summary>소대가 모두 날고 있으면 부호를 띄울 화면 고도(평균), 아니면 0.</summary>
        private static float UnitAltitude(Unit unit)
        {
            if (!(unit is Platoon platoon))
                return 0f;
            float sum = 0f;
            int count = 0;
            foreach (Element element in platoon.Elements)
            {
                if (!element.IsActive() || element.IsMounted())
                    continue;
                if (!element.IsAir())
                    return 0f;
                sum += VisualAltitude(element);
                ++count;
            }
            return count > 0 ? sum / count : 0f;
        }

        private static bool TryEnemyUnitPosition(
            Battle battle,
            Unit unit,
            out Vector2 position,
            out SymbolContactState contact)
        {
            Vector2 liveSum = Vector2.zero;
            int liveCount = 0;
            Vector2 reportSum = Vector2.zero;
            int reportCount = 0;
            long newest = long.MinValue;
            foreach (Element element in unit.AllElements())
            {
                if (element.IsAlive() && element.OnMap && !element.IsMounted()
                    && battle.Detection.SeenRecently(battle.PlayerSide, element.Id, battle.World.Tick))
                {
                    liveSum += ToVector(element.Pos);
                    liveCount++;
                }
                if (TryKnownReport(battle, element.Id, out Vector2 report, out long reportTick))
                {
                    reportSum += report;
                    reportCount++;
                    newest = Math.Max(newest, reportTick);
                }
            }
            if (liveCount > 0)
            {
                position = liveSum / liveCount;
                contact = SymbolContactState.Current;
                return true;
            }
            if (reportCount > 0)
            {
                position = reportSum / reportCount;
                contact = ReportContact(battle.World.Tick, newest);
                return true;
            }
            position = Vector2.zero;
            contact = SymbolContactState.Hidden;
            return false;
        }

        private static void AddMovementAndOrders(Battle battle, Unit unit, SymbolUnitSnapshot snapshot)
        {
            var movement = new SymbolMovementSnapshot();
            if (unit.Guide.IsMoving())
            {
                movement.Path.Add(ToVector(unit.Guide.Pos));
                for (int i = unit.Guide.PathIndex; i < unit.Guide.Path.Count; i++)
                    movement.Path.Add(ToVector(unit.Guide.Path[i]));
            }
            if (unit is Platoon platoon)
            {
                foreach (Element element in platoon.FormationMembers())
                    movement.FormationGhosts.Add(ToVector(element.MoveTarget));
            }
            movement.IsDefending = unit.Posture == "defending" && !double.IsNaN(unit.DefendFacing);
            if (movement.IsDefending)
                movement.DefendDirection = ToVector(FormationSolver.ForwardVector(unit.DefendFacing));
            snapshot.Movement = movement;

            if (battle.OrderSystem == null)
                return;
            foreach (Order order in battle.OrderSystem.PendingForUnit(unit.Id))
            {
                if (!order.HasPos())
                    continue;
                snapshot.PendingOrders.Add(new SymbolPendingOrderSnapshot
                {
                    TargetPosition = ToVector(order.GetPos()),
                    TypeName = order.TypeName(),
                    RemainingSeconds = Mathf.Max(0f, (order.EtaTick() - battle.World.Tick) / (float)SimConst.TickRate),
                    WaitingForComms = order.BlockedSinceTick >= 0
                });
            }
        }

        private static Vector2 UnitPosition(Unit unit, float alpha)
        {
            List<Element> elements = unit.ActiveElements();
            Vector2 sum = Vector2.zero;
            int count = 0;
            foreach (Element element in elements)
            {
                if (element.IsMounted())
                    continue;
                sum += Vector2.Lerp(ToVector(element.PrevPos), ToVector(element.Pos), Mathf.Clamp01(alpha));
                count++;
            }
            return count > 0 ? sum / count : ToVector(unit.Guide.Pos);
        }

        private static void AddControlPoints(Battle battle, List<SymbolControlPointSnapshot> output)
        {
            if (battle.Scoring == null)
                return;
            foreach (ScoringSystem.PointState point in battle.Scoring.Points)
            {
                output.Add(new SymbolControlPointSnapshot
                {
                    Id = point.Def.Id,
                    DisplayName = point.Def.DisplayName,
                    Position = ToVector(point.Def.Pos),
                    RadiusMeters = (float)point.Def.RadiusM,
                    OwnerSide = point.Owner,
                    Contested = point.Contested
                });
            }
        }

        private static SymbolDeploymentSnapshot CreateDeploymentSnapshot(Battle battle)
        {
            DeploymentZoneData zone = CreateDeploymentZone(battle);
            var snapshot = new SymbolDeploymentSnapshot { Zone = zone.Zone };
            snapshot.EntryPoints.AddRange(zone.EntryPoints);
            return snapshot;
        }

        private static OrbatNodeData CreateOrbatNode(Battle battle, Unit unit)
        {
            var node = new OrbatNodeData(CreateUnitView(battle, unit));
            foreach (Unit child in unit.ChildrenUnits())
                node.Children.Add(CreateOrbatNode(battle, child));
            return node;
        }

        private static bool TryKnownReport(Battle battle, int elementId, out Vector2 position, out long tick)
        {
            TaskForce taskForce = battle.GetTaskForce(battle.PlayerSide);
            if (taskForce != null && taskForce.KnownEnemies.TryGetValue(elementId, out Dictionary<string, object> report))
            {
                position = Vector(report, "pos");
                tick = Long(Member(report, "tick"), -1);
                return true;
            }
            position = Vector2.zero;
            tick = -1;
            return false;
        }

        private static SymbolContactState ReportContact(long tick, long reportTick)
        {
            return tick - reportTick > 300 ? SymbolContactState.Stale : SymbolContactState.Reported;
        }

        private static bool IsVisible(Battle battle, Element element, bool fogEnabled)
        {
            return !fogEnabled || element.Side == battle.PlayerSide
                || battle.Detection.SeenRecently(battle.PlayerSide, element.Id, battle.World.Tick);
        }

        private static List<TerrainVectorFeature> Features(List<MapDef.Feature> features)
        {
            var output = new List<TerrainVectorFeature>(features.Count);
            foreach (MapDef.Feature feature in features)
            {
                var points = new List<Vector2>(feature.Points.Count);
                foreach (SimVec2 point in feature.Points)
                    points.Add(ToVector(point));
                output.Add(new TerrainVectorFeature(feature.Name, points, (float)feature.WidthM));
            }
            return output;
        }

        private static Vector3 TerrainNormal(Battle battle, Vector2 position)
        {
            double step = battle.Terrain.CellM;
            float left = GroundHeight(battle, position + Vector2.left * (float)step);
            float right = GroundHeight(battle, position + Vector2.right * (float)step);
            float down = GroundHeight(battle, position + Vector2.down * (float)step);
            float up = GroundHeight(battle, position + Vector2.up * (float)step);
            return new Vector3(left - right, 2f * (float)step, down - up).normalized;
        }

        private static Vector3 GroundVector(Vector2 position)
        {
            return new Vector3(position.x, 0f, position.y);
        }

        private static int TickToInt(long tick)
        {
            return tick > int.MaxValue ? int.MaxValue : tick < int.MinValue ? int.MinValue : (int)tick;
        }

        private static string Id(int id)
        {
            return id.ToString(CultureInfo.InvariantCulture);
        }

        private static int ParseId(string id)
        {
            return int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : -1;
        }

        private static Vector2 Vector(IReadOnlyDictionary<string, object> source, string key)
        {
            object value = Member(source, key);
            if (value is SimVec2 sim)
                return ToVector(sim);
            if (value is Vector2 vector)
                return vector;
            if (value is IList list && list.Count >= 2)
            {
                return new Vector2(
                    Convert.ToSingle(list[0], CultureInfo.InvariantCulture),
                    Convert.ToSingle(list[1], CultureInfo.InvariantCulture));
            }
            return Vector2.zero;
        }

        private static string Text(IReadOnlyDictionary<string, object> source, string key)
        {
            return Text(Member(source, key));
        }

        private static string Text(object value)
        {
            return value == null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static int VictoryPoints(IReadOnlyDictionary<string, object> payload, string side)
        {
            object value = Member(payload, "vp");
            if (value is IDictionary || value is IReadOnlyDictionary<string, object>)
            {
                string scoreSide = string.IsNullOrEmpty(side) ? Text(payload, "winner") : side;
                return Int(Member(value, scoreSide), 0);
            }
            return Int(value, 0);
        }

        private static int Int(IReadOnlyDictionary<string, object> source, string key, int fallback)
        {
            object value = Member(source, key);
            if (value is string text && !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                return fallback;
            try
            {
                return value == null ? fallback : Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch (InvalidCastException error)
            {
                throw new InvalidCastException(
                    $"Battle event field '{key}' expects an integer, received {value} ({value.GetType().FullName}).",
                    error);
            }
        }

        private static int Int(object value, int fallback)
        {
            return value == null ? fallback : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static long Long(object value, long fallback)
        {
            return value == null ? fallback : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        private static double Number(IReadOnlyDictionary<string, object> source, string key, double fallback = 0.0)
        {
            return Number(Member(source, key), fallback);
        }

        private static double Number(object value, double fallback = 0.0)
        {
            return value == null ? fallback : Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        private static bool Bool(IReadOnlyDictionary<string, object> source, string key)
        {
            object value = Member(source, key);
            return value != null && Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }

        private static object Member(object source, string key)
        {
            if (source is IReadOnlyDictionary<string, object> readOnly
                && readOnly.TryGetValue(key, out object readOnlyValue))
                return readOnlyValue;
            if (source is IDictionary dictionary && dictionary.Contains(key))
                return dictionary[key];
            return null;
        }

        private static IEnumerable Items(object source)
        {
            return source is IEnumerable enumerable && !(source is string) ? enumerable : Array.Empty<object>();
        }

        private static void RequireBattle(Battle battle)
        {
            if (battle == null)
                throw new ArgumentNullException(nameof(battle));
        }
    }
}
