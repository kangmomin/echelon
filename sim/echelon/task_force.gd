class_name TaskForce
extends Unit
## 대대 전투단. 플레이어(또는 적 AI) 자리. 중대·직할 소대·지휘소를 가진다. PLAN 5장.

var companies: Array[Company] = []
## 직할 소대(정찰·박격포·보급·관측반·방공 등). 지휘소 반은 cp_section 에 따로 둔다.
var organic: Array[Platoon] = []
var cp_section: Platoon = null
var toe: TOETemplate = null
var faction: StringName = &"US"

## 대대망에 보고된 적: element_id → {"pos": Vector2, "tick": int, "class": StringName}
var known_enemies: Dictionary[int, Dictionary] = {}
## 승점.
var vp: float = 0.0
## 초기 전력(붕괴 판정 기준) — 포인트 합.
var initial_value: float = 0.0
var command_capacity_per_min: float = 6.0
## 지도 밖 화력 자원.
var artillery_missions: int = 0
var cas_sorties: int = 0


func _init() -> void:
	level = Level.TASK_FORCE


func children_units() -> Array[Unit]:
	var out: Array[Unit] = []
	if cp_section != null:
		out.append(cp_section)
	for c: Company in companies:
		out.append(c)
	for p: Platoon in organic:
		out.append(p)
	return out


## TF 아래 모든 제대, 위에서 아래 순서(중대 → 중대본부·소대, 이어서 직할 소대). 결정론적 순서.
func all_units() -> Array[Unit]:
	var out: Array[Unit] = [self]
	if cp_section != null:
		out.append(cp_section)
	for c: Company in companies:
		out.append(c)
		if c.hq != null:
			out.append(c.hq)
		for p: Platoon in c.platoons:
			out.append(p)
	for p: Platoon in organic:
		out.append(p)
	return out


func all_platoons() -> Array[Platoon]:
	var out: Array[Platoon] = []
	for u: Unit in all_units():
		if u.level == Level.PLATOON:
			out.append(u as Platoon)
	return out


func all_elements() -> Array[Element]:
	var out: Array[Element] = []
	for p: Platoon in all_platoons():
		out.append_array(p.elements)
	return out


func leader() -> Element:
	if commander != null and commander.is_alive():
		return commander
	return null
