class_name ScoringSystem
extends SimSystem
## 거점·임무 목표·승점·승패 판정. PLAN 11.1.
## - 거점: 반경 안에 한쪽 엘리먼트만 있으면 그쪽 점령(양쪽 다 있으면 소유 유지 = 경합). 점령 거점마다 분당 vp_per_min.
## - 임무 목표: seize = 제한 시각 전에 점령하면 1회 승점. hold = 누적 점령 시간이 minutes 에 이르면 1회 승점.
## - 전투력 붕괴: 전장에 투입된 TF 가치(엘리먼트 cost, 손상 60%)가 투입 기준값의 collapse_threshold 이하 → 즉시 패배.
## - 시간 종료: time_limit_min. 승점 많은 쪽 승리, 같으면 무승부.

const INTERVAL: int = 10

class PointState:
	extends RefCounted
	var def: ScenarioDef.ControlPoint
	var owner: StringName = &""
	var contested: bool = false
	## side → 누적 점령 초.
	var held_sec: Dictionary[StringName, float] = {&"blue": 0.0, &"red": 0.0}


var battle: Battle
var points: Array[PointState] = []
var objectives_done: Dictionary[StringName, bool] = {}
var vp: Dictionary[StringName, float] = {&"blue": 0.0, &"red": 0.0}
## 전장에 투입된 엘리먼트의 정상 가치 합.
var committed_value: Dictionary[StringName, float] = {}
var _committed_elements: Dictionary[int, bool] = {}


func _init(p_battle: Battle) -> void:
	battle = p_battle
	for cp: ScenarioDef.ControlPoint in battle.scenario.control_points:
		var ps := PointState.new()
		ps.def = cp
		points.append(ps)


func get_system_name() -> StringName:
	return &"scoring"


func state_hash() -> int:
	var h: int = hash([roundi(vp[&"blue"] * 10.0), roundi(vp[&"red"] * 10.0)])
	for ps: PointState in points:
		h = hash([h, ps.owner, ps.contested])
	return h


func get_point(point_id: StringName) -> PointState:
	for ps: PointState in points:
		if ps.def.id == point_id:
			return ps
	return null


## 전장에 있는 엘리먼트의 가치 합(cost × 상태). 증원 대기(지도 밖)는 제외한다.
func force_value(side: StringName) -> float:
	var tf: TaskForce = battle.task_forces.get(side)
	if tf == null:
		return 0.0
	var total: float = 0.0
	for e: Element in tf.all_elements():
		if e.on_map:
			total += e.def.cost * e.strength()
	return total


func value_ratio(side: StringName) -> float:
	var base: float = committed_value.get(side, 0.0)
	if base <= 0.0:
		return 1.0
	return force_value(side) / base


func step(world: SimWorld) -> void:
	if battle.result.size() > 0:
		return
	_update_committed_value()
	if world.tick % INTERVAL != 0:
		return
	var sec: float = INTERVAL * SimConst.TICK_DT
	_update_points(world.tick, sec)
	_update_objectives(world.tick)
	_check_end(world)


func _update_committed_value() -> void:
	for side: StringName in Battle.SIDES:
		var tf: TaskForce = battle.task_forces.get(side)
		if tf == null:
			continue
		for e: Element in tf.all_elements():
			if not e.on_map or _committed_elements.has(e.id):
				continue
			_committed_elements[e.id] = true
			committed_value[side] = committed_value.get(side, 0.0) + e.def.cost


func _update_points(tick: int, sec: float) -> void:
	for ps: PointState in points:
		var present: Dictionary[StringName, int] = {&"blue": 0, &"red": 0}
		for e: Element in battle.elements_near(ps.def.pos, ps.def.radius_m):
			if e.is_mounted() or not e.is_active():
				continue
			present[e.side] = present.get(e.side, 0) + 1
		var blue: bool = present[&"blue"] > 0
		var red: bool = present[&"red"] > 0
		ps.contested = blue and red
		var new_owner: StringName = ps.owner
		if blue and not red:
			new_owner = &"blue"
		elif red and not blue:
			new_owner = &"red"
		if new_owner != ps.owner:
			var old: StringName = ps.owner
			ps.owner = new_owner
			battle.world.events.emit_event(&"point_captured", tick, {"point": ps.def.id, "name": ps.def.display_name,
				"side": new_owner, "from": old})
		if ps.owner != &"":
			vp[ps.owner] += ps.def.vp_per_min / 60.0 * sec
			ps.held_sec[ps.owner] = ps.held_sec.get(ps.owner, 0.0) + sec
	for side: StringName in Battle.SIDES:
		var tf: TaskForce = battle.task_forces.get(side)
		if tf != null:
			tf.vp = vp[side]


func _update_objectives(tick: int) -> void:
	var minutes_now: float = tick / float(SimConst.TICK_RATE) / 60.0
	for obj: ScenarioDef.Objective in battle.scenario.objectives:
		if objectives_done.has(obj.id):
			continue
		var ps: PointState = get_point(obj.point)
		if ps == null:
			continue
		var done: bool = false
		if obj.objective_type == &"seize":
			if minutes_now > obj.minutes:
				objectives_done[obj.id] = false
				battle.world.events.emit_event(&"objective_failed", tick, {"objective": obj.id, "side": obj.side,
					"name": ps.def.display_name})
				continue
			done = ps.owner == obj.side and not ps.contested
		else:
			done = ps.held_sec.get(obj.side, 0.0) >= obj.minutes * 60.0
		if done:
			objectives_done[obj.id] = true
			vp[obj.side] += obj.vp
			battle.world.events.emit_event(&"objective_done", tick, {"objective": obj.id, "side": obj.side, "vp": obj.vp,
				"name": ps.def.display_name, "type": obj.objective_type})


func _check_end(world: SimWorld) -> void:
	var tick: int = world.tick
	for side: StringName in Battle.SIDES:
		if value_ratio(side) <= battle.scenario.collapse_threshold:
			_end(Battle.enemy_of(side), "collapse", tick, side)
			return
	if world.get_sim_time_sec() >= battle.scenario.time_limit_min * 60.0:
		var b: float = vp[&"blue"]
		var r: float = vp[&"red"]
		var winner: StringName = &"blue" if b > r else (&"red" if r > b else &"")
		_end(winner, "time", tick, &"")


func _end(winner: StringName, reason: String, tick: int, collapsed: StringName) -> void:
	battle.result = {
		"winner": String(winner), "reason": reason, "tick": tick, "collapsed": String(collapsed),
		"vp": {"blue": vp[&"blue"], "red": vp[&"red"]},
		"value_ratio": {"blue": value_ratio(&"blue"), "red": value_ratio(&"red")},
	}
	battle.world.set_paused(true)
	battle.world.events.emit_event(&"battle_ended", tick, battle.result.duplicate(true))
