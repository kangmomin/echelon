class_name SuccessionSystem
extends SimSystem
## 지휘관 손실과 승계. PLAN 5.1.
## - 중대장 손실 → XO 가 60~90초 뒤 승계. XO 도 없으면 선임 소대장이 2배 시간(120~180초) 뒤 승계, 이후 명령 지연 +50%.
## - 승계 중(지휘 공백): 새 명령 수신 불가, 마지막 명령 유지.
## - 소대장 손실 → 다음 엘리먼트가 20초 뒤 승계.
## - 대대 지휘소 손실 → 60초 지휘 공백 후 선임 중대장이 대대 지휘, 지휘 용량 영구 50%.

const CHECK_INTERVAL: int = 10
const XO_MIN_TICKS: int = 600
const XO_MAX_TICKS: int = 900
const SENIOR_MULT: int = 2
const SENIOR_DELAY_MULT: float = 1.5
const PLATOON_TICKS: int = 200
const TF_TICKS: int = 600

var battle: Battle


func _init(p_battle: Battle) -> void:
	battle = p_battle


func get_system_name() -> StringName:
	return &"succession"


func step(world: SimWorld) -> void:
	if world.tick % CHECK_INTERVAL != 0:
		return
	for side: StringName in Battle.SIDES:
		var tf: TaskForce = battle.task_forces.get(side)
		if tf == null:
			continue
		for u: Unit in tf.all_units():
			if u.is_destroyed() and u.level != Unit.Level.TASK_FORCE:
				continue
			_check(u, world.tick)


func state_hash() -> int:
	var h: int = 0
	for u: Unit in battle.units:
		h = hash([h, u.succession_until_tick, u.commander.id if u.commander != null else -1])
	return h


func _check(u: Unit, tick: int) -> void:
	# 진행 중인 승계 완료.
	if u.succession_until_tick >= 0:
		if tick < u.succession_until_tick:
			return
		var s: Element = u.pending_successor
		u.succession_until_tick = -1
		u.pending_successor = null
		if s != null and s.is_alive():
			u.commander = s
			battle.world.events.emit_event(&"succession_done", tick, {"unit": u.id, "side": u.side, "commander": s.id})
			return
		# 후계자도 죽었으면 아래에서 다시 시작.
	if u.commander != null and u.commander.is_alive():
		return
	var successor: Element = null
	var wait: int = 0
	match u.level:
		Unit.Level.PLATOON:
			successor = _first_alive((u as Platoon).elements)
			wait = PLATOON_TICKS
		Unit.Level.COMPANY:
			var c: Company = u
			if c.xo != null and c.xo.is_alive() and c.xo != c.commander:
				successor = c.xo
				wait = battle.world.rng.next_int(XO_MIN_TICKS, XO_MAX_TICKS)
			else:
				for p: Platoon in c.platoons:
					var l: Element = p.leader()
					if l != null:
						successor = l
						break
				wait = battle.world.rng.next_int(XO_MIN_TICKS, XO_MAX_TICKS) * SENIOR_MULT
				u.succession_delay_mult = SENIOR_DELAY_MULT
		Unit.Level.TASK_FORCE:
			var tf: TaskForce = u
			tf.cp_lost = true
			for c: Company in tf.companies:
				var l: Element = c.leader()
				if l != null:
					successor = l
					break
			if successor == null:
				successor = _first_alive(tf.all_elements())
			wait = TF_TICKS
	if successor == null:
		return
	var lost_id: int = u.commander.id if u.commander != null else -1
	u.commander = null
	u.pending_successor = successor
	u.succession_until_tick = tick + wait
	battle.world.events.emit_event(&"commander_lost", tick, {"unit": u.id, "side": u.side, "element": lost_id,
		"seconds": wait / float(SimConst.TICK_RATE)})


static func _first_alive(elems: Array[Element]) -> Element:
	for e: Element in elems:
		if e.is_alive():
			return e
	return null
