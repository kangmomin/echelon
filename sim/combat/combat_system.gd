class_name CombatSystem
extends SimSystem
## 직접 사격: 표적 선정 → 사격 → 명중 → 관통·피해·제압. PLAN 9.2~9.5.
##
## - 표적은 자기 소대가 최근 10초 안에 본 적(contacts) 중에서, 1초마다(틱 분산) 다시 고른다.
## - 명중률 = 거리별 기본 명중률 × 숙련 × 이동(사수 0.7, 표적 0.85) × 제압 × 대형 전환 × 표적 엄폐 × 크기 × 대형 정면 화력.
## - 관통: 맞은 방향(정면·측면·후면·상부)의 장갑과 거리별 관통력을 비교.
##   관통/장갑 > 1.1 → 격파 70% / 손상 30%, 0.9~1.1 → 손상 50%, < 0.9 → 튕김(제압만).
## - 보병: 인원 손실 = 피해 × 계수 × 엄폐, 고폭은 반경 안 보병 모두.

const TARGET_INTERVAL: int = 10
const LOS_TOP_CHECKS: int = 3
const SKILL_ACC: Dictionary[StringName, float] = {&"recruit": 0.85, &"regular": 1.0, &"veteran": 1.1, &"elite": 1.2}
const MOVING_SHOOTER: float = 0.7
const MOVING_TARGET: float = 0.85
const SUPPRESSED_ACC: float = 0.7
const REFORMING_ACC: float = 0.75
const BOUNDING_BONUS: float = 1.15
const TARGET_COVER_ACC: Dictionary[int, float] = {Terrain.Type.FOREST: 0.75, Terrain.Type.URBAN: 0.7}
const INF_COVER: Dictionary[int, float] = {Terrain.Type.FOREST: 0.6, Terrain.Type.URBAN: 0.5}
const DUG_IN_TICKS: int = 600
const DUG_IN_COVER: float = 0.7
const KILL_P: float = 0.7
const MARGINAL_P: float = 0.5
const OVERMATCH: float = 1.1
const UNDERMATCH: float = 0.9
const INF_CASUALTY_PER_DAMAGE: float = 0.25
const HE_SPLASH_CASUALTY_PER_DAMAGE: float = 0.06
const MISS_SUPPRESSION: float = 0.4
const VEHICLE_SUPPRESSION: float = 0.5
const THREAT_RADIUS_M: float = 100.0
const THREAT_AMOUNT: float = 1.0
const RETURN_FIRE_TICKS: int = 100
## 사선 가림: 같은 소대 차량이 사선 앞 이 거리 안, 이 폭 안에 있으면 쏠 수 없다(DECISIONS D-019).
const MASK_RANGE_M: float = 250.0
const MASK_HALF_WIDTH_M: float = 7.0
const MASKED_SCORE: float = 0.2

var battle: Battle
var shots_fired: int = 0
var hits: int = 0
var kills: int = 0


func _init(p_battle: Battle) -> void:
	battle = p_battle


func get_system_name() -> StringName:
	return &"combat"


func state_hash() -> int:
	return hash([shots_fired, hits, kills])


func step(world: SimWorld) -> void:
	var dt: float = SimConst.TICK_DT
	var tick: int = world.tick
	for e: Element in battle.elements:
		if not e.is_active() or e.is_mounted():
			continue
		for w: StringName in e.reload.keys():
			e.reload[w] = maxf(0.0, e.reload[w] - dt)
		if (e.id + tick) % TARGET_INTERVAL == 0:
			e.target_id = choose_target(e, tick)
		if e.target_id >= 0:
			_try_fire(e, tick)


# --- 표적 ----------------------------------------------------------------

func choose_target(e: Element, tick: int) -> int:
	var p: Platoon = e.platoon
	if p == null or p.sop.roe == &"hold_fire" and tick - p.last_hit_tick > RETURN_FIRE_TICKS:
		return -1
	var scored: Array[Array] = []
	for key: int in p.contacts.keys():
		var seen_tick: int = p.contacts[key]
		if tick - seen_tick > DetectionSystem.CONTACT_MEMORY_TICKS:
			continue
		var t: Element = battle.get_element(key)
		if t == null or not t.is_active() or t.is_mounted():
			continue
		if p.sop.roe == &"designated" and t.is_infantry():
			continue
		var d: float = e.pos.distance_to(t.pos)
		var s: float = _score(e, t, d)
		if s > 0.0 and is_masked(e, t):
			s *= MASKED_SCORE
		if s > 0.0:
			scored.append([s, t.id, t])
	if scored.is_empty():
		return -1
	scored.sort_custom(func(a: Array, b: Array) -> bool:
		var sa: float = a[0]
		var sb: float = b[0]
		var ia: int = a[1]
		var ib: int = b[1]
		return sa > sb or (sa == sb and ia < ib))
	for i: int in mini(scored.size(), LOS_TOP_CHECKS):
		var t: Element = scored[i][2]
		if Los.between(battle.terrain, battle.smokes, tick, e, t):
			return t.id
	return -1


## 이 표적을 쏠 가치(0 = 쏠 무기 없음).
func _score(e: Element, t: Element, d: float) -> float:
	var best: float = 0.0
	for wid: String in e.def.weapons:
		var w: WeaponDef = battle.catalog.get_weapon(StringName(wid))
		if not _usable(e, w, t, d):
			continue
		var eff: float = w.accuracy_at(d)
		if t.is_vehicle():
			eff *= clampf(w.penetration_at(d) / maxf(t.def.get_armor(&"front"), 1.0), 0.05, 1.5)
		else:
			eff *= w.damage * 0.1
		best = maxf(best, eff)
	if best <= 0.0:
		return 0.0
	var value: float = float(t.def.cost) / 50.0
	if t.def.element_class == &"tank":
		value *= 1.5
	return best * value / (1.0 + d / 1000.0)


func _usable(e: Element, w: WeaponDef, t: Element, d: float) -> bool:
	if w == null or w.indirect or w.weapon_type == &"SAM":
		return false
	if e.ammo.get(w.id, 0) < 1:
		return false
	if not w.can_target(&"infantry" if t.is_infantry() else &"vehicle"):
		return false
	if d < w.range_min_m or d > w.range_max_m:
		return false
	if e.state == Element.State.DAMAGED and e.damage_kind == &"firepower" and w.weapon_type != &"MG":
		return false
	return true


# --- 사격 ----------------------------------------------------------------

func _try_fire(e: Element, tick: int) -> void:
	var t: Element = battle.get_element(e.target_id)
	if t == null or not t.is_active() or t.is_mounted():
		e.target_id = -1
		return
	var d: float = e.pos.distance_to(t.pos)
	var moving: bool = e.velocity.length() > 1.0
	if is_masked(e, t):
		return
	for wid: String in e.def.weapons:
		var w: WeaponDef = battle.catalog.get_weapon(StringName(wid))
		if not _usable(e, w, t, d):
			continue
		if e.reload.get(w.id, 0.0) > 0.0:
			continue
		if moving and not w.fire_on_move:
			continue
		_fire(e, w, t, d, tick)
		if not t.is_alive():
			e.target_id = -1
			return


func _fire(e: Element, w: WeaponDef, t: Element, d: float, tick: int) -> void:
	var left: int = e.ammo.get(w.id, 0)
	e.ammo[w.id] = maxi(0, left - w.burst)
	var interval: float = w.interval_sec()
	if e.suppression >= 50.0:
		interval *= 1.25
	e.reload[w.id] = interval
	e.last_fired_tick = tick
	shots_fired += 1
	var p_hit: float = hit_chance(e, w, t, d, tick)
	var hit: bool = battle.world.rng.chance(p_hit)
	battle.world.events.emit_event(&"shot", tick, {"from": e.id, "to": t.id, "weapon": w.weapon_type, "hit": hit,
		"fp": e.pos, "tp": t.pos})
	_mark_under_fire(t, tick)
	if not hit:
		_suppress(t, w.suppression * MISS_SUPPRESSION)
		return
	hits += 1
	if t.is_vehicle():
		_resolve_vehicle_hit(e, w, t, d, tick)
	else:
		_resolve_infantry_hit(e, w, t, tick, w.damage * INF_CASUALTY_PER_DAMAGE)
	if w.he_radius_m > 0.0:
		_he_splash(e, w, t.pos, t, tick)


func hit_chance(e: Element, w: WeaponDef, t: Element, d: float, tick: int) -> float:
	var p: float = w.accuracy_at(d)
	if e.platoon != null:
		p *= SKILL_ACC.get(e.platoon.skill, 1.0)
		if e.platoon.is_reforming(tick):
			p *= REFORMING_ACC
		if e.platoon.sop.technique == &"bounding" and e.velocity.length() < 0.5:
			p *= BOUNDING_BONUS
	if e.velocity.length() > 1.0:
		p *= MOVING_SHOOTER
	if t.velocity.length() > 1.0:
		p *= MOVING_TARGET
	if e.suppression >= 50.0:
		p *= SUPPRESSED_ACC
	p *= TARGET_COVER_ACC.get(battle.terrain.type_at(t.pos), 1.0)
	p *= clampf(sqrt(t.def.size), 0.6, 1.2)
	return clampf(p, 0.01, 0.98)


## 사선 가림: 같은 소대 차량이 사수와 표적 사이 가까이(사선 폭 안)에 있으면 쏠 수 없다.
## 종대는 앞 차가 뒤 차의 사선을 막고, 횡대는 모두 쏠 수 있다 — 대형의 정면 화력이 기하로 생긴다.
func is_masked(e: Element, t: Element) -> bool:
	if e.platoon == null or e.is_infantry():
		return false
	var to_t: Vector2 = t.pos - e.pos
	var d: float = to_t.length()
	if d < 1.0:
		return false
	var dir: Vector2 = to_t / d
	var reach: float = minf(d - 5.0, MASK_RANGE_M)
	for o: Element in e.platoon.elements:
		if o == e or not o.is_active() or o.is_mounted() or o.is_infantry():
			continue
		var rel: Vector2 = o.pos - e.pos
		var along: float = rel.dot(dir)
		if along <= 5.0 or along > reach:
			continue
		if absf(rel.cross(dir)) < MASK_HALF_WIDTH_M:
			return true
	return false


## 맞은 방향: 표적 정면 기준 사수 방향 각도.
static func hit_facing(shooter_pos: Vector2, t: Element, top_attack: bool) -> StringName:
	if top_attack:
		return &"top"
	var rel: float = absf(angle_difference(t.heading, FormationSolver.heading_from_vector(shooter_pos - t.pos)))
	if rel <= PI / 4.0:
		return &"front"
	if rel >= PI * 0.75:
		return &"rear"
	return &"side"


## 관통 판정 분류: &"overmatch"(격파 70%) / &"marginal"(손상 50%) / &"bounce".
static func classify_penetration(pen: float, armor: float) -> StringName:
	var ratio: float = pen / maxf(armor, 1.0)
	if ratio > OVERMATCH:
		return &"overmatch"
	if ratio >= UNDERMATCH:
		return &"marginal"
	return &"bounce"


func _resolve_vehicle_hit(e: Element, w: WeaponDef, t: Element, d: float, tick: int) -> void:
	var facing: StringName = hit_facing(e.pos, t, w.top_attack)
	_suppress(t, w.suppression * VEHICLE_SUPPRESSION)
	var rng: SimRng = battle.world.rng
	match classify_penetration(w.penetration_at(d), t.def.get_armor(facing)):
		&"overmatch":
			if rng.chance(KILL_P):
				destroy(t, e, tick)
			else:
				damage(t, e, tick)
		&"marginal":
			if rng.chance(MARGINAL_P):
				damage(t, e, tick)


func _resolve_infantry_hit(e: Element, w: WeaponDef, t: Element, tick: int, expected: float) -> void:
	_suppress(t, w.suppression)
	var cover: float = INF_COVER.get(battle.terrain.type_at(t.pos), 1.0)
	if t.stationary_ticks > DUG_IN_TICKS:
		cover *= DUG_IN_COVER
	var x: float = expected * cover
	var casualties: int = floori(x) + (1 if battle.world.rng.chance(x - floorf(x)) else 0)
	if casualties <= 0:
		return
	t.hp = maxf(0.0, t.hp - casualties)
	battle.morale.on_casualties(t.platoon, casualties)
	if t.hp <= 0.0:
		destroy(t, e, tick)


func _he_splash(e: Element, w: WeaponDef, center: Vector2, direct: Element, tick: int) -> void:
	for o: Element in battle.elements_near(center, w.he_radius_m):
		if o == direct or o.side == e.side or o.is_mounted():
			continue
		_suppress(o, w.suppression * 0.5)
		if o.is_infantry():
			_resolve_infantry_hit(e, w, o, tick, w.damage * HE_SPLASH_CASUALTY_PER_DAMAGE)


## 간접화력 한 발 착탄. 반경 안 피해는 거리·대형 포격 취약도·피복·진지에 따라 줄고, 2배 반경까지 제압. PLAN 10.1.
## 차량은 상면 장갑 대 관통력, 보병은 인원 손실. 아군 오사는 다루지 않는다.
func indirect_impact(pos: Vector2, w: WeaponDef, firing_side: StringName, source: Element, tick: int) -> void:
	var r: float = maxf(w.he_radius_m, 1.0)
	var rng: SimRng = battle.world.rng
	for o: Element in battle.elements_near(pos, r * 2.0):
		if o.side == firing_side or o.is_mounted() or not o.is_alive():
			continue
		var d: float = o.pos.distance_to(pos)
		_suppress(o, w.suppression * clampf(1.0 - d / (r * 2.0), 0.0, 1.0))
		o.last_hit_tick = tick
		if o.platoon != null:
			o.platoon.last_hit_tick = tick
			o.platoon.last_shelled_tick = tick
			o.platoon.last_shell_pos = pos
		if d > r:
			continue
		var k: float = (1.0 - d / r) * arty_vulnerability(o)
		if o.is_infantry():
			_resolve_infantry_hit(source, w, o, tick, w.damage * HE_SPLASH_CASUALTY_PER_DAMAGE * 2.0 * k)
			continue
		match classify_penetration(w.penetration, o.def.get_armor(&"top")):
			&"overmatch":
				if rng.chance(k * KILL_P):
					destroy(o, source, tick)
				elif rng.chance(k):
					damage(o, source, tick)
			&"marginal":
				if rng.chance(k * MARGINAL_P):
					damage(o, source, tick)


## 포격 취약도: 대형(arty_vuln) × 지형 피복 × 진지(오래 정지).
func arty_vulnerability(o: Element) -> float:
	var v: float = 1.0
	if o.platoon != null and o.platoon.formation != null:
		v *= o.platoon.formation.get_mod(&"arty_vuln", 1.0)
	v *= TARGET_COVER_ACC.get(battle.terrain.type_at(o.pos), 1.0)
	if o.stationary_ticks > DUG_IN_TICKS:
		v *= DUG_IN_COVER
	return v


func _suppress(t: Element, amount: float) -> void:
	t.suppression = minf(100.0, t.suppression + amount)


func _mark_under_fire(t: Element, tick: int) -> void:
	t.last_hit_tick = tick
	if t.platoon != null:
		t.platoon.last_hit_tick = tick
		t.platoon.last_contact_tick = tick


# --- 피해 ----------------------------------------------------------------

## 격파. 탑승 분대는 50% 사상 후 하차. PLAN 9.5.
func destroy(t: Element, killer: Element, tick: int) -> void:
	if not t.is_alive():
		return
	t.state = Element.State.DESTROYED
	t.destroyed_tick = tick
	t.velocity = Vector2.ZERO
	t.target_id = -1
	kills += 1
	if killer != null:
		t.killed_by = killer.id
		killer.kills += 1
	for squad: Element in t.passengers:
		squad.mounted_in = null
		squad.hp = floorf(squad.hp * 0.5)
		if squad.hp <= 0.0:
			squad.state = Element.State.DESTROYED
			squad.destroyed_tick = tick
		else:
			squad.pos = battle.terrain.nearest_passable(t.pos + FormationSolver.right_vector(t.heading) * 10.0, Terrain.MOBILITY_FOOT)
			squad.suppression = 80.0
	t.passengers.clear()
	if t.platoon != null and t.platoon.has_squads():
		t.platoon.mounted = false
	battle.pathfinder.add_threat(t.side, t.pos, THREAT_RADIUS_M, THREAT_AMOUNT * 2.0)
	battle.morale.on_element_lost(t)
	battle.world.events.emit_event(&"element_destroyed", tick, {"element": t.id, "side": t.side,
		"killer": killer.id if killer != null else -1, "pos": t.pos, "unit": t.platoon.id if t.platoon != null else -1})


## 손상. 이미 손상이면 50% 확률로 격파.
func damage(t: Element, attacker: Element, tick: int) -> void:
	if t.state == Element.State.DAMAGED:
		if battle.world.rng.chance(0.5):
			destroy(t, attacker, tick)
		return
	t.state = Element.State.DAMAGED
	t.damage_kind = &"mobility" if battle.world.rng.chance(0.5) else &"firepower"
	battle.pathfinder.add_threat(t.side, t.pos, THREAT_RADIUS_M, THREAT_AMOUNT)
	battle.morale.on_element_damaged(t)
	battle.world.events.emit_event(&"element_damaged", tick, {"element": t.id, "side": t.side, "kind": t.damage_kind,
		"unit": t.platoon.id if t.platoon != null else -1})
