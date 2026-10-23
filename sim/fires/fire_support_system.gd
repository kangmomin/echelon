class_name FireSupportSystem
extends SimSystem
## 간접화력: 대대 박격포(지도 위), 지도 밖 포병, 근접항공지원(CAS), 연막. 요청은 지휘망을 탄다. PLAN 10.1.
##
## 절차: 요청(지휘 용량 1) → FDC 처리(지연) → 초탄(수정탄) → 수정 → 효력사.
## - 관측: 표적을 보고 있는 아군 엘리먼트 중 관측반(fo)이 최우선. 관측반 = 품질 0, 다른 엘리먼트 = 1, 없음(지도 좌표) = 2.
##   품질에 따라 산포 ×1 / ×1.5 / ×2, 지연 +0 / +10 / +30초. 관측자가 없으면 수정 없이 바로 효력사.
## - 지연 = FDC + 통신 구간마다 HOP × 통신 품질 계수 + 난수. 통신이 끊겼으면 요청 불가.
## - 박격포는 박격포 소대 엘리먼트가 실제로 쏜다(탄약 소모, 이동 중이면 대기). 포병·CAS 는 편성 때 산 횟수만큼.
## - CAS 는 항공 통제관(JTAC) 또는 관측반이 표적을 봐야 요청 가능. 표적 근처 적 대공 무기가 격추를 시도한다.

enum Asset { MORTAR, ARTILLERY, CAS }
enum Phase { PROCESSING, ADJUSTING, EFFECT, DONE, CANCELLED }

const ASSET_NAMES: Array[String] = ["박격포", "포병", "근접항공지원"]
const PHASE_NAMES: Array[String] = ["처리 중", "수정 사격", "효력사", "완료", "취소"]

const FDC_SEC: Array[float] = [30.0, 100.0, 75.0]
const HOP_SEC: Array[float] = [10.0, 15.0, 15.0]
const RANDOM_SEC: Array[float] = [20.0, 30.0, 40.0]
## 효력사 산포(표준편차, m).
const SIGMA_M: Array[float] = [35.0, 50.0, 20.0]
const ADJUST_SIGMA_MULT: float = 3.0
const CORRECTION_SEC: float = 15.0
const OBSERVER_SIGMA: Array[float] = [1.0, 1.5, 2.0]
const OBSERVER_DELAY: Array[float] = [0.0, 10.0, 30.0]
const MORTAR_ROUNDS_PER_TUBE: int = 6
const MORTAR_SMOKE_PER_TUBE: int = 3
const MORTAR_SHELL_SPEED: float = 250.0
const ARTILLERY_VOLLEY_SEC: float = 15.0
const SMOKE_RADIUS: Array[float] = [35.0, 50.0, 0.0]
const SMOKE_SEC: Array[float] = [120.0, 150.0, 0.0]
## 관측자가 표적을 볼 수 있다고 치는 표적점 높이.
const TARGET_POINT_H: float = 1.5
const OBSERVE_RADIUS_M: float = 0.0


class Mission:
	extends RefCounted
	var id: int = 0
	var side: StringName = &"blue"
	var asset: int = Asset.MORTAR
	var smoke: bool = false
	var target: Vector2 = Vector2.ZERO
	var observer_id: int = -1
	var observer_quality: int = 2
	var phase: int = Phase.PROCESSING
	var requested_tick: int = 0
	var ready_tick: int = 0
	var next_round_tick: int = 0
	var rounds_left: int = 0
	var rounds_fired: int = 0
	## 박격포 소대 id.
	var shooter_unit_id: int = -1
	var weapon: StringName = &""
	var sigma: float = 0.0
	var delay_sec: float = 0.0
	var first_impact: Vector2 = Vector2.INF

	func label() -> String:
		return "%s%s" % [ASSET_NAMES[asset], " 연막" if smoke else ""]


class Shell:
	extends RefCounted
	var mission_id: int
	var pos: Vector2
	var land_tick: int
	var weapon: StringName
	var smoke: bool
	var asset: int
	var source_id: int = -1
	var side: StringName


var battle: Battle
var missions: Array[Mission] = []
var _shells: Array[Shell] = []
var _next_id: int = 1


func _init(p_battle: Battle) -> void:
	battle = p_battle


func get_system_name() -> StringName:
	return &"fires"


func state_hash() -> int:
	var h: int = 0
	for m: Mission in missions:
		h = hash([h, m.id, m.phase, m.rounds_left])
	return h


## 진행 중인 임무(완료·취소 제외).
func active_missions(side: StringName) -> Array[Mission]:
	var out: Array[Mission] = []
	for m: Mission in missions:
		if m.side == side and m.phase != Phase.DONE and m.phase != Phase.CANCELLED:
			out.append(m)
	return out


# --- 요청 ----------------------------------------------------------------

## 화력 요청. 성공하면 {"ok": true, "mission": Mission}, 실패하면 {"ok": false, "error": String}.
func request(side: StringName, asset: int, target: Vector2, smoke: bool = false) -> Dictionary:
	var tf: TaskForce = battle.task_forces.get(side)
	if tf == null or tf.leader() == null:
		return _fail("대대 지휘소가 없습니다.")
	if asset == Asset.CAS and smoke:
		return _fail("항공은 연막을 쓸 수 없습니다.")
	target = battle.terrain.clamp_to_map(target, 10.0)
	var tick: int = battle.world.tick
	var m := Mission.new()
	m.side = side
	m.asset = asset
	m.smoke = smoke
	m.target = target
	m.requested_tick = tick
	var observer: Element = find_observer(side, target, asset == Asset.CAS)
	if observer != null:
		m.observer_id = observer.id
		m.observer_quality = observer_quality(observer, asset)
	elif asset == Asset.CAS:
		return _fail("항공 통제관이나 관측반이 표적을 보고 있어야 합니다.")
	# 통신: 관측자 → 대대.
	var delay: float = FDC_SEC[asset] + OBSERVER_DELAY[m.observer_quality]
	if observer != null and observer.platoon != null:
		var path: float = _comms_to_tf(observer.platoon, tf, HOP_SEC[asset])
		if path < 0.0:
			return _fail("관측자 통신 두절")
		delay += path
	# 자산.
	var toe: TOETemplate = tf.toe
	match asset:
		Asset.MORTAR:
			var mortar: Platoon = find_mortar(side, target)
			if mortar == null:
				return _fail("사거리 안에 쏠 수 있는 박격포 소대가 없습니다.")
			for other: Mission in active_missions(side):
				if other.shooter_unit_id == mortar.id:
					return _fail("%s 가 다른 임무를 쏘는 중입니다." % mortar.label())
			var l: CommsNet.Link = battle.comms.link_between(tf, mortar)
			if not l.ok:
				return _fail("박격포 소대 통신 두절")
			delay += HOP_SEC[asset] * CommsNet.delay_factor(l.quality) + l.relays * CommsNet.RELAY_DELAY_SEC
			m.shooter_unit_id = mortar.id
			m.weapon = _indirect_weapon(mortar)
			m.rounds_left = mortar.alive_elements().size() * (MORTAR_SMOKE_PER_TUBE if smoke else MORTAR_ROUNDS_PER_TUBE)
		Asset.ARTILLERY:
			if tf.artillery_missions <= 0:
				return _fail("남은 포병 사격 임무가 없습니다.")
			m.weapon = toe.artillery_weapon if toe != null else &""
			m.rounds_left = (toe.battery_guns * toe.rounds_per_gun) if toe != null else 18
		Asset.CAS:
			if tf.cas_sorties <= 0:
				return _fail("남은 항공 출격이 없습니다.")
			m.weapon = toe.cas_weapon if toe != null else &""
			m.rounds_left = toe.cas_bombs if toe != null else 2
	if battle.catalog.get_weapon(m.weapon) == null:
		return _fail("화력 자산 무기 정의가 없습니다.")
	if battle.order_system != null and not battle.order_system.try_spend(side, 1.0):
		return _fail("지휘 용량이 부족합니다.")
	if asset == Asset.ARTILLERY:
		tf.artillery_missions -= 1
	elif asset == Asset.CAS:
		tf.cas_sorties -= 1
	delay += battle.world.rng.next_range(0.0, RANDOM_SEC[asset])
	m.delay_sec = delay
	m.ready_tick = tick + roundi(delay * SimConst.TICK_RATE)
	m.sigma = SIGMA_M[asset] * OBSERVER_SIGMA[m.observer_quality]
	m.id = _next_id
	_next_id += 1
	missions.append(m)
	battle.world.events.emit_event(&"fire_requested", tick, {"side": side, "mission": m.id, "asset": asset,
		"smoke": smoke, "pos": target, "seconds": delay, "observer": m.observer_id, "quality": m.observer_quality})
	return {"ok": true, "mission": m}


func _fail(msg: String) -> Dictionary:
	return {"ok": false, "error": msg}


## 요청 전 미리보기: 관측 품질과 예상 지연(초). 요청 불가면 {"error": ...}.
func preview(side: StringName, asset: int, target: Vector2) -> Dictionary:
	var tf: TaskForce = battle.task_forces.get(side)
	if tf == null:
		return {"error": "전투단 없음"}
	var observer: Element = find_observer(side, target, asset == Asset.CAS)
	var q: int = observer_quality(observer, asset) if observer != null else 2
	if asset == Asset.CAS and observer == null:
		return {"error": "표적을 보는 통제관 없음"}
	var delay: float = FDC_SEC[asset] + OBSERVER_DELAY[q] + RANDOM_SEC[asset] * 0.5
	if observer != null and observer.platoon != null:
		var path: float = _comms_to_tf(observer.platoon, tf, HOP_SEC[asset])
		if path < 0.0:
			return {"error": "관측자 통신 두절"}
		delay += path
	if asset == Asset.MORTAR:
		var mortar: Platoon = find_mortar(side, target)
		if mortar == null:
			return {"error": "박격포 사거리 밖"}
		delay += HOP_SEC[asset]
	elif asset == Asset.ARTILLERY and tf.artillery_missions <= 0:
		return {"error": "포병 임무 없음"}
	elif asset == Asset.CAS and tf.cas_sorties <= 0:
		return {"error": "항공 출격 없음"}
	return {"quality": q, "seconds": delay, "observer": observer.id if observer != null else -1}


## 관측자 → 대대 지휘소 통신 지연 합(초). 끊겼으면 -1.
func _comms_to_tf(p: Platoon, tf: TaskForce, hop: float) -> float:
	var total: float = 0.0
	var chain: Array[Array] = []
	if p.parent != null and p.parent.level == Unit.Level.COMPANY:
		chain.append([p.parent, p])
		chain.append([tf, p.parent])
	elif p.parent == tf:
		chain.append([tf, p])
	for pair: Array in chain:
		var a: Unit = pair[0]
		var b2: Unit = pair[1]
		var l: CommsNet.Link = battle.comms.link_between(a, b2)
		if not l.ok:
			return -1.0
		total += hop * CommsNet.delay_factor(l.quality) + l.relays * CommsNet.RELAY_DELAY_SEC
	return total


## 표적점을 보고 있는 가장 좋은 아군 관측자. cas = true 면 관측반(fo)만.
func find_observer(side: StringName, target: Vector2, cas: bool) -> Element:
	var best: Element = null
	var best_q: int = 99
	var best_d: float = INF
	var th: float = TARGET_POINT_H
	for e: Element in battle.elements:
		if e.side != side or not e.is_active() or e.is_mounted():
			continue
		var is_fo: bool = e.def.element_class == &"fo"
		if cas and not is_fo:
			continue
		var d: float = e.pos.distance_to(target)
		if d > e.def.optics_m:
			continue
		var q: int = 0 if is_fo else 1
		if q > best_q or (q == best_q and d >= best_d):
			continue
		if not Los.check(battle.terrain, battle.smokes, battle.world.tick, e.pos, Los.eye_height(e), target, th):
			continue
		best = e
		best_q = q
		best_d = d
	return best


## 0 = 전문 관측자, 1 = 일반 엘리먼트. CAS 는 JTAC 만 0, 다른 관측반은 1.
static func observer_quality(e: Element, asset: int) -> int:
	if e.def.element_class != &"fo":
		return 1
	if asset == Asset.CAS:
		return 0 if String(e.def.id).contains("jtac") else 1
	return 0


## 사거리 안에서 쏠 수 있는 박격포 소대(탄이 남은).
func find_mortar(side: StringName, target: Vector2) -> Platoon:
	var tf: TaskForce = battle.task_forces.get(side)
	if tf == null:
		return null
	var best: Platoon = null
	var best_d: float = INF
	for p: Platoon in tf.all_platoons():
		if p.kind != &"mortar" or not p.on_map or p.is_destroyed():
			continue
		var wid: StringName = _indirect_weapon(p)
		var w: WeaponDef = battle.catalog.get_weapon(wid)
		if w == null:
			continue
		var has_ammo: bool = false
		for e: Element in p.alive_elements():
			if e.ammo.get(wid, 0) > 0:
				has_ammo = true
		var d: float = p.centroid().distance_to(target)
		if not has_ammo or d > w.range_max_m or d < w.range_min_m:
			continue
		if d < best_d:
			best = p
			best_d = d
	return best


func _indirect_weapon(p: Platoon) -> StringName:
	for e: Element in p.elements:
		for wid: String in e.def.weapons:
			var w: WeaponDef = battle.catalog.get_weapon(StringName(wid))
			if w != null and w.indirect:
				return w.id
	return &""


# --- 진행 ----------------------------------------------------------------

func step(world: SimWorld) -> void:
	var tick: int = world.tick
	for m: Mission in missions:
		if m.phase == Phase.DONE or m.phase == Phase.CANCELLED:
			continue
		_advance(m, tick)
	if not _shells.is_empty():
		var keep: Array[Shell] = []
		for s: Shell in _shells:
			if tick >= s.land_tick:
				_land(s, tick)
			else:
				keep.append(s)
		_shells = keep
	if tick % 600 == 0:
		_prune(tick)


func _advance(m: Mission, tick: int) -> void:
	match m.phase:
		Phase.PROCESSING:
			if tick < m.ready_tick:
				return
			if m.asset == Asset.CAS:
				_cas_pass(m, tick)
				return
			if m.observer_quality < 2:
				# 수정탄 한 발.
				m.phase = Phase.ADJUSTING
				if _fire_round(m, tick, m.sigma * ADJUST_SIGMA_MULT):
					m.next_round_tick = tick + roundi(CORRECTION_SEC * SimConst.TICK_RATE)
				_emit_phase(m, tick)
			else:
				m.phase = Phase.EFFECT
				m.next_round_tick = tick
				_emit_phase(m, tick)
		Phase.ADJUSTING:
			if tick < m.next_round_tick:
				return
			# 관측자가 아직 보고 있으면 수정 성공, 아니면 산포 2배로 효력사.
			var obs: Element = battle.get_element(m.observer_id)
			if obs == null or not obs.is_active() or not Los.check(battle.terrain, battle.smokes, tick, obs.pos,
					Los.eye_height(obs), m.target, TARGET_POINT_H):
				m.sigma = SIGMA_M[m.asset] * OBSERVER_SIGMA[2]
			m.phase = Phase.EFFECT
			m.next_round_tick = tick
			_emit_phase(m, tick)
		Phase.EFFECT:
			if tick < m.next_round_tick:
				return
			if m.rounds_left <= 0:
				_finish(m, tick)
				return
			match m.asset:
				Asset.MORTAR:
					_mortar_volley(m, tick)
				Asset.ARTILLERY:
					var tf: TaskForce = battle.task_forces.get(m.side)
					var guns: int = tf.toe.battery_guns if tf != null and tf.toe != null else 6
					for i: int in mini(guns, m.rounds_left):
						_fire_round(m, tick, m.sigma)
					m.next_round_tick = tick + roundi(ARTILLERY_VOLLEY_SEC * SimConst.TICK_RATE)


## 박격포: 쏠 수 있는 관마다 한 발씩(재장전 간격 = 60 / 분당 발사 수).
func _mortar_volley(m: Mission, tick: int) -> void:
	var p: Platoon = battle.get_unit(m.shooter_unit_id) as Platoon
	var w: WeaponDef = battle.catalog.get_weapon(m.weapon)
	if p == null or p.is_destroyed() or w == null:
		_finish(m, tick)
		return
	var fired: int = 0
	var any_ammo: bool = false
	var interval: int = roundi(60.0 / maxf(w.rof_rpm, 0.1) * SimConst.TICK_RATE)
	for e: Element in p.alive_elements():
		var ammo: int = e.ammo.get(m.weapon, 0)
		if ammo <= 0:
			continue
		any_ammo = true
		if e.velocity.length() > 0.5 or m.rounds_left <= 0:
			continue
		e.ammo[m.weapon] = ammo - 1
		e.last_fired_tick = tick
		_fire_round(m, tick, m.sigma, e)
		fired += 1
	if not any_ammo:
		_finish(m, tick)
		return
	m.next_round_tick = tick + (interval if fired > 0 else SimConst.TICK_RATE)


## 한 발을 쏜다(비행 시간 뒤 착탄). 박격포 수정탄은 소대 첫 엘리먼트가 쏜다.
func _fire_round(m: Mission, tick: int, sigma: float, source: Element = null) -> bool:
	var tof_ticks: int = 0
	if m.asset == Asset.MORTAR:
		if source == null:
			var p: Platoon = battle.get_unit(m.shooter_unit_id) as Platoon
			if p == null:
				return false
			for e: Element in p.alive_elements():
				var ammo: int = e.ammo.get(m.weapon, 0)
				if ammo > 0:
					source = e
					e.ammo[m.weapon] = ammo - 1
					break
			if source == null:
				return false
		tof_ticks = roundi(source.pos.distance_to(m.target) / MORTAR_SHELL_SPEED * SimConst.TICK_RATE)
	var s := Shell.new()
	s.mission_id = m.id
	s.pos = battle.terrain.clamp_to_map(m.target + battle.world.rng.next_scatter(sigma), 5.0)
	s.land_tick = tick + maxi(1, tof_ticks)
	s.weapon = m.weapon
	s.smoke = m.smoke
	s.asset = m.asset
	s.side = m.side
	s.source_id = source.id if source != null else -1
	_shells.append(s)
	m.rounds_left -= 1
	m.rounds_fired += 1
	if source != null:
		battle.world.events.emit_event(&"indirect_fire", tick, {"from": source.id, "side": m.side, "pos": source.pos})
	return true


func _land(s: Shell, tick: int) -> void:
	var m: Mission = _mission(s.mission_id)
	if m != null and m.first_impact == Vector2.INF:
		m.first_impact = s.pos
	battle.world.events.emit_event(&"shell", tick, {"pos": s.pos, "smoke": s.smoke, "asset": s.asset, "side": s.side,
		"radius": SMOKE_RADIUS[s.asset] if s.smoke else 0.0})
	if s.smoke:
		var sm := Los.Smoke.new()
		sm.pos = s.pos
		sm.radius = SMOKE_RADIUS[s.asset]
		sm.until_tick = tick + roundi(SMOKE_SEC[s.asset] * SimConst.TICK_RATE)
		battle.smokes.append(sm)
		return
	var w: WeaponDef = battle.catalog.get_weapon(s.weapon)
	if w != null:
		battle.combat.indirect_impact(s.pos, w, s.side, battle.get_element(s.source_id), tick)


## CAS: 직선 진입 1회 공격. 표적 근처 적 대공 무기가 먼저 격추를 시도한다.
func _cas_pass(m: Mission, tick: int) -> void:
	for e: Element in battle.elements:
		if e.side == m.side or not e.is_active() or e.is_mounted():
			continue
		for wid: String in e.def.weapons:
			var w: WeaponDef = battle.catalog.get_weapon(StringName(wid))
			if w == null or not w.can_target(&"air") or e.ammo.get(w.id, 0) <= 0:
				continue
			if e.pos.distance_to(m.target) > w.range_max_m:
				continue
			e.ammo[w.id] = e.ammo.get(w.id, 0) - 1
			e.last_fired_tick = tick
			battle.world.events.emit_event(&"sam_launch", tick, {"from": e.id, "side": e.side, "pos": e.pos, "to": m.target})
			if battle.world.rng.chance(w.accuracy_base):
				m.phase = Phase.CANCELLED
				battle.world.events.emit_event(&"cas_shot_down", tick, {"side": m.side, "mission": m.id, "pos": m.target,
					"by": e.id})
				return
			break
	m.phase = Phase.EFFECT
	_emit_phase(m, tick)
	while m.rounds_left > 0:
		_fire_round(m, tick, m.sigma)
	_finish(m, tick)


func _finish(m: Mission, tick: int) -> void:
	m.phase = Phase.DONE
	_emit_phase(m, tick)


func _emit_phase(m: Mission, tick: int) -> void:
	battle.world.events.emit_event(&"fire_mission", tick, {"side": m.side, "mission": m.id, "asset": m.asset,
		"phase": m.phase, "smoke": m.smoke, "pos": m.target})


func _mission(mission_id: int) -> Mission:
	for m: Mission in missions:
		if m.id == mission_id:
			return m
	return null


## 끝난 지 오래된 임무와 사라진 연막 정리.
func _prune(tick: int) -> void:
	var keep: Array[Mission] = []
	for m: Mission in missions:
		if (m.phase == Phase.DONE or m.phase == Phase.CANCELLED) and tick - m.ready_tick > 1200:
			continue
		keep.append(m)
	missions = keep
	var smokes: Array[Los.Smoke] = []
	for sm: Los.Smoke in battle.smokes:
		if sm.until_tick > tick:
			smokes.append(sm)
	battle.smokes = smokes
