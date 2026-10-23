class_name TaskForceAI
extends SimSystem
## 대대(TF)급 AI. AI 진영만. PLAN 11.3, 17.5.
## - 30초마다 거점 가치(분당 승점 + 아직 못 이룬 임무 목표 승점) × 거리 × 적 추정 전력으로 중대에 거점을 배분.
##   점령한 거점은 방어, 아니면 확보(적이 있으면 공격 이동). 이전 배정에 가산점을 줘 자주 바꾸지 않는다.
## - 전투력 50% 이하 중대는 예비로 돌려 후방으로 철수.
## - 직할: 정찰 소대는 전방 차장, 관측반·전자전은 선도 중대 뒤, 박격포는 사거리 안으로 전진.
## - 화력: 대대 그림(known_enemies)의 적 밀집 지점에 박격포·포병·CAS 를 요청.
## - 보급: 탄약 35% 이하이고 접촉이 없는 소대에 보급 명령.
## 명령은 플레이어와 같은 지휘망(지연·지휘 용량)을 탄다.

const EVAL_INTERVAL: int = 300
const EVAL_OFFSET: int = 7
const RESERVE_STRENGTH: float = 0.5
const STICKY_BONUS: float = 1.3
const ENEMY_NEAR_M: float = 900.0
const ARRIVE_M: float = 250.0
const FRESH_TICKS: int = 300
const CLUSTER_M: float = 150.0
const ARTILLERY_GAP_TICKS: int = 1200
const LOW_AMMO: float = 0.35

var battle: Battle
var sides: Array[StringName] = []
## 중대 id → 배정된 거점 id(예비면 &"reserve").
var assignment: Dictionary[int, StringName] = {}
## 중대 id → 마지막으로 낸 명령 종류.
var last_order: Dictionary[int, int] = {}
var _last_artillery: Dictionary[StringName, int] = {&"blue": -100000, &"red": -100000}
var _last_cas: Dictionary[StringName, int] = {&"blue": -100000, &"red": -100000}


func _init(p_battle: Battle) -> void:
	battle = p_battle


func get_system_name() -> StringName:
	return &"tf_ai"


func state_hash() -> int:
	return hash([assignment.keys(), assignment.values()])


func step(world: SimWorld) -> void:
	if battle.deploying or not battle.result.is_empty():
		return
	if world.tick % EVAL_INTERVAL != EVAL_OFFSET:
		return
	for side: StringName in sides:
		var tf: TaskForce = battle.task_forces.get(side)
		if tf != null and tf.leader() != null:
			evaluate(tf)


func evaluate(tf: TaskForce) -> void:
	_assign_companies(tf)
	_organic(tf)
	_fires(tf)
	_resupply(tf)


# --- 중대 임무 ------------------------------------------------------------

func _point_value(tf: TaskForce, ps: ScoringSystem.PointState) -> float:
	var v: float = ps.def.vp_per_min * 10.0
	for obj: ScenarioDef.Objective in battle.scenario.objectives:
		if obj.point == ps.def.id and obj.side == tf.side and not battle.scoring.objectives_done.has(obj.id):
			v += obj.vp
	if ps.owner == tf.side and not ps.contested:
		v *= 0.6  # 이미 가진 곳은 지키기만
	return v


## 대대가 알고 있는(신선한) 적 엘리먼트 수.
func _enemies_near(tf: TaskForce, pos: Vector2, radius: float) -> int:
	var n: int = 0
	var tick: int = battle.world.tick
	for key: int in tf.known_enemies.keys():
		var info: Dictionary = tf.known_enemies[key]
		var t: int = info["tick"]
		var p: Vector2 = info["pos"]
		if tick - t <= FRESH_TICKS * 4 and p.distance_to(pos) <= radius:
			n += 1
	return n


func _assign_companies(tf: TaskForce) -> void:
	var active: Array[Company] = []
	for c: Company in tf.companies:
		if not c.on_map or c.is_destroyed():
			continue
		if c.strength() <= RESERVE_STRENGTH or c.routed:
			if assignment.get(c.id, &"") != &"reserve":
				assignment[c.id] = &"reserve"
				_order(c, Order.Type.WITHDRAW, _rear(tf), NAN)
			continue
		active.append(c)
	if active.is_empty():
		return
	var pts: Array[ScoringSystem.PointState] = battle.scoring.points
	var point_load: Dictionary[StringName, int] = {}
	var todo: Array[Company] = active.duplicate()
	var new_assign: Dictionary[int, StringName] = {}
	while not todo.is_empty():
		var best_score: float = -1.0
		var best_c: Company = null
		var best_p: ScoringSystem.PointState = null
		for c: Company in todo:
			for ps: ScoringSystem.PointState in pts:
				var already: int = point_load.get(ps.def.id, 0)
				var enemy: int = _enemies_near(tf, ps.def.pos, ENEMY_NEAR_M)
				var dist_km: float = c.centroid().distance_to(ps.def.pos) / 1000.0
				var score: float = _point_value(tf, ps) / (1.0 + dist_km) / (1.0 + 0.15 * enemy)
				score /= 1.0 + already * 1.5
				if assignment.get(c.id, &"") == ps.def.id:
					score *= STICKY_BONUS
				if score > best_score:
					best_score = score
					best_c = c
					best_p = ps
		if best_c == null:
			break
		todo.erase(best_c)
		new_assign[best_c.id] = best_p.def.id
		point_load[best_p.def.id] = point_load.get(best_p.def.id, 0) + 1
	for c: Company in active:
		var pid: StringName = new_assign.get(c.id, &"")
		if pid == &"":
			continue
		var ps: ScoringSystem.PointState = battle.scoring.get_point(pid)
		var changed: bool = assignment.get(c.id, &"") != pid
		assignment[c.id] = pid
		var held: bool = ps.owner == tf.side and not ps.contested
		var enemy_near: bool = _enemies_near(tf, ps.def.pos, ENEMY_NEAR_M) > 0
		var type: int = Order.Type.DEFEND if held else (Order.Type.ATTACK_MOVE if enemy_near else Order.Type.SEIZE)
		var far: bool = c.centroid().distance_to(ps.def.pos) > ARRIVE_M
		var idle: bool = c.posture == &"idle" or c.mission == null or c.mission.is_finished()
		if changed or last_order.get(c.id, -1) != type or (idle and far):
			var facing: float = FormationSolver.heading_from_vector(_enemy_side_center(tf) - ps.def.pos)
			# 같은 거점에 두 번째 중대면 옆으로 벌린다.
			var offset: Vector2 = Vector2.ZERO
			var index_here: int = 0
			for other: Company in active:
				if other == c:
					break
				if new_assign.get(other.id, &"") == pid:
					index_here += 1
			if index_here > 0:
				offset = FormationSolver.right_vector(facing) * 350.0 * (1 if index_here % 2 == 1 else -1)
			_order(c, type, ps.def.pos + offset, facing)


func _enemy_side_center(tf: TaskForce) -> Vector2:
	var setup: ScenarioDef.SideSetup = battle.scenario.sides.get(Battle.enemy_of(tf.side))
	return setup.deploy_zone.get_center() if setup != null else battle.terrain.size_m * 0.5


func _rear(tf: TaskForce) -> Vector2:
	var setup: ScenarioDef.SideSetup = battle.scenario.sides.get(tf.side)
	return setup.deploy_zone.get_center() if setup != null else tf.entry_pos


func _order(u: Unit, type: int, pos: Vector2, facing: float, params: Dictionary = {}) -> Order:
	var o: Order = Order.make(type, u.id, params)
	o.issuer = &"ai"
	o.issuer_unit_id = u.task_force().id if u.task_force() != null else -1
	o.set_pos(battle.terrain.nearest_passable(battle.terrain.clamp_to_map(pos, 40.0), Terrain.MOBILITY_VEHICLE))
	if not is_nan(facing):
		o.set_facing(facing)
	last_order[u.id] = type
	return battle.issue_order(o)


# --- 직할 ----------------------------------------------------------------

func _lead_company(tf: TaskForce) -> Company:
	var best: Company = null
	var best_v: float = -1.0
	for c: Company in tf.companies:
		if not c.on_map or c.is_destroyed():
			continue
		var pid: StringName = assignment.get(c.id, &"")
		if pid == &"" or pid == &"reserve":
			continue
		var ps: ScoringSystem.PointState = battle.scoring.get_point(pid)
		var v: float = _point_value(tf, ps) if ps != null else 0.0
		if v > best_v:
			best_v = v
			best = c
	return best


func _organic(tf: TaskForce) -> void:
	var lead: Company = _lead_company(tf)
	var enemy_dir: Vector2 = (_enemy_side_center(tf) - tf.centroid()).normalized()
	for p: Platoon in tf.organic:
		if not p.on_map or p.is_destroyed() or p.routed:
			continue
		var idle: bool = p.mission == null or p.mission.is_finished()
		match p.kind:
			&"recon":
				if lead != null and idle:
					var target: Vector2 = battle.scoring.get_point(assignment[lead.id]).def.pos
					var screen: Vector2 = target - enemy_dir * 300.0
					if p.centroid().distance_to(screen) > ARRIVE_M:
						_order(p, Order.Type.MOVE, screen, FormationSolver.heading_from_vector(enemy_dir))
			&"fo", &"ew", &"aa":
				if lead != null and idle:
					var back: float = 500.0 if p.kind == &"fo" else 900.0
					var spot: Vector2 = lead.centroid() - enemy_dir * back
					if p.centroid().distance_to(spot) > 400.0:
						_order(p, Order.Type.MOVE, spot, NAN)
			&"mortar":
				if lead != null and idle:
					var w: WeaponDef = battle.catalog.get_weapon(battle.fires._indirect_weapon(p))
					var target2: Vector2 = battle.scoring.get_point(assignment[lead.id]).def.pos
					if w != null and p.centroid().distance_to(target2) > w.range_max_m * 0.7:
						_order(p, Order.Type.MOVE, p.centroid().move_toward(target2, 1200.0), NAN)


# --- 화력 ----------------------------------------------------------------

func _fires(tf: TaskForce) -> void:
	var tick: int = battle.world.tick
	var best_pos: Vector2 = Vector2.INF
	var best_n: int = 0
	var best_tanks: int = 0
	for key: int in tf.known_enemies.keys():
		var info: Dictionary = tf.known_enemies[key]
		var t: int = info["tick"]
		if tick - t > FRESH_TICKS:
			continue
		var p: Vector2 = info["pos"]
		var n: int = 0
		var tanks: int = 0
		for key2: int in tf.known_enemies.keys():
			var info2: Dictionary = tf.known_enemies[key2]
			var t2: int = info2["tick"]
			var p2: Vector2 = info2["pos"]
			if tick - t2 <= FRESH_TICKS and p2.distance_to(p) <= CLUSTER_M:
				n += 1
				if info2.get("class", &"") == &"tank":
					tanks += 1
		if n > best_n:
			best_n = n
			best_pos = p
			best_tanks = tanks
	if best_pos == Vector2.INF:
		return
	var busy_mortar: bool = false
	for m: FireSupportSystem.Mission in battle.fires.active_missions(tf.side):
		if m.asset == FireSupportSystem.Asset.MORTAR:
			busy_mortar = true
	if not busy_mortar:
		battle.fires.request(tf.side, FireSupportSystem.Asset.MORTAR, best_pos)
	if best_n >= 3 and tf.artillery_missions > 0 and tick - _last_artillery[tf.side] > ARTILLERY_GAP_TICKS:
		var r: Dictionary = battle.fires.request(tf.side, FireSupportSystem.Asset.ARTILLERY, best_pos)
		if r.get("ok", false) == true:
			_last_artillery[tf.side] = tick
	if best_tanks >= 2 and tf.cas_sorties > 0 and tick - _last_cas[tf.side] > ARTILLERY_GAP_TICKS:
		var r2: Dictionary = battle.fires.request(tf.side, FireSupportSystem.Asset.CAS, best_pos)
		if r2.get("ok", false) == true:
			_last_cas[tf.side] = tick


# --- 보급 ----------------------------------------------------------------

func _resupply(tf: TaskForce) -> void:
	var tick: int = battle.world.tick
	for p: Platoon in tf.all_platoons():
		if not p.on_map or p.is_destroyed() or p.kind == &"supply" or p.posture == &"resupplying":
			continue
		if PlatoonAI.in_contact(p, tick) or SupplySystem.ammo_ratio(p) > LOW_AMMO:
			continue
		var o: Order = Order.make(Order.Type.RESUPPLY, p.id)
		o.issuer = &"ai"
		battle.issue_order(o)

