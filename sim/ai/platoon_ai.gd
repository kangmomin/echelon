class_name PlatoonAI
extends SimSystem
## 소대·중대의 자율 반응(SOP). 양 진영 모두 같은 코드로 돈다. PLAN 8.5, 17.5.
## - 접촉 시 반응: 정지 후 대응 사격 / 대응 사격 후 계속 / 돌격 / 즉시 철수.
## - 탑승 보병: 접촉 시 하차.
## - 철수 기준: 전투력이 SOP 비율 이하면 스스로 후방으로 철수.
## - 패주: 후방으로 달아난다.
## 자율 행동은 지휘망을 타지 않는다(제대 자신의 판단). 명령은 issuer = &"self" 로 남긴다.

const UPDATE_INTERVAL: int = 10
const IN_CONTACT_TICKS: int = 150
const HALT_TICKS: int = 150
## 공격 이동: 접촉 시 15초 정지 사격 후 최소 12초는 전진한다(멀리서 보이는 적 때문에 영영 멈추지 않게).
const ATTACK_ADVANCE_TICKS: int = 120
const WITHDRAW_DISTANCE_M: float = 600.0
const BREAK_CONTACT_DISTANCE_M: float = 400.0
const ROUT_DISTANCE_M: float = 800.0
const ASSAULT_MAX_M: float = 1200.0
## 포격 SOP "분산": 착탄 3초 안이면 정지해 있던 제대가 착탄점 반대쪽으로 250 m 옮긴다(60초에 한 번).
const SHELLED_TICKS: int = 30
const DISPERSE_M: float = 250.0
const DISPERSE_COOLDOWN: int = 600

var battle: Battle


func _init(p_battle: Battle) -> void:
	battle = p_battle


func get_system_name() -> StringName:
	return &"platoon_ai"


func step(world: SimWorld) -> void:
	if world.tick % UPDATE_INTERVAL != 0:
		return
	for side: StringName in Battle.SIDES:
		var tf: TaskForce = battle.task_forces.get(side)
		if tf == null:
			continue
		for c: Company in tf.companies:
			if c.on_map and not c.is_destroyed():
				_company(c, world.tick)
		for p: Platoon in tf.all_platoons():
			if p.on_map and not p.is_destroyed():
				_platoon(p, world.tick)


static func in_contact(u: Unit, tick: int) -> bool:
	return tick - u.last_contact_tick < IN_CONTACT_TICKS or tick - u.last_hit_tick < IN_CONTACT_TICKS


func _company(c: Company, tick: int) -> void:
	var contact: bool = false
	for p: Platoon in c.formation_platoons():
		if in_contact(p, tick):
			contact = true
			c.last_contact_tick = maxi(c.last_contact_tick, p.last_contact_tick)
			c.last_hit_tick = maxi(c.last_hit_tick, p.last_hit_tick)
	if contact and c.posture == &"moving" and c.mission != null and c.mission.type == Order.Type.ATTACK_MOVE:
		_attack_halt(c, tick)
	elif contact and c.posture == &"moving":
		match c.sop.on_contact:
			&"halt_and_fire":
				c.contact_halt_until = tick + HALT_TICKS
			&"break_contact":
				_self_order(c, Order.Type.WITHDRAW, _away_from_contact(c, BREAK_CONTACT_DISTANCE_M))
	var shelled: Platoon = null
	for p: Platoon in c.formation_platoons():
		if tick - p.last_shelled_tick < SHELLED_TICKS:
			shelled = p
	if shelled != null:
		_disperse(c, shelled.last_shell_pos, tick)
	if c.sop.withdraw_at > 0.0 and c.strength() <= c.sop.withdraw_at and c.posture != &"withdrawing" \
			and not c.auto_withdrawn:
		c.auto_withdrawn = true
		_self_order(c, Order.Type.WITHDRAW, _rear_point(c, WITHDRAW_DISTANCE_M))
		battle.world.events.emit_event(&"auto_withdraw", tick, {"unit": c.id, "side": c.side})


func _platoon(p: Platoon, tick: int) -> void:
	var contact: bool = in_contact(p, tick)
	# 탑승 보병.
	if p.has_squads() and p.has_transport():
		if p.mounted and (p.sop.mount_policy == &"stay_dismounted" or (contact and p.sop.mount_policy == &"dismount_on_contact")):
			battle.executor.set_mounted(p, false)
	# 패주.
	if p.routed:
		if not p.guide.is_moving() and not p.guide.is_waiting_for_path():
			p.detached = true
			_self_order(p, Order.Type.WITHDRAW, _rear_point(p, ROUT_DISTANCE_M))
		return
	if p.follows_parent():
		return  # 대형 안의 소대는 중대가 반응한다.
	if tick - p.last_shelled_tick < SHELLED_TICKS:
		_disperse(p, p.last_shell_pos, tick)
	if contact and p.posture == &"moving" and p.mission != null and p.mission.type == Order.Type.ATTACK_MOVE:
		_attack_halt(p, tick)
	elif contact and (p.posture == &"moving" or p.posture == &"idle"):
		match p.sop.on_contact:
			&"halt_and_fire":
				if p.posture == &"moving":
					p.contact_halt_until = tick + HALT_TICKS
			&"break_contact":
				if p.posture != &"withdrawing":
					_self_order(p, Order.Type.WITHDRAW, _away_from_contact(p, BREAK_CONTACT_DISTANCE_M))
			&"assault":
				var target: Vector2 = _nearest_contact(p, tick)
				if target != Vector2.INF and p.centroid().distance_to(target) < ASSAULT_MAX_M and p.posture != &"moving":
					_self_order(p, Order.Type.ATTACK_MOVE, target)
	if p.sop.withdraw_at > 0.0 and p.strength() <= p.sop.withdraw_at and p.posture != &"withdrawing" \
			and not p.auto_withdrawn and not p.is_hq_section:
		p.auto_withdrawn = true
		_self_order(p, Order.Type.WITHDRAW, _rear_point(p, WITHDRAW_DISTANCE_M))
		battle.world.events.emit_event(&"auto_withdraw", tick, {"unit": p.id, "side": p.side})


## 포격을 받으면(SOP 분산) 정지해 있던 제대가 착탄점에서 멀어진다. 이동 중이면 그대로 계속 간다.
func _disperse(u: Unit, shell_pos: Vector2, tick: int) -> void:
	if u.sop.under_fire != &"disperse" or tick < u.next_disperse_tick:
		return
	if u.posture != &"idle" and u.posture != &"defending":
		return
	if u.guide.is_moving() or u.guide.is_waiting_for_path():
		return
	u.next_disperse_tick = tick + DISPERSE_COOLDOWN
	var here: Vector2 = u.centroid()
	var away: Vector2 = here - shell_pos
	if away.length() < 5.0:
		away = FormationSolver.right_vector(u.guide.heading)
	var dest: Vector2 = here + away.normalized() * DISPERSE_M
	var facing: float = u.guide.heading
	battle.self_order(u, Order.Type.MOVE, battle.terrain.nearest_passable(dest, Terrain.MOBILITY_VEHICLE), {"facing": facing})
	battle.world.events.emit_event(&"disperse", tick, {"unit": u.id, "side": u.side})


## 공격 이동의 짧은 정지: 정지 사격 HALT_TICKS → 전진 ATTACK_ADVANCE_TICKS 를 반복.
func _attack_halt(u: Unit, tick: int) -> void:
	if tick < u.next_halt_tick:
		return
	u.contact_halt_until = tick + HALT_TICKS
	u.next_halt_tick = tick + HALT_TICKS + ATTACK_ADVANCE_TICKS


func _self_order(u: Unit, type: int, pos: Vector2) -> void:
	battle.self_order(u, type, pos)


## 자기 진영 후방(배치 구역 쪽)으로 distance 만큼 물러난 지점.
func _rear_point(u: Unit, distance: float) -> Vector2:
	var here: Vector2 = u.centroid()
	var home: Vector2 = here
	var side: ScenarioDef.SideSetup = battle.scenario.sides.get(u.side)
	if side != null:
		home = side.deploy_zone.get_center()
	var dir: Vector2 = (home - here)
	if dir.length() < 1.0:
		dir = Vector2(0, -1) if u.side == &"blue" else Vector2(0, 1)
	return here + dir.normalized() * minf(distance, maxf(dir.length(), 50.0))


func _away_from_contact(u: Unit, distance: float) -> Vector2:
	var here: Vector2 = u.centroid()
	var threat: Vector2 = _nearest_contact_any(u, battle.world.tick)
	if threat == Vector2.INF:
		return _rear_point(u, distance)
	var dir: Vector2 = (here - threat).normalized()
	return here + dir * distance


func _nearest_contact(p: Platoon, tick: int) -> Vector2:
	var best: Vector2 = Vector2.INF
	var best_d: float = INF
	for key: int in p.contacts.keys():
		var seen_tick: int = p.contacts[key]
		if tick - seen_tick > DetectionSystem.CONTACT_MEMORY_TICKS:
			continue
		var t: Element = battle.get_element(key)
		if t == null or not t.is_alive():
			continue
		var d: float = t.pos.distance_squared_to(p.centroid())
		if d < best_d:
			best_d = d
			best = t.pos
	return best


func _nearest_contact_any(u: Unit, tick: int) -> Vector2:
	if u is Platoon:
		return _nearest_contact(u as Platoon, tick)
	var best: Vector2 = Vector2.INF
	var best_d: float = INF
	for child: Unit in u.children_units():
		if child is Platoon:
			var p: Vector2 = _nearest_contact(child as Platoon, tick)
			if p != Vector2.INF and p.distance_squared_to(u.centroid()) < best_d:
				best_d = p.distance_squared_to(u.centroid())
				best = p
	return best
