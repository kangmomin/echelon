class_name Order
extends RefCounted
## 명령 객체. 플레이어·AI·상위 제대가 발행하고, 지휘망을 따라 hop 단위로 전파된다. PLAN 16.5, 17.2.
## params 에는 직렬화 가능한 값만 넣는다(명령 로그·세이브에 그대로 쓴다).

enum Type {
	MOVE, FAST_MOVE, ATTACK_MOVE, SEIZE, DEFEND, WITHDRAW, RESUPPLY, MOUNT, DISMOUNT,
	SET_FORMATION, SET_SOP, CALL_FIRE, DETACH, REJOIN, REORGANIZE, CANCEL, FOLLOW, HALT,
	LAY_MINES, CLEAR_MINES, FORTIFY,
}
enum State { QUEUED, PROPAGATING, RECEIVED, EXECUTING, DONE, CANCELLED }

const TYPE_NAMES: Array[String] = [
	"MOVE", "FAST_MOVE", "ATTACK_MOVE", "SEIZE", "DEFEND", "WITHDRAW", "RESUPPLY", "MOUNT", "DISMOUNT",
	"SET_FORMATION", "SET_SOP", "CALL_FIRE", "DETACH", "REJOIN", "REORGANIZE", "CANCEL", "FOLLOW", "HALT",
	"LAY_MINES", "CLEAR_MINES", "FORTIFY",
]
const STATE_NAMES: Array[String] = ["QUEUED", "PROPAGATING", "RECEIVED", "EXECUTING", "DONE", "CANCELLED"]
## 이동을 동반하는 명령.
const MOVEMENT_TYPES: Array[int] = [Type.MOVE, Type.FAST_MOVE, Type.ATTACK_MOVE, Type.SEIZE, Type.DEFEND, Type.WITHDRAW,
	Type.LAY_MINES, Type.CLEAR_MINES, Type.FORTIFY]
## 공병 작업 명령(지점까지 이동한 뒤 작업).
const ENGINEER_TYPES: Array[int] = [Type.LAY_MINES, Type.CLEAR_MINES, Type.FORTIFY]

var id: int = 0
var tick_issued: int = 0
## &"player", &"ai", 또는 &"unit"(상위 제대가 번역해서 낸 하위 명령).
var issuer: StringName = &"player"
## 발행한 제대 id(플레이어·AI 명령이면 그 진영 TF id).
var issuer_unit_id: int = -1
var target_unit_id: int = -1
var type: int = Type.MOVE
var params: Dictionary = {}
## [{"from": unit_id, "to": unit_id, "arrive_tick": int}]
var hops: Array[Dictionary] = []
var hop_index: int = 0
var state: int = State.QUEUED
## 하위 명령이면 원 명령 id.
var parent_order_id: int = -1
## 지휘 용량 비용.
var cost: float = 0.0
## 통신 두절로 대기를 시작한 틱(-1 = 대기 아님).
var blocked_since_tick: int = -1
## 플레이어가 소대에 직접 내린 명령(bypass)인지.
var bypass: bool = false
var received_tick: int = -1


static func make(p_type: int, target: int, p_params: Dictionary = {}) -> Order:
	var o := Order.new()
	o.type = p_type
	o.target_unit_id = target
	o.params = p_params
	return o


func type_name() -> String:
	return TYPE_NAMES[type]


func state_name() -> String:
	return STATE_NAMES[state]


func is_movement() -> bool:
	return MOVEMENT_TYPES.has(type)


func is_finished() -> bool:
	return state == State.DONE or state == State.CANCELLED


func has_pos() -> bool:
	return params.has("pos")


func get_pos() -> Vector2:
	var v: Variant = params.get("pos", [0.0, 0.0])
	if typeof(v) == TYPE_VECTOR2:
		return v
	var arr: Array = v
	return Vector2(DefReader.to_float(arr[0]), DefReader.to_float(arr[1]))


func set_pos(p: Vector2) -> void:
	params["pos"] = [p.x, p.y]


## 도착 방향(라디안). 없으면 NAN.
func get_facing() -> float:
	if not params.has("facing"):
		return NAN
	return DefReader.to_float(params["facing"])


func set_facing(heading: float) -> void:
	params["facing"] = heading


func get_string(key: String, default: String = "") -> String:
	return str(params.get(key, default))


## 마지막 hop 의 도착 예정 틱(전파 미리보기용).
func eta_tick() -> int:
	if hops.is_empty():
		return tick_issued
	return hops[hops.size() - 1]["arrive_tick"]


func to_dict() -> Dictionary:
	return {
		"id": id, "tick": tick_issued, "issuer": String(issuer), "issuer_unit": issuer_unit_id,
		"target": target_unit_id, "type": type_name(), "params": params.duplicate(true),
		"bypass": bypass,
	}


static func from_dict(d: Dictionary) -> Order:
	var o := Order.new()
	o.id = int(DefReader.to_float(d.get("id", 0)))
	o.tick_issued = int(DefReader.to_float(d.get("tick", 0)))
	o.issuer = StringName(str(d.get("issuer", "player")))
	o.issuer_unit_id = int(DefReader.to_float(d.get("issuer_unit", -1)))
	o.target_unit_id = int(DefReader.to_float(d.get("target", -1)))
	o.type = maxi(0, TYPE_NAMES.find(str(d.get("type", "MOVE"))))
	var p: Variant = d.get("params", {})
	o.params = p if typeof(p) == TYPE_DICTIONARY else {}
	o.bypass = d.get("bypass", false) == true
	return o
