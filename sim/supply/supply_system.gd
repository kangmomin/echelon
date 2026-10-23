class_name SupplySystem
extends SimSystem
## 보급과 수리. PLAN 10.2.
## - 소대에 보급(RESUPPLY) 명령 → 가장 가까운 보급반이 소대로 간다 → 소대는 정지 → 150 m 안에서 분당 20 단위 전달.
## - 엘리먼트의 필요량 = supply_full × 탄약 부족 비율. 받은 만큼 무기별 최대 탄수에 비례해 채운다.
## - 보급반이 비면 증원 진입 지점(보급소)으로 돌아가 분당 100 단위로 다시 싣는다.
## - 수리: 손상 엘리먼트가 보급 트럭 150 m(보급 접촉 거리) 안에서 둘 다 정지해 5분 있으면 정상.

const INTERVAL: int = 10
const TRANSFER_PER_MIN: float = 20.0
const REFILL_PER_MIN: float = 100.0
const CONTACT_M: float = 150.0
const REFILL_RADIUS_M: float = 300.0
const REPAIR_RADIUS_M: float = 150.0
const REPAIR_SEC: float = 300.0
const REMOVE_SEC: int = 10

var battle: Battle
## 보급을 기다리는 소대 id(요청 순서).
var requests: Array[int] = []
## 보급반 id → 맡은 소대 id.
var tasks: Dictionary[int, int] = {}
## 보급소로 돌아가는 보급반 id.
var returning: Dictionary[int, bool] = {}
var delivered_total: float = 0.0


func _init(p_battle: Battle) -> void:
	battle = p_battle


func get_system_name() -> StringName:
	return &"supply"


func state_hash() -> int:
	var h: int = hash([requests, tasks.keys(), tasks.values(), roundi(delivered_total)])
	for e: Element in battle.elements:
		if e.def.supply_capacity > 0:
			h = hash([h, roundi(e.supply_left)])
	return h


## 소대 보급 요청(명령 실행기가 부른다).
func request(p: Platoon) -> void:
	if p.is_destroyed() or p.kind == &"supply":
		return
	p.posture = &"resupplying"
	p.guide.clear_path()
	if not requests.has(p.id) and not tasks.values().has(p.id):
		requests.append(p.id)
	battle.world.events.emit_event(&"resupply_requested", battle.world.tick, {"unit": p.id, "side": p.side})


static func element_need(e: Element) -> float:
	if e.def.supply_full <= 0 or not e.is_alive():
		return 0.0
	var missing: float = 0.0
	var n: int = 0
	for wid: StringName in e.def.ammo.keys():
		var full: int = e.def.ammo[wid]
		if full <= 0:
			continue
		var cur: int = e.ammo.get(wid, 0)
		missing += clampf(1.0 - cur / float(full), 0.0, 1.0)
		n += 1
	return e.def.supply_full * (missing / n) if n > 0 else 0.0


## 제대 탄약 비율(1 = 가득). 엘리먼트 보급 필요량 기준.
static func ammo_ratio(u: Unit) -> float:
	var full: float = 0.0
	var need: float = 0.0
	for e: Element in u.alive_elements():
		full += e.def.supply_full
		need += element_need(e)
	return 1.0 - need / full if full > 0.0 else 1.0


static func platoon_need(p: Platoon) -> float:
	var total: float = 0.0
	for e: Element in p.elements:
		total += element_need(e)
	return total


## 보급 단위를 받아 탄을 채운다. 실제로 쓴 단위를 돌려준다.
static func give(e: Element, units: float) -> float:
	var need: float = element_need(e)
	if need <= 0.0 or units <= 0.0:
		return 0.0
	var used: float = minf(need, units)
	var frac: float = used / e.def.supply_full
	for wid: StringName in e.def.ammo.keys():
		var full: int = e.def.ammo[wid]
		var cur: int = e.ammo.get(wid, 0)
		if cur >= full:
			continue
		var add: int = maxi(1, ceili(full * frac))
		e.ammo[wid] = mini(full, cur + add)
	return used


static func section_supply(p: Platoon) -> float:
	var total: float = 0.0
	for e: Element in p.alive_elements():
		total += e.supply_left
	return total


static func section_capacity(p: Platoon) -> float:
	var total: float = 0.0
	for e: Element in p.alive_elements():
		total += e.def.supply_capacity
	return total


func step(world: SimWorld) -> void:
	if world.tick % INTERVAL != 0:
		return
	var sec: float = INTERVAL * SimConst.TICK_DT
	_assign(world.tick)
	for sid: int in tasks.keys():
		_work(sid, tasks[sid], world.tick, sec)
	for sid: int in returning.keys():
		_refill(sid, world.tick, sec)
	_repair(world.tick, sec)


func _sections(side: StringName) -> Array[Platoon]:
	var out: Array[Platoon] = []
	var tf: TaskForce = battle.task_forces.get(side)
	if tf == null:
		return out
	for p: Platoon in tf.all_platoons():
		if p.kind == &"supply" and p.on_map and not p.is_destroyed():
			out.append(p)
	return out


func _assign(tick: int) -> void:
	var still: Array[int] = []
	for pid: int in requests:
		var p: Platoon = battle.get_unit(pid) as Platoon
		if p == null or p.is_destroyed() or p.posture != &"resupplying":
			continue
		var best: Platoon = null
		var best_d: float = INF
		for s: Platoon in _sections(p.side):
			if tasks.has(s.id) or returning.has(s.id) or section_supply(s) < 1.0:
				continue
			var d: float = s.centroid().distance_to(p.centroid())
			if d < best_d:
				best = s
				best_d = d
		if best == null:
			still.append(pid)
			continue
		tasks[best.id] = pid
		_send(best, p)
		battle.world.events.emit_event(&"supply_dispatched", tick, {"unit": best.id, "target": pid, "side": p.side})
	requests = still


func _send(section: Platoon, p: Platoon) -> void:
	battle.self_order(section, Order.Type.MOVE, p.centroid() - FormationSolver.forward_vector(p.guide.heading) * 60.0)


func _work(sid: int, pid: int, tick: int, sec: float) -> void:
	var s: Platoon = battle.get_unit(sid) as Platoon
	var p: Platoon = battle.get_unit(pid) as Platoon
	if s == null or s.is_destroyed():
		tasks.erase(sid)
		if p != null and not p.is_destroyed() and p.posture == &"resupplying":
			requests.append(pid)
		return
	if p == null or p.is_destroyed() or p.posture != &"resupplying":
		tasks.erase(sid)
		return
	var d: float = s.centroid().distance_to(p.centroid())
	if d > CONTACT_M:
		if not s.guide.is_moving() and not s.guide.is_waiting_for_path() and tick % (REMOVE_SEC * SimConst.TICK_RATE) == 0:
			_send(s, p)
		return
	var budget: float = minf(TRANSFER_PER_MIN / 60.0 * sec, section_supply(s))
	var used_total: float = 0.0
	for e: Element in p.elements:
		if budget - used_total <= 0.0:
			break
		used_total += give(e, budget - used_total)
	_take(s, used_total)
	delivered_total += used_total
	if platoon_need(p) <= 0.01:
		p.posture = &"idle"
		tasks.erase(sid)
		battle.world.events.emit_event(&"resupplied", tick, {"unit": pid, "side": p.side, "by": sid})
		if section_supply(s) < section_capacity(s) * 0.25:
			_go_refill(s)
	elif section_supply(s) < 1.0:
		tasks.erase(sid)
		requests.append(pid)
		_go_refill(s)


func _take(s: Platoon, amount: float) -> void:
	var left: float = amount
	for e: Element in s.alive_elements():
		var t: float = minf(e.supply_left, left)
		e.supply_left -= t
		left -= t
		if left <= 0.0:
			return


func _go_refill(s: Platoon) -> void:
	var tf: TaskForce = s.task_force()
	if tf == null:
		return
	returning[s.id] = true
	battle.self_order(s, Order.Type.MOVE, tf.entry_pos)
	battle.world.events.emit_event(&"supply_returning", battle.world.tick, {"unit": s.id, "side": s.side})


func _refill(sid: int, tick: int, sec: float) -> void:
	var s: Platoon = battle.get_unit(sid) as Platoon
	if s == null or s.is_destroyed():
		returning.erase(sid)
		return
	var tf: TaskForce = s.task_force()
	if tf == null or s.centroid().distance_to(tf.entry_pos) > REFILL_RADIUS_M:
		return
	var add: float = REFILL_PER_MIN / 60.0 * sec
	for e: Element in s.alive_elements():
		var room: float = e.def.supply_capacity - e.supply_left
		var t: float = minf(room, add)
		e.supply_left += t
		add -= t
	if section_supply(s) >= section_capacity(s) - 0.01:
		returning.erase(sid)
		battle.world.events.emit_event(&"supply_refilled", tick, {"unit": sid, "side": s.side})


func _repair(tick: int, sec: float) -> void:
	for e: Element in battle.elements:
		if e.state != Element.State.DAMAGED or not e.is_active() or e.velocity.length() > 0.5:
			continue
		var near: bool = false
		for o: Element in battle.elements_near(e.pos, REPAIR_RADIUS_M):
			if o.side == e.side and o.def.supply_capacity > 0 and o.is_active() and o.velocity.length() < 0.5:
				near = true
				break
		if not near:
			continue
		e.repair_sec += sec
		if e.repair_sec >= REPAIR_SEC:
			e.state = Element.State.OK
			e.damage_kind = &""
			e.repair_sec = 0.0
			battle.world.events.emit_event(&"repaired", tick, {"element": e.id, "side": e.side,
				"unit": e.platoon.id if e.platoon != null else -1})
