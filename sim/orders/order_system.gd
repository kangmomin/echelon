class_name OrderSystem
extends SimSystem
## 명령 전파. PLAN 8.1, 8.3, 17.2.
##
## 1) 플레이어·AI 명령은 발행 진영 대대(TF)의 지휘 용량을 쓴다. 모자라면 TF 대기열에서 순서대로 기다린다.
## 2) 제대 트리를 따라 hop 을 만든다. 대대→중대 15초, 중대→소대 10초, 대대→소대(직접·직할) 20초/15초,
##    소대→엘리먼트(실행 준비) 5초. hop 마다 × 통신 품질 × 숙련 × 상태 배율, 중계 1번당 +10초.
## 3) hop 시작 시점에 통신이 끊겼거나 받는 제대가 지휘 공백이면 기다린다. 120초 넘게 막히면 취소.
## 4) 마지막 hop 이 끝나면 실행기(OrderExecutor)가 실행한다.

const BASE_TF_TO_CO_SEC: float = 15.0
const BASE_CO_TO_PLT_SEC: float = 10.0
const BASE_TF_TO_PLT_BYPASS_SEC: float = 20.0
const BASE_TF_TO_ORGANIC_SEC: float = 15.0
const BASE_EXECUTION_SEC: float = 5.0
## 통신 두절로 이만큼(틱) 넘게 막히면 취소. PLAN 8.1.
const BLOCK_TIMEOUT_TICKS: int = 1200
## 막힌 hop 을 다시 시도하는 주기(틱).
const RETRY_INTERVAL: int = 10
## 중대 지휘를 잃은 소대에 번역 명령을 다시 보내는 주기(틱).
const REBRIEF_INTERVAL: int = 50
## 지휘 용량 비용. PLAN 8.3.
const COST_MISSION: float = 1.0
const COST_BYPASS: float = 2.0
const COST_FIRE: float = 1.0
const COST_SOP: float = 0.5
const COST_FREE: float = 0.0
## 상태 배율. PLAN 8.1.
const K_SUPPRESSED: float = 1.5
const K_INEFFECTIVE: float = 2.0
const K_AD_HOC: float = 1.2

var battle: Battle
var comms: CommsNet
## 전파 중인 명령(발행 순서).
var _active: Array[Order] = []
## 진영별 지휘 용량 대기열.
var _capacity_queue: Dictionary[StringName, Array] = {}
## 진영별 남은 지휘 용량(토큰). 분당 용량 비율로 계속 차오른다(DECISIONS D-014).
var capacity: Dictionary[StringName, float] = {}
## 통계(AAR): 완료된 명령의 발행→실행 지연(초).
var delay_samples: PackedFloat32Array = PackedFloat32Array()
var cancelled_count: int = 0


func _init(p_battle: Battle, p_comms: CommsNet) -> void:
	battle = p_battle
	comms = p_comms
	for side: StringName in Battle.SIDES:
		_capacity_queue[side] = []
		capacity[side] = -1.0


func get_system_name() -> StringName:
	return &"orders"


func state_hash() -> int:
	return hash([_active.size(), delay_samples.size(), cancelled_count])


# --- 공개 API ------------------------------------------------------------

## Battle.issue_order() 가 부른다. 비용을 매기고 용량이 있으면 전파를 시작한다.
func submit(order: Order) -> void:
	var unit: Unit = battle.get_unit(order.target_unit_id)
	if unit == null or unit.task_force() == null:
		order.state = Order.State.CANCELLED
		return
	var tf: TaskForce = unit.task_force()
	if order.issuer_unit_id < 0:
		order.issuer_unit_id = tf.id
	if order.issuer != &"unit":
		order.bypass = is_bypass(order, unit)
		order.cost = cost_of(order, unit)
	_ensure_capacity(tf)
	if order.cost > 0.0 and (capacity[tf.side] < order.cost or not (_capacity_queue[tf.side] as Array).is_empty()):
		order.state = Order.State.QUEUED
		(_capacity_queue[tf.side] as Array).append(order)
		battle.world.events.emit_event(&"order_waiting_capacity", battle.world.tick, {"order": order.id, "side": tf.side})
		return
	capacity[tf.side] -= order.cost
	_start(order, unit)


## 상위 제대가 하위 제대에 내리는 번역 명령(비용 없음).
func submit_sub_order(order: Order, from_unit: Unit) -> void:
	order.issuer = &"unit"
	order.issuer_unit_id = from_unit.id
	order.cost = 0.0
	battle.register_order(order)
	var unit: Unit = battle.get_unit(order.target_unit_id)
	if unit == null:
		order.state = Order.State.CANCELLED
		return
	_start(order, unit)


func cost_of(order: Order, unit: Unit) -> float:
	match order.type:
		Order.Type.SET_SOP, Order.Type.SET_FORMATION, Order.Type.HALT, Order.Type.DETACH, Order.Type.REJOIN, \
				Order.Type.MOUNT, Order.Type.DISMOUNT:
			return COST_SOP
		Order.Type.CANCEL:
			return COST_FREE
		Order.Type.CALL_FIRE:
			return COST_FIRE
	if is_bypass(order, unit):
		return COST_BYPASS
	return COST_MISSION


## 중대 대형 안 소대에 이동 계열 명령을 직접 내리는 경우.
static func is_bypass(order: Order, unit: Unit) -> bool:
	return order.bypass or (order.is_movement() and unit is Platoon and (unit as Platoon).follows_parent()
		and order.issuer != &"unit")


## 이 제대를 향해 전파 중이거나 용량을 기다리는 명령(표시용).
func pending_for_unit(unit_id: int) -> Array:
	var out: Array = []
	for o: Order in _active:
		if o.target_unit_id == unit_id and o.issuer != &"unit":
			out.append(o)
	for side: StringName in Battle.SIDES:
		for item: Variant in _capacity_queue[side]:
			var o: Order = item
			if o.target_unit_id == unit_id:
				out.append(o)
	return out


func active_count() -> int:
	return _active.size()


func capacity_queue_size(side: StringName) -> int:
	return (_capacity_queue[side] as Array).size()


func capacity_max(side: StringName) -> float:
	var tf: TaskForce = battle.task_forces.get(side)
	if tf == null:
		return 0.0
	return tf.command_capacity_per_min * (0.5 if tf.leader() == null or tf.cp_lost else 1.0)


## 명령을 지금 내리면 실행까지 대략 몇 초 걸릴지(미리보기). 통신 두절이면 -1.
func estimate_delay_sec(unit: Unit, order_type: int, bypass: bool) -> float:
	var probe: Order = Order.make(order_type, unit.id)
	probe.bypass = bypass
	var hops: Array[Dictionary] = _plan_hops(probe, unit)
	var total: float = 0.0
	for h: Dictionary in hops:
		var est: float = _hop_seconds(h)
		if est < 0.0:
			return -1.0
		total += est
	var tf: TaskForce = unit.task_force()
	if tf != null:
		_ensure_capacity(tf)
		var need: float = cost_of(probe, unit) - capacity[tf.side]
		if need > 0.0:
			total += need / (capacity_max(tf.side) / 60.0)
	return total


# --- 틱 ------------------------------------------------------------------

func step(world: SimWorld) -> void:
	for side: StringName in Battle.SIDES:
		var tf: TaskForce = battle.task_forces.get(side)
		if tf == null:
			continue
		_ensure_capacity(tf)
		var cap: float = capacity_max(side)
		capacity[side] = minf(cap, capacity[side] + cap / 60.0 * SimConst.TICK_DT)
		var queue: Array = _capacity_queue[side]
		while not queue.is_empty():
			var o: Order = queue[0]
			if o.state == Order.State.CANCELLED:
				queue.pop_front()
				continue
			if capacity[side] < o.cost:
				break
			queue.pop_front()
			capacity[side] -= o.cost
			var u: Unit = battle.get_unit(o.target_unit_id)
			if u != null:
				_start(o, u)
	var still: Array[Order] = []
	for o: Order in _active:
		if _advance(o, world.tick):
			still.append(o)
	_active = still
	if world.tick % REBRIEF_INTERVAL == 0:
		_rebrief_orphans()


## 번역 명령이 취소돼(통신 두절) 중대 지휘를 기다리는 소대에 다시 보낸다(통신이 돌아오면 닿는다).
func _rebrief_orphans() -> void:
	var pending: Dictionary = {}
	for o: Order in _active:
		if o.type == Order.Type.FOLLOW:
			pending[o.target_unit_id] = true
	for side: StringName in Battle.SIDES:
		var tf: TaskForce = battle.task_forces.get(side)
		if tf == null:
			continue
		for c: Company in tf.companies:
			for p: Unit in c.children_units():
				if p.awaiting_follow and not pending.has(p.id) and not p.is_destroyed():
					var sub: Order = Order.make(Order.Type.FOLLOW, p.id)
					submit_sub_order(sub, c)


# --- 내부 ----------------------------------------------------------------

func _ensure_capacity(tf: TaskForce) -> void:
	if capacity[tf.side] < 0.0:
		capacity[tf.side] = capacity_max(tf.side)


func _start(order: Order, unit: Unit) -> void:
	order.state = Order.State.PROPAGATING
	order.hops = _plan_hops(order, unit)
	order.hop_index = 0
	order.blocked_since_tick = -1
	_active.append(order)


## 발행 제대 → 대상 제대까지 hop 목록. 각 hop: {"from", "to", "base", "start", "arrive", "relays"}
func _plan_hops(order: Order, unit: Unit) -> Array[Dictionary]:
	var hops: Array[Dictionary] = []
	var issuer: Unit = battle.get_unit(order.issuer_unit_id) if order.issuer_unit_id >= 0 else unit.task_force()
	if issuer == null:
		issuer = unit.task_force()
	if issuer != unit:
		if issuer.level == Unit.Level.TASK_FORCE and unit.level == Unit.Level.PLATOON:
			var p: Platoon = unit
			if is_bypass(order, unit):
				hops.append(_hop(issuer, unit, BASE_TF_TO_PLT_BYPASS_SEC))
			elif p.parent != null and p.parent.level == Unit.Level.COMPANY:
				hops.append(_hop(issuer, p.parent, BASE_TF_TO_CO_SEC))
				hops.append(_hop(p.parent, unit, BASE_CO_TO_PLT_SEC))
			else:
				hops.append(_hop(issuer, unit, BASE_TF_TO_ORGANIC_SEC))
		elif issuer.level == Unit.Level.TASK_FORCE and unit.level == Unit.Level.COMPANY:
			hops.append(_hop(issuer, unit, BASE_TF_TO_CO_SEC))
		elif issuer.level == Unit.Level.COMPANY and unit.level == Unit.Level.PLATOON:
			hops.append(_hop(issuer, unit, BASE_CO_TO_PLT_SEC))
		else:
			hops.append(_hop(issuer, unit, BASE_CO_TO_PLT_SEC))
	if unit.level == Unit.Level.PLATOON and _needs_execution_delay(order):
		hops.append(_hop(unit, unit, BASE_EXECUTION_SEC))
	return hops


func _needs_execution_delay(order: Order) -> bool:
	return order.is_movement() or order.type == Order.Type.FOLLOW or order.type == Order.Type.MOUNT \
		or order.type == Order.Type.DISMOUNT


func _hop(from_unit: Unit, to_unit: Unit, base: float) -> Dictionary:
	return {"from": from_unit.id, "to": to_unit.id, "base": base, "start": -1, "arrive": -1, "relays": 0}


## 한 틱 진행. 아직 전파 중이면 true.
func _advance(o: Order, tick: int) -> bool:
	if o.state == Order.State.CANCELLED:
		return false
	while o.hop_index < o.hops.size():
		var h: Dictionary = o.hops[o.hop_index]
		var start: int = h["start"]
		if start < 0:
			if not _try_start_hop(o, h, tick):
				if o.blocked_since_tick >= 0 and tick - o.blocked_since_tick > BLOCK_TIMEOUT_TICKS:
					_cancel(o, "통신 두절")
					return false
				return true
			continue
		var arrive: int = h["arrive"]
		if tick < arrive:
			return true
		o.hop_index += 1
	_deliver(o, tick)
	return false


func _try_start_hop(o: Order, h: Dictionary, tick: int) -> bool:
	if o.blocked_since_tick >= 0 and (tick - o.blocked_since_tick) % RETRY_INTERVAL != 0:
		return false
	var sec: float = _hop_seconds(h)
	if sec < 0.0:
		if o.blocked_since_tick < 0:
			o.blocked_since_tick = tick
			battle.world.events.emit_event(&"order_blocked", tick, {"order": o.id, "unit": o.target_unit_id})
		return false
	o.blocked_since_tick = -1
	h["start"] = tick
	h["arrive"] = tick + roundi(sec * SimConst.TICK_RATE)
	return true


## hop 소요 시간(초). 지금 전달할 수 없으면 -1.
func _hop_seconds(h: Dictionary) -> float:
	var from_id: int = h["from"]
	var to_id: int = h["to"]
	var base: float = h["base"]
	var from_unit: Unit = battle.get_unit(from_id)
	var to_unit: Unit = battle.get_unit(to_id)
	if from_unit == null or to_unit == null or to_unit.is_destroyed():
		return -1.0
	var tick: int = battle.world.tick
	if to_unit.is_leaderless(tick) or from_unit.is_leaderless(tick):
		return -1.0
	var k_comms: float = 1.0
	var relays: int = 0
	if from_unit != to_unit:
		var link: CommsNet.Link = comms.link_between(from_unit, to_unit)
		if not link.ok:
			return -1.0
		k_comms = CommsNet.delay_factor(link.quality)
		relays = link.relays
	h["relays"] = relays
	return base * k_comms * to_unit.skill_delay_factor() * _state_factor(to_unit) + relays * CommsNet.RELAY_DELAY_SEC


func _state_factor(u: Unit) -> float:
	var k: float = 1.0
	if _avg_suppression(u) >= 50.0:
		k *= K_SUPPRESSED
	if u.level != Unit.Level.TASK_FORCE and u.is_combat_ineffective():
		k *= K_INEFFECTIVE
	if u.ad_hoc:
		k *= K_AD_HOC
	return k


func _avg_suppression(u: Unit) -> float:
	var total: float = 0.0
	var n: int = 0
	for e: Element in u.alive_elements():
		total += e.suppression
		n += 1
	return total / n if n > 0 else 0.0


func _deliver(o: Order, tick: int) -> void:
	var unit: Unit = battle.get_unit(o.target_unit_id)
	if unit == null or unit.is_destroyed():
		o.state = Order.State.CANCELLED
		return
	o.state = Order.State.RECEIVED
	o.received_tick = tick
	if o.issuer != &"unit":
		delay_samples.append((tick - o.tick_issued) / float(SimConst.TICK_RATE))
	battle.world.events.emit_event(&"order_received", tick, {"order": o.id, "unit": unit.id, "type": o.type_name()})
	battle.executor.execute(unit, o)


func _cancel(o: Order, reason: String) -> void:
	o.state = Order.State.CANCELLED
	cancelled_count += 1
	battle.world.events.emit_event(&"order_cancelled", battle.world.tick,
		{"order": o.id, "unit": o.target_unit_id, "reason": reason})
