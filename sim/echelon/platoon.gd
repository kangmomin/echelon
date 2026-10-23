class_name Platoon
extends Unit
## 소대(또는 중대본부·대대 지휘소 같은 분대 규모 반). 최소 조작 단위. PLAN 5장.

var elements: Array[Element] = []
## 중대본부(중대장·XO) 또는 대대 지휘소 반.
var is_hq_section: bool = false
## 기계화 소대: 분대가 차량에 타고 있는지.
var mounted: bool = true
var loadout: StringName = &""
## 대대 배속 자산(관측반·방공 등).
var attached: bool = false
## 최근 포격을 받은 틱과 착탄점(포격 SOP 반응용).
var last_shelled_tick: int = -1000000
var last_shell_pos: Vector2 = Vector2.ZERO
## 이동 시스템 캐시: 마지막으로 엘리먼트 목표를 계산한 가이드 위치·방향과 구성원 수.
var last_assign_key: Vector3 = Vector3.INF
var last_assign_members: int = -1
## 이 소대가 최근 본 적: element_id → 틱. 사격 표적은 여기서 고른다.
var contacts: Dictionary[int, int] = {}
## 공병: 현재 작업(&"" | &"lay" | &"clear" | &"fortify")과 진행(초), 남은 지뢰 지대 수, 만든 진지 수.
var eng_task: StringName = &""
var eng_progress: float = 0.0
var mines_left: int = 0
var positions_built: int = 0


func _init() -> void:
	level = Level.PLATOON


func all_elements() -> Array[Element]:
	return elements


## 성능: 배열을 만들지 않고 살아 있는 엘리먼트가 있는지만 본다(자주 불린다).
func is_destroyed() -> bool:
	for e: Element in elements:
		if e.state == Element.State.OK or e.state == Element.State.DAMAGED:
			return false
	return true


func vehicles() -> Array[Element]:
	var out: Array[Element] = []
	for e: Element in elements:
		if e.is_vehicle():
			out.append(e)
	return out


func squads() -> Array[Element]:
	var out: Array[Element] = []
	for e: Element in elements:
		if e.is_infantry():
			out.append(e)
	return out


func has_squads() -> bool:
	for e: Element in elements:
		if e.is_infantry():
			return true
	return false


func has_transport() -> bool:
	for e: Element in elements:
		if e.def.transport_capacity > 0:
			return true
	return false


## 대형 슬롯을 차지하는 엘리먼트(원래 순서). 하차한 기계화 소대는 분대가 슬롯을 차지하고 차량은 뒤를 따른다.
func formation_members() -> Array[Element]:
	var out: Array[Element] = []
	var dismounted_mech: bool = has_squads() and not mounted and has_transport()
	for e: Element in elements:
		if not e.is_active() or e.is_mounted() or not e.can_move():
			continue
		if dismounted_mech and e.is_vehicle():
			continue
		out.append(e)
	return out


## 하차한 기계화 소대에서 분대 뒤를 따르는 차량.
func trailing_vehicles() -> Array[Element]:
	var out: Array[Element] = []
	if not (has_squads() and not mounted and has_transport()):
		return out
	for e: Element in elements:
		if e.is_vehicle() and e.is_active() and e.can_move():
			out.append(e)
	return out


func leader() -> Element:
	if commander != null and commander.is_alive():
		return commander
	for e: Element in elements:
		if e.is_alive():
			return e
	return null


## 대형 전체가 낼 수 있는 기준 속도(m/s): 가장 느린 구성원.
func base_speed() -> float:
	var best: float = INF
	for e: Element in formation_members():
		best = minf(best, e.base_speed_mps())
	return 0.0 if best == INF else best


func follows_parent() -> bool:
	return parent != null and parent.level == Level.COMPANY and not detached and not routed


func company() -> Company:
	return parent as Company if parent != null and parent.level == Level.COMPANY else null
