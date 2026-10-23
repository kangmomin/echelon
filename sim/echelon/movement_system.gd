class_name MovementSystem
extends SimSystem
## 이동: 가이드(중대 → 소대) 진행, 대형 슬롯 배정, 엘리먼트 조향. PLAN 7장, 17.1, 17.3.
##
## 순서(틱마다):
##   1) 이전 위치 저장, 경로 요청 처리
##   2) 위에서 아래로 제대 가이드 갱신(중대 → 중대본부·소대 → 직할 소대)
##   3) 소대마다 엘리먼트 목표 위치 배정
##   4) 엘리먼트 조향·이동(id 순)
##   5) 탑승 분대 위치 동기화, 공간 해시 재구성

## 엘리먼트가 목표에 도착했다고 보는 반경(m).
const ARRIVE_RADIUS_M: float = 6.0
## 이 거리 안에서는 감속한다.
const SLOW_RADIUS_M: float = 40.0
## 분리 벡터 반경(m).
const SEPARATION_RADIUS_M: float = 20.0
## 소대 대형 대기: 엘리먼트가 슬롯에서 이만큼 떨어지면 가이드가 기다린다. PLAN 17.1.
const PLATOON_WAIT_M: float = 150.0
## 중대 대형 대기: 소대 가이드가 슬롯에서 이만큼 떨어지면 중대 가이드가 기다린다.
const COMPANY_WAIT_M: float = 300.0
## 이 시간(틱) 넘게 슬롯에서 멀면 낙오로 보고 대기 규칙에서 뺀다.
const STRAGGLER_TICKS: int = 600
## 종대(도로·병목) 간격(m).
const PLATOON_COLUMN_SPACING_M: float = 25.0
const COMPANY_COLUMN_SPACING_M: float = 160.0
## 하차 기계화 소대에서 차량이 분대 뒤로 떨어지는 거리(m).
const TRAILING_VEHICLE_GAP_M: float = 60.0
## 병목(다리) 탐지: 가이드 앞 이 거리 안에 물가 도로가 있으면 종대.
const CHOKE_LOOKAHEAD_M: float = 200.0
## 경계 이동: 선도 엘리먼트가 앞서 가는 거리(m). PLAN 7.3.
const OVERWATCH_LEAD_M: float = 100.0
## 대형 소대가 중대 슬롯을 따라잡을 때 낼 수 있는 속도 배율.
const CATCH_UP_MULT: float = 1.15
## 도착 판정: 가이드 도착 후 대형 오차가 이 값 이하면 완료. 아니면 SETTLE_TIMEOUT 뒤 완료.
const SETTLED_ERROR_M: float = 50.0
const SETTLE_TIMEOUT_TICKS: int = 1200
## 엘리먼트가 직선으로 못 가는지 다시 확인하는 주기(틱).
const LINE_CHECK_INTERVAL: int = 20
const STUCK_TICKS_BEFORE_PATH: int = 10
## 대형 추종 중 경로 재요청: 목표가 경로 끝에서 이만큼 멀어지면(m), 이 주기(틱)로.
const FOLLOW_REPATH_M: float = 80.0
## 중대가 소대 브리핑을 기다리는 최대 시간(틱). 통신 두절 소대 때문에 중대 전체가 멈추지 않게.
const BRIEFING_TIMEOUT_TICKS: int = 300
const FOLLOW_REPATH_INTERVAL: int = 10

var battle: Battle
var _company_targets: Dictionary[int, Vector2] = {}


func _init(p_battle: Battle) -> void:
	battle = p_battle


func get_system_name() -> StringName:
	return &"movement"


func step(world: SimWorld) -> void:
	var dt: float = SimConst.TICK_DT
	for e: Element in battle.elements:
		e.prev_pos = e.pos
		e.prev_heading = e.heading
	battle.pathfinder.process()
	_company_targets.clear()
	for side: StringName in Battle.SIDES:
		var tf: TaskForce = battle.task_forces.get(side)
		if tf == null:
			continue
		for u: Unit in tf.all_units():
			if not u.on_map or u.level == Unit.Level.TASK_FORCE:
				continue
			if u.level == Unit.Level.COMPANY:
				_update_company(u as Company, world.tick, dt)
			else:
				_update_platoon(u as Platoon, world.tick, dt)
	for e: Element in battle.elements:
		if e.is_active() and not e.is_mounted():
			_move_element(e, world.tick, dt)
	for e: Element in battle.elements:
		if e.is_mounted():
			e.pos = e.mounted_in.pos
			e.heading = e.mounted_in.heading
	battle.rebuild_spatial()


func state_hash() -> int:
	var h: int = 0
	for e: Element in battle.elements:
		h = hash([h, e.pos.x, e.pos.y, e.heading])
	return h


# --- 중대 ----------------------------------------------------------------

func _update_company(c: Company, tick: int, dt: float) -> void:
	_poll_path(c)
	var speed: float = c.base_speed() * c.formation.get_mod(&"speed", 1.0)
	if c.is_reforming(tick):
		speed *= 0.5
	if c.column_mode and battle.terrain.type_at(c.guide.pos) == Terrain.Type.ROAD:
		speed = _road_march_speed(c, c.base_speed())
	if c.formation_error > COMPANY_WAIT_M or tick < c.contact_halt_until:
		speed = 0.0
	# 소대들이 번역 명령을 받을 때까지(최대 BRIEFING_TIMEOUT) 출발하지 않는다.
	if c.mission != null and c.mission.received_tick >= 0 and tick - c.mission.received_tick < BRIEFING_TIMEOUT_TICKS:
		for p: Platoon in c.formation_platoons():
			if p.awaiting_follow:
				speed = 0.0
				break
	if c.is_routed(tick) or c.is_leaderless(tick):
		speed = minf(speed, c.base_speed() * 0.5)
	c.guide_speed_limit = speed
	var moved: float = _advance_guide(c, dt, speed)

	# 소대 목표 위치 계산.
	var plts: Array[Platoon] = c.formation_platoons()
	if c.guide.is_moving() and (_on_road_ahead(c.guide) or _choke_ahead(c.guide)):
		c.column_hold_m = maxf(c.column_hold_m, (plts.size() + 2) * COMPANY_COLUMN_SPACING_M + 100.0)
	c.column_hold_m = maxf(0.0, c.column_hold_m - moved)
	c.column_mode = c.guide.is_moving() and (c.column_hold_m > 0.0 or c.formation.id == &"co_column")
	var terrain_name: StringName = battle.terrain.type_name_at(c.guide.pos)
	var slots: PackedVector2Array = FormationSolver.world_slots(c.formation, plts.size(), c.guide.pos, c.guide.heading, terrain_name)
	var worst: float = 0.0
	var column_index: int = 0
	for i: int in plts.size():
		var p: Platoon = plts[i]
		var target: Vector2
		if c.column_mode:
			target = c.guide.trail_point_behind(column_index * COMPANY_COLUMN_SPACING_M)
			column_index += 1
			if i == 0:
				column_index += 1  # 중대본부가 선도 소대 뒤에 들어간다.
		else:
			target = slots[i]
			if not _reachable_straight(c.guide.pos, target, Terrain.MOBILITY_VEHICLE):
				target = c.guide.trail_point_behind(absf(FormationSolver.world_to_local(target, c.guide.pos, c.guide.heading).y) + i * 30.0)
		target = battle.terrain.nearest_passable(target, OrderExecutor.path_mobility(p))
		_company_targets[p.id] = target
		# 아직 중대 지휘를 받지 못한 소대(번역 명령 대기·통신 두절)는 중대를 붙잡지 않는다.
		if not p.is_destroyed() and not p.awaiting_follow and p.comms_state != Unit.Comms.CUT:
			# 소대 가이드 오차 + 소대 안 엘리먼트 오차(가이드만 앞서 가는 것을 막는다).
			worst = maxf(worst, maxf(p.guide.pos.distance_to(target), p.formation_error))
	if c.hq != null and c.hq.on_map:
		var hq_target: Vector2
		if c.column_mode:
			hq_target = c.guide.trail_point_behind(COMPANY_COLUMN_SPACING_M)
		else:
			hq_target = FormationSolver.local_to_world(Vector2(0.0, -ForceFactory.HQ_OFFSET_BEHIND_M), c.guide.pos, c.guide.heading)
			if not _reachable_straight(c.guide.pos, hq_target, Terrain.MOBILITY_VEHICLE):
				hq_target = c.guide.trail_point_behind(ForceFactory.HQ_OFFSET_BEHIND_M)
		_company_targets[c.hq.id] = battle.terrain.nearest_passable(hq_target, Terrain.MOBILITY_VEHICLE)
	c.formation_error = worst
	_check_settled(c)


# --- 소대 ----------------------------------------------------------------

func _update_platoon(p: Platoon, tick: int, dt: float) -> void:
	if p.is_destroyed():
		return
	var base: float = p.base_speed()
	var speed: float = base * p.formation.get_mod(&"speed", 1.0)
	if p.is_reforming(tick):
		speed *= 0.5
	if p.sop.technique == &"bounding":
		speed *= 0.5
	if p.is_routed(tick):
		speed = base
	var waiting: bool = p.formation_error > PLATOON_WAIT_M

	if p.follows_parent() and _company_targets.has(p.id):
		var c: Company = p.company()
		var target: Vector2 = _company_targets[p.id]
		var face: float = c.guide.heading
		if not c.guide.is_moving() and not is_nan(c.defend_facing):
			face = c.defend_facing
		var catch_up: float = base * CATCH_UP_MULT
		if p.guide.pos.distance_to(target) < 40.0:
			catch_up = maxf(c.guide.speed, base * 0.5)
		if waiting or p.awaiting_follow:
			catch_up = 0.0
		p.guide_speed_limit = catch_up
		var before: Vector2 = p.guide.pos
		_follow_target(p, target, dt, catch_up, face, tick)
		if c.column_mode:
			p.column_hold_m = maxf(p.column_hold_m, (p.elements.size() + 1) * PLATOON_COLUMN_SPACING_M + 50.0)
		p.column_hold_m = maxf(0.0, p.column_hold_m - before.distance_to(p.guide.pos))
		p.column_mode = c.column_mode or p.column_hold_m > 0.0
		if not c.guide.is_moving():
			p.posture = c.posture
			p.defend_facing = c.defend_facing
	else:
		_poll_path(p)
		if p.guide.is_moving() and battle.terrain.type_at(p.guide.pos) == Terrain.Type.ROAD and _on_road_ahead(p.guide):
			speed = _road_march_speed(p, base)
		if waiting or tick < p.contact_halt_until:
			speed = 0.0
		p.guide_speed_limit = speed
		var moved: float = _advance_guide(p, dt, speed)
		if p.guide.is_moving() and (_on_road_ahead(p.guide) or _choke_ahead(p.guide)):
			p.column_hold_m = maxf(p.column_hold_m, (p.elements.size() + 1) * PLATOON_COLUMN_SPACING_M + 50.0)
		p.column_hold_m = maxf(0.0, p.column_hold_m - moved)
		p.column_mode = p.guide.is_moving() and (p.column_hold_m > 0.0 or p.formation.id == &"column")
	_assign_element_targets(p, tick)


## 소대 엘리먼트에게 목표 위치(대형 슬롯 또는 종대 위치)를 준다.
func _assign_element_targets(p: Platoon, _tick: int) -> void:
	var members: Array[Element] = p.formation_members()
	var g: Guide = p.guide
	var rest: float = g.heading
	if p.posture == &"defending" and not is_nan(p.defend_facing):
		rest = p.defend_facing
	var targets := PackedVector2Array()
	targets.resize(members.size())
	if p.column_mode:
		for i: int in members.size():
			targets[i] = g.trail_point_behind(i * PLATOON_COLUMN_SPACING_M)
	else:
		var terrain_name: StringName = battle.terrain.type_name_at(g.pos)
		var f: FormationDef = p.formation
		# 지형 압축: 숲·시가지에서는 횡대를 쐐기로 강등한다. PLAN 7.3.
		if (terrain_name == &"forest" or terrain_name == &"urban") and f.id == &"line":
			f = battle.catalog.get_formation(&"wedge")
		var slots: PackedVector2Array = FormationSolver.world_slots(f, members.size(), g.pos, g.heading, terrain_name)
		if p.sop.technique == &"overwatch" and g.is_moving() and slots.size() > 1:
			slots[0] += FormationSolver.forward_vector(g.heading) * OVERWATCH_LEAD_M
		for i: int in members.size():
			var t: Vector2 = slots[i]
			if not _reachable_straight(g.pos, t, members[i].mobility()):
				t = g.trail_point_behind(i * PLATOON_COLUMN_SPACING_M)
			targets[i] = t
	var worst: float = 0.0
	for i: int in members.size():
		var e: Element = members[i]
		e.move_target = battle.terrain.nearest_passable(targets[i], e.mobility(), 10)
		e.has_move_target = true
		e.rest_heading = rest
		var d: float = e.pos.distance_to(e.move_target)
		if d > PLATOON_WAIT_M:
			e.straggler_ticks += 1
		elif d < 50.0:
			e.straggler_ticks = 0
		if e.straggler_ticks < STRAGGLER_TICKS:
			worst = maxf(worst, d)
	# 하차한 기계화 소대: 차량은 분대 뒤를 따른다.
	var trailing: Array[Element] = p.trailing_vehicles()
	for i: int in trailing.size():
		var v: Element = trailing[i]
		var anchor: Vector2 = members[i].move_target if i < members.size() else g.pos
		var t: Vector2 = anchor - FormationSolver.forward_vector(g.heading) * TRAILING_VEHICLE_GAP_M
		v.move_target = battle.terrain.nearest_passable(t, Terrain.MOBILITY_VEHICLE, 10)
		v.has_move_target = true
		v.rest_heading = rest
	p.formation_error = worst
	_check_settled(p)


# --- 엘리먼트 ------------------------------------------------------------

func _move_element(e: Element, tick: int, dt: float) -> void:
	if not e.has_move_target or not e.can_move():
		e.velocity = Vector2.ZERO
		e.stationary_ticks += 1
		return
	var terrain: Terrain = battle.terrain
	var mob: StringName = e.mobility()
	var goal: Vector2 = e.move_target

	# 국지 경로(직선으로 못 갈 때).
	if e.path_request != null and e.path_request.done:
		if e.path_request.ok:
			e.local_path = e.path_request.points
			e.local_path_index = 1
		e.path_request = null
	var steer: Vector2 = goal
	if not e.local_path.is_empty():
		if e.local_path[e.local_path.size() - 1].distance_to(goal) > 60.0:
			e.clear_local_path()
		else:
			while e.local_path_index < e.local_path.size() and e.pos.distance_to(e.local_path[e.local_path_index]) < 8.0:
				e.local_path_index += 1
			if e.local_path_index >= e.local_path.size():
				e.clear_local_path()
			else:
				steer = e.local_path[e.local_path_index]

	var to: Vector2 = steer - e.pos
	var d: float = to.length()
	if d < ARRIVE_RADIUS_M and steer == goal:
		e.velocity = Vector2.ZERO
		e.turn_toward(e.rest_heading, dt)
		e.stationary_ticks += 1
		e.stuck_ticks = 0
		return

	var max_speed: float = e.max_speed_mps(terrain)
	var desired: float = max_speed
	if steer == goal and d < SLOW_RADIUS_M:
		desired = maxf(max_speed * d / SLOW_RADIUS_M, 1.0)
	var dir: Vector2 = to / d
	var sep: Vector2 = _separation(e)
	if sep != Vector2.ZERO:
		dir = (dir + sep).normalized()
	e.turn_toward(FormationSolver.heading_from_vector(dir), dt)
	# 차량은 방향이 크게 틀어져 있으면 먼저 돈다.
	if e.is_vehicle() and absf(angle_difference(e.heading, FormationSolver.heading_from_vector(dir))) > PI / 3.0:
		desired *= 0.3
	var delta_step: Vector2 = dir * desired * dt
	var next: Vector2 = terrain.clamp_to_map(e.pos + delta_step)
	var moved: bool = false
	if terrain.is_passable(next, mob):
		e.pos = next
		moved = true
	else:
		var slide_x: Vector2 = terrain.clamp_to_map(e.pos + Vector2(delta_step.x, 0.0))
		var slide_y: Vector2 = terrain.clamp_to_map(e.pos + Vector2(0.0, delta_step.y))
		if absf(delta_step.x) > absf(delta_step.y) and terrain.is_passable(slide_x, mob):
			e.pos = slide_x
			moved = true
		elif terrain.is_passable(slide_y, mob):
			e.pos = slide_y
			moved = true
		elif terrain.is_passable(slide_x, mob):
			e.pos = slide_x
			moved = true
	if moved:
		e.stuck_ticks = 0
		e.stationary_ticks = 0
	else:
		e.stuck_ticks += 1
		e.stationary_ticks += 1
	e.velocity = (e.pos - e.prev_pos) / dt

	var need_path: bool = e.stuck_ticks > STUCK_TICKS_BEFORE_PATH
	if not need_path and e.local_path.is_empty() and (tick + e.id) % LINE_CHECK_INTERVAL == 0 and d > SLOW_RADIUS_M:
		need_path = not terrain.line_passable(e.pos, goal, mob)
	if need_path and e.path_request == null and e.local_path.is_empty():
		e.path_request = battle.pathfinder.request(e.pos, goal, mob, e.side)
		e.stuck_ticks = 0


func _separation(e: Element) -> Vector2:
	var push: Vector2 = Vector2.ZERO
	for item: Variant in battle.spatial.query(e.pos, SEPARATION_RADIUS_M):
		var o: Element = item
		if o == e or o.side != e.side or o.is_mounted() or not o.is_active():
			continue
		var away: Vector2 = e.pos - o.pos
		var d: float = away.length()
		if d < 0.01:
			away = Vector2(1.0, 0.0).rotated(float(e.id))
			d = 0.01
		if d < SEPARATION_RADIUS_M:
			push += away / d * (1.0 - d / SEPARATION_RADIUS_M)
	return push * 0.8


# --- 가이드 공통 ---------------------------------------------------------

func _poll_path(u: Unit) -> void:
	var req: GridPathfinder.PathRequest = u.guide.pending
	if req == null or not req.done:
		return
	u.guide.pending = null
	if req.ok:
		u.guide.set_path(req.points)
	else:
		if u.mission != null:
			u.mission.state = Order.State.CANCELLED
			u.mission = null
		u.posture = &"idle"


## 경로를 따라 가이드를 움직이고, 도착하면 명령 도착 처리를 부른다. 이동한 거리를 돌려준다.
func _advance_guide(u: Unit, dt: float, speed: float) -> float:
	var was_moving: bool = u.guide.is_moving()
	var moved: float = u.guide.advance(dt, speed)
	if was_moving and not u.guide.is_moving():
		u.settling_since_tick = battle.world.tick
	if not u.guide.is_moving() and not is_nan(u.guide.final_facing):
		u.guide.turn_toward(u.guide.final_facing, dt)
	return moved


## 상위 대형 슬롯을 따라간다. 직선으로 못 가면(강·급경사) 경로를 받아 돌아간다.
func _follow_target(p: Platoon, target: Vector2, dt: float, speed: float, face: float, tick: int) -> void:
	var mob: StringName = OrderExecutor.path_mobility(p)
	if _reachable_straight(p.guide.pos, target, mob):
		if p.guide.is_moving() or p.guide.pending != null:
			p.guide.clear_path()
		p.guide.move_toward(target, dt, speed, face)
		return
	var req: GridPathfinder.PathRequest = p.guide.pending
	if req != null and req.done:
		p.guide.pending = null
		if req.ok:
			p.guide.set_path(req.points)
	var stale: bool = p.guide.has_destination and p.guide.destination.distance_to(target) > FOLLOW_REPATH_M
	if p.guide.pending == null and (not p.guide.is_moving() or stale) and (tick + p.id) % FOLLOW_REPATH_INTERVAL == 0:
		p.guide.pending = battle.pathfinder.request(p.guide.pos, target, mob, p.side)
	p.guide.advance(dt, speed)


## 가이드 도착 후 하위 단위가 정렬됐는지 본다. formation_error 를 이번 틱 값으로 다시 계산한 뒤에 부른다.
func _check_settled(u: Unit) -> void:
	if u.settling_since_tick < 0 or u.guide.is_moving() or battle.world.tick <= u.settling_since_tick:
		return
	if u.formation_error <= SETTLED_ERROR_M or battle.world.tick - u.settling_since_tick >= SETTLE_TIMEOUT_TICKS:
		u.settling_since_tick = -1
		battle.executor.on_arrival(u)


func _reachable_straight(a: Vector2, b: Vector2, mob: StringName) -> bool:
	return battle.terrain.is_passable(b, mob) and battle.terrain.line_passable(a, b, mob)


## 가이드가 도로 위에 있고 앞으로도 도로를 따라 간다.
func _on_road_ahead(g: Guide) -> bool:
	if not g.is_moving() or battle.terrain.type_at(g.pos) != Terrain.Type.ROAD:
		return false
	var ahead: PackedVector2Array = g.points_ahead(60.0, 20.0)
	for p: Vector2 in ahead:
		if battle.terrain.type_at(p) != Terrain.Type.ROAD:
			return false
	return true


## 앞길에 다리(물가 도로)가 있으면 종대로 좁힌다.
func _choke_ahead(g: Guide) -> bool:
	for p: Vector2 in g.points_ahead(CHOKE_LOOKAHEAD_M, 20.0):
		if battle.terrain.is_bridge(p):
			return true
	return false


## 도로 행군 속도: 야지 속도 × 도로 보너스(대형 road_speed, 기본 1.2), 단 가장 느린 구성원 도로 속도의 90% 이하.
func _road_march_speed(u: Unit, base: float) -> float:
	var bonus: float = u.formation.get_mod(&"road_speed", 1.2) if u.formation != null else 1.2
	return minf(base * bonus, base * _road_speed_ratio(u) * 0.9)


## 도로 속도/야지 속도 비(가장 느린 구성원 기준).
func _road_speed_ratio(u: Unit) -> float:
	var ratio: float = INF
	for e: Element in u.alive_elements():
		if e.is_mounted():
			continue
		ratio = minf(ratio, e.def.speed_road_kmh / maxf(e.def.speed_offroad_kmh, 1.0))
	return 1.0 if ratio == INF else ratio
