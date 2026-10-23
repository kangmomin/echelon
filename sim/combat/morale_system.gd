class_name MoraleSystem
extends SimSystem
## 제압 감소와 사기. PLAN 9.3, 9.4.
## - 제압: 초당 2 감소(소대장이 살아 있으면 3).
## - 사기(소대 0~100): 엘리먼트 손실 -15(같은 중대 다른 소대 -3), 손상 -5, 인원 손실 1명당 -1.5, 지휘관 손실 -10,
##   통신 두절 중 초당 -0.5(30 아래로는 안 내려감), 접촉 없이 30초 지나면 초당 +0.3, 적 격파 +3.
## - 40 이하: 공격 명령 거부. 20 이하: 패주(60초간 후방으로, 명령 불가) 후 사기 30 으로 회복.
## - 중대 사기 = 소대 사기 평균.

const SUPPRESSION_DECAY: float = 2.0
const SUPPRESSION_DECAY_LED: float = 3.0
const LOSS_PLATOON: float = 15.0
const LOSS_COMPANY: float = 3.0
const DAMAGE_PLATOON: float = 5.0
const CASUALTY_PER_MAN: float = 1.5
const COMMANDER_LOSS: float = 10.0
const COMMS_CUT_PER_SEC: float = 0.5
const COMMS_CUT_FLOOR: float = 30.0
const RECOVERY_PER_SEC: float = 0.3
const RECOVERY_QUIET_TICKS: int = 300
const KILL_BONUS: float = 3.0
const SHAKEN: float = 40.0
const ROUT: float = 20.0
const ROUT_TICKS: int = 600
const AFTER_ROUT: float = 30.0
const UPDATE_INTERVAL: int = 10

var battle: Battle


func _init(p_battle: Battle) -> void:
	battle = p_battle


func get_system_name() -> StringName:
	return &"morale"


func state_hash() -> int:
	var h: int = 0
	for u: Unit in battle.units:
		h = hash([h, roundi(u.morale * 10.0)])
	return h


func step(world: SimWorld) -> void:
	var dt: float = SimConst.TICK_DT
	for e: Element in battle.elements:
		if e.suppression <= 0.0 or not e.is_alive():
			continue
		var led: bool = e.platoon != null and e.platoon.leader() != null
		e.suppression = maxf(0.0, e.suppression - (SUPPRESSION_DECAY_LED if led else SUPPRESSION_DECAY) * dt)
	if world.tick % UPDATE_INTERVAL != 0:
		return
	var sec: float = UPDATE_INTERVAL * dt
	for side: StringName in Battle.SIDES:
		var tf: TaskForce = battle.task_forces.get(side)
		if tf == null:
			continue
		for p: Platoon in tf.all_platoons():
			if p.is_destroyed() or not p.on_map:
				continue
			if p.comms_state == Unit.Comms.CUT and p.morale > COMMS_CUT_FLOOR:
				p.morale = maxf(COMMS_CUT_FLOOR, p.morale - COMMS_CUT_PER_SEC * sec)
			elif world.tick - p.last_contact_tick > RECOVERY_QUIET_TICKS and world.tick - p.last_hit_tick > RECOVERY_QUIET_TICKS:
				p.morale = minf(100.0, p.morale + RECOVERY_PER_SEC * sec)
			_check_rout(p, world.tick)
		for c: Company in tf.companies:
			c.morale = _average_morale(c)


func _average_morale(c: Company) -> float:
	var total: float = 0.0
	var n: int = 0
	for p: Platoon in c.platoons:
		if not p.is_destroyed():
			total += p.morale
			n += 1
	return total / n if n > 0 else 0.0


func _check_rout(p: Platoon, tick: int) -> void:
	if p.routed:
		if tick >= p.routed_until_tick:
			p.routed = false
			p.morale = maxf(p.morale, AFTER_ROUT)
			p.posture = &"idle"
			battle.world.events.emit_event(&"rally", tick, {"unit": p.id, "side": p.side})
		return
	if p.morale <= ROUT:
		p.routed = true
		p.routed_until_tick = tick + ROUT_TICKS
		p.posture = &"routed"
		battle.world.events.emit_event(&"routed", tick, {"unit": p.id, "side": p.side})


static func is_shaken(u: Unit) -> bool:
	return u.morale <= SHAKEN


# --- 전투 시스템이 부르는 훅 ------------------------------------------------

func on_element_lost(e: Element) -> void:
	var p: Platoon = e.platoon
	if p == null:
		return
	p.morale = maxf(0.0, p.morale - LOSS_PLATOON)
	var c: Company = p.company()
	if c != null:
		for other: Platoon in c.platoons:
			if other != p:
				other.morale = maxf(0.0, other.morale - LOSS_COMPANY)
	var killer: Element = battle.get_element(e.killed_by)
	if killer != null and killer.platoon != null:
		killer.platoon.morale = minf(100.0, killer.platoon.morale + KILL_BONUS)


func on_element_damaged(e: Element) -> void:
	if e.platoon != null:
		e.platoon.morale = maxf(0.0, e.platoon.morale - DAMAGE_PLATOON)


func on_casualties(p: Platoon, men: int) -> void:
	if p != null:
		p.morale = maxf(0.0, p.morale - CASUALTY_PER_MAN * men)


func on_commander_lost(u: Unit) -> void:
	if u is Platoon:
		u.morale = maxf(0.0, u.morale - COMMANDER_LOSS)
	elif u is Company:
		for p: Platoon in (u as Company).platoons:
			p.morale = maxf(0.0, p.morale - COMMANDER_LOSS)
