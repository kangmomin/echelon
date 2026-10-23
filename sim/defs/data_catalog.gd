class_name DataCatalog
extends RefCounted
## 로드된 정의 전체. 시뮬과 UI 는 읽기만 한다.

var elements: Dictionary[StringName, ElementDef] = {}
var weapons: Dictionary[StringName, WeaponDef] = {}
var formations: Dictionary[StringName, FormationDef] = {}
var toe_templates: Dictionary[StringName, TOETemplate] = {}
var scenarios: Dictionary[StringName, ScenarioDef] = {}

var errors: PackedStringArray = []
var warnings: PackedStringArray = []


func is_valid() -> bool:
	return errors.is_empty()


func get_element(element_id: StringName) -> ElementDef:
	return elements.get(element_id)


func get_weapon(weapon_id: StringName) -> WeaponDef:
	return weapons.get(weapon_id)


func get_formation(formation_id: StringName) -> FormationDef:
	return formations.get(formation_id)


func get_toe(toe_id: StringName) -> TOETemplate:
	return toe_templates.get(toe_id)


func get_scenario(scenario_id: StringName) -> ScenarioDef:
	return scenarios.get(scenario_id)


## 해당 제대의 대형을 파일에 적힌 순서대로 돌려준다.
func formations_for(echelon: StringName) -> Array[FormationDef]:
	var out: Array[FormationDef] = []
	for f: FormationDef in formations.values():
		if f.echelon == echelon:
			out.append(f)
	return out


func summary() -> String:
	return "elements %d, weapons %d, formations %d, toe %d, scenarios %d, errors %d, warnings %d" % [
		elements.size(), weapons.size(), formations.size(), toe_templates.size(), scenarios.size(),
		errors.size(), warnings.size()]
