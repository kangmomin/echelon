class_name Platoon
extends Unit
## 소대(또는 중대본부·대대 지휘소 같은 분대 규모 반). 최소 조작 단위. PLAN 5장.

var elements: Array[Element] = []
## 중대본부(중대장·XO) 또는 대대 지휘소 반.
var is_hq_section: bool = false
## 기계화 소대: 분대가 차량에 타고 있는지.
var mounted: bool = true
var loadout: StringName = &""
## 이 소대가 최근 본 적: element_id → 틱. 사격 표적은 여기서 고른다.
var contacts: Dictionary[int, int] = {}


func _init() -> void:
	level = Level.PLATOON


func all_elements() -> Array[Element]:
	return elements


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
