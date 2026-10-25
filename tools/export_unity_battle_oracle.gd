extends SceneTree

const SNAPSHOT_SCHEMA: String = "echelon.battle_oracle.snapshot.v1"
const CHECKPOINT_SCHEMA: String = "echelon.battle_oracle.checkpoints.v1"
const RUN_SCHEMA: String = "echelon.battle_oracle.run.v1"
const SCHEMA_MANIFEST: String = "echelon.battle_oracle.schema.v1"
const DEFAULT_OUT: String = "res://build/unity_battle_oracle"
const DEFAULT_GOLDEN: String = "res://tests/golden/battle"
const BASELINE_ROOT: String = "res://build/balance/after"

var _out_root: String = DEFAULT_OUT
var _golden_root: String = DEFAULT_GOLDEN
var _golden_enabled: bool = true
var _seeds: Array[int] = [1, 2, 3, 4, 5, 6, 7, 8]
var _detail_seeds: Array[int] = [1, 8]
var _max_ticks: int = 0
var _arrival_checkpoints: Dictionary = {}
var _failed: bool = false
var _run_seeds: Array = []


func _init() -> void:
	_parse_args()
	DirAccess.make_dir_recursive_absolute(_out_root)
	if _golden_enabled:
		DirAccess.make_dir_recursive_absolute(_golden_root)
	var catalog: DataCatalog = DataLoader.load_catalog()
	if not catalog.is_valid():
		push_error("Battle oracle: invalid catalog: %s" % str(catalog.errors))
		quit(1)
		return
	var blue: ForceSpec = catalog.default_force(&"US")
	var red: ForceSpec = catalog.default_force(&"RU")
	if blue == null or red == null:
		push_error("Battle oracle: default US/RU forces are missing")
		quit(1)
		return
	_emit_root("schema.json", _canonical_bytes(_schema_manifest()))
	for seed_value: int in _seeds:
		_run_seed(catalog, blue, red, seed_value)
	var manifest: Dictionary = {
		"schema": RUN_SCHEMA,
		"engine": Engine.get_version_info(),
		"runner": {
			"script": "res://tools/export_unity_battle_oracle.gd",
			"scenario": "skirmish_01",
			"blue_force": "catalog.default_force(US)",
			"red_force": "catalog.default_force(RU)",
			"ai_sides": ["blue", "red"],
			"tick_rate": SimConst.TICK_RATE,
			"stepping": "Battle.step_tick directly; no frame delta",
			"event_drain": "after ticks divisible by 1200, before their checkpoint",
			"max_ticks_override": _max_ticks,
		},
		"inputs": _catalog_inputs(),
		"simulation_inputs": _simulation_inputs(),
		"seeds": _run_seeds,
		"all_baselines_match": not _failed,
	}
	var totals: Dictionary = {"seeds": _run_seeds.size(), "checkpoints": 0, "files": 1, "bytes": 0}
	for item: Dictionary in _run_seeds:
		totals["checkpoints"] += item["checkpoint_count"]
		totals["files"] += (item["files"] as Array).size()
		for file: Dictionary in item["files"]:
			totals["bytes"] += file["bytes"]
	var schema_bytes: PackedByteArray = FileAccess.get_file_as_bytes(_out_root.path_join("schema.json"))
	totals["bytes"] += schema_bytes.size()
	manifest["totals"] = totals
	_emit_root("run_manifest.json", _canonical_bytes(manifest))
	if _failed:
		push_error("Battle oracle: one or more baseline comparisons failed")
		quit(2)
		return
	print("[battle-oracle] seeds=%d checkpoints=%d files=%d bytes=%d" % [
		_run_seeds.size(), totals["checkpoints"], totals["files"], totals["bytes"]])
	quit(0)


func _parse_args() -> void:
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--out="):
			_out_root = arg.get_slice("=", 1)
		elif arg.begins_with("--golden="):
			_golden_root = arg.get_slice("=", 1)
		elif arg == "--no-golden":
			_golden_enabled = false
		elif arg.begins_with("--seed="):
			_seeds = [arg.get_slice("=", 1).to_int()]
		elif arg.begins_with("--seeds="):
			_seeds = _parse_int_list(arg.get_slice("=", 1))
		elif arg.begins_with("--detail="):
			_detail_seeds = _parse_int_list(arg.get_slice("=", 1))
		elif arg.begins_with("--max-ticks="):
			_max_ticks = arg.get_slice("=", 1).to_int()


func _parse_int_list(text: String) -> Array[int]:
	var out: Array[int] = []
	if text == "none" or text.is_empty():
		return out
	for part: String in text.split(","):
		out.append(part.to_int())
	return out


func _run_seed(catalog: DataCatalog, blue: ForceSpec, red: ForceSpec, seed_value: int) -> void:
	_arrival_checkpoints.clear()
	var battle: Battle = BattleSetup.skirmish(catalog, blue, red, &"skirmish_01", seed_value,
		[&"blue", &"red"] as Array[StringName])
	battle.world.events.add_listener(_on_battle_event)
	battle.begin_battle()
	var written: Array = []
	var checkpoints: Array = []
	_capture_checkpoint(battle, seed_value, ["initial"], written, checkpoints)
	var limit: int = roundi(battle.scenario.time_limit_min * 60.0 * SimConst.TICK_RATE) + 100
	if _max_ticks > 0:
		limit = mini(limit, _max_ticks)
	while battle.result.is_empty() and battle.tick() < limit:
		battle.step_tick()
		if battle.tick() % 1200 == 0:
			battle.world.events.drain()
		var reasons: Array = []
		if battle.tick() == 100 or battle.tick() == 600 or battle.tick() % 1200 == 0:
			reasons.append("scheduled")
		if _arrival_checkpoints.has(battle.tick()):
			reasons.append("reinforcement_arrival")
		if not battle.result.is_empty() or battle.tick() >= limit:
			reasons.append("final" if not battle.result.is_empty() else "truncated")
		if not reasons.is_empty():
			_capture_checkpoint(battle, seed_value, reasons, written, checkpoints)
	var aar: Dictionary = battle.stats.summary()
	aar["seed"] = battle.world.rng.seed_value
	var aar_bytes: PackedByteArray = _canonical_bytes(aar)
	var compat_text: String = JSON.stringify(aar, "  ")
	var compat_bytes: PackedByteArray = compat_text.to_utf8_buffer()
	_emit_seed_file(seed_value, "aar.json", aar_bytes, written, true)
	_emit_seed_file(seed_value, "aar.compat.json", compat_bytes, written, true)
	var baseline: Dictionary = _compare_baseline(seed_value, compat_text)
	if not baseline["matches"]:
		_failed = true
		var actual: Variant = JSON.parse_string(compat_text)
		var expected: Variant = JSON.parse_string(FileAccess.get_file_as_string(
			BASELINE_ROOT.path_join("aar_%d.json" % seed_value)))
		if expected is Dictionary:
			(expected as Dictionary).erase("real_sec")
		var differences: Array = []
		_collect_differences(expected, actual, "$", differences)
		_emit_seed_file(seed_value, "baseline_diff.json", _canonical_bytes({
			"baseline": baseline,
			"differences": differences,
		}), written, false)
	var index: Dictionary = {
		"schema": CHECKPOINT_SCHEMA,
		"seed": seed_value,
		"scenario": String(battle.scenario.id),
		"final_tick": battle.tick(),
		"result_tick": battle.result.get("tick", -1),
		"truncated": battle.result.is_empty(),
		"checkpoints": checkpoints,
		"final_aar": {
			"canonical_file": "aar.json",
			"canonical_sha256": _sha256(aar_bytes),
			"compatibility_file": "aar.compat.json",
			"compatibility_sha256": _sha256(compat_bytes),
			"real_sec": "excluded",
		},
		"baseline": baseline,
	}
	var index_bytes: PackedByteArray = _canonical_bytes(index)
	_emit_seed_file(seed_value, "checkpoints.json", index_bytes, written, true)
	_run_seeds.append({
		"seed": seed_value,
		"winner": battle.result.get("winner", ""),
		"reason": battle.result.get("reason", ""),
		"result_tick": battle.result.get("tick", -1),
		"final_tick": battle.tick(),
		"checkpoint_count": checkpoints.size(),
		"detailed_snapshots": _detail_seeds.has(seed_value),
		"baseline_matches": baseline["matches"],
		"files": written,
	})
	print("[battle-oracle] seed=%d final_tick=%d winner=%s checkpoints=%d baseline=%s" % [
		seed_value, battle.tick(), battle.result.get("winner", "?"), checkpoints.size(), baseline["matches"]])
	battle.world.events.clear_listeners()
	battle.dispose()


func _on_battle_event(event: Dictionary) -> void:
	if event.get("type", &"") == &"reinforcement_arrived":
		_arrival_checkpoints[int(event["tick"]) + 1] = true


func _capture_checkpoint(battle: Battle, seed_value: int, reasons: Array, written: Array, checkpoints: Array) -> void:
	if not checkpoints.is_empty() and int((checkpoints[-1] as Dictionary)["tick"]) == battle.tick():
		return
	var arrival_ticks: Array = []
	for checkpoint_tick: Variant in _arrival_checkpoints.keys():
		if int(checkpoint_tick) == battle.tick():
			arrival_ticks.append(int(checkpoint_tick) - 1)
	arrival_ticks.sort()
	var snapshot: Dictionary = _snapshot(battle, seed_value, reasons, arrival_ticks)
	var bytes: PackedByteArray = _canonical_bytes(snapshot)
	var parsed: Variant = JSON.parse_string(bytes.get_string_from_utf8())
	if parsed == null:
		_failed = true
		push_error("Battle oracle: generated invalid canonical JSON at seed %d tick %d" % [seed_value, battle.tick()])
	var relative: String = ""
	if _detail_seeds.has(seed_value):
		relative = "snapshots/tick_%06d.json" % battle.tick()
		_emit_seed_file(seed_value, relative, bytes, written, true)
	checkpoints.append({
		"tick": battle.tick(),
		"reasons": reasons,
		"arrival_event_ticks": arrival_ticks,
		"detail_file": relative,
		"bytes": bytes.size(),
		"sha256": _sha256(bytes),
		"engine_state_hash": _i64(battle.world.state_hash()),
		"counts": _key_counts(battle),
	})


func _snapshot(battle: Battle, seed_value: int, reasons: Array, arrival_ticks: Array) -> Dictionary:
	var path_info: Dictionary = _path_info(battle)
	var request_refs: Dictionary = path_info["refs"]
	var elements: Array = []
	for element: Element in battle.elements:
		elements.append(_element_state(element, request_refs))
	var units: Array = []
	for unit: Unit in battle.units:
		units.append(_unit_state(unit, request_refs))
	var orders: Array = []
	var order_ids: Array = battle.orders_by_id.keys()
	order_ids.sort()
	for order_id: int in order_ids:
		orders.append(_order_state(battle.orders_by_id[order_id]))
	var systems: Array = []
	for system: SimSystem in battle.world.get_systems():
		systems.append({
			"name": String(system.get_system_name()),
			"omitted_from_world_hash": system.omit_from_hash(),
			"state_hash": _i64(system.state_hash()),
		})
	return {
		"schema": SNAPSHOT_SCHEMA,
		"seed": seed_value,
		"scenario": String(battle.scenario.id),
		"checkpoint": {"tick": battle.tick(), "reasons": reasons, "arrival_event_ticks": arrival_ticks},
		"battle": {
			"deploying": battle.deploying,
			"player_side": String(battle.player_side),
			"result": battle.result,
		},
		"clock": {
			"tick": battle.world.tick,
			"paused": battle.world.paused,
			"speed_index": battle.world.speed_index,
			"accumulator": battle.world._accum,
		},
		"rng": {"seed": battle.world.rng.seed_value, "state": _i64(battle.world.rng.get_state())},
		"next_ids": {
			"element": battle._next_element_id,
			"unit": battle._next_unit_id,
			"order": battle._next_order_id,
			"fire_mission": battle.fires._next_id,
			"minefield": battle.engineers._next_id,
		},
		"engine_hashes": {"world": _i64(battle.world.state_hash()), "systems": systems},
		"elements": elements,
		"units": units,
		"orders": orders,
		"order_system": _order_system_state(battle),
		"path": path_info["state"],
		"movement": _movement_state(battle),
		"spatial": _spatial_state(battle),
		"comms": _comms_state(battle),
		"detection": _detection_state(battle),
		"combat": {
			"shots_fired": battle.combat.shots_fired,
			"hits": battle.combat.hits,
			"kills": battle.combat.kills,
		},
		"ai": _ai_state(battle),
		"fires": _fires_state(battle),
		"smokes": _smokes_state(battle),
		"supply": _supply_state(battle),
		"engineers": _engineer_state(battle),
		"reinforcements": _reinforcement_state(battle),
		"scoring": _scoring_state(battle),
		"stats": _stats_state(battle),
		"events": {
			"pending_count": battle.world.events.pending_count(),
			"pending_sha256": _sha256(_canonical_bytes(battle.world.events._queue)),
			"payloads": "omitted; presentation queue only; hash covers canonical payloads",
		},
	}


func _key_counts(battle: Battle) -> Dictionary:
	var contacts: int = 0
	var known: int = 0
	for unit: Unit in battle.units:
		if unit is Platoon:
			contacts += (unit as Platoon).contacts.size()
		if unit is TaskForce:
			known += (unit as TaskForce).known_enemies.size()
	var committed: int = battle.scoring._committed_elements.size()
	return {
		"elements": battle.elements.size(),
		"units": battle.units.size(),
		"orders": battle.orders_by_id.size(),
		"active_orders": battle.order_system._active.size(),
		"pending_paths": battle.pathfinder.pending_count(),
		"contacts": contacts,
		"known_enemies": known,
		"shots_fired": battle.combat.shots_fired,
		"hits": battle.combat.hits,
		"kills": battle.combat.kills,
		"fire_missions": battle.fires.missions.size(),
		"shells": battle.fires._shells.size(),
		"smokes": battle.smokes.size(),
		"supply_requests": battle.supply.requests.size(),
		"supply_tasks": battle.supply.tasks.size(),
		"minefields": battle.engineers.minefields.size(),
		"field_positions": battle.engineers.positions.size(),
		"committed_elements": committed,
		"pending_events": battle.world.events.pending_count(),
	}


func _element_state(element: Element, request_refs: Dictionary) -> Dictionary:
	return {
		"id": element.id,
		"def": String(element.def.id),
		"side": String(element.side),
		"platoon": element.platoon.id if element.platoon != null else -1,
		"index_in_platoon": element.index_in_platoon,
		"pos": element.pos,
		"prev_pos": element.prev_pos,
		"heading": element.heading,
		"prev_heading": element.prev_heading,
		"velocity": element.velocity,
		"state": element.state,
		"state_name": Element.STATE_NAMES[element.state],
		"damage_kind": String(element.damage_kind),
		"hp": element.hp,
		"suppression": element.suppression,
		"ammo": _sorted_int_map(element.ammo),
		"reload": _sorted_float_map(element.reload),
		"target_id": element.target_id,
		"move_target": element.move_target,
		"has_move_target": element.has_move_target,
		"rest_heading": element.rest_heading,
		"local_path": element.local_path,
		"local_path_index": element.local_path_index,
		"path_request_ref": _request_ref(element.path_request, request_refs),
		"stuck_ticks": element.stuck_ticks,
		"straggler_ticks": element.straggler_ticks,
		"mounted_in": element.mounted_in.id if element.mounted_in != null else -1,
		"passengers": _element_ids(element.passengers),
		"on_map": element.on_map,
		"stationary_ticks": element.stationary_ticks,
		"last_fired_tick": element.last_fired_tick,
		"last_hit_tick": element.last_hit_tick,
		"kills": element.kills,
		"killed_by": element.killed_by,
		"supply_left": element.supply_left,
		"repair_sec": element.repair_sec,
		"destroyed_tick": element.destroyed_tick,
	}


func _unit_state(unit: Unit, request_refs: Dictionary) -> Dictionary:
	var platoon_state: Variant = null
	var company_state: Variant = null
	var task_force_state: Variant = null
	if unit is Platoon:
		var platoon: Platoon = unit
		platoon_state = {
			"is_hq_section": platoon.is_hq_section,
			"mounted": platoon.mounted,
			"loadout": String(platoon.loadout),
			"attached": platoon.attached,
			"last_shelled_tick": platoon.last_shelled_tick,
			"last_shell_pos": platoon.last_shell_pos,
			"last_assign_key": platoon.last_assign_key,
			"last_assign_members": platoon.last_assign_members,
			"contacts": _ordered_int_map(platoon.contacts),
			"eng_task": String(platoon.eng_task),
			"eng_progress": platoon.eng_progress,
			"mines_left": platoon.mines_left,
			"positions_built": platoon.positions_built,
		}
	elif unit is Company:
		var company: Company = unit
		company_state = {
			"platoons": _unit_ids(company.platoons),
			"hq": company.hq.id if company.hq != null else -1,
			"xo": company.xo.id if company.xo != null else -1,
		}
	elif unit is TaskForce:
		var task_force: TaskForce = unit
		var known: Array = []
		for element_id: int in task_force.known_enemies.keys():
			var info: Dictionary = task_force.known_enemies[element_id]
			known.append({
				"element": element_id,
				"pos": info["pos"],
				"tick": info["tick"],
				"class": String(info["class"]),
			})
		task_force_state = {
			"companies": _unit_ids(task_force.companies),
			"organic": _unit_ids(task_force.organic),
			"cp_section": task_force.cp_section.id if task_force.cp_section != null else -1,
			"toe": task_force.toe.id if task_force.toe != null else "",
			"faction": String(task_force.faction),
			"known_enemies": known,
			"vp": task_force.vp,
			"command_capacity_per_min": task_force.command_capacity_per_min,
			"cp_lost": task_force.cp_lost,
			"artillery_missions": task_force.artillery_missions,
			"cas_sorties": task_force.cas_sorties,
			"entry_pos": task_force.entry_pos,
		}
	return {
		"id": unit.id,
		"display_name": unit.display_name,
		"short_name": unit.short_name,
		"side": String(unit.side),
		"level": unit.level,
		"parent": unit.parent.id if unit.parent != null else -1,
		"template_id": String(unit.template_id),
		"kind": String(unit.kind),
		"guide": _guide_state(unit.guide, request_refs),
		"formation": String(unit.formation.id) if unit.formation != null else "",
		"sop": unit.sop.to_dict(),
		"skill": String(unit.skill),
		"mission": unit.mission.id if unit.mission != null else -1,
		"queued_orders": _order_ids(unit.queued_orders),
		"detached": unit.detached,
		"commander": unit.commander.id if unit.commander != null else -1,
		"succession_until_tick": unit.succession_until_tick,
		"succession_delay_mult": unit.succession_delay_mult,
		"pending_successor": unit.pending_successor.id if unit.pending_successor != null else -1,
		"ad_hoc": unit.ad_hoc,
		"awaiting_follow": unit.awaiting_follow,
		"morale": unit.morale,
		"comms_quality": unit.comms_quality,
		"comms_state": unit.comms_state,
		"comms_hops": unit.comms_hops,
		"reform_until_tick": unit.reform_until_tick,
		"on_map": unit.on_map,
		"phase": unit.phase,
		"role": String(unit.role),
		"posture": String(unit.posture),
		"defend_facing": unit.defend_facing,
		"routed_until_tick": unit.routed_until_tick,
		"initial_element_count": unit.initial_element_count,
		"last_contact_tick": unit.last_contact_tick,
		"last_hit_tick": unit.last_hit_tick,
		"contact_halt_until": unit.contact_halt_until,
		"next_halt_tick": unit.next_halt_tick,
		"next_disperse_tick": unit.next_disperse_tick,
		"routed": unit.routed,
		"auto_withdrawn": unit.auto_withdrawn,
		"formation_error": unit.formation_error,
		"column_mode": unit.column_mode,
		"column_hold_m": unit.column_hold_m,
		"settling_since_tick": unit.settling_since_tick,
		"guide_speed_limit": unit.guide_speed_limit,
		"platoon": platoon_state,
		"company": company_state,
		"task_force": task_force_state,
	}


func _guide_state(guide: Guide, request_refs: Dictionary) -> Dictionary:
	return {
		"pos": guide.pos,
		"heading": guide.heading,
		"path": guide.path,
		"path_index": guide.path_index,
		"speed": guide.speed,
		"final_facing": guide.final_facing,
		"trail": guide.trail,
		"pending_ref": _request_ref(guide.pending, request_refs),
		"destination": guide.destination,
		"has_destination": guide.has_destination,
	}


func _order_state(order: Order) -> Dictionary:
	var hops: Array = []
	for hop: Dictionary in order.hops:
		hops.append({
			"from": hop["from"],
			"to": hop["to"],
			"base": hop["base"],
			"start": hop["start"],
			"arrive": hop["arrive"],
			"relays": hop["relays"],
		})
	return {
		"id": order.id,
		"tick_issued": order.tick_issued,
		"issuer": String(order.issuer),
		"issuer_unit_id": order.issuer_unit_id,
		"target_unit_id": order.target_unit_id,
		"type": order.type,
		"type_name": order.type_name(),
		"params": order.params,
		"hops": hops,
		"hop_index": order.hop_index,
		"state": order.state,
		"state_name": order.state_name(),
		"parent_order_id": order.parent_order_id,
		"cost": order.cost,
		"blocked_since_tick": order.blocked_since_tick,
		"bypass": order.bypass,
		"received_tick": order.received_tick,
	}


func _order_system_state(battle: Battle) -> Dictionary:
	var capacity: Array = []
	var queues: Array = []
	for side: StringName in Battle.SIDES:
		capacity.append({"side": String(side), "tokens": battle.order_system.capacity[side],
			"tokens_now": battle.order_system.capacity_now(side), "tokens_max": battle.order_system.capacity_max(side)})
		queues.append({"side": String(side), "orders": _order_ids(battle.order_system._capacity_queue[side])})
	return {
		"active_orders": _order_ids(battle.order_system._active),
		"capacity": capacity,
		"capacity_queues": queues,
		"delay_samples": battle.order_system.delay_samples,
		"cancelled_count": battle.order_system.cancelled_count,
	}


func _path_info(battle: Battle) -> Dictionary:
	var refs: Dictionary = {}
	var queue_indices: Dictionary = {}
	var owners: Dictionary = {}
	var requests: Array = []
	for index: int in battle.pathfinder._queue.size():
		var request: GridPathfinder.PathRequest = battle.pathfinder._queue[index]
		_register_request(request, refs, owners, requests)
		queue_indices[request.get_instance_id()] = index
	for element: Element in battle.elements:
		if element.path_request != null:
			_register_request(element.path_request, refs, owners, requests)
			owners[element.path_request.get_instance_id()].append({"kind": "element", "id": element.id})
	for unit: Unit in battle.units:
		if unit.guide.pending != null:
			_register_request(unit.guide.pending, refs, owners, requests)
			owners[unit.guide.pending.get_instance_id()].append({"kind": "unit", "id": unit.id})
	var request_states: Array = []
	var queue_order: Array = []
	for index: int in requests.size():
		var request: GridPathfinder.PathRequest = requests[index]
		var instance_id: int = request.get_instance_id()
		var queue_index: int = queue_indices.get(instance_id, -1)
		if queue_index >= 0:
			queue_order.append(index)
		var owner_list: Array = owners[instance_id]
		owner_list.sort_custom(func(a: Dictionary, b: Dictionary) -> bool:
			if a["kind"] != b["kind"]:
				return a["kind"] < b["kind"]
			return a["id"] < b["id"])
		request_states.append({
			"ref": index,
			"queue_index": queue_index,
			"owners": owner_list,
			"from": request.from,
			"to": request.to,
			"mobility": String(request.mobility),
			"side": String(request.side),
			"points": request.points,
			"done": request.done,
			"ok": request.ok,
			"cancelled": request.cancelled,
		})
	var threats: Array = []
	for side: StringName in GridPathfinder.SIDES:
		var cells: Array = []
		var keys: Array = (battle.pathfinder._threat_cells[side] as Dictionary).keys()
		keys.sort()
		var values: PackedFloat32Array = battle.pathfinder._threat[side]
		for cell_index: int in keys:
			cells.append({"index": cell_index, "value": values[cell_index]})
		threats.append({"side": String(side), "cells": cells})
	var grids: Array = battle.pathfinder._grids.keys()
	grids.sort_custom(func(a: Variant, b: Variant) -> bool: return String(a) < String(b))
	return {
		"refs": refs,
		"state": {
			"requests_processed": battle.pathfinder.requests_processed,
			"queue_order": queue_order,
			"requests": request_states,
			"threats": threats,
			"built_grids": grids,
		},
	}


func _register_request(request: GridPathfinder.PathRequest, refs: Dictionary, owners: Dictionary, requests: Array) -> void:
	var instance_id: int = request.get_instance_id()
	if refs.has(instance_id):
		return
	refs[instance_id] = requests.size()
	owners[instance_id] = []
	requests.append(request)


func _request_ref(request: GridPathfinder.PathRequest, refs: Dictionary) -> int:
	if request == null:
		return -1
	return refs.get(request.get_instance_id(), -1)


func _movement_state(battle: Battle) -> Dictionary:
	var current_targets: Array = []
	var target_ids: Array = battle.movement._company_targets.keys()
	target_ids.sort()
	for unit_id: int in target_ids:
		current_targets.append({"unit": unit_id, "target": battle.movement._company_targets[unit_id]})
	var rest: Array = []
	var company_ids: Array = battle.movement._company_rest.keys()
	company_ids.sort()
	for company_id: int in company_ids:
		var saved: Dictionary = battle.movement._company_rest[company_id]
		var targets: Array = []
		for unit_id: int in (saved["targets"] as Dictionary).keys():
			targets.append({"unit": unit_id, "target": saved["targets"][unit_id]})
		rest.append({"company": company_id, "key": saved["key"], "member_count": saved["n"], "targets": targets})
	return {
		"company_targets": current_targets,
		"company_rest": rest,
		"transient_caches": "members/speed/platoon caches omitted; cleared at the start of every movement step",
	}


func _spatial_state(battle: Battle) -> Dictionary:
	var cells: Array = battle.spatial._buckets.keys()
	cells.sort_custom(func(a: Vector2i, b: Vector2i) -> bool:
		return a.y < b.y or (a.y == b.y and a.x < b.x))
	var buckets: Array = []
	for cell: Vector2i in cells:
		var ids: Array = []
		for item: Variant in battle.spatial._buckets[cell]:
			ids.append((item as Element).id)
		buckets.append({"cell": cell, "element_ids": ids})
	return {"cell_size": battle.spatial.cell_size, "buckets": buckets}


func _comms_state(battle: Battle) -> Dictionary:
	var sides: Array = []
	for side: StringName in Battle.SIDES:
		var nodes: Array = battle.comms._nodes.get(side, [])
		var node_ids: Array = _element_ids(nodes)
		var edges: Array = []
		for node: Variant in nodes:
			var from_element: Element = node
			var row: Dictionary = (battle.comms._edges.get(side, {}) as Dictionary).get(from_element.id, {})
			for to_id: int in row.keys():
				edges.append({"from": from_element.id, "to": to_id, "quality": row[to_id]})
		sides.append({
			"side": String(side),
			"nodes": node_ids,
			"edges": edges,
			"jammers": _element_ids(battle.comms._jammers.get(side, [])),
		})
	return {"built_tick": battle.comms._built_tick, "sides": sides}


func _detection_state(battle: Battle) -> Dictionary:
	var sides: Array = []
	for side: StringName in Battle.SIDES:
		var seen: Array = _sorted_id_map(battle.detection.seen[side])
		var pending: Array = []
		for target_id: int in (battle.detection._pending[side] as Dictionary).keys():
			var report: Dictionary = battle.detection._pending[side][target_id]
			pending.append({
				"target": target_id,
				"pos": report["pos"],
				"class": String(report["cls"]),
				"tick": report["tick"],
				"platoon": report["platoon"],
				"deliver": report["deliver"],
			})
		sides.append({"side": String(side), "seen": seen, "pending_reports": pending})
	return {
		"detections_total": battle.detection.detections_total,
		"reports_delivered": battle.detection.reports_delivered,
		"sides": sides,
	}


func _ai_state(battle: Battle) -> Dictionary:
	var assignments: Array = []
	var assignment_ids: Array = battle.tf_ai.assignment.keys()
	assignment_ids.sort()
	for unit_id: int in assignment_ids:
		assignments.append({"company": unit_id, "point": String(battle.tf_ai.assignment[unit_id])})
	var last_orders: Array = []
	var order_ids: Array = battle.tf_ai.last_order.keys()
	order_ids.sort()
	for unit_id: int in order_ids:
		last_orders.append({"unit": unit_id, "type": battle.tf_ai.last_order[unit_id]})
	var cooldowns: Array = []
	for side: StringName in Battle.SIDES:
		cooldowns.append({"side": String(side), "last_artillery": battle.tf_ai._last_artillery[side],
			"last_cas": battle.tf_ai._last_cas[side]})
	var sides: Array = []
	for side: StringName in battle.tf_ai.sides:
		sides.append(String(side))
	return {"sides": sides, "assignments": assignments, "last_orders": last_orders, "fire_cooldowns": cooldowns}


func _fires_state(battle: Battle) -> Dictionary:
	var missions: Array = []
	for mission: FireSupportSystem.Mission in battle.fires.missions:
		missions.append({
			"id": mission.id,
			"side": String(mission.side),
			"asset": mission.asset,
			"smoke": mission.smoke,
			"target": mission.target,
			"observer_id": mission.observer_id,
			"observer_quality": mission.observer_quality,
			"phase": mission.phase,
			"requested_tick": mission.requested_tick,
			"ready_tick": mission.ready_tick,
			"next_round_tick": mission.next_round_tick,
			"rounds_left": mission.rounds_left,
			"rounds_fired": mission.rounds_fired,
			"shooter_unit_id": mission.shooter_unit_id,
			"weapon": String(mission.weapon),
			"sigma": mission.sigma,
			"delay_sec": mission.delay_sec,
			"first_impact": mission.first_impact,
		})
	var shells: Array = []
	for index: int in battle.fires._shells.size():
		var shell: FireSupportSystem.Shell = battle.fires._shells[index]
		shells.append({
			"sequence": index,
			"mission_id": shell.mission_id,
			"pos": shell.pos,
			"land_tick": shell.land_tick,
			"weapon": String(shell.weapon),
			"smoke": shell.smoke,
			"asset": shell.asset,
			"source_id": shell.source_id,
			"side": String(shell.side),
		})
	return {"missions": missions, "shells": shells}


func _smokes_state(battle: Battle) -> Array:
	var out: Array = []
	for index: int in battle.smokes.size():
		var smoke: Los.Smoke = battle.smokes[index]
		out.append({"sequence": index, "pos": smoke.pos, "radius": smoke.radius, "until_tick": smoke.until_tick})
	return out


func _supply_state(battle: Battle) -> Dictionary:
	var tasks: Array = []
	for section_id: int in battle.supply.tasks.keys():
		tasks.append({"section": section_id, "target": battle.supply.tasks[section_id]})
	var returning: Array = []
	for section_id: int in battle.supply.returning.keys():
		returning.append(section_id)
	return {
		"requests": battle.supply.requests,
		"tasks": tasks,
		"returning": returning,
		"delivered_total": battle.supply.delivered_total,
	}


func _engineer_state(battle: Battle) -> Dictionary:
	var minefields: Array = []
	for minefield: EngineerSystem.Minefield in battle.engineers.minefields:
		var known: Array = []
		for side: StringName in Battle.SIDES:
			known.append({"side": String(side), "known": minefield.known.get(side, false)})
		minefields.append({
			"id": minefield.id,
			"pos": minefield.pos,
			"radius": minefield.radius,
			"side": String(minefield.side),
			"charges": minefield.charges,
			"known_by": known,
		})
	var positions: Array = []
	for index: int in battle.engineers.positions.size():
		var position: EngineerSystem.FieldPosition = battle.engineers.positions[index]
		positions.append({"sequence": index, "pos": position.pos, "radius": position.radius, "side": String(position.side)})
	return {"minefields": minefields, "positions": positions, "mine_strikes": battle.engineers.mine_strikes}


func _reinforcement_state(battle: Battle) -> Dictionary:
	var waves: Array = []
	for side: StringName in Battle.SIDES:
		waves.append({"side": String(side), "arrived_wave": battle.reinforcements.arrived_waves[side]})
	return {"waves": waves}


func _scoring_state(battle: Battle) -> Dictionary:
	var points: Array = []
	for point: ScoringSystem.PointState in battle.scoring.points:
		var held: Array = []
		for side: StringName in Battle.SIDES:
			held.append({"side": String(side), "seconds": point.held_sec[side]})
		points.append({
			"id": String(point.def.id),
			"name": point.def.display_name,
			"pos": point.def.pos,
			"radius_m": point.def.radius_m,
			"vp_per_min": point.def.vp_per_min,
			"owner": String(point.owner),
			"contested": point.contested,
			"held": held,
		})
	var objectives: Array = []
	for objective: ScenarioDef.Objective in battle.scenario.objectives:
		objectives.append({"id": String(objective.id), "done": battle.scoring.objectives_done.get(objective.id, null),
			"recorded": battle.scoring.objectives_done.has(objective.id)})
	var sides: Array = []
	var committed_ids: Array = battle.scoring._committed_elements.keys()
	committed_ids.sort()
	for side: StringName in Battle.SIDES:
		var ids: Array = []
		for element_id: int in committed_ids:
			var element: Element = battle.get_element(element_id)
			if element != null and element.side == side:
				ids.append(element_id)
		sides.append({
			"side": String(side),
			"vp": battle.scoring.vp[side],
			"committed_value": battle.scoring.committed_value.get(side, 0.0),
			"committed_element_ids": ids,
			"force_value": battle.scoring.force_value(side),
			"value_ratio": battle.scoring.value_ratio(side),
		})
	return {
		"points": points,
		"objectives": objectives,
		"sides": sides,
		"collapse_threshold": battle.scenario.collapse_threshold,
		"committed_rule": "D-043: full element cost added once when first on_map; off-map reinforcements excluded",
	}


func _stats_state(battle: Battle) -> Dictionary:
	var sides: Array = []
	for side: StringName in Battle.SIDES:
		var losses: Array = _sorted_int_map(battle.stats.losses[side])
		sides.append({
			"side": String(side),
			"losses": losses,
			"damaged": battle.stats.damaged[side],
			"order_delays": battle.stats.order_delays[side],
			"fire_missions": battle.stats.fire_missions[side],
			"comms_cuts": battle.stats.comms_cuts[side],
			"resupplies": battle.stats.resupplies[side],
			"shots": battle.stats.shots[side],
		})
	return {"first_contact_tick": battle.stats.first_contact_tick, "sides": sides, "timeline": battle.stats.timeline}


func _element_ids(elements: Array) -> Array:
	var out: Array = []
	for element: Element in elements:
		out.append(element.id)
	return out


func _unit_ids(units: Array) -> Array:
	var out: Array = []
	for unit: Unit in units:
		out.append(unit.id)
	return out


func _order_ids(orders: Array) -> Array:
	var out: Array = []
	for order: Order in orders:
		out.append(order.id)
	return out


func _sorted_int_map(values: Dictionary) -> Array:
	var keys: Array = values.keys()
	keys.sort_custom(func(a: Variant, b: Variant) -> bool: return str(a) < str(b))
	var out: Array = []
	for key: Variant in keys:
		out.append({"key": str(key), "value": values[key]})
	return out


func _sorted_id_map(values: Dictionary) -> Array:
	var keys: Array = values.keys()
	keys.sort()
	var out: Array = []
	for key: Variant in keys:
		out.append({"key": str(key), "value": values[key]})
	return out


func _ordered_int_map(values: Dictionary) -> Array:
	var out: Array = []
	for key: Variant in values.keys():
		out.append({"key": str(key), "value": values[key]})
	return out


func _sorted_float_map(values: Dictionary) -> Array:
	var keys: Array = values.keys()
	keys.sort_custom(func(a: Variant, b: Variant) -> bool: return str(a) < str(b))
	var out: Array = []
	for key: Variant in keys:
		out.append({"key": str(key), "value": values[key]})
	return out


func _i64(value: int) -> Dictionary:
	return {"$i64": str(value)}


func _compare_baseline(seed_value: int, compatibility_text: String) -> Dictionary:
	var path: String = BASELINE_ROOT.path_join("aar_%d.json" % seed_value)
	var bytes: PackedByteArray = FileAccess.get_file_as_bytes(path)
	var baseline_text: String = bytes.get_string_from_utf8()
	var kept := PackedStringArray()
	for line: String in baseline_text.split("\n"):
		if line.strip_edges().begins_with("\"real_sec\":"):
			continue
		kept.append(line)
	var normalized: String = "\n".join(kept)
	var expected: Variant = JSON.parse_string(baseline_text)
	var actual: Variant = JSON.parse_string(compatibility_text)
	if expected is Dictionary:
		(expected as Dictionary).erase("real_sec")
	var semantic_match: bool = expected == actual
	return {
		"file": "build/balance/after/aar_%d.json" % seed_value,
		"sha256_with_real_sec": _sha256(bytes),
		"matches": normalized == compatibility_text and semantic_match,
		"exact_compatibility_bytes_after_removing_real_sec_line": normalized == compatibility_text,
		"semantic_json_after_removing_real_sec": semantic_match,
	}


func _collect_differences(expected: Variant, actual: Variant, path: String, out: Array) -> void:
	if out.size() >= 200 or expected == actual:
		return
	if expected is Dictionary and actual is Dictionary:
		var keys: Dictionary = {}
		for key: Variant in (expected as Dictionary).keys():
			keys[key] = true
		for key: Variant in (actual as Dictionary).keys():
			keys[key] = true
		var sorted_keys: Array = keys.keys()
		sorted_keys.sort()
		for key: Variant in sorted_keys:
			if not (expected as Dictionary).has(key) or not (actual as Dictionary).has(key):
				out.append({"path": path + "." + str(key), "expected": (expected as Dictionary).get(key, null),
					"actual": (actual as Dictionary).get(key, null)})
			else:
				_collect_differences((expected as Dictionary)[key], (actual as Dictionary)[key], path + "." + str(key), out)
		return
	if expected is Array and actual is Array:
		if (expected as Array).size() != (actual as Array).size():
			out.append({"path": path + ".length", "expected": (expected as Array).size(), "actual": (actual as Array).size()})
		for index: int in mini((expected as Array).size(), (actual as Array).size()):
			_collect_differences((expected as Array)[index], (actual as Array)[index], "%s[%d]" % [path, index], out)
		return
	out.append({"path": path, "expected": expected, "actual": actual})


func _catalog_inputs() -> Array:
	var paths: Array = []
	_collect_json_files("res://data", paths)
	paths.sort()
	var out: Array = []
	for path: String in paths:
		var bytes: PackedByteArray = FileAccess.get_file_as_bytes(path)
		out.append({"file": path.trim_prefix("res://"), "bytes": bytes.size(), "sha256": _sha256(bytes)})
	return out


func _collect_json_files(root: String, out: Array) -> void:
	var directory: DirAccess = DirAccess.open(root)
	if directory == null:
		_failed = true
		push_error("Battle oracle: cannot list " + root)
		return
	directory.list_dir_begin()
	var name: String = directory.get_next()
	while not name.is_empty():
		if name != "." and name != "..":
			var path: String = root.path_join(name)
			if directory.current_is_dir():
				_collect_json_files(path, out)
			elif name.ends_with(".json"):
				out.append(path)
		name = directory.get_next()
	directory.list_dir_end()


func _emit_seed_file(seed_value: int, relative: String, bytes: PackedByteArray, written: Array, golden: bool) -> void:
	var seed_relative: String = "seed_%d/%s" % [seed_value, relative]
	_write_bytes(_out_root.path_join(seed_relative), bytes)
	if _golden_enabled and golden:
		_write_bytes(_golden_root.path_join(seed_relative), bytes)
	written.append({"file": relative, "bytes": bytes.size(), "sha256": _sha256(bytes)})


func _emit_root(relative: String, bytes: PackedByteArray) -> void:
	_write_bytes(_out_root.path_join(relative), bytes)
	if _golden_enabled:
		_write_bytes(_golden_root.path_join(relative), bytes)


func _write_bytes(path: String, bytes: PackedByteArray) -> void:
	DirAccess.make_dir_recursive_absolute(path.get_base_dir())
	var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		_failed = true
		push_error("Battle oracle: cannot write " + path)
		return
	file.store_buffer(bytes)
	file.close()


func _canonical_bytes(value: Variant) -> PackedByteArray:
	return _encode_json(_canonical(value)).to_utf8_buffer()


func _canonical(value: Variant) -> Variant:
	match typeof(value):
		TYPE_NIL, TYPE_BOOL, TYPE_STRING:
			return value
		TYPE_STRING_NAME:
			return String(value)
		TYPE_INT:
			if value > 9007199254740991 or value < -9007199254740991:
				return _i64(value)
			return value
		TYPE_FLOAT:
			return {"$f64": _double_hex(value)}
		TYPE_VECTOR2:
			return [_canonical(value.x), _canonical(value.y)]
		TYPE_VECTOR2I:
			return [value.x, value.y]
		TYPE_VECTOR3:
			return [_canonical(value.x), _canonical(value.y), _canonical(value.z)]
		TYPE_VECTOR3I:
			return [value.x, value.y, value.z]
		TYPE_DICTIONARY:
			var out: Dictionary = {}
			for key: Variant in (value as Dictionary).keys():
				if typeof(key) != TYPE_STRING and typeof(key) != TYPE_STRING_NAME:
					_failed = true
					push_error("Battle oracle: non-string canonical key " + str(key))
				out[str(key)] = _canonical((value as Dictionary)[key])
			return out
		TYPE_ARRAY:
			var out: Array = []
			for item: Variant in value:
				out.append(_canonical(item))
			return out
		TYPE_PACKED_VECTOR2_ARRAY:
			var out: Array = []
			for item: Vector2 in value:
				out.append(_canonical(item))
			return out
		TYPE_PACKED_VECTOR3_ARRAY:
			var out: Array = []
			for item: Vector3 in value:
				out.append(_canonical(item))
			return out
		TYPE_PACKED_FLOAT32_ARRAY, TYPE_PACKED_FLOAT64_ARRAY:
			var out: Array = []
			for item: float in value:
				out.append(_canonical(item))
			return out
		TYPE_PACKED_INT32_ARRAY, TYPE_PACKED_INT64_ARRAY, TYPE_PACKED_BYTE_ARRAY:
			var out: Array = []
			for item: int in value:
				out.append(_canonical(item))
			return out
		TYPE_PACKED_STRING_ARRAY:
			var out: Array = []
			for item: String in value:
				out.append(item)
			return out
		_:
			_failed = true
			push_error("Battle oracle: unsupported canonical type %s" % type_string(typeof(value)))
			return null


func _encode_json(value: Variant) -> String:
	match typeof(value):
		TYPE_NIL:
			return "null"
		TYPE_BOOL:
			return "true" if value else "false"
		TYPE_INT:
			return str(value)
		TYPE_FLOAT:
			return _encode_json({"$f64": _double_hex(value)})
		TYPE_STRING:
			return JSON.stringify(value)
		TYPE_ARRAY:
			var parts := PackedStringArray()
			for item: Variant in value:
				parts.append(_encode_json(item))
			return "[" + ",".join(parts) + "]"
		TYPE_DICTIONARY:
			var keys: Array = (value as Dictionary).keys()
			keys.sort()
			var parts := PackedStringArray()
			for key: String in keys:
				parts.append(JSON.stringify(key) + ":" + _encode_json((value as Dictionary)[key]))
			return "{" + ",".join(parts) + "}"
		_:
			_failed = true
			push_error("Battle oracle: cannot encode canonical type " + type_string(typeof(value)))
			return "null"


func _double_hex(value: float) -> String:
	var peer := StreamPeerBuffer.new()
	peer.big_endian = true
	peer.put_double(value)
	return peer.data_array.hex_encode()


func _sha256(bytes: PackedByteArray) -> String:
	var context := HashingContext.new()
	context.start(HashingContext.HASH_SHA256)
	context.update(bytes)
	return context.finish().hex_encode()


func _schema_manifest() -> Dictionary:
	return {
		"schema": SCHEMA_MANIFEST,
		"version": 1,
		"scope": "Godot 4.7.2 current-working-tree default skirmish oracle only; no Unity parity claim",
		"artifacts": {
			"snapshot": "seed_N/snapshots/tick_NNNNNN.json: complete canonical state for detailed seeds",
			"checkpoints": "seed_N/checkpoints.json: ordered tick hashes and key counts for every seed",
			"aar": "seed_N/aar.json: canonical final BattleStats.summary plus seed; real_sec absent",
			"aar_compatibility": "seed_N/aar.compat.json: legacy Godot pretty JSON used only for exact baseline comparison",
			"run": "run_manifest.json: engine, runner, input hashes, per-seed files and totals",
		},
		"canonical_json": {
			"bytes": "UTF-8 without BOM or trailing newline; no insignificant whitespace",
			"object_key_order": "ascending String ordinal order; schema keys are ASCII, so this is also UTF-8 byte order",
			"array_order": "preserved exactly; see collection_order",
			"string": "JSON string escaping produced by Godot 4.7.2 JSON.stringify(string)",
			"integer": "JSON decimal integer within +/-9007199254740991; otherwise {$i64: signed decimal string}",
			"explicit_int64": "rng.state, engine hashes, and checkpoint engine_state_hash are always {$i64: signed decimal string}",
			"float": "every float is {$f64: sixteen lowercase hexadecimal digits}; digits are IEEE-754 binary64 bits in big-endian order",
			"float_coverage": "finite values, infinities, all NaN bit patterns, and negative zero are preserved without decimal conversion",
			"vec2": "two-element array [x,y] with each component encoded as float; vec2i uses integer components",
			"vec3": "three-element array [x,y,z] with each component encoded as float; vec3i uses integer components",
			"null_id": "object references use -1; null string references use empty string; nullable extension objects use null",
			"sha256": "lowercase SHA-256 over exact artifact bytes; checkpoint sha256 is over the complete canonical snapshot bytes",
			"compatibility_exception": "aar.compat.json is not canonical and uses Godot JSON.stringify(aar, two spaces); aar.json is authoritative for precision",
		},
		"tick_semantics": {
			"tick": "number of completed Battle.step_tick calls; systems processing tick t run while world.tick=t, then world.tick becomes t+1",
			"initial": "tick 0 after BattleSetup.skirmish and begin_battle, before the first step",
			"scheduled": "ticks divisible by 1200, plus tick 100 and tick 600 when reached",
			"arrival": "snapshot tick is emitted reinforcement_arrived event tick + 1, after the whole simulation step; arrival_event_ticks records the event ticks",
			"final": "immediately after the step that fills battle.result; final_tick is normally result.tick+1",
			"event_queue": "at scheduled ticks divisible by 1200 the baseline runner drain occurs before capture",
		},
		"enums": {
			"Element.State": {"OK": 0, "DAMAGED": 1, "DESTROYED": 2, "ABANDONED": 3},
			"Unit.Level": {"TASK_FORCE": 0, "COMPANY": 1, "PLATOON": 2},
			"Unit.Comms": {"GOOD": 0, "POOR": 1, "CUT": 2},
			"Order.State": {"QUEUED": 0, "PROPAGATING": 1, "RECEIVED": 2, "EXECUTING": 3, "DONE": 4, "CANCELLED": 5},
			"Order.Type": {"MOVE": 0, "FAST_MOVE": 1, "ATTACK_MOVE": 2, "SEIZE": 3, "DEFEND": 4, "WITHDRAW": 5, "RESUPPLY": 6, "MOUNT": 7, "DISMOUNT": 8, "SET_FORMATION": 9, "SET_SOP": 10, "CALL_FIRE": 11, "DETACH": 12, "REJOIN": 13, "REORGANIZE": 14, "CANCEL": 15, "FOLLOW": 16, "HALT": 17, "LAY_MINES": 18, "CLEAR_MINES": 19, "FORTIFY": 20},
			"FireSupport.Asset": {"MORTAR": 0, "ARTILLERY": 1, "CAS": 2},
			"FireSupport.Phase": {"PROCESSING": 0, "ADJUSTING": 1, "EFFECT": 2, "DONE": 3, "CANCELLED": 4},
		},
		"records": _schema_records(),
		"collection_order": {
			"elements": "ascending element id (battle registration order)",
			"units": "ascending unit id (battle registration order)",
			"orders": "ascending order id; hops in propagation order; order_system.active_orders and capacity queues in live processing order",
			"path.requests": "snapshot-local ref ascending: live queue order first, then first owner in element-id/unit-id order; queue_order is authoritative FIFO",
			"path.threats.cells": "ascending linear terrain cell index",
			"path.built_grids": "ascending side/mobility string key; construction order has no simulation effect",
			"known_enemies": "TaskForce dictionary insertion/iteration order; this order can affect AI tie-breaking",
			"platoon.contacts": "contacts dictionary insertion/iteration order",
			"detection.pending_reports": "pending dictionary insertion/iteration order; detection.seen sorted by target id",
			"comms.nodes": "live node construction order; comms.edges grouped in node order with each row's original receiver order",
			"fire missions": "live mission array order; shells and smokes in creation/array order with explicit sequence",
			"supply.requests": "request FIFO; supply.tasks and returning in live dictionary iteration order",
			"minefields and positions": "live array order; positions carry explicit sequence",
			"scoring.points": "scenario control-point definition order; scoring.sides and all side-only records use blue then red",
			"scoring.committed_element_ids": "ascending element id within each side",
			"spatial.buckets": "cell y then x ascending; element_ids within a bucket preserve insertion order",
			"ai.assignments and last_orders": "ascending unit/company id",
			"key_value_maps": "ammo, reload, losses, and detection.seen use {key,value} entries sorted by string key unless a behavioral order is specified above",
			"stats.timeline": "event emission order",
			"checkpoints": "strictly ascending snapshot tick; reasons retain initial/scheduled, reinforcement_arrival, final priority",
		},
		"coverage": {
			"elements": "all mutable Element fields including paths, mounting, ammunition, reload, damage, supply and repair",
			"units": "all mutable Unit/Platoon/Company/TaskForce fields including guide trails, mission, comms, succession, morale, posture and membership",
			"orders": "all registered orders with complete params and hop state, active propagation and command-capacity queues",
			"intelligence": "platoon contacts, side seen state, pending reports and TF known_enemies",
			"systems": "path requests/threats, comms graph, combat counters, AI, fires/shells/smokes, supply, engineers, reinforcements, scoring, BattleStats and spatial buckets",
			"d043": "committed value, current force value/ratio, and exact committed element membership per side",
			"engine_hashes": "included as supplemental diagnostics only; canonical artifact hashes cover the detailed state",
		},
		"omissions": [
			{"subsystem": "catalog/static definitions", "captured": "definition ids plus SHA-256 of every data JSON in run_manifest", "omitted": "full weapon/element/unit/TOE/map/formation definitions"},
			{"subsystem": "terrain and AStar grids", "captured": "built grid keys and all sparse dynamic threat values", "omitted": "static terrain arrays/base weights and derived AStar internals; rebuild from hashed inputs"},
			{"subsystem": "movement transient caches", "captured": "persistent company_rest and current company_targets", "omitted": "members/speed/platoon caches cleared before each movement step"},
			{"subsystem": "presentation event queue", "captured": "pending count and canonical payload SHA-256; BattleStats timeline is detailed", "omitted": "individual pending event payload records"},
			{"subsystem": "wall clock/rendering/UI/audio", "captured": "none", "omitted": "real_sec, interpolation/render state, UI, audio and other presentation-only state"},
			{"subsystem": "RNG algorithm portability", "captured": "seed and exact Godot RandomNumberGenerator state", "omitted": "no claim that Godot RNG state or arithmetic automatically transfers to Unity"},
			{"subsystem": "runtime identity", "captured": "simulation ids and deterministic snapshot-local path request refs", "omitted": "Godot object instance ids and pointer values"},
		],
	}


func _schema_records() -> Dictionary:
	var records: Dictionary = {
		"snapshot": _record({"schema": "string", "seed": "int", "scenario": "string", "checkpoint": "checkpoint", "battle": "battle", "clock": "clock", "rng": "rng", "next_ids": "next_ids", "engine_hashes": "engine_hashes", "elements": "element[]", "units": "unit[]", "orders": "order[]", "order_system": "order_system", "path": "path", "movement": "movement", "spatial": "spatial", "comms": "comms", "detection": "detection", "combat": "combat", "ai": "ai", "fires": "fires", "smokes": "smoke[]", "supply": "supply", "engineers": "engineers", "reinforcements": "reinforcements", "scoring": "scoring", "stats": "stats", "events": "events"}),
		"checkpoint": _record({"tick": "int", "reasons": "string[]", "arrival_event_ticks": "int[]"}),
		"battle": _record({"deploying": "bool", "player_side": "string", "result": "object"}),
		"clock": _record({"tick": "int", "paused": "bool", "speed_index": "int", "accumulator": "float"}),
		"rng": _record({"seed": "int", "state": "i64"}),
		"next_ids": _record({"element": "int", "unit": "int", "order": "int", "fire_mission": "int", "minefield": "int"}),
		"element": _record({"id": "int", "def": "string", "side": "string", "platoon": "int", "index_in_platoon": "int", "pos": "vec2", "prev_pos": "vec2", "heading": "float", "prev_heading": "float", "velocity": "vec2", "state": "int", "state_name": "string", "damage_kind": "string", "hp": "float", "suppression": "float", "ammo": "key_int[]", "reload": "key_float[]", "target_id": "int", "move_target": "vec2", "has_move_target": "bool", "rest_heading": "float", "local_path": "vec2[]", "local_path_index": "int", "path_request_ref": "int", "stuck_ticks": "int", "straggler_ticks": "int", "mounted_in": "int", "passengers": "int[]", "on_map": "bool", "stationary_ticks": "int", "last_fired_tick": "int", "last_hit_tick": "int", "kills": "int", "killed_by": "int", "supply_left": "float", "repair_sec": "float", "destroyed_tick": "int"}),
		"guide": _record({"pos": "vec2", "heading": "float", "path": "vec2[]", "path_index": "int", "speed": "float", "final_facing": "float", "trail": "vec2[]", "pending_ref": "int", "destination": "vec2", "has_destination": "bool"}),
		"unit": _record({"id": "int", "display_name": "string", "short_name": "string", "side": "string", "level": "int", "parent": "int", "template_id": "string", "kind": "string", "guide": "guide", "formation": "string", "sop": "object", "skill": "string", "mission": "int", "queued_orders": "int[]", "detached": "bool", "commander": "int", "succession_until_tick": "int", "succession_delay_mult": "float", "pending_successor": "int", "ad_hoc": "bool", "awaiting_follow": "bool", "morale": "float", "comms_quality": "float", "comms_state": "int", "comms_hops": "int", "reform_until_tick": "int", "on_map": "bool", "phase": "int", "role": "string", "posture": "string", "defend_facing": "float", "routed_until_tick": "int", "initial_element_count": "int", "last_contact_tick": "int", "last_hit_tick": "int", "contact_halt_until": "int", "next_halt_tick": "int", "next_disperse_tick": "int", "routed": "bool", "auto_withdrawn": "bool", "formation_error": "float", "column_mode": "bool", "column_hold_m": "float", "settling_since_tick": "int", "guide_speed_limit": "float", "platoon": "platoon_extension|null", "company": "company_extension|null", "task_force": "task_force_extension|null"}),
		"platoon_extension": _record({"is_hq_section": "bool", "mounted": "bool", "loadout": "string", "attached": "bool", "last_shelled_tick": "int", "last_shell_pos": "vec2", "last_assign_key": "vec3", "last_assign_members": "int", "contacts": "key_int[]", "eng_task": "string", "eng_progress": "float", "mines_left": "int", "positions_built": "int"}),
		"company_extension": _record({"platoons": "int[]", "hq": "int", "xo": "int"}),
		"task_force_extension": _record({"companies": "int[]", "organic": "int[]", "cp_section": "int", "toe": "string", "faction": "string", "known_enemies": "known_enemy[]", "vp": "float", "command_capacity_per_min": "float", "cp_lost": "bool", "artillery_missions": "int", "cas_sorties": "int", "entry_pos": "vec2"}),
		"known_enemy": _record({"element": "int", "pos": "vec2", "tick": "int", "class": "string"}),
		"order": _record({"id": "int", "tick_issued": "int", "issuer": "string", "issuer_unit_id": "int", "target_unit_id": "int", "type": "int", "type_name": "string", "params": "object", "hops": "hop[]", "hop_index": "int", "state": "int", "state_name": "string", "parent_order_id": "int", "cost": "float", "blocked_since_tick": "int", "bypass": "bool", "received_tick": "int"}),
		"hop": _record({"from": "int", "to": "int", "base": "float", "start": "int", "arrive": "int", "relays": "int"}),
		"path_request": _record({"ref": "int", "queue_index": "int", "owners": "object[]", "from": "vec2", "to": "vec2", "mobility": "string", "side": "string", "points": "vec2[]", "done": "bool", "ok": "bool", "cancelled": "bool"}),
		"fire_mission": _record({"id": "int", "side": "string", "asset": "int", "smoke": "bool", "target": "vec2", "observer_id": "int", "observer_quality": "int", "phase": "int", "requested_tick": "int", "ready_tick": "int", "next_round_tick": "int", "rounds_left": "int", "rounds_fired": "int", "shooter_unit_id": "int", "weapon": "string", "sigma": "float", "delay_sec": "float", "first_impact": "vec2"}),
		"shell": _record({"sequence": "int", "mission_id": "int", "pos": "vec2", "land_tick": "int", "weapon": "string", "smoke": "bool", "asset": "int", "source_id": "int", "side": "string"}),
		"scoring_side": _record({"side": "string", "vp": "float", "committed_value": "float", "committed_element_ids": "int[]", "force_value": "float", "value_ratio": "float"}),
		"checkpoint_index_entry": _record({"tick": "int", "reasons": "string[]", "arrival_event_ticks": "int[]", "detail_file": "string", "bytes": "int", "sha256": "sha256", "engine_state_hash": "i64", "counts": "object"}),
	}
	records.merge(_schema_additional_records(), true)
	return records


func _schema_additional_records() -> Dictionary:
	return {
		"engine_hashes": _record({"world": "i64", "systems": "system_hash[]"}),
		"system_hash": _record({"name": "string", "omitted_from_world_hash": "bool", "state_hash": "i64"}),
		"order_system": _record({"active_orders": "int[]", "capacity": "capacity[]", "capacity_queues": "capacity_queue[]", "delay_samples": "float[]", "cancelled_count": "int"}),
		"capacity": _record({"side": "string", "tokens": "float", "tokens_now": "float", "tokens_max": "float"}),
		"capacity_queue": _record({"side": "string", "orders": "int[]"}),
		"path": _record({"requests_processed": "int", "queue_order": "int[]", "requests": "path_request[]", "threats": "path_threat[]", "built_grids": "string[]"}),
		"path_threat": _record({"side": "string", "cells": "path_threat_cell[]"}),
		"path_threat_cell": _record({"index": "int", "value": "float"}),
		"request_owner": _record({"kind": "string", "id": "int"}),
		"movement": _record({"company_targets": "unit_target[]", "company_rest": "company_rest[]", "transient_caches": "string"}),
		"unit_target": _record({"unit": "int", "target": "vec2"}),
		"company_rest": _record({"company": "int", "key": "vec3", "member_count": "int", "targets": "unit_target[]"}),
		"spatial": _record({"cell_size": "float", "buckets": "spatial_bucket[]"}),
		"spatial_bucket": _record({"cell": "vec2i", "element_ids": "int[]"}),
		"comms": _record({"built_tick": "int", "sides": "comms_side[]"}),
		"comms_side": _record({"side": "string", "nodes": "int[]", "edges": "comms_edge[]", "jammers": "int[]"}),
		"comms_edge": _record({"from": "int", "to": "int", "quality": "float"}),
		"detection": _record({"detections_total": "int", "reports_delivered": "int", "sides": "detection_side[]"}),
		"combat": _record({"shots_fired": "int", "hits": "int", "kills": "int"}),
		"detection_side": _record({"side": "string", "seen": "key_int[]", "pending_reports": "pending_report[]"}),
		"pending_report": _record({"target": "int", "pos": "vec2", "class": "string", "tick": "int", "platoon": "int", "deliver": "int"}),
		"ai": _record({"sides": "string[]", "assignments": "ai_assignment[]", "last_orders": "ai_last_order[]", "fire_cooldowns": "ai_fire_cooldown[]"}),
		"ai_assignment": _record({"company": "int", "point": "string"}),
		"ai_last_order": _record({"unit": "int", "type": "int"}),
		"ai_fire_cooldown": _record({"side": "string", "last_artillery": "int", "last_cas": "int"}),
		"fires": _record({"missions": "fire_mission[]", "shells": "shell[]"}),
		"smoke": _record({"sequence": "int", "pos": "vec2", "radius": "float", "until_tick": "int"}),
		"supply": _record({"requests": "int[]", "tasks": "supply_task[]", "returning": "int[]", "delivered_total": "float"}),
		"supply_task": _record({"section": "int", "target": "int"}),
		"engineers": _record({"minefields": "minefield[]", "positions": "field_position[]", "mine_strikes": "int"}),
		"minefield": _record({"id": "int", "pos": "vec2", "radius": "float", "side": "string", "charges": "int", "known_by": "side_bool[]"}),
		"field_position": _record({"sequence": "int", "pos": "vec2", "radius": "float", "side": "string"}),
		"side_bool": _record({"side": "string", "known": "bool"}),
		"reinforcements": _record({"waves": "reinforcement_wave[]"}),
		"reinforcement_wave": _record({"side": "string", "arrived_wave": "int"}),
		"scoring": _record({"points": "scoring_point[]", "objectives": "scoring_objective[]", "sides": "scoring_side[]", "collapse_threshold": "float", "committed_rule": "string"}),
		"scoring_point": _record({"id": "string", "name": "string", "pos": "vec2", "radius_m": "float", "vp_per_min": "float", "owner": "string", "contested": "bool", "held": "side_seconds[]"}),
		"side_seconds": _record({"side": "string", "seconds": "float"}),
		"scoring_objective": _record({"id": "string", "done": "bool|null", "recorded": "bool"}),
		"stats": _record({"first_contact_tick": "int", "sides": "stats_side[]", "timeline": "object[]"}),
		"stats_side": _record({"side": "string", "losses": "key_int[]", "damaged": "int", "order_delays": "float[]", "fire_missions": "int", "comms_cuts": "int", "resupplies": "int", "shots": "int"}),
		"events": _record({"pending_count": "int", "pending_sha256": "sha256", "payloads": "string"}),
		"key_int": _record({"key": "string", "value": "int"}),
		"key_float": _record({"key": "string", "value": "float"}),
		"checkpoint_index": _record({"schema": "string", "seed": "int", "scenario": "string", "final_tick": "int", "result_tick": "int", "truncated": "bool", "checkpoints": "checkpoint_index_entry[]", "final_aar": "object", "baseline": "object"}),
		"aar": _record({"result": "object", "duration_sec": "float", "first_contact_sec": "float", "seed": "int", "sides": "object", "points": "object[]", "timeline": "object[]"}),
	}


func _record(types: Dictionary) -> Dictionary:
	var fields: Array = types.keys()
	fields.sort()
	return {"field_order": fields, "types": types}


func _simulation_inputs() -> Array:
	var paths: Array = []
	_collect_gd_files("res://sim", paths)
	paths.append("res://services/battle_setup.gd")
	paths.append("res://services/data_loader.gd")
	paths.append("res://services/force_store.gd")
	paths.append("res://tools/export_unity_battle_oracle.gd")
	paths.append("res://project.godot")
	paths.sort()
	var out: Array = []
	for path: String in paths:
		var bytes: PackedByteArray = FileAccess.get_file_as_bytes(path)
		out.append({"file": path.trim_prefix("res://"), "bytes": bytes.size(), "sha256": _sha256(bytes)})
	return out


func _collect_gd_files(root: String, out: Array) -> void:
	var directory: DirAccess = DirAccess.open(root)
	if directory == null:
		_failed = true
		return
	directory.list_dir_begin()
	var name: String = directory.get_next()
	while not name.is_empty():
		if name != "." and name != "..":
			var path: String = root.path_join(name)
			if directory.current_is_dir():
				_collect_gd_files(path, out)
			elif name.ends_with(".gd"):
				out.append(path)
		name = directory.get_next()
	directory.list_dir_end()
