class_name CommsNet
extends SimSystem
## 지휘망. 2초(20틱)마다 진영별로 지휘관 엘리먼트 사이 무전 연결 그래프를 다시 만든다. PLAN 8.2, 17.2-7.
##
## - 노드: 지도 위 모든 제대의 지휘관 엘리먼트(대대 지휘소·중대장·소대장 …). 다른 지휘관을 거쳐 중계할 수 있다.
## - 간선 품질(발신자 무전 범위 기준): 유효 범위의 60% 안 = 1.0, 범위 끝 = 0.3, 밖 = 연결 없음.
## - 지형 감쇠: 숲 -20%, 시가지 -30%(양 끝 중 하나라도), 능선 차폐(사이 최고 지면이 양 끝보다 40 m 높음) -50%.
## - 재머: 적 전자전 엘리먼트 반경 1.5 km 안의 수신자는 품질 × 0.3.
## - 중계: 직결이 없으면 같은 무전망 안의 다른 지휘관을 거친다(최대 3번). 중계 1번마다 +10초.
##   중대망 = 중대장·중대본부·그 중대 소대장들, 대대망 = 대대 지휘소·중대장들·직할 소대장들(DECISIONS D-015).

const UPDATE_INTERVAL: int = 20
const FULL_QUALITY_RANGE: float = 0.6
const EDGE_QUALITY: float = 0.3
const FOREST_ATTEN: float = 0.8
const URBAN_ATTEN: float = 0.7
const RIDGE_ATTEN: float = 0.5
const RIDGE_CLEARANCE_M: float = 40.0
const RIDGE_SAMPLES: int = 12
const JAM_RADIUS_M: float = 1500.0
const JAM_FACTOR: float = 0.3
const MAX_RELAYS: int = 3
const RELAY_DELAY_SEC: float = 10.0
const GOOD_THRESHOLD: float = 0.7


class Link:
	extends RefCounted
	var ok: bool = false
	var quality: float = 0.0
	var relays: int = 0


var battle: Battle
## side → 노드(지휘관 엘리먼트) 목록
var _nodes: Dictionary[StringName, Array] = {}
## side → {from_element_id: {to_element_id: quality}}
var _edges: Dictionary[StringName, Dictionary] = {}
var _jammers: Dictionary[StringName, Array] = {}
var _built_tick: int = -1


func _init(p_battle: Battle) -> void:
	battle = p_battle


func get_system_name() -> StringName:
	return &"comms"


func step(world: SimWorld) -> void:
	if _built_tick < 0 or world.tick - _built_tick >= UPDATE_INTERVAL:
		rebuild()
		_update_unit_states()


func state_hash() -> int:
	var h: int = 0
	for u: Unit in battle.units:
		h = hash([h, u.comms_state, snappedf(u.comms_quality, 0.01)])
	return h


## 그래프를 지금 다시 만든다(테스트·명령 전파 직전에 부를 수 있다).
func rebuild() -> void:
	_built_tick = battle.world.tick
	for side: StringName in Battle.SIDES:
		var nodes: Array = []
		var tf: TaskForce = battle.task_forces.get(side)
		if tf != null:
			for u: Unit in tf.all_units():
				var c: Element = u.leader()
				if c != null and c.is_active() and not nodes.has(c):
					nodes.append(c)
		_nodes[side] = nodes
		var jam: Array = []
		for e: Element in battle.elements:
			if e.side != side and e.is_active() and e.def.element_class == &"ew":
				jam.append(e)
		_jammers[side] = jam
		var edges: Dictionary = {}
		for a: Variant in nodes:
			var ea: Element = a
			var row: Dictionary = {}
			for b: Variant in nodes:
				var eb: Element = b
				if ea == eb:
					continue
				var q: float = link_quality(ea, eb)
				if q > 0.0:
					row[eb.id] = q
			edges[ea.id] = row
		_edges[side] = edges


## sender → receiver 직결 품질(0 = 연결 없음).
func link_quality(sender: Element, receiver: Element) -> float:
	var d: float = sender.pos.distance_to(receiver.pos)
	var r: float = sender.def.radio_range_m * _attenuation(sender, receiver)
	if d > r:
		return 0.0
	var q: float = 1.0
	if d > r * FULL_QUALITY_RANGE:
		q = lerpf(1.0, EDGE_QUALITY, (d - r * FULL_QUALITY_RANGE) / (r * (1.0 - FULL_QUALITY_RANGE)))
	return q * _jam_factor(receiver)


## 상위 제대 지휘관 → 하위 제대 지휘관 연결. 직결이 없으면 중계 경로를 찾는다.
func link_between(from_unit: Unit, to_unit: Unit) -> Link:
	var link := Link.new()
	var a: Element = from_unit.leader()
	var b: Element = to_unit.leader()
	if a == null or b == null or not a.is_active() or not b.is_active():
		return link
	if a == b:
		link.ok = true
		link.quality = 1.0
		return link
	var edges: Dictionary = _edges.get(from_unit.side, {})
	var row_a: Dictionary = edges.get(a.id, {})
	var direct: float = row_a.get(b.id, 0.0)
	if direct > 0.0:
		link.ok = true
		link.quality = direct
		return link
	# BFS 중계(노드 순서가 결정론적이므로 결과도 결정론적). 같은 무전망 구성원만 중계한다.
	var net: Dictionary = _net_members(from_unit)
	net[b.id] = true
	var best_q: Dictionary = {a.id: 1.0}
	var frontier: Array[int] = [a.id]
	for depth: int in MAX_RELAYS + 1:
		var next: Array[int] = []
		for nid: int in frontier:
			var row: Dictionary = edges.get(nid, {})
			for key: Variant in row.keys():
				var mid: int = key
				if not net.has(mid):
					continue
				var q_prev: float = best_q[nid]
				var q_edge: float = row[key]
				var q: float = minf(q_prev, q_edge)
				if mid == b.id:
					link.ok = true
					link.quality = q
					link.relays = depth
					return link
				if not best_q.has(mid):
					best_q[mid] = q
					next.append(mid)
		frontier = next
	return link


## 발신 제대가 속한 무전망의 지휘관 엘리먼트 id 집합.
func _net_members(from_unit: Unit) -> Dictionary:
	var out: Dictionary = {}
	var members: Array[Unit] = []
	if from_unit.level == Unit.Level.COMPANY:
		members.append(from_unit)
		members.append_array(from_unit.children_units())
	else:
		var tf: TaskForce = from_unit.task_force()
		if tf != null:
			members.append(tf)
			members.append_array(tf.children_units())
	for u: Unit in members:
		var l: Element = u.leader()
		if l != null:
			out[l.id] = true
	return out


## 명령 지연 배율 k_comms: 품질 1.0 → 1.0, 품질 0 → 2.0. PLAN 8.1.
static func delay_factor(quality: float) -> float:
	return clampf(2.0 - quality, 1.0, 2.0)


static func state_of(link: Link) -> int:
	if not link.ok:
		return Unit.Comms.CUT
	if link.quality >= GOOD_THRESHOLD and link.relays == 0:
		return Unit.Comms.GOOD
	return Unit.Comms.POOR


## 제대별 통신 상태(상위 지휘관과의 연결)를 갱신하고, 바뀌면 이벤트를 낸다.
func _update_unit_states() -> void:
	for u: Unit in battle.units:
		if u.level == Unit.Level.TASK_FORCE or not u.on_map:
			continue
		var sup: Unit = superior_of(u)
		if sup == null:
			continue
		var link: Link = link_between(sup, u)
		var old: int = u.comms_state
		u.comms_quality = link.quality
		u.comms_hops = link.relays + 1
		u.comms_state = state_of(link)
		if old != u.comms_state and (old == Unit.Comms.CUT or u.comms_state == Unit.Comms.CUT):
			battle.world.events.emit_event(&"comms_changed", battle.world.tick,
				{"unit": u.id, "side": u.side, "state": u.comms_state})


## 명령 계통상 바로 위 제대. 중대 안 소대 = 중대(분리돼도 지휘 계통은 중대), 중대·직할 소대 = 대대.
static func superior_of(u: Unit) -> Unit:
	return u.parent


func _attenuation(a: Element, b: Element) -> float:
	var t: Terrain = battle.terrain
	var f: float = 1.0
	var ta: int = t.type_at(a.pos)
	var tb: int = t.type_at(b.pos)
	if ta == Terrain.Type.FOREST or tb == Terrain.Type.FOREST:
		f *= FOREST_ATTEN
	if ta == Terrain.Type.URBAN or tb == Terrain.Type.URBAN:
		f *= URBAN_ATTEN
	var ha: float = t.ground_height(a.pos)
	var hb: float = t.ground_height(b.pos)
	var top: float = maxf(ha, hb)
	for i: int in range(1, RIDGE_SAMPLES):
		if t.ground_height(a.pos.lerp(b.pos, float(i) / RIDGE_SAMPLES)) > top + RIDGE_CLEARANCE_M:
			f *= RIDGE_ATTEN
			break
	return f


func _jam_factor(receiver: Element) -> float:
	for j: Variant in _jammers.get(receiver.side, []):
		var je: Element = j
		if je.is_active() and je.pos.distance_to(receiver.pos) <= JAM_RADIUS_M:
			return JAM_FACTOR
	return 1.0
