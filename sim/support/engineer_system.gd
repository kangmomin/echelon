class_name EngineerSystem
extends SimSystem
## 공병: 지뢰 매설·제거, 진지 구축, 지뢰 피해.
##
## - 공병 소대(kind = engineer)가 LAY_MINES / CLEAR_MINES / FORTIFY 명령 지점에 도착하면 작업을 시작한다.
##   작업은 살아 있는 공병 분대가 있어야 진행되고, 접촉 중(최근 피격)에는 멈춘다.
## - 지뢰 지대: 반경 70 m, 지뢰 8발. 그 안을 움직이는 적 지상 엘리먼트는 0.5초마다 일정 확률로 밟는다
##   (차량 격파 50% / 기동 손상 50%, 보병 2명 사상). 밟은 진영은 그 지뢰 지대를 알게 되고 경로탐색이 피한다.
## - 적 공병은 150 m 안의 지뢰 지대를 알아챈다. 제거는 발당 6초(돌파 장비가 살아 있으면 3초).
## - 진지: 반경 90 m. 같은 편이 그 안에서 3초 넘게 멈춰 있으면 피격 확률·포격 피해·보병 사상이 줄어든다.
## 지뢰·진지가 하나도 없으면 난수를 쓰지 않고 상태 해시도 0 이다(기존 전투의 결정론 해시를 바꾸지 않는다).

const STEP_INTERVAL: int = 5
const MINEFIELD_RADIUS_M: float = 70.0
const MINEFIELD_CHARGES: int = 8
const MINEFIELDS_PER_PLATOON: int = 2
const LAY_SEC: float = 45.0
const CLEAR_SEC_PER_CHARGE: float = 6.0
const CLEAR_REACH_M: float = 150.0
const DETECT_RANGE_M: float = 150.0
const FORTIFY_SEC: float = 60.0
const POSITION_RADIUS_M: float = 90.0
const POSITIONS_PER_PLATOON: int = 3
const POSITION_SETTLE_TICKS: int = 30
## 진지 안: 피격 확률·포격 취약도·보병 사상에 곱한다.
const POSITION_FACTOR: float = 0.6
## 0.5초마다 지뢰를 밟을 확률(움직이는 차량 / 보병).
const STRIKE_P_VEHICLE: float = 0.12
const STRIKE_P_FOOT: float = 0.05
const MINE_INF_CASUALTIES: float = 2.0
const THREAT_AMOUNT: float = 6.0
const WORK_BLOCKED_BY_HIT_TICKS: int = 100


class Minefield:
	extends RefCounted
	var id: int
	var pos: Vector2
	var radius: float = MINEFIELD_RADIUS_M
	var side: StringName
	var charges: int = MINEFIELD_CHARGES
	## 이 지뢰 지대를 아는 진영(매설한 쪽은 처음부터 안다).
	var known: Dictionary[StringName, bool] = {}


class FieldPosition:
	extends RefCounted
	var pos: Vector2
	var radius: float = POSITION_RADIUS_M
	var side: StringName


var battle: Battle
var minefields: Array[Minefield] = []
var positions: Array[FieldPosition] = []
var mine_strikes: int = 0
var _next_id: int = 1


func _init(p_battle: Battle) -> void:
	battle = p_battle


func get_system_name() -> StringName:
	return &"engineers"


func omit_from_hash() -> bool:
	return minefields.is_empty() and positions.is_empty() and mine_strikes == 0


func state_hash() -> int:
	var parts: Array = [mine_strikes]
	for m: Minefield in minefields:
		parts.append([m.id, m.charges, m.pos.x, m.pos.y])
	for fp: FieldPosition in positions:
		parts.append([fp.pos.x, fp.pos.y])
	return hash(parts)


static func is_engineer(u: Unit) -> bool:
	return u is Platoon and u.kind == &"engineer"


func step(world: SimWorld) -> void:
	if world.tick % STEP_INTERVAL != 0:
		return
	var dt: float = STEP_INTERVAL * SimConst.TICK_DT
	for side: StringName in Battle.SIDES:
		var tf: TaskForce = battle.task_forces.get(side)
		if tf == null:
			continue
		for p: Platoon in tf.all_platoons():
			if p.kind == &"engineer" and p.on_map and not p.is_destroyed():
				_work(p, world.tick, dt)
	if not minefields.is_empty():
		_strikes(world.tick)
		_engineer_detection()


# --- 작업 ----------------------------------------------------------------

func _work(p: Platoon, tick: int, dt: float) -> void:
	if p.eng_task == &"":
		return
	if p.mission == null or p.mission.is_finished() or not Order.ENGINEER_TYPES.has(p.mission.type):
		p.eng_task = &""
		p.eng_progress = 0.0
		return
	if _sappers(p) == 0 or tick - p.last_hit_tick < WORK_BLOCKED_BY_HIT_TICKS:
		return
	p.eng_progress += dt
	match p.eng_task:
		&"lay":
			if p.eng_progress >= LAY_SEC:
				lay_minefield(p.side, p.guide.pos)
				p.mines_left -= 1
				_finish(p, &"minefield_laid", {"pos": p.guide.pos})
		&"fortify":
			if p.eng_progress >= FORTIFY_SEC:
				var fp := FieldPosition.new()
				fp.pos = p.guide.pos
				fp.side = p.side
				positions.append(fp)
				p.positions_built += 1
				_finish(p, &"position_built", {"pos": p.guide.pos})
		&"clear":
			var m: Minefield = _nearest_minefield(p.guide.pos, CLEAR_REACH_M)
			if m == null:
				_finish(p, &"minefield_cleared", {"pos": p.guide.pos, "none": true})
				return
			m.known[p.side] = true
			var per_charge: float = CLEAR_SEC_PER_CHARGE * (0.5 if _has_breacher(p) else 1.0)
			while p.eng_progress >= per_charge and m.charges > 0:
				p.eng_progress -= per_charge
				m.charges -= 1
			if m.charges <= 0:
				minefields.erase(m)
				_finish(p, &"minefield_cleared", {"pos": m.pos})


func _finish(p: Platoon, event: StringName, payload: Dictionary) -> void:
	p.eng_task = &""
	p.eng_progress = 0.0
	if p.mission != null:
		p.mission.state = Order.State.DONE
	p.mission = null
	p.posture = &"idle"
	payload["unit"] = p.id
	payload["side"] = p.side
	battle.world.events.emit_event(event, battle.world.tick, payload)


## 도착한 공병 소대에 작업을 맡긴다(OrderExecutor.on_arrival 이 부른다). 할 수 없으면 false.
func begin_task(p: Platoon, order: Order) -> bool:
	match order.type:
		Order.Type.LAY_MINES:
			if p.mines_left <= 0:
				return false
			p.eng_task = &"lay"
			p.posture = &"laying"
		Order.Type.CLEAR_MINES:
			p.eng_task = &"clear"
			p.posture = &"clearing"
		Order.Type.FORTIFY:
			if p.positions_built >= POSITIONS_PER_PLATOON:
				return false
			p.eng_task = &"fortify"
			p.posture = &"fortifying"
		_:
			return false
	p.eng_progress = 0.0
	return true


func lay_minefield(side: StringName, pos: Vector2) -> Minefield:
	var m := Minefield.new()
	m.id = _next_id
	_next_id += 1
	m.pos = pos
	m.side = side
	m.known[side] = true
	minefields.append(m)
	return m


func _sappers(p: Platoon) -> int:
	var n: int = 0
	for e: Element in p.elements:
		if e.is_infantry() and e.is_alive():
			n += 1
	return n


func _has_breacher(p: Platoon) -> bool:
	for e: Element in p.elements:
		if e.def.element_class == &"eng" and e.is_alive():
			return true
	return false


func _nearest_minefield(pos: Vector2, reach: float) -> Minefield:
	var best: Minefield = null
	var best_d: float = INF
	for m: Minefield in minefields:
		var d: float = m.pos.distance_to(pos) - m.radius
		if d <= reach and d < best_d:
			best_d = d
			best = m
	return best


# --- 지뢰 피해 ------------------------------------------------------------

func _strikes(tick: int) -> void:
	var rng: SimRng = battle.world.rng
	for m: Minefield in minefields.duplicate():
		if m.charges <= 0:
			minefields.erase(m)
			continue
		for e: Element in battle.elements_near(m.pos, m.radius):
			if e.side == m.side or e.is_mounted() or e.is_air() or not e.is_alive():
				continue
			if e.velocity.length() < 0.5:
				continue
			var p: float = STRIKE_P_FOOT if e.is_infantry() else STRIKE_P_VEHICLE
			# 지뢰 지대를 아는 쪽은 조심해서 지나간다(공병이 연 통로는 제거로 처리).
			if m.known.get(e.side, false):
				p *= 0.5
			if not rng.chance(p):
				continue
			m.charges -= 1
			mine_strikes += 1
			_reveal(m, e.side)
			battle.world.events.emit_event(&"mine_strike", tick, {"pos": e.pos, "element": e.id, "side": e.side,
				"unit": e.platoon.id if e.platoon != null else -1})
			if e.is_infantry():
				var men: int = roundi(MINE_INF_CASUALTIES)
				e.hp = maxf(0.0, e.hp - men)
				if e.platoon != null:
					battle.morale.on_casualties(e.platoon, men)
				if e.hp <= 0.0:
					battle.combat.destroy(e, null, tick)
			elif rng.chance(0.5):
				battle.combat.destroy(e, null, tick)
			else:
				e.state = Element.State.DAMAGED
				e.damage_kind = &"mobility"
				battle.morale.on_element_damaged(e)
				battle.world.events.emit_event(&"element_damaged", tick, {"element": e.id, "side": e.side,
					"kind": e.damage_kind, "unit": e.platoon.id if e.platoon != null else -1})
			if m.charges <= 0:
				break


func _reveal(m: Minefield, side: StringName) -> void:
	if m.known.get(side, false):
		return
	m.known[side] = true
	battle.pathfinder.add_threat(side, m.pos, m.radius + 20.0, THREAT_AMOUNT)
	battle.world.events.emit_event(&"minefield_found", battle.world.tick, {"pos": m.pos, "side": side})


func _engineer_detection() -> void:
	for side: StringName in Battle.SIDES:
		var tf: TaskForce = battle.task_forces.get(side)
		if tf == null:
			continue
		for p: Platoon in tf.all_platoons():
			if p.kind != &"engineer" or not p.on_map or p.is_destroyed():
				continue
			for m: Minefield in minefields:
				if m.side != side and not m.known.get(side, false) and m.pos.distance_to(p.guide.pos) < m.radius + DETECT_RANGE_M:
					_reveal(m, side)


# --- 진지 ----------------------------------------------------------------

## 진지 안에서 멈춰 있는 엘리먼트면 POSITION_FACTOR, 아니면 1.
func position_factor(e: Element) -> float:
	if positions.is_empty() or e.is_air() or e.stationary_ticks < POSITION_SETTLE_TICKS:
		return 1.0
	for fp: FieldPosition in positions:
		if fp.side == e.side and fp.pos.distance_squared_to(e.pos) <= fp.radius * fp.radius:
			return POSITION_FACTOR
	return 1.0


## side 가 아는 지뢰 지대(표시용).
func known_minefields(side: StringName) -> Array[Minefield]:
	var out: Array[Minefield] = []
	for m: Minefield in minefields:
		if m.known.get(side, false):
			out.append(m)
	return out
