class_name ReinforcementSystem
extends SimSystem
## 증원 진입. PLAN 11.2.
## - 편성 때 정한 단계(1 = T+10분, 2 = T+20분)가 되면 지도 밖 제대가 진입 지점에 종대로 나타난다.
## - 소속 중대가 이미 지도 위에 있으면 소대는 중대 대형으로 합류하러 움직인다(중대 추종).
## - 중대째 들어오면 진입 지점 안쪽에 모여 명령을 기다린다(AI 진영이면 TF AI 가 임무를 준다).

const WAVE_SEC: Array[float] = [0.0, 600.0, 1200.0]
## 진입 지점에서 지도 안쪽으로 들어온 첫 위치까지 거리, 후속 제대 간격.
const ENTRY_INSET_M: float = 120.0
const COLUMN_GAP_M: float = 90.0

var battle: Battle
var arrived_waves: Dictionary[StringName, int] = {&"blue": 0, &"red": 0}


func _init(p_battle: Battle) -> void:
	battle = p_battle


func get_system_name() -> StringName:
	return &"reinforcement"


func state_hash() -> int:
	return hash([arrived_waves[&"blue"], arrived_waves[&"red"]])


static func wave_tick(wave: int) -> int:
	return roundi(WAVE_SEC[clampi(wave, 0, WAVE_SEC.size() - 1)] * SimConst.TICK_RATE)


func step(world: SimWorld) -> void:
	if world.tick % SimConst.TICK_RATE != 0:
		return
	for side: StringName in Battle.SIDES:
		var next: int = arrived_waves[side] + 1
		if next >= WAVE_SEC.size() or world.tick < wave_tick(next):
			continue
		arrived_waves[side] = next
		_bring_in(side, next, world.tick)


## 해당 단계 제대를 지도에 올린다.
func _bring_in(side: StringName, wave: int, tick: int) -> void:
	var tf: TaskForce = battle.task_forces.get(side)
	if tf == null:
		return
	var inward: Vector2 = (battle.terrain.size_m * 0.5 - tf.entry_pos).normalized()
	var heading: float = FormationSolver.heading_from_vector(inward)
	var slot: int = 0
	# 중대째 들어오는 경우 먼저(본부 + 소대), 그다음 소대 단위.
	for c: Company in tf.companies:
		if c.on_map or c.phase != wave:
			continue
		var pos: Vector2 = _entry_point(tf, inward, slot)
		slot += 3
		c.on_map = true
		c.guide.reset(pos, heading)
		for p: Platoon in c.children_units():
			if p.phase == wave:
				_place(p, pos, heading)
		_announce(c, tick)
	for p: Platoon in tf.all_platoons():
		if p.on_map or p.phase != wave:
			continue
		var pos: Vector2 = _entry_point(tf, inward, slot)
		slot += 1
		_place(p, pos, heading)
		_announce(p, tick)


func _entry_point(tf: TaskForce, inward: Vector2, slot: int) -> Vector2:
	var p: Vector2 = tf.entry_pos + inward * (ENTRY_INSET_M + slot * COLUMN_GAP_M)
	return battle.terrain.nearest_passable(battle.terrain.clamp_to_map(p, 30.0), Terrain.MOBILITY_VEHICLE)


func _place(p: Platoon, pos: Vector2, heading: float) -> void:
	p.on_map = true
	for e: Element in p.elements:
		e.on_map = true
	p.guide.reset(pos, heading)
	battle.factory.place_elements(p)
	p.posture = &"idle"


func _announce(u: Unit, tick: int) -> void:
	battle.world.events.emit_event(&"reinforcement_arrived", tick, {"unit": u.id, "side": u.side,
		"pos": u.guide.pos})
