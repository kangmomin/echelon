class_name DetectionSystem
extends SimSystem
## 탐지와 보고. PLAN 8.4, 9.1, 17.4.
##
## - 엘리먼트마다 2초에 한 번(틱 분산) 광학 거리 안 적을 가까운 순으로 최대 20개 확인: LOS → 탐지 확률 굴림.
## - 탐지하면 그 소대가 즉시 안다(소대 contacts). 사격 표적은 소대가 아는 적에서 고른다.
## - 보고: 소대 → 중대 → 대대(hop 당 5초 × 통신 배율). 통신이 끊기면 회복될 때까지 보류.
##   대대에 닿은 보고만 TF.known_enemies 에 들어가고, 플레이어 화면은 그것만 보여준다.

const SCAN_INTERVAL: int = 20
const MAX_CANDIDATES: int = 20
const BASE_PROBABILITY: float = 0.6
const MAX_PROBABILITY: float = 0.95
const REPORT_HOP_SEC: float = 5.0
const REPORT_RETRY_INTERVAL: int = 10
## 소대가 접촉을 기억하는 시간(틱). 이 안에 본 적만 사격 표적이 된다.
const CONTACT_MEMORY_TICKS: int = 100
## 대대가 보고를 버리는 시간(틱).
const KNOWN_EXPIRE_TICKS: int = 1800
const TRACKING_BONUS: float = 2.0
const TERRAIN_CONCEAL: Dictionary[int, float] = {Terrain.Type.FOREST: 0.5, Terrain.Type.URBAN: 0.6}

var battle: Battle
## side → {표적 element_id: 마지막으로 본 틱} (진영 단위 추적 보너스용)
var seen: Dictionary[StringName, Dictionary] = {&"blue": {}, &"red": {}}
## 표적 element_id → 대기 중인 보고 {"side", "pos", "cls", "tick", "platoon", "deliver"}
var _pending: Dictionary[StringName, Dictionary] = {&"blue": {}, &"red": {}}
var detections_total: int = 0
var reports_delivered: int = 0


func _init(p_battle: Battle) -> void:
	battle = p_battle


func get_system_name() -> StringName:
	return &"detection"


func state_hash() -> int:
	return hash([detections_total, reports_delivered])


func step(world: SimWorld) -> void:
	var tick: int = world.tick
	for e: Element in battle.elements:
		if (e.id + tick) % SCAN_INTERVAL != 0:
			continue
		if not e.is_active() or e.is_mounted():
			continue
		_scan(e, tick)
	_process_reports(tick)
	if tick % 50 == 0:
		_expire(tick)


# --- 탐지 ----------------------------------------------------------------

func _scan(obs: Element, tick: int) -> void:
	var optics: float = obs.def.optics_m
	var r2: float = optics * optics
	var cands: Array[Element] = []
	for item: Variant in battle.spatial.query(obs.pos, optics):
		var t: Element = item
		if t.side == obs.side or not t.is_active() or t.is_mounted():
			continue
		if t.pos.distance_squared_to(obs.pos) <= r2:
			cands.append(t)
	if cands.is_empty():
		return
	cands.sort_custom(func(a: Element, b: Element) -> bool:
		var da: float = a.pos.distance_squared_to(obs.pos)
		var db: float = b.pos.distance_squared_to(obs.pos)
		return da < db or (da == db and a.id < b.id))
	var n: int = mini(cands.size(), MAX_CANDIDATES)
	for i: int in n:
		var t: Element = cands[i]
		if not Los.between(battle.terrain, battle.smokes, tick, obs, t):
			continue
		var p: float = probability(obs, t, obs.pos.distance_to(t.pos), tick)
		if battle.world.rng.chance(p):
			_on_detected(obs, t, tick)


## 한 번의 스캔(2초)에서 obs 가 t 를 탐지할 확률.
func probability(obs: Element, t: Element, d: float, tick: int) -> float:
	var range_f: float = clampf(1.0 - pow(d / maxf(obs.def.optics_m, 1.0), 1.5), 0.0, 1.0)
	var conceal: float = 1.0 - t.def.stealth
	var terrain_f: float = TERRAIN_CONCEAL.get(battle.terrain.type_at(t.pos), 1.0)
	var state_f: float = 1.0
	if tick - t.last_fired_tick < 50:
		state_f = 2.0
	elif t.velocity.length() > 1.0:
		state_f = 1.5
	elif t.stationary_ticks > 300:
		state_f = 0.5
	var side_seen: Dictionary = seen[obs.side]
	var tracking: float = TRACKING_BONUS if side_seen.get(t.id, -100000) > tick - CONTACT_MEMORY_TICKS else 1.0
	var flank_f: float = 1.0
	if obs.platoon != null and obs.platoon.formation != null:
		var rel: float = absf(angle_difference(obs.platoon.guide.heading, FormationSolver.heading_from_vector(t.pos - obs.pos)))
		if rel > PI / 4.0:
			flank_f = 0.5 + obs.platoon.formation.get_mod(&"flank_security", 0.5)
	var p: float = BASE_PROBABILITY * range_f * t.def.size * conceal * terrain_f * state_f * tracking * flank_f
	return clampf(p, 0.0, MAX_PROBABILITY)


func _on_detected(obs: Element, t: Element, tick: int) -> void:
	var p: Platoon = obs.platoon
	p.contacts[t.id] = tick
	p.last_contact_tick = tick
	if p.parent != null and p.parent.level == Unit.Level.COMPANY:
		p.parent.last_contact_tick = tick
	seen[obs.side][t.id] = tick
	detections_total += 1
	_queue_report(p, t, tick)


# --- 보고 ----------------------------------------------------------------

func _queue_report(p: Platoon, t: Element, tick: int) -> void:
	var pending: Dictionary = _pending[p.side]
	if pending.has(t.id):
		var rep: Dictionary = pending[t.id]
		rep["pos"] = t.pos
		rep["tick"] = tick
		var deliver: int = rep["deliver"]
		if deliver < 0:
			var sec: float = route_delay_sec(p)
			if sec >= 0.0:
				rep["platoon"] = p.id
				rep["deliver"] = tick + roundi(sec * SimConst.TICK_RATE)
		return
	var delay: float = route_delay_sec(p)
	pending[t.id] = {
		"pos": t.pos, "cls": t.def.element_class, "tick": tick, "platoon": p.id,
		"deliver": tick + roundi(delay * SimConst.TICK_RATE) if delay >= 0.0 else -1,
	}


## 소대 → 대대 보고 지연(초). 경로가 끊겼으면 -1.
func route_delay_sec(p: Platoon) -> float:
	var tf: TaskForce = p.task_force()
	if tf == null:
		return -1.0
	var comms: CommsNet = battle.comms
	if p.parent != null and p.parent.level == Unit.Level.COMPANY:
		var c: Unit = p.parent
		var l1: CommsNet.Link = comms.link_between(c, p)
		var l2: CommsNet.Link = comms.link_between(tf, c)
		if not l1.ok or not l2.ok:
			return -1.0
		return REPORT_HOP_SEC * (CommsNet.delay_factor(l1.quality) + CommsNet.delay_factor(l2.quality)) \
			+ (l1.relays + l2.relays) * CommsNet.RELAY_DELAY_SEC
	var l: CommsNet.Link = comms.link_between(tf, p)
	if not l.ok:
		return -1.0
	return REPORT_HOP_SEC * CommsNet.delay_factor(l.quality) + l.relays * CommsNet.RELAY_DELAY_SEC


func _process_reports(tick: int) -> void:
	for side: StringName in Battle.SIDES:
		var tf: TaskForce = battle.task_forces.get(side)
		var pending: Dictionary = _pending[side]
		if pending.is_empty() or tf == null:
			continue
		var done: Array[int] = []
		for key: Variant in pending.keys():
			var tid: int = key
			var rep: Dictionary = pending[key]
			var deliver: int = rep["deliver"]
			if deliver < 0:
				if tick % REPORT_RETRY_INTERVAL == 0:
					var pid: int = rep["platoon"]
					var p: Platoon = battle.get_unit(pid) as Platoon
					var sec: float = route_delay_sec(p) if p != null and not p.is_destroyed() else -1.0
					if sec >= 0.0:
						rep["deliver"] = tick + roundi(sec * SimConst.TICK_RATE)
				continue
			if tick < deliver:
				continue
			done.append(tid)
			var rep_tick: int = rep["tick"]
			var old: Dictionary = tf.known_enemies.get(tid, {})
			var old_tick: int = old.get("tick", -1)
			if rep_tick >= old_tick:
				tf.known_enemies[tid] = {"pos": rep["pos"], "tick": rep_tick, "class": rep["cls"]}
			reports_delivered += 1
		for tid: int in done:
			pending.erase(tid)


func _expire(tick: int) -> void:
	for side: StringName in Battle.SIDES:
		var tf: TaskForce = battle.task_forces.get(side)
		if tf == null:
			continue
		for key: int in tf.known_enemies.keys():
			var info: Dictionary = tf.known_enemies[key]
			var info_tick: int = info["tick"]
			var target: Element = battle.get_element(key)
			if tick - info_tick > KNOWN_EXPIRE_TICKS or (target != null and not target.is_alive() and tick - info_tick > 100):
				tf.known_enemies.erase(key)
		for p: Platoon in tf.all_platoons():
			for key: int in p.contacts.keys():
				var last: int = p.contacts[key]
				if tick - last > CONTACT_MEMORY_TICKS * 3:
					p.contacts.erase(key)


## side 의 누군가가 최근(CONTACT_MEMORY_TICKS 이내) 직접 본 적인지. 플레이어 화면의 "지금 보이는 적".
func seen_recently(side: StringName, element_id: int, tick: int) -> bool:
	var s: Dictionary = seen.get(side, {})
	if not s.has(element_id):
		return false
	var last: int = s[element_id]
	return tick - last <= CONTACT_MEMORY_TICKS


## 대대 보고 신뢰도: 0 = 신규(5초 이내), 1 = 추정(30초 이내), 2 = 오래됨. PLAN 8.4.
static func confidence(info: Dictionary, tick: int) -> int:
	var t: int = info["tick"]
	var age: int = tick - t
	if age <= 50:
		return 0
	if age <= 300:
		return 1
	return 2
