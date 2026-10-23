class_name Element
extends RefCounted
## 엘리먼트: 지도 위에 실체(위치·체력·무장)를 가진 유일한 단위. 차량 1대 또는 분대 1개. PLAN 5장, 16.6.

enum State { OK, DAMAGED, DESTROYED, ABANDONED }

const STATE_NAMES: Array[String] = ["정상", "손상", "격파", "유기"]
## 선회 속도(라디안/초).
const TURN_RATE_VEHICLE: float = PI / 3.0
const TURN_RATE_FOOT: float = PI

var id: int = 0
var def: ElementDef
var side: StringName = &"blue"
var platoon: Platoon = null
## 원래 편제 안 순번(소대 안 위치). 0 = 소대장.
var index_in_platoon: int = 0

var pos: Vector2 = Vector2.ZERO
var prev_pos: Vector2 = Vector2.ZERO
var heading: float = 0.0
var prev_heading: float = 0.0
var velocity: Vector2 = Vector2.ZERO

var state: int = State.OK
## 손상 종류: &"mobility" | &"firepower"
var damage_kind: StringName = &""
## 차량은 0~100 내구, 분대는 인원 수.
var hp: float = 100.0
var suppression: float = 0.0
var ammo: Dictionary[StringName, int] = {}
## 무기별 재장전 남은 시간(초).
var reload: Dictionary[StringName, float] = {}
var target_id: int = -1

## 이동 목표(대형 슬롯 또는 개별 이동 지점).
var move_target: Vector2 = Vector2.ZERO
var has_move_target: bool = false
## 정지했을 때 바라볼 방향.
var rest_heading: float = 0.0
var local_path: PackedVector2Array = PackedVector2Array()
var local_path_index: int = 0
var path_request: GridPathfinder.PathRequest = null
var stuck_ticks: int = 0
## 슬롯에서 오래 떨어져 대형 대기 규칙에서 제외된 낙오 상태.
var straggler_ticks: int = 0

## 탑승 중인 차량(분대만). null = 하차.
var mounted_in: Element = null
var passengers: Array[Element] = []

var on_map: bool = true
var stationary_ticks: int = 0
var last_fired_tick: int = -1000000
var last_hit_tick: int = -1000000
var kills: int = 0
## 이 엘리먼트를 쓰러뜨린 상대 id(AAR).
var killed_by: int = -1
## 보급 트럭: 남은 보급 단위.
var supply_left: float = 0.0
## 수리 진행(초).
var repair_sec: float = 0.0
var destroyed_tick: int = -1


func is_alive() -> bool:
	return state == State.OK or state == State.DAMAGED


func is_active() -> bool:
	return is_alive() and on_map


func is_mounted() -> bool:
	return mounted_in != null


func is_infantry() -> bool:
	return def.element_class == &"inf_squad"


func is_vehicle() -> bool:
	return not is_infantry()


func mobility() -> StringName:
	return Terrain.MOBILITY_FOOT if is_infantry() else Terrain.MOBILITY_VEHICLE


func element_class() -> StringName:
	return def.element_class


func display_name() -> String:
	return "%s #%d" % [def.display_name, id]


func can_move() -> bool:
	return is_alive() and not (state == State.DAMAGED and damage_kind == &"mobility" and is_infantry())


## 현재 위치 지형 기준 최대 속도(m/s).
func max_speed_mps(terrain: Terrain) -> float:
	var t: int = terrain.type_at(pos)
	var kmh: float
	if t == Terrain.Type.ROAD and is_vehicle():
		kmh = def.speed_road_kmh
	else:
		kmh = def.speed_offroad_kmh * terrain.speed_factor(t, mobility())
	if state == State.DAMAGED and damage_kind == &"mobility":
		kmh *= 0.5
	if suppression >= 50.0:
		kmh *= 0.7
	return kmh / 3.6


## 기준 속도(개활지, 손상·제압 반영). 대형 속도 상한 계산용.
func base_speed_mps() -> float:
	var kmh: float = def.speed_offroad_kmh
	if state == State.DAMAGED and damage_kind == &"mobility":
		kmh *= 0.5
	return kmh / 3.6


func turn_rate() -> float:
	return TURN_RATE_FOOT if is_infantry() else TURN_RATE_VEHICLE


func turn_toward(target_heading: float, dt: float) -> void:
	var diff: float = angle_difference(heading, target_heading)
	var max_turn: float = turn_rate() * dt
	heading = wrapf(heading + clampf(diff, -max_turn, max_turn), -PI, PI)


func clear_local_path() -> void:
	local_path = PackedVector2Array()
	local_path_index = 0
	if path_request != null:
		path_request.cancelled = true
	path_request = null


## 전투력 기여(0~1). 분대는 인원 비율, 차량은 상태.
func strength() -> float:
	if not is_alive():
		return 0.0
	if is_infantry():
		return clampf(hp / maxf(def.hp, 1.0), 0.0, 1.0)
	return 0.6 if state == State.DAMAGED else 1.0
