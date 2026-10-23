class_name OrderExecutor
extends RefCounted
## 제대가 명령을 "수신"했을 때 실제 행동으로 바꾼다. 전파(지연)는 OrderSystem 이, 하위 번역은 중대 AI 가 맡는다.
## 이동 명령은 제대 가이드에 경로를 요청하고, 도착 처리는 MovementSystem 이 on_arrival() 로 돌려준다.

var battle: Battle


func _init(p_battle: Battle) -> void:
	battle = p_battle


func execute(unit: Unit, order: Order) -> void:
	# 플레이어가 중대 대형 안의 소대에 직접 명령하면 그 소대는 분리된다. PLAN 5.1.
	if order.bypass and unit is Platoon and (unit as Platoon).follows_parent():
		unit.detached = true
	# 사기가 꺾였거나 전투 불능인 제대는 공격 명령을 거부한다. PLAN 5.1, 9.4.
	if (order.type == Order.Type.ATTACK_MOVE or order.type == Order.Type.SEIZE) and order.issuer != &"self" \
			and unit.level != Unit.Level.TASK_FORCE and (MoraleSystem.is_shaken(unit) or unit.is_combat_ineffective()):
		order.state = Order.State.CANCELLED
		battle.world.events.emit_event(&"order_refused", battle.world.tick, {"unit": unit.id, "side": unit.side,
			"type": order.type})
		return
	# Shift 대기열: 진행 중인 이동 임무가 끝나면 실행.
	if order.is_movement() and order.params.get("append", false) == true and unit.mission != null and not unit.mission.is_finished():
		order.state = Order.State.RECEIVED
		unit.queued_orders.append(order)
		return
	order.state = Order.State.EXECUTING
	match order.type:
		Order.Type.MOVE, Order.Type.FAST_MOVE, Order.Type.ATTACK_MOVE, Order.Type.SEIZE, Order.Type.DEFEND, Order.Type.WITHDRAW:
			_start_movement(unit, order)
		Order.Type.HALT:
			_finish_mission(unit, Order.State.CANCELLED)
			unit.guide.clear_path()
			unit.posture = &"idle"
			order.state = Order.State.DONE
		Order.Type.SET_FORMATION:
			_set_formation(unit, order)
			order.state = Order.State.DONE
		Order.Type.SET_SOP:
			var sop_changes: Dictionary = order.params.get("sop", {})
			unit.sop.apply(sop_changes)
			if order.params.get("recursive", true) == true:
				for child: Unit in unit.children_units():
					child.sop.apply(sop_changes)
			order.state = Order.State.DONE
		Order.Type.DETACH:
			unit.detached = true
			order.state = Order.State.DONE
		Order.Type.REJOIN:
			unit.detached = false
			unit.awaiting_follow = false
			_finish_mission(unit, Order.State.CANCELLED)
			unit.guide.clear_path()
			unit.posture = &"idle"
			order.state = Order.State.DONE
		Order.Type.MOUNT, Order.Type.DISMOUNT:
			if unit is Platoon:
				set_mounted(unit as Platoon, order.type == Order.Type.MOUNT)
			for child: Unit in unit.children_units():
				if child is Platoon:
					set_mounted(child as Platoon, order.type == Order.Type.MOUNT)
			order.state = Order.State.DONE
		Order.Type.CANCEL:
			order.state = Order.State.DONE
		Order.Type.FOLLOW:
			# 중대의 번역 명령이 소대에 닿았다: 중대 대형을 따라 움직이기 시작.
			unit.awaiting_follow = false
			if unit.mission != null and unit.mission.issuer != &"unit":
				unit.mission.state = Order.State.CANCELLED
			unit.mission = null
			unit.guide.clear_path()
			order.state = Order.State.DONE
		_:
			# RESUPPLY, CALL_FIRE, REORGANIZE 등은 해당 시스템이 처리한다.
			pass


## 이동 계열 명령 시작: 가이드 경로 요청, 자세·대형 설정.
func _start_movement(unit: Unit, order: Order) -> void:
	if unit.mission != null and unit.mission != order and not unit.mission.is_finished():
		unit.mission.state = Order.State.CANCELLED
	unit.mission = order
	var target: Vector2 = order.get_pos() if order.has_pos() else unit.guide.pos
	target = battle.terrain.clamp_to_map(target)
	unit.guide.clear_path()
	unit.settling_since_tick = -1
	unit.guide.final_facing = order.get_facing()
	unit.defend_facing = NAN
	match order.type:
		Order.Type.WITHDRAW:
			unit.posture = &"withdrawing"
		Order.Type.DEFEND:
			unit.posture = &"moving"
		_:
			unit.posture = &"moving"
	if order.params.has("formation"):
		_apply_formation_id(unit, StringName(str(order.params["formation"])))
	if order.params.has("technique"):
		unit.sop.apply({"technique": str(order.params["technique"])})
	if unit is Company:
		_brief_platoons(unit as Company, order)
	if unit.guide.pos.distance_to(target) < 5.0:
		on_arrival(unit)
		return
	unit.guide.pending = battle.pathfinder.request(unit.guide.pos, target, path_mobility(unit), unit.side)


## 중대가 새 이동 명령을 받으면 소대마다 번역 명령(FOLLOW)을 보낸다. 중대→소대 지연을 거쳐 닿는다. PLAN 5.1.
func _brief_platoons(c: Company, order: Order) -> void:
	if battle.order_system == null:
		return
	var targets: Array[Platoon] = c.formation_platoons()
	if c.hq != null and c.hq.on_map:
		targets.append(c.hq)
	for p: Platoon in targets:
		if p == c.hq and c.leader() != null and c.leader().platoon == p:
			continue  # 중대장이 탄 본부 반은 즉시 따른다.
		p.awaiting_follow = true
		var sub: Order = Order.make(Order.Type.FOLLOW, p.id)
		sub.parent_order_id = order.id
		battle.order_system.submit_sub_order(sub, c)


## 가이드가 목적지에 도착했을 때(MovementSystem 이 부른다).
func on_arrival(unit: Unit) -> void:
	var order: Order = unit.mission
	if order == null:
		unit.posture = &"idle"
		return
	var facing: float = order.get_facing()
	if is_nan(facing):
		facing = unit.guide.heading
	match order.type:
		Order.Type.SEIZE, Order.Type.DEFEND:
			unit.posture = &"defending"
			unit.defend_facing = facing
			unit.guide.final_facing = facing
			if order.type == Order.Type.SEIZE and not order.params.has("formation"):
				_apply_formation_id(unit, &"co_line" if unit.level == Unit.Level.COMPANY else &"line")
		Order.Type.WITHDRAW:
			unit.posture = &"idle"
			unit.guide.final_facing = facing
		_:
			unit.posture = &"idle"
			unit.guide.final_facing = order.get_facing()
	order.state = Order.State.DONE
	battle.world.events.emit_event(&"arrived", battle.world.tick, {"unit": unit.id, "side": unit.side, "type": order.type})
	_start_next_queued(unit)


func _start_next_queued(unit: Unit) -> void:
	if unit.queued_orders.is_empty():
		return
	var next: Order = unit.queued_orders.pop_front()
	execute(unit, next)


func _finish_mission(unit: Unit, state: int) -> void:
	if unit.mission != null and not unit.mission.is_finished():
		unit.mission.state = state
	unit.mission = null
	unit.queued_orders.clear()


func _set_formation(unit: Unit, order: Order) -> void:
	var fid := StringName(order.get_string("formation"))
	_apply_formation_id(unit, fid)
	if order.params.has("child_formation"):
		var cfid := StringName(order.get_string("child_formation"))
		for child: Unit in unit.children_units():
			if child is Platoon and not (child as Platoon).is_hq_section:
				_apply_formation_id(child, cfid)


## 대형 변경. 제대에 맞지 않는 대형은 무시한다. 전환 시간 = 기본 전환 시간 × 통제 난이도 × 숙련. PLAN 7.3.
func _apply_formation_id(unit: Unit, fid: StringName) -> bool:
	var f: FormationDef = battle.catalog.get_formation(fid)
	if f == null:
		return false
	var want_echelon: StringName = &"company" if unit.level == Unit.Level.COMPANY else &"platoon"
	if f.echelon != want_echelon or f == unit.formation:
		return false
	unit.formation = f
	var sec: float = f.get_mod(&"transition_sec", 10.0) * f.get_mod(&"control", 1.0) * unit.skill_delay_factor()
	unit.reform_until_tick = battle.world.tick + roundi(sec * SimConst.TICK_RATE)
	return true


func set_mounted(p: Platoon, mount: bool) -> void:
	if not p.has_squads() or not p.has_transport() or p.mounted == mount:
		return
	p.mounted = mount
	if mount:
		for squad: Element in p.squads():
			if not squad.is_alive():
				continue
			var ride: Element = _free_seat(p, squad)
			if ride != null:
				squad.mounted_in = ride
				ride.passengers.append(squad)
				squad.pos = ride.pos
	else:
		for v: Element in p.vehicles():
			for squad: Element in v.passengers:
				squad.mounted_in = null
				squad.pos = battle.terrain.nearest_passable(v.pos + FormationSolver.right_vector(v.heading) * 8.0, Terrain.MOBILITY_FOOT)
				squad.heading = v.heading
			v.passengers.clear()
	p.reform_until_tick = battle.world.tick + 10 * SimConst.TICK_RATE


func _free_seat(p: Platoon, squad: Element) -> Element:
	var best: Element = null
	var best_d: float = INF
	for v: Element in p.vehicles():
		if v.is_alive() and v.passengers.size() < v.def.transport_capacity:
			var d: float = v.pos.distance_squared_to(squad.pos)
			if d < best_d:
				best_d = d
				best = v
	return best


static func path_mobility(unit: Unit) -> StringName:
	for e: Element in unit.alive_elements():
		if e.is_vehicle():
			return Terrain.MOBILITY_VEHICLE
	return Terrain.MOBILITY_FOOT
