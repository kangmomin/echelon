using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Echelon.Core;
using Newtonsoft.Json.Linq;
using R = Echelon.Headless.SnapshotReflection;

namespace Echelon.Headless
{
    public static class CanonicalSnapshot
    {
        public const string SnapshotSchema = "echelon.battle_oracle.snapshot.v1";
        public const string CheckpointSchema = "echelon.battle_oracle.checkpoints.v1";
        public const string RunSchema = "echelon.battle_oracle.run.v1";

        public static byte[] SnapshotBytes(Battle battle, int seed, IReadOnlyList<string> reasons,
            IReadOnlyList<long> arrivalEventTicks)
        {
            if (battle == null)
                throw new ArgumentNullException(nameof(battle));
            return CanonicalJson.Bytes(Snapshot(battle, seed, reasons, arrivalEventTicks));
        }

        public static byte[] AarBytes(Battle battle, int seed)
        {
            if (battle == null)
                throw new ArgumentNullException(nameof(battle));
            Dictionary<string, object> aar = battle.Stats.Summary();
            aar["seed"] = battle.World.Rng.SeedValue;
            return CanonicalJson.Bytes(aar);
        }

        public static byte[] CanonicalBytes(object value)
        {
            return CanonicalJson.Bytes(value);
        }

        public static int Run(DataCatalog catalog, string[] args)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            if (!catalog.IsValid())
                throw new InvalidDataException("Cannot export canonical snapshots from an invalid catalog");
            string outputRoot = Option(args, "--out=") ?? "build/snapshot_unity/csharp";
            string baselineRoot = Option(args, "--baselines=") ?? "build/balance/after";
            int maxTicks = int.Parse(Option(args, "--max-ticks=") ?? "0", CultureInfo.InvariantCulture);
            List<int> seeds = IntList(Option(args, "--seeds=") ?? Option(args, "--seed=") ?? "1,8");
            HashSet<int> detailSeeds = new HashSet<int>(IntList(Option(args, "--detail=") ?? "1,8"));
            bool quiet = Flag(args, "--quiet");
            ForceSpec blue = catalog.DefaultForce("US") ?? throw new InvalidDataException("Default US force is missing");
            ForceSpec red = catalog.DefaultForce("RU") ?? throw new InvalidDataException("Default RU force is missing");
            bool incomplete = false;
            Directory.CreateDirectory(outputRoot);
            foreach (int seed in seeds)
            {
                var arrivals = new SortedDictionary<long, long>();
                Battle battle = BattleSetup.Skirmish(catalog, blue, red, "skirmish_01", seed,
                    new[] { "blue", "red" });
                try
                {
                    battle.World.Events.AddListener(simEvent =>
                    {
                        if (simEvent.Type == "reinforcement_arrived" && !arrivals.ContainsKey(simEvent.Tick + 1))
                            arrivals.Add(simEvent.Tick + 1, simEvent.Tick);
                    });
                    battle.BeginBattle();
                    string seedDirectory = Path.Combine(outputRoot, "seed_" + seed.ToString(CultureInfo.InvariantCulture));
                    Directory.CreateDirectory(seedDirectory);
                    var checkpoints = new List<object?>();
                    CaptureCheckpoint(battle, seed, new[] { "initial" }, Array.Empty<long>(), checkpoints,
                        seedDirectory, detailSeeds.Contains(seed));
                    int limit = (int)Math.Round(battle.Scenario.TimeLimitMin * 60.0 * SimConst.TickRate,
                        MidpointRounding.AwayFromZero) + 100;
                    if (maxTicks > 0)
                        limit = Math.Min(limit, maxTicks);
                    while (!battle.HasResult && battle.Tick() < limit)
                    {
                        battle.StepTick();
                        if (battle.Tick() % 1200 == 0)
                            battle.World.Events.Drain();
                        var reasons = new List<string>();
                        if (battle.Tick() == 100 || battle.Tick() == 600 || battle.Tick() % 1200 == 0)
                            reasons.Add("scheduled");
                        bool arrival = arrivals.ContainsKey(battle.Tick());
                        if (arrival)
                            reasons.Add("reinforcement_arrival");
                        if (battle.HasResult)
                            reasons.Add("final");
                        else if (battle.Tick() >= limit)
                            reasons.Add("truncated");
                        if (reasons.Count == 0)
                            continue;
                        long[] arrivalTicks = arrival ? new[] { arrivals[battle.Tick()] } : Array.Empty<long>();
                        CaptureCheckpoint(battle, seed, reasons, arrivalTicks, checkpoints,
                            seedDirectory, detailSeeds.Contains(seed));
                    }
                    incomplete |= !battle.HasResult;
                    byte[] aarBytes = AarBytes(battle, seed);
                    Dictionary<string, object> rawAar = battle.Stats.Summary();
                    rawAar["seed"] = battle.World.Rng.SeedValue;
                    byte[] compatBytes = PrettyBytes(rawAar);
                    WriteArtifact(Path.Combine(seedDirectory, "aar.json"), aarBytes);
                    WriteArtifact(Path.Combine(seedDirectory, "aar.compat.json"), compatBytes);
                    string baselinePath = Path.Combine(baselineRoot, "aar_" + seed + ".json");
                    SortedDictionary<string, object?> baseline = CompareBaseline(baselinePath, compatBytes);
                    var index = O(
                        ("baseline", baseline),
                        ("checkpoints", checkpoints),
                        ("final_aar", O(
                            ("canonical_file", "aar.json"),
                            ("canonical_sha256", CanonicalJson.Sha256(aarBytes)),
                            ("compatibility_file", "aar.compat.json"),
                            ("compatibility_sha256", CanonicalJson.Sha256(compatBytes)),
                            ("real_sec", "excluded"))),
                        ("final_tick", I(battle.Tick())),
                        ("result_tick", I(ResultInt(battle.Result, "tick", -1))),
                        ("scenario", battle.Scenario.Id),
                        ("schema", CheckpointSchema),
                        ("seed", I(seed)),
                        ("truncated", !battle.HasResult));
                    WriteArtifact(Path.Combine(seedDirectory, "checkpoints.json"), CanonicalJson.Bytes(index));
                    if (!quiet)
                    {
                        string winner = ResultText(battle.Result, "winner");
                        string reason = ResultText(battle.Result, "reason");
                        Console.WriteLine($"[canonical-snapshot] seed={seed} final_tick={battle.Tick()} " +
                            $"winner={winner} reason={reason} checkpoints={checkpoints.Count} " +
                            $"baseline={baseline["matches"]}");
                    }
                }
                finally
                {
                    battle.Dispose();
                }
            }
            return incomplete ? 1 : 0;
        }

        private static SortedDictionary<string, object?> Snapshot(Battle battle, int seed,
            IReadOnlyList<string> reasons, IReadOnlyList<long> arrivalEventTicks)
        {
            PathInfo path = PathState(battle);
            var elements = new List<object?>();
            foreach (Element element in battle.Elements)
                elements.Add(ElementState(element, path));
            var units = new List<object?>();
            foreach (Unit unit in battle.Units)
                units.Add(UnitState(unit, path));
            var orders = new List<object?>();
            foreach (int orderId in battle.OrdersById.Keys.OrderBy(id => id))
                orders.Add(OrderState(battle.OrdersById[orderId]));
            var systems = new List<object?>();
            foreach (SimSystem system in battle.World.GetSystems())
            {
                systems.Add(O(
                    ("name", system.GetSystemName()),
                    ("omitted_from_world_hash", system.OmitFromHash()),
                    ("state_hash", CanonicalJson.Int64(system.StateHash()))));
            }
            return O(
                ("ai", AiState(battle)),
                ("battle", O(
                    ("deploying", battle.Deploying),
                    ("player_side", battle.PlayerSide),
                    ("result", battle.Result))),
                ("checkpoint", O(
                    ("arrival_event_ticks", Integers(arrivalEventTicks)),
                    ("reasons", reasons),
                    ("tick", I(battle.Tick())))),
                ("clock", O(
                    ("accumulator", F(R.Get<double>(battle.World, "_accum"))),
                    ("paused", battle.World.Paused),
                    ("speed_index", I(battle.World.SpeedIndex)),
                    ("tick", I(battle.World.Tick)))),
                ("combat", O(
                    ("hits", I(battle.Combat.Hits)),
                    ("kills", I(battle.Combat.Kills)),
                    ("shots_fired", I(battle.Combat.ShotsFired)))),
                ("comms", CommsState(battle)),
                ("detection", DetectionState(battle)),
                ("elements", elements),
                ("engine_hashes", O(
                    ("systems", systems),
                    ("world", CanonicalJson.Int64(battle.World.StateHash())))),
                ("engineers", EngineerState(battle)),
                ("events", EventState(battle)),
                ("fires", FiresState(battle)),
                ("movement", MovementState(battle)),
                ("next_ids", O(
                    ("element", I(R.Get<int>(battle, "_nextElementId"))),
                    ("fire_mission", I(R.Get<int>(battle.Fires, "_nextId"))),
                    ("minefield", I(R.Get<int>(RequireEngineers(battle), "_nextId"))),
                    ("order", I(R.Get<int>(battle, "_nextOrderId"))),
                    ("unit", I(R.Get<int>(battle, "_nextUnitId"))))),
                ("order_system", OrderSystemState(battle)),
                ("orders", orders),
                ("path", path.State),
                ("reinforcements", O(("waves", ReinforcementWaves(battle)))),
                ("rng", O(
                    ("seed", I(battle.World.Rng.SeedValue)),
                    ("state", CanonicalJson.Int64(battle.World.Rng.GetState())))),
                ("scenario", battle.Scenario.Id),
                ("schema", SnapshotSchema),
                ("scoring", ScoringState(battle)),
                ("seed", I(seed)),
                ("smokes", SmokeStates(battle)),
                ("spatial", SpatialState(battle)),
                ("stats", StatsState(battle)),
                ("supply", SupplyState(battle)),
                ("units", units));
        }

        private static SortedDictionary<string, object?> ElementState(Element element, PathInfo path)
        {
            return O(
                ("ammo", SortedStringIntMap(element.Ammo)),
                ("damage_kind", element.DamageKind),
                ("def", element.Def.Id),
                ("destroyed_tick", I(element.DestroyedTick)),
                ("has_move_target", element.HasMoveTarget),
                ("heading", F(element.Heading)),
                ("hp", F(element.Hp)),
                ("id", I(element.Id)),
                ("index_in_platoon", I(element.IndexInPlatoon)),
                ("killed_by", I(element.KilledBy)),
                ("kills", I(element.Kills)),
                ("last_fired_tick", I(element.LastFiredTick)),
                ("last_hit_tick", I(element.LastHitTick)),
                ("local_path", CanonicalJson.Normalize(element.LocalPath)),
                ("local_path_index", I(element.LocalPathIndex)),
                ("mounted_in", I(element.MountedIn?.Id ?? -1)),
                ("move_target", CanonicalJson.Vec2(element.MoveTarget)),
                ("on_map", element.OnMap),
                ("passengers", ElementIds(element.Passengers)),
                ("path_request_ref", I(RequestRef(element.PathRequest, path))),
                ("platoon", I(element.Platoon?.Id ?? -1)),
                ("pos", CanonicalJson.Vec2(element.Pos)),
                ("prev_heading", F(element.PrevHeading)),
                ("prev_pos", CanonicalJson.Vec2(element.PrevPos)),
                ("reload", SortedFloatMap(element.Reload)),
                ("repair_sec", F(element.RepairSec)),
                ("rest_heading", F(element.RestHeading)),
                ("side", element.Side),
                ("state", I((int)element.State)),
                ("state_name", Element.StateNames[(int)element.State]),
                ("stationary_ticks", I(element.StationaryTicks)),
                ("straggler_ticks", I(element.StragglerTicks)),
                ("stuck_ticks", I(element.StuckTicks)),
                ("supply_left", F(element.SupplyLeft)),
                ("suppression", F(element.Suppression)),
                ("target_id", I(element.TargetId)),
                ("velocity", CanonicalJson.Vec2(element.Velocity)));
        }

        private static SortedDictionary<string, object?> UnitState(Unit unit, PathInfo path)
        {
            object? platoonState = null;
            object? companyState = null;
            object? taskForceState = null;
            if (unit is Platoon platoon)
            {
                platoonState = O(
                    ("attached", platoon.Attached),
                    ("contacts", OrderedIntMap(platoon.Contacts)),
                    ("eng_progress", F(platoon.EngProgress)),
                    ("eng_task", platoon.EngTask),
                    ("is_hq_section", platoon.IsHqSection),
                    ("last_assign_key", CanonicalJson.Vec3(platoon.LastAssignKey.X,
                        platoon.LastAssignKey.Y, platoon.LastAssignKey.Z)),
                    ("last_assign_members", I(platoon.LastAssignMembers)),
                    ("last_shell_pos", CanonicalJson.Vec2(platoon.LastShellPos)),
                    ("last_shelled_tick", I(platoon.LastShelledTick)),
                    ("loadout", platoon.Loadout),
                    ("mines_left", I(platoon.MinesLeft)),
                    ("mounted", platoon.Mounted),
                    ("positions_built", I(platoon.PositionsBuilt)));
            }
            else if (unit is Company company)
            {
                companyState = O(
                    ("hq", I(company.Hq?.Id ?? -1)),
                    ("platoons", UnitIds(company.Platoons)),
                    ("xo", I(company.Xo?.Id ?? -1)));
            }
            else if (unit is TaskForce taskForce)
            {
                var known = new List<object?>();
                foreach (KeyValuePair<int, Dictionary<string, object>> pair in taskForce.KnownEnemies)
                {
                    known.Add(O(
                        ("class", Text(pair.Value, "class")),
                        ("element", I(pair.Key)),
                        ("pos", CanonicalJson.Normalize(pair.Value["pos"])),
                        ("tick", CanonicalJson.Normalize(pair.Value["tick"]))));
                }
                taskForceState = O(
                    ("artillery_missions", I(taskForce.ArtilleryMissions)),
                    ("cas_sorties", I(taskForce.CasSorties)),
                    ("command_capacity_per_min", F(taskForce.CommandCapacityPerMin)),
                    ("companies", UnitIds(taskForce.Companies)),
                    ("cp_lost", taskForce.CpLost),
                    ("cp_section", I(taskForce.CpSection?.Id ?? -1)),
                    ("entry_pos", CanonicalJson.Vec2(taskForce.EntryPos)),
                    ("faction", taskForce.Faction),
                    ("known_enemies", known),
                    ("organic", UnitIds(taskForce.Organic)),
                    ("toe", taskForce.Toe?.Id ?? ""),
                    ("vp", F(taskForce.Vp)));
            }
            return O(
                ("ad_hoc", unit.AdHoc),
                ("auto_withdrawn", unit.AutoWithdrawn),
                ("awaiting_follow", unit.AwaitingFollow),
                ("column_hold_m", F(unit.ColumnHoldM)),
                ("column_mode", unit.ColumnMode),
                ("commander", I(unit.Commander?.Id ?? -1)),
                ("comms_hops", I(unit.CommsHops)),
                ("comms_quality", F(unit.CommsQuality)),
                ("comms_state", I((int)unit.CommsState)),
                ("company", companyState),
                ("contact_halt_until", I(unit.ContactHaltUntil)),
                ("defend_facing", F(unit.DefendFacing)),
                ("detached", unit.Detached),
                ("display_name", unit.DisplayName),
                ("formation", unit.Formation?.Id ?? ""),
                ("formation_error", F(unit.FormationError)),
                ("guide", GuideState(unit.Guide, path)),
                ("guide_speed_limit", F(unit.GuideSpeedLimit)),
                ("id", I(unit.Id)),
                ("initial_element_count", I(unit.InitialElementCount)),
                ("kind", unit.Kind),
                ("last_contact_tick", I(unit.LastContactTick)),
                ("last_hit_tick", I(unit.LastHitTick)),
                ("level", I((int)unit.Level)),
                ("mission", I(unit.Mission?.Id ?? -1)),
                ("morale", F(unit.Morale)),
                ("next_disperse_tick", I(unit.NextDisperseTick)),
                ("next_halt_tick", I(unit.NextHaltTick)),
                ("on_map", unit.OnMap),
                ("parent", I(unit.Parent?.Id ?? -1)),
                ("pending_successor", I(unit.PendingSuccessor?.Id ?? -1)),
                ("phase", I(unit.Phase)),
                ("platoon", platoonState),
                ("posture", unit.Posture),
                ("queued_orders", OrderIds(unit.QueuedOrders)),
                ("reform_until_tick", I(unit.ReformUntilTick)),
                ("role", unit.Role),
                ("routed", unit.Routed),
                ("routed_until_tick", I(unit.RoutedUntilTick)),
                ("settling_since_tick", I(unit.SettlingSinceTick)),
                ("short_name", unit.ShortName),
                ("side", unit.Side),
                ("skill", unit.Skill),
                ("sop", unit.Sop.ToDict()),
                ("succession_delay_mult", F(unit.SuccessionDelayMult)),
                ("succession_until_tick", I(unit.SuccessionUntilTick)),
                ("task_force", taskForceState),
                ("template_id", unit.TemplateId));
        }

        private static SortedDictionary<string, object?> GuideState(Guide guide, PathInfo path)
        {
            return O(
                ("destination", CanonicalJson.Vec2(guide.Destination)),
                ("final_facing", F(guide.FinalFacing)),
                ("has_destination", guide.HasDestination),
                ("heading", F(guide.Heading)),
                ("path", CanonicalJson.Normalize(guide.Path)),
                ("path_index", I(guide.PathIndex)),
                ("pending_ref", I(RequestRef(guide.Pending, path))),
                ("pos", CanonicalJson.Vec2(guide.Pos)),
                ("speed", F(guide.Speed)),
                ("trail", CanonicalJson.Normalize(guide.Trail)));
        }

        private static SortedDictionary<string, object?> OrderState(Order order)
        {
            var hops = new List<object?>();
            foreach (OrderHop hop in order.Hops)
            {
                hops.Add(O(
                    ("arrive", I(hop.ArriveTick)),
                    ("base", F(hop.BaseSeconds)),
                    ("from", I(hop.FromUnitId)),
                    ("relays", I(hop.Relays)),
                    ("start", I(hop.StartTick)),
                    ("to", I(hop.ToUnitId))));
            }
            return O(
                ("blocked_since_tick", I(order.BlockedSinceTick)),
                ("bypass", order.Bypass),
                ("cost", F(order.Cost)),
                ("hop_index", I(order.HopIndex)),
                ("hops", hops),
                ("id", I(order.Id)),
                ("issuer", order.Issuer),
                ("issuer_unit_id", I(order.IssuerUnitId)),
                ("params", order.Params),
                ("parent_order_id", I(order.ParentOrderId)),
                ("received_tick", I(order.ReceivedTick)),
                ("state", I((int)order.State)),
                ("state_name", order.StateName()),
                ("target_unit_id", I(order.TargetUnitId)),
                ("tick_issued", I(order.TickIssued)),
                ("type", I((int)order.Type)),
                ("type_name", order.TypeName()));
        }

        private static SortedDictionary<string, object?> OrderSystemState(Battle battle)
        {
            OrderSystem system = battle.OrderSystem ?? throw new InvalidOperationException("Order system is absent");
            var active = R.List<Order>(system, "_active");
            var capacities = R.Get<Dictionary<string, CommandCapacity>>(system, "_capacity");
            var capacityStates = new List<object?>();
            var queueStates = new List<object?>();
            foreach (string side in Battle.Sides)
            {
                CommandCapacity capacity = capacities[side];
                capacityStates.Add(O(
                    ("side", side),
                    ("tokens", F(capacity.RawTokens)),
                    ("tokens_max", F(system.CapacityMax(side))),
                    ("tokens_now", F(system.CapacityNow(side)))));
                queueStates.Add(O(("orders", OrderIds(capacity.Queue)), ("side", side)));
            }
            return O(
                ("active_orders", OrderIds(active)),
                ("cancelled_count", I(system.CancelledCount)),
                ("capacity", capacityStates),
                ("capacity_queues", queueStates),
                ("delay_samples", CanonicalJson.Normalize(system.DelaySamples)));
        }

        private static PathInfo PathState(Battle battle)
        {
            var info = new PathInfo();
            var queue = R.Get<Queue<GridPathfinder.PathRequest>>(battle.Pathfinder, "_queue");
            int queueIndex = 0;
            foreach (GridPathfinder.PathRequest request in queue)
            {
                RegisterRequest(request, info);
                info.QueueIndices[request] = queueIndex++;
            }
            foreach (Element element in battle.Elements)
            {
                if (element.PathRequest == null)
                    continue;
                RegisterRequest(element.PathRequest, info);
                info.Owners[element.PathRequest].Add(O(("id", I(element.Id)), ("kind", "element")));
            }
            foreach (Unit unit in battle.Units)
            {
                if (unit.Guide.Pending == null)
                    continue;
                RegisterRequest(unit.Guide.Pending, info);
                info.Owners[unit.Guide.Pending].Add(O(("id", I(unit.Id)), ("kind", "unit")));
            }
            var requests = new List<object?>();
            var queueOrder = new List<object?>();
            for (int i = 0; i < info.Requests.Count; ++i)
            {
                GridPathfinder.PathRequest request = info.Requests[i];
                int index = info.QueueIndices.TryGetValue(request, out int found) ? found : -1;
                if (index >= 0)
                    queueOrder.Add(I(i));
                List<SortedDictionary<string, object?>> owners = info.Owners[request];
                owners.Sort((left, right) =>
                {
                    int comparison = string.CompareOrdinal((string)left["kind"]!, (string)right["kind"]!);
                    return comparison != 0 ? comparison : ((long)left["id"]!).CompareTo((long)right["id"]!);
                });
                var ownerValues = new List<object?>();
                foreach (SortedDictionary<string, object?> owner in owners)
                    ownerValues.Add(owner);
                requests.Add(O(
                    ("cancelled", request.Cancelled),
                    ("done", request.Done),
                    ("from", CanonicalJson.Vec2(request.From)),
                    ("mobility", request.Mobility),
                    ("ok", request.Ok),
                    ("owners", ownerValues),
                    ("points", CanonicalJson.Normalize(request.Points)),
                    ("queue_index", I(index)),
                    ("ref", I(i)),
                    ("side", request.Side),
                    ("to", CanonicalJson.Vec2(request.To))));
            }
            var threats = new List<object?>();
            var threatArrays = R.Get<Dictionary<string, float[]>>(battle.Pathfinder, "_threat");
            IDictionary threatCells = R.Get<IDictionary>(battle.Pathfinder, "_threatCells");
            foreach (string side in GridPathfinder.Sides)
            {
                object cellSet = threatCells[side] ?? throw new InvalidOperationException("Missing threat side " + side);
                MethodInfo snapshot = cellSet.GetType().GetMethod("Snapshot",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
                    throw new MissingMethodException(cellSet.GetType().FullName, "Snapshot");
                int[] indices = (int[])snapshot.Invoke(cellSet, null)!;
                Array.Sort(indices);
                var cells = new List<object?>();
                foreach (int index in indices)
                    cells.Add(O(("index", I(index)), ("value", F(threatArrays[side][index]))));
                threats.Add(O(("cells", cells), ("side", side)));
            }
            var grids = R.Get<Dictionary<string, AStarGrid2D>>(battle.Pathfinder, "_grids").Keys.ToList();
            grids.Sort(StringComparer.Ordinal);
            info.State = O(
                ("built_grids", grids),
                ("queue_order", queueOrder),
                ("requests", requests),
                ("requests_processed", I(battle.Pathfinder.RequestsProcessed)),
                ("threats", threats));
            return info;
        }

        private static void RegisterRequest(GridPathfinder.PathRequest request, PathInfo info)
        {
            if (info.Refs.ContainsKey(request))
                return;
            info.Refs.Add(request, info.Requests.Count);
            info.Requests.Add(request);
            info.Owners.Add(request, new List<SortedDictionary<string, object?>>());
        }

        private static int RequestRef(GridPathfinder.PathRequest? request, PathInfo info)
        {
            return request != null && info.Refs.TryGetValue(request, out int reference) ? reference : -1;
        }

        private static SortedDictionary<string, object?> MovementState(Battle battle)
        {
            var targets = R.Get<Dictionary<int, SimVec2>>(battle.Movement, "_companyTargets");
            var currentTargets = new List<object?>();
            foreach (int unitId in targets.Keys.OrderBy(id => id))
                currentTargets.Add(UnitTarget(unitId, targets[unitId]));
            IDictionary rest = R.Get<IDictionary>(battle.Movement, "_companyRest");
            var companyIds = new List<int>();
            foreach (DictionaryEntry entry in rest)
                companyIds.Add((int)entry.Key);
            companyIds.Sort();
            var companyRest = new List<object?>();
            foreach (int companyId in companyIds)
            {
                object saved = rest[companyId] ?? throw new InvalidOperationException("Missing company rest record");
                (float X, float Y, float Z) key = R.Get<(float X, float Y, float Z)>(saved, "Key");
                int memberCount = R.Get<int>(saved, "N");
                var restTargets = new List<object?>();
                foreach (KeyValuePair<int, SimVec2> pair in R.Get<List<KeyValuePair<int, SimVec2>>>(saved, "Targets"))
                    restTargets.Add(UnitTarget(pair.Key, pair.Value));
                companyRest.Add(O(
                    ("company", I(companyId)),
                    ("key", CanonicalJson.Vec3(key.X, key.Y, key.Z)),
                    ("member_count", I(memberCount)),
                    ("targets", restTargets)));
            }
            return O(
                ("company_rest", companyRest),
                ("company_targets", currentTargets),
                ("transient_caches", "members/speed/platoon caches omitted; cleared at the start of every movement step"));
        }

        private static SortedDictionary<string, object?> UnitTarget(int unitId, SimVec2 target)
        {
            return O(("target", CanonicalJson.Vec2(target)), ("unit", I(unitId)));
        }

        private static SortedDictionary<string, object?> SpatialState(Battle battle)
        {
            var buckets = R.Get<Dictionary<SimVec2I, List<Element>>>(battle.Spatial, "_buckets");
            var cells = buckets.Keys.ToList();
            cells.Sort((left, right) => left.Y != right.Y ? left.Y.CompareTo(right.Y) : left.X.CompareTo(right.X));
            var states = new List<object?>();
            foreach (SimVec2I cell in cells)
                states.Add(O(("cell", CanonicalJson.Vec2I(cell)), ("element_ids", ElementIds(buckets[cell]))));
            return O(("buckets", states), ("cell_size", F(battle.Spatial.CellSize)));
        }

        private static SortedDictionary<string, object?> CommsState(Battle battle)
        {
            var nodes = R.Get<Dictionary<string, List<Element>>>(battle.Comms, "_nodes");
            var edges = R.Get<Dictionary<string, Dictionary<int, List<CommsEdge>>>>(battle.Comms, "_edges");
            var jammers = R.Get<Dictionary<string, List<Element>>>(battle.Comms, "_jammers");
            var sides = new List<object?>();
            foreach (string side in Battle.Sides)
            {
                List<Element> sideNodes = nodes.TryGetValue(side, out List<Element>? found) ? found : new List<Element>();
                var edgeStates = new List<object?>();
                foreach (Element from in sideNodes)
                {
                    if (!edges.TryGetValue(side, out Dictionary<int, List<CommsEdge>>? sideEdges) ||
                        !sideEdges.TryGetValue(from.Id, out List<CommsEdge>? row))
                        continue;
                    foreach (CommsEdge edge in row)
                    {
                        edgeStates.Add(O(
                            ("from", I(from.Id)),
                            ("quality", F(edge.Quality)),
                            ("to", I(edge.ToElementId))));
                    }
                }
                sides.Add(O(
                    ("edges", edgeStates),
                    ("jammers", ElementIds(jammers.TryGetValue(side, out List<Element>? foundJammers)
                        ? foundJammers : new List<Element>())),
                    ("nodes", ElementIds(sideNodes)),
                    ("side", side)));
            }
            return O(("built_tick", I(R.Get<long>(battle.Comms, "_builtTick"))), ("sides", sides));
        }

        private static SortedDictionary<string, object?> DetectionState(Battle battle)
        {
            var pending = R.Get<Dictionary<string, InsertionOrderedDictionary<int, ContactReport>>>(
                battle.Detection, "_pending");
            var sides = new List<object?>();
            foreach (string side in Battle.Sides)
            {
                var reports = new List<object?>();
                foreach (KeyValuePair<int, ContactReport> pair in pending[side])
                {
                    ContactReport report = pair.Value;
                    reports.Add(O(
                        ("class", report.ElementClass),
                        ("deliver", I(report.DeliverTick)),
                        ("platoon", I(report.PlatoonId)),
                        ("pos", CanonicalJson.Vec2(report.Position)),
                        ("target", I(pair.Key)),
                        ("tick", I(report.Tick))));
                }
                sides.Add(O(
                    ("pending_reports", reports),
                    ("seen", SortedIntMap(battle.Detection.Seen[side])),
                    ("side", side)));
            }
            return O(
                ("detections_total", I(battle.Detection.DetectionsTotal)),
                ("reports_delivered", I(battle.Detection.ReportsDelivered)),
                ("sides", sides));
        }

        private static SortedDictionary<string, object?> AiState(Battle battle)
        {
            var assignments = new List<object?>();
            foreach (int unitId in battle.TfAi.Assignment.Keys.OrderBy(id => id))
                assignments.Add(O(("company", I(unitId)), ("point", battle.TfAi.Assignment[unitId])));
            var lastOrders = new List<object?>();
            foreach (int unitId in battle.TfAi.LastOrder.Keys.OrderBy(id => id))
                lastOrders.Add(O(("type", I((int)battle.TfAi.LastOrder[unitId])), ("unit", I(unitId))));
            var lastArtillery = R.Get<Dictionary<string, long>>(battle.TfAi, "_lastArtillery");
            var lastCas = R.Get<Dictionary<string, long>>(battle.TfAi, "_lastCas");
            var cooldowns = new List<object?>();
            foreach (string side in Battle.Sides)
            {
                cooldowns.Add(O(
                    ("last_artillery", I(lastArtillery[side])),
                    ("last_cas", I(lastCas[side])),
                    ("side", side)));
            }
            return O(
                ("assignments", assignments),
                ("fire_cooldowns", cooldowns),
                ("last_orders", lastOrders),
                ("sides", battle.TfAi.Sides));
        }

        private static SortedDictionary<string, object?> FiresState(Battle battle)
        {
            var missions = new List<object?>();
            foreach (FireSupportSystem.Mission mission in battle.Fires.Missions)
            {
                missions.Add(O(
                    ("asset", I((int)mission.Asset)),
                    ("delay_sec", F(mission.DelaySec)),
                    ("first_impact", CanonicalJson.Vec2(mission.FirstImpact)),
                    ("id", I(mission.Id)),
                    ("next_round_tick", I(mission.NextRoundTick)),
                    ("observer_id", I(mission.ObserverId)),
                    ("observer_quality", I(mission.ObserverQuality)),
                    ("phase", I((int)mission.Phase)),
                    ("ready_tick", I(mission.ReadyTick)),
                    ("requested_tick", I(mission.RequestedTick)),
                    ("rounds_fired", I(mission.RoundsFired)),
                    ("rounds_left", I(mission.RoundsLeft)),
                    ("shooter_unit_id", I(mission.ShooterUnitId)),
                    ("side", mission.Side),
                    ("sigma", F(mission.Sigma)),
                    ("smoke", mission.Smoke),
                    ("target", CanonicalJson.Vec2(mission.Target)),
                    ("weapon", mission.Weapon)));
            }
            var shells = new List<object?>();
            for (int i = 0; i < battle.Fires.Shells.Count; ++i)
            {
                FireSupportSystem.Shell shell = battle.Fires.Shells[i];
                shells.Add(O(
                    ("asset", I((int)shell.Asset)),
                    ("land_tick", I(shell.LandTick)),
                    ("mission_id", I(shell.MissionId)),
                    ("pos", CanonicalJson.Vec2(shell.Pos)),
                    ("sequence", I(i)),
                    ("side", shell.Side),
                    ("smoke", shell.Smoke),
                    ("source_id", I(shell.SourceId)),
                    ("weapon", shell.Weapon)));
            }
            return O(("missions", missions), ("shells", shells));
        }

        private static List<object?> SmokeStates(Battle battle)
        {
            var states = new List<object?>();
            for (int i = 0; i < battle.Smokes.Count; ++i)
            {
                Los.Smoke smoke = battle.Smokes[i];
                states.Add(O(
                    ("pos", CanonicalJson.Vec2(smoke.Pos)),
                    ("radius", F(smoke.Radius)),
                    ("sequence", I(i)),
                    ("until_tick", I(smoke.UntilTick))));
            }
            return states;
        }

        private static SortedDictionary<string, object?> SupplyState(Battle battle)
        {
            SupplySystem supply = battle.Supply as SupplySystem ??
                throw new InvalidOperationException("Battle supply is not SupplySystem");
            var tasks = new List<object?>();
            foreach (KeyValuePair<int, int> pair in supply.Tasks)
                tasks.Add(O(("section", I(pair.Key)), ("target", I(pair.Value))));
            return O(
                ("delivered_total", F(supply.DeliveredTotal)),
                ("requests", Integers(supply.Requests)),
                ("returning", Integers(supply.Returning.Keys)),
                ("tasks", tasks));
        }

        private static SortedDictionary<string, object?> EngineerState(Battle battle)
        {
            EngineerSystem engineers = RequireEngineers(battle);
            var minefields = new List<object?>();
            foreach (EngineerSystem.Minefield minefield in engineers.Minefields)
            {
                var known = new List<object?>();
                foreach (string side in Battle.Sides)
                    known.Add(O(("known", minefield.Known.TryGetValue(side, out bool value) && value), ("side", side)));
                minefields.Add(O(
                    ("charges", I(minefield.Charges)),
                    ("id", I(minefield.Id)),
                    ("known_by", known),
                    ("pos", CanonicalJson.Vec2(minefield.Pos)),
                    ("radius", F(minefield.Radius)),
                    ("side", minefield.Side)));
            }
            var positions = new List<object?>();
            for (int i = 0; i < engineers.Positions.Count; ++i)
            {
                EngineerSystem.FieldPosition position = engineers.Positions[i];
                positions.Add(O(
                    ("pos", CanonicalJson.Vec2(position.Pos)),
                    ("radius", F(position.Radius)),
                    ("sequence", I(i)),
                    ("side", position.Side)));
            }
            return O(
                ("mine_strikes", I(engineers.MineStrikes)),
                ("minefields", minefields),
                ("positions", positions));
        }

        private static EngineerSystem RequireEngineers(Battle battle)
        {
            return battle.Engineers as EngineerSystem ??
                throw new InvalidOperationException("Battle engineers are not EngineerSystem");
        }

        private static List<object?> ReinforcementWaves(Battle battle)
        {
            var waves = new List<object?>();
            foreach (string side in Battle.Sides)
                waves.Add(O(("arrived_wave", I(battle.Reinforcements.ArrivedWaves[side])), ("side", side)));
            return waves;
        }

        private static SortedDictionary<string, object?> ScoringState(Battle battle)
        {
            var points = new List<object?>();
            foreach (ScoringSystem.PointState point in battle.Scoring.Points)
            {
                var held = new List<object?>();
                foreach (string side in Battle.Sides)
                    held.Add(O(("seconds", F(point.HeldSec[side])), ("side", side)));
                points.Add(O(
                    ("contested", point.Contested),
                    ("held", held),
                    ("id", point.Def.Id),
                    ("name", point.Def.DisplayName),
                    ("owner", point.Owner),
                    ("pos", CanonicalJson.Vec2(point.Def.Pos)),
                    ("radius_m", F(point.Def.RadiusM)),
                    ("vp_per_min", F(point.Def.VpPerMin))));
            }
            var objectives = new List<object?>();
            foreach (ScenarioDef.Objective objective in battle.Scenario.Objectives)
            {
                bool recorded = battle.Scoring.ObjectivesDone.TryGetValue(objective.Id, out bool done);
                objectives.Add(O(
                    ("done", recorded ? (object)done : null),
                    ("id", objective.Id),
                    ("recorded", recorded)));
            }
            var committed = R.Get<HashSet<int>>(battle.Scoring, "_committedElements");
            var committedIds = committed.ToList();
            committedIds.Sort();
            var sides = new List<object?>();
            foreach (string side in Battle.Sides)
            {
                var ids = new List<object?>();
                foreach (int elementId in committedIds)
                {
                    Element? element = battle.GetElement(elementId);
                    if (element != null && element.Side == side)
                        ids.Add(I(elementId));
                }
                sides.Add(O(
                    ("committed_element_ids", ids),
                    ("committed_value", F(battle.Scoring.CommittedValue.TryGetValue(side, out double value) ? value : 0.0)),
                    ("force_value", F(battle.Scoring.ForceValue(side))),
                    ("side", side),
                    ("value_ratio", F(battle.Scoring.ValueRatio(side))),
                    ("vp", F(battle.Scoring.Vp[side]))));
            }
            return O(
                ("collapse_threshold", F(battle.Scenario.CollapseThreshold)),
                ("committed_rule", "D-043: full element cost added once when first on_map; off-map reinforcements excluded"),
                ("objectives", objectives),
                ("points", points),
                ("sides", sides));
        }

        private static SortedDictionary<string, object?> StatsState(Battle battle)
        {
            var sides = new List<object?>();
            foreach (string side in Battle.Sides)
            {
                sides.Add(O(
                    ("comms_cuts", I(battle.Stats.CommsCuts[side])),
                    ("damaged", I(battle.Stats.Damaged[side])),
                    ("fire_missions", I(battle.Stats.FireMissions[side])),
                    ("losses", SortedStringIntMap(battle.Stats.Losses[side])),
                    ("order_delays", CanonicalJson.Normalize(battle.Stats.OrderDelays[side])),
                    ("resupplies", I(battle.Stats.Resupplies[side])),
                    ("shots", I(battle.Stats.Shots[side])),
                    ("side", side)));
            }
            return O(
                ("first_contact_tick", I(battle.Stats.FirstContactTick)),
                ("sides", sides),
                ("timeline", battle.Stats.Timeline));
        }

        private static SortedDictionary<string, object?> EventState(Battle battle)
        {
            var events = new List<object?>();
            foreach (SimEvent simEvent in R.List<SimEvent>(battle.World.Events, "_queue"))
            {
                events.Add(O(
                    ("payload", simEvent.Payload),
                    ("tick", I(simEvent.Tick)),
                    ("type", simEvent.Type)));
            }
            return O(
                ("payloads", "omitted; presentation queue only; hash covers canonical payloads"),
                ("pending_count", I(battle.World.Events.PendingCount)),
                ("pending_sha256", CanonicalJson.Sha256(CanonicalJson.Bytes(events))));
        }

        private static SortedDictionary<string, object?> KeyCounts(Battle battle)
        {
            int contacts = 0;
            int known = 0;
            foreach (Unit unit in battle.Units)
            {
                if (unit is Platoon platoon)
                    contacts += platoon.Contacts.Count;
                if (unit is TaskForce taskForce)
                    known += taskForce.KnownEnemies.Count;
            }
            EngineerSystem engineers = RequireEngineers(battle);
            SupplySystem supply = (SupplySystem)battle.Supply;
            OrderSystem orderSystem = battle.OrderSystem!;
            return O(
                ("active_orders", I(orderSystem.ActiveCount())),
                ("committed_elements", I(R.Get<HashSet<int>>(battle.Scoring, "_committedElements").Count)),
                ("contacts", I(contacts)),
                ("elements", I(battle.Elements.Count)),
                ("field_positions", I(engineers.Positions.Count)),
                ("fire_missions", I(battle.Fires.Missions.Count)),
                ("hits", I(battle.Combat.Hits)),
                ("kills", I(battle.Combat.Kills)),
                ("known_enemies", I(known)),
                ("minefields", I(engineers.Minefields.Count)),
                ("orders", I(battle.OrdersById.Count)),
                ("pending_events", I(battle.World.Events.PendingCount)),
                ("pending_paths", I(battle.Pathfinder.PendingCount())),
                ("shells", I(battle.Fires.Shells.Count)),
                ("shots_fired", I(battle.Combat.ShotsFired)),
                ("smokes", I(battle.Smokes.Count)),
                ("supply_requests", I(supply.Requests.Count)),
                ("supply_tasks", I(supply.Tasks.Count)),
                ("units", I(battle.Units.Count)));
        }

        private static void CaptureCheckpoint(Battle battle, int seed, IReadOnlyList<string> reasons,
            IReadOnlyList<long> arrivalTicks, List<object?> checkpoints, string seedDirectory, bool detailed)
        {
            if (checkpoints.Count != 0)
            {
                var previous = (SortedDictionary<string, object?>)checkpoints[checkpoints.Count - 1]!;
                if ((long)previous["tick"]! == battle.Tick())
                    return;
            }
            byte[] bytes = SnapshotBytes(battle, seed, reasons, arrivalTicks);
            string relative = "";
            if (detailed)
            {
                relative = "snapshots/tick_" + battle.Tick().ToString("D6", CultureInfo.InvariantCulture) + ".json";
                WriteArtifact(Path.Combine(seedDirectory, relative.Replace('/', Path.DirectorySeparatorChar)), bytes);
            }
            checkpoints.Add(O(
                ("arrival_event_ticks", Integers(arrivalTicks)),
                ("bytes", I(bytes.Length)),
                ("counts", KeyCounts(battle)),
                ("detail_file", relative),
                ("engine_state_hash", CanonicalJson.Int64(battle.World.StateHash())),
                ("reasons", reasons),
                ("sha256", CanonicalJson.Sha256(bytes)),
                ("tick", I(battle.Tick()))));
        }

        private static List<object?> SortedStringIntMap(IEnumerable<KeyValuePair<string, int>> values)
        {
            var pairs = values.ToList();
            pairs.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
            var output = new List<object?>();
            foreach (KeyValuePair<string, int> pair in pairs)
                output.Add(O(("key", pair.Key), ("value", I(pair.Value))));
            return output;
        }

        private static List<object?> SortedFloatMap(IEnumerable<KeyValuePair<string, double>> values)
        {
            var pairs = values.ToList();
            pairs.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
            var output = new List<object?>();
            foreach (KeyValuePair<string, double> pair in pairs)
                output.Add(O(("key", pair.Key), ("value", F(pair.Value))));
            return output;
        }

        private static List<object?> SortedIntMap(IEnumerable<KeyValuePair<int, long>> values)
        {
            var pairs = values.ToList();
            pairs.Sort((left, right) => left.Key.CompareTo(right.Key));
            return OrderedIntMap(pairs);
        }

        private static List<object?> OrderedIntMap(IEnumerable<KeyValuePair<int, long>> values)
        {
            var output = new List<object?>();
            foreach (KeyValuePair<int, long> pair in values)
                output.Add(O(("key", pair.Key.ToString(CultureInfo.InvariantCulture)), ("value", I(pair.Value))));
            return output;
        }

        private static List<object?> ElementIds(IEnumerable<Element> elements)
        {
            var output = new List<object?>();
            foreach (Element element in elements)
                output.Add(I(element.Id));
            return output;
        }

        private static List<object?> UnitIds(IEnumerable<Unit> units)
        {
            var output = new List<object?>();
            foreach (Unit unit in units)
                output.Add(I(unit.Id));
            return output;
        }

        private static List<object?> OrderIds(IEnumerable<Order> orders)
        {
            var output = new List<object?>();
            foreach (Order order in orders)
                output.Add(I(order.Id));
            return output;
        }

        private static List<object?> Integers(IEnumerable<long> values)
        {
            var output = new List<object?>();
            foreach (long value in values)
                output.Add(I(value));
            return output;
        }

        private static List<object?> Integers(IEnumerable<int> values)
        {
            var output = new List<object?>();
            foreach (int value in values)
                output.Add(I(value));
            return output;
        }

        private static SortedDictionary<string, object?> CompareBaseline(string path, byte[] compatBytes)
        {
            string relative = path.Replace('\\', '/');
            if (!File.Exists(path))
            {
                return O(
                    ("exact_compatibility_bytes_after_removing_real_sec_line", false),
                    ("file", relative),
                    ("matches", false),
                    ("semantic_json_after_removing_real_sec", false),
                    ("sha256_with_real_sec", ""));
            }
            byte[] raw = File.ReadAllBytes(path);
            string text = Encoding.UTF8.GetString(raw);
            var kept = new List<string>();
            foreach (string line in text.Split('\n'))
            {
                if (!line.TrimStart().StartsWith("\"real_sec\":", StringComparison.Ordinal))
                    kept.Add(line);
            }
            string normalized = string.Join("\n", kept);
            string compatibility = Encoding.UTF8.GetString(compatBytes);
            bool bytesMatch = normalized == compatibility;
            JObject expected = JObject.Parse(text);
            expected.Remove("real_sec");
            JObject actual = JObject.Parse(compatibility);
            bool semanticMatch = JToken.DeepEquals(expected, actual);
            return O(
                ("exact_compatibility_bytes_after_removing_real_sec_line", bytesMatch),
                ("file", relative),
                ("matches", bytesMatch && semanticMatch),
                ("semantic_json_after_removing_real_sec", semanticMatch),
                ("sha256_with_real_sec", CanonicalJson.Sha256(raw)));
        }

        private static byte[] PrettyBytes(object value)
        {
            var builder = new StringBuilder();
            WritePretty(builder, value, 0);
            return new UTF8Encoding(false).GetBytes(builder.ToString());
        }

        private static void WritePretty(StringBuilder builder, object? value, int indent)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }
            if (value is string text)
            {
                CanonicalJson.QuoteString(builder, text);
                return;
            }
            if (value is bool boolean)
            {
                builder.Append(boolean ? "true" : "false");
                return;
            }
            if (value is double number)
            {
                builder.Append(CanonicalJson.PrettyNumber(number));
                return;
            }
            if (value is float single)
            {
                builder.Append(CanonicalJson.PrettyNumber(single));
                return;
            }
            if (value is IFormattable && !(value is IDictionary) && !(value is IEnumerable))
            {
                builder.Append(((IFormattable)value).ToString(null, CultureInfo.InvariantCulture));
                return;
            }
            if (value is IDictionary dictionary)
            {
                if (dictionary.Count == 0)
                {
                    builder.Append("{}");
                    return;
                }
                var entries = new List<DictionaryEntry>();
                foreach (DictionaryEntry entry in dictionary)
                    entries.Add(entry);
                entries.Sort((left, right) => string.CompareOrdinal((string)left.Key, (string)right.Key));
                builder.Append("{\n");
                for (int i = 0; i < entries.Count; ++i)
                {
                    builder.Append(' ', indent + 2);
                    CanonicalJson.QuoteString(builder, (string)entries[i].Key);
                    builder.Append(": ");
                    WritePretty(builder, entries[i].Value, indent + 2);
                    if (i + 1 < entries.Count)
                        builder.Append(',');
                    builder.Append('\n');
                }
                builder.Append(' ', indent).Append('}');
                return;
            }
            if (value is IEnumerable sequence)
            {
                var items = sequence.Cast<object?>().ToList();
                if (items.Count == 0)
                {
                    builder.Append("[]");
                    return;
                }
                builder.Append("[\n");
                for (int i = 0; i < items.Count; ++i)
                {
                    builder.Append(' ', indent + 2);
                    WritePretty(builder, items[i], indent + 2);
                    if (i + 1 < items.Count)
                        builder.Append(',');
                    builder.Append('\n');
                }
                builder.Append(' ', indent).Append(']');
                return;
            }
            throw new InvalidOperationException("Unsupported compatibility JSON value: " + value.GetType().FullName);
        }

        private static void WriteArtifact(string path, byte[] bytes)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllBytes(path, bytes);
        }

        private static string Text(IReadOnlyDictionary<string, object> values, string key)
        {
            return values.TryGetValue(key, out object? value)
                ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? "" : "";
        }

        private static long ResultInt(IReadOnlyDictionary<string, object> result, string key, long fallback)
        {
            return result.TryGetValue(key, out object? value)
                ? Convert.ToInt64(value, CultureInfo.InvariantCulture) : fallback;
        }

        private static string ResultText(IReadOnlyDictionary<string, object> result, string key)
        {
            return result.TryGetValue(key, out object? value)
                ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? "" : "";
        }

        private static SortedDictionary<string, object?> O(params (string Key, object? Value)[] fields)
        {
            return CanonicalJson.Object(fields);
        }

        private static object I(long value)
        {
            return CanonicalJson.Integer(value);
        }

        private static SortedDictionary<string, object?> F(double value)
        {
            return CanonicalJson.Float64(value);
        }

        private static string? Option(string[] args, string prefix)
        {
            foreach (string arg in args)
            {
                if (arg.StartsWith(prefix, StringComparison.Ordinal))
                    return arg.Substring(prefix.Length);
            }
            return null;
        }

        private static bool Flag(string[] args, string flag)
        {
            return args.Any(arg => arg == flag);
        }

        private static List<int> IntList(string text)
        {
            var output = new List<int>();
            if (text == "none" || text.Length == 0)
                return output;
            foreach (string part in text.Split(','))
                output.Add(int.Parse(part, CultureInfo.InvariantCulture));
            return output;
        }

        private sealed class PathInfo
        {
            internal readonly Dictionary<GridPathfinder.PathRequest, int> Refs =
                new Dictionary<GridPathfinder.PathRequest, int>();
            internal readonly Dictionary<GridPathfinder.PathRequest, int> QueueIndices =
                new Dictionary<GridPathfinder.PathRequest, int>();
            internal readonly List<GridPathfinder.PathRequest> Requests =
                new List<GridPathfinder.PathRequest>();
            internal readonly Dictionary<GridPathfinder.PathRequest, List<SortedDictionary<string, object?>>> Owners =
                new Dictionary<GridPathfinder.PathRequest, List<SortedDictionary<string, object?>>>();
            internal SortedDictionary<string, object?> State { get; set; } = null!;
        }
    }
}
