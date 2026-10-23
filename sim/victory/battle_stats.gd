class_name BattleStats
extends RefCounted
## 사후 검토(AAR)용 전투 기록. 시뮬 이벤트를 구독해 쌓는다. PLAN 4장 "사후 검토", 18.4 헤드리스 AAR JSON.

## 타임라인에 남길 이벤트.
const TIMELINE_TYPES: Array[StringName] = [
	&"battle_started", &"point_captured", &"objective_done", &"objective_failed", &"reinforcement_arrived",
	&"routed", &"commander_lost", &"cas_shot_down", &"battle_ended", &"auto_withdraw",
]

var battle: Battle
## side → 병과 → 잃은 수
var losses: Dictionary[StringName, Dictionary] = {&"blue": {}, &"red": {}}
var damaged: Dictionary[StringName, int] = {&"blue": 0, &"red": 0}
## side → 명령 지연 표본(초). 플레이어·AI 명령만(상위 제대 번역 명령 제외).
var order_delays: Dictionary[StringName, Array] = {&"blue": [], &"red": []}
var fire_missions: Dictionary[StringName, int] = {&"blue": 0, &"red": 0}
var comms_cuts: Dictionary[StringName, int] = {&"blue": 0, &"red": 0}
var resupplies: Dictionary[StringName, int] = {&"blue": 0, &"red": 0}
var shots: Dictionary[StringName, int] = {&"blue": 0, &"red": 0}
var first_contact_tick: int = -1
var timeline: Array[Dictionary] = []


func _init(p_battle: Battle) -> void:
	battle = p_battle
	battle.world.events.add_listener(on_event)


func on_event(ev: Dictionary) -> void:
	var type: StringName = ev["type"]
	var tick: int = ev["tick"]
	var p: Dictionary = ev["payload"]
	var side: StringName = p.get("side", &"")
	match type:
		&"element_destroyed":
			var eid: int = p.get("element", -1)
			var e: Element = battle.get_element(eid)
			if e != null:
				var by_class: Dictionary = losses[e.side]
				var prev: int = by_class.get(e.def.element_class, 0)
				by_class[e.def.element_class] = prev + 1
		&"element_damaged":
			if damaged.has(side):
				damaged[side] += 1
		&"order_received":
			var issuer: String = p.get("issuer", "")
			if (issuer == "player" or issuer == "ai") and order_delays.has(side):
				var delay: float = p.get("delay", 0.0)
				order_delays[side].append(delay)
		&"fire_requested":
			if fire_missions.has(side):
				fire_missions[side] += 1
		&"comms_changed":
			var st: int = p.get("state", 0)
			if st == Unit.Comms.CUT and comms_cuts.has(side):
				comms_cuts[side] += 1
		&"resupplied":
			if resupplies.has(side):
				resupplies[side] += 1
		&"shot":
			var fid: int = p.get("from", -1)
			var shooter: Element = battle.get_element(fid)
			if shooter != null:
				shots[shooter.side] += 1
				if first_contact_tick < 0:
					first_contact_tick = tick
	if TIMELINE_TYPES.has(type):
		timeline.append({"tick": tick, "type": String(type), "side": String(side), "text": describe(type, p)})


func describe(type: StringName, p: Dictionary) -> String:
	var uid: int = p.get("unit", -1)
	var u: Unit = battle.get_unit(uid)
	var who: String = u.label() if u != null else ""
	var side_name: String = side_label(p.get("side", &""))
	match type:
		&"battle_started":
			return "전투 시작"
		&"point_captured":
			return "%s: %s 점령" % [side_name, str(p.get("name", ""))]
		&"objective_done":
			var gained: int = p.get("vp", 0)
			return "%s: 임무 목표 달성 — %s (+%d)" % [side_name, str(p.get("name", "")), gained]
		&"objective_failed":
			return "%s: 임무 목표 실패 — %s" % [side_name, str(p.get("name", ""))]
		&"reinforcement_arrived":
			return "%s: %s 증원 도착" % [side_name, who]
		&"routed":
			return "%s: %s 패주" % [side_name, who]
		&"commander_lost":
			return "%s: %s 지휘관 손실" % [side_name, who]
		&"cas_shot_down":
			return "%s: 근접항공지원 격추됨" % side_name
		&"auto_withdraw":
			return "%s: %s 전투력 저하로 철수" % [side_name, who]
		&"battle_ended":
			return "전투 종료"
	return String(type)


static func side_label(side: Variant) -> String:
	var s: String = str(side)
	return {"blue": "청군", "red": "적군"}.get(s, s)


static func _mean(a: Array) -> float:
	if a.is_empty():
		return 0.0
	var total: float = 0.0
	for v: Variant in a:
		total += DefReader.to_float(v)
	return total / a.size()


static func _max(a: Array) -> float:
	var m: float = 0.0
	for v: Variant in a:
		m = maxf(m, DefReader.to_float(v))
	return m


## AAR 요약(직렬화 가능).
func summary() -> Dictionary:
	var sides: Dictionary = {}
	for side: StringName in Battle.SIDES:
		var lost: int = 0
		for v: Variant in losses[side].values():
			lost += roundi(DefReader.to_float(v))
		var tf: TaskForce = battle.task_forces.get(side)
		sides[String(side)] = {
			"name": tf.display_name if tf != null else "",
			"losses": losses[side].duplicate(),
			"losses_total": lost,
			"damaged": damaged[side],
			"elements": tf.all_elements().size() if tf != null else 0,
			"order_delay_mean": _mean(order_delays[side]),
			"order_delay_max": _max(order_delays[side]),
			"orders": order_delays[side].size(),
			"fire_missions": fire_missions[side],
			"comms_cuts": comms_cuts[side],
			"resupplies": resupplies[side],
			"shots": shots[side],
			"vp": battle.scoring.vp[side] if battle.scoring != null else 0.0,
			"value_ratio": battle.scoring.value_ratio(side) if battle.scoring != null else 1.0,
		}
	var pts: Array = []
	if battle.scoring != null:
		for ps: ScoringSystem.PointState in battle.scoring.points:
			pts.append({"id": String(ps.def.id), "name": ps.def.display_name, "owner": String(ps.owner),
				"held_blue_sec": ps.held_sec[&"blue"], "held_red_sec": ps.held_sec[&"red"]})
	return {
		"result": battle.result.duplicate(true),
		"duration_sec": battle.world.get_sim_time_sec(),
		"first_contact_sec": first_contact_tick / float(SimConst.TICK_RATE) if first_contact_tick >= 0 else -1.0,
		"sides": sides,
		"points": pts,
		"timeline": timeline.duplicate(true),
	}
