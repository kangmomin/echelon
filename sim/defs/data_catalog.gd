class_name DataCatalog
extends RefCounted
## 로드된 정의 전체. 시뮬과 UI 는 읽기만 한다.

var elements: Dictionary[StringName, ElementDef] = {}
var weapons: Dictionary[StringName, WeaponDef] = {}
var formations: Dictionary[StringName, FormationDef] = {}
var toe_templates: Dictionary[StringName, TOETemplate] = {}
var scenarios: Dictionary[StringName, ScenarioDef] = {}
var unit_templates: Dictionary[StringName, UnitTemplate] = {}
var maps: Dictionary[StringName, MapDef] = {}
## 기본 편성(data/forces). 키 = 파일 이름.
var forces: Dictionary[StringName, ForceSpec] = {}

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


func get_unit_template(template_id: StringName) -> UnitTemplate:
	return unit_templates.get(template_id)


func get_map(map_id: StringName) -> MapDef:
	return maps.get(map_id)


## 진영·제대·유형으로 템플릿 검색(파일 이름순).
func unit_templates_for(faction: StringName, echelon: StringName = &"") -> Array[UnitTemplate]:
	var out: Array[UnitTemplate] = []
	for t: UnitTemplate in unit_templates.values():
		if t.faction == faction and (echelon == &"" or t.echelon == echelon):
			out.append(t)
	return out


## 해당 제대의 대형을 파일에 적힌 순서대로 돌려준다.
func formations_for(echelon: StringName) -> Array[FormationDef]:
	var out: Array[FormationDef] = []
	for f: FormationDef in formations.values():
		if f.echelon == echelon:
			out.append(f)
	return out


func summary() -> String:
	return "elements %d, weapons %d, formations %d, units %d, toe %d, maps %d, scenarios %d, forces %d, errors %d, warnings %d" % [
		elements.size(), weapons.size(), formations.size(), unit_templates.size(), toe_templates.size(),
		maps.size(), scenarios.size(), forces.size(), errors.size(), warnings.size()]


## 진영의 기본 편성(첫 번째). 없으면 TOE 기본값.
func default_force(faction: StringName) -> ForceSpec:
	var keys: Array[StringName] = forces.keys()
	keys.sort()
	for key: StringName in keys:
		var f: ForceSpec = forces[key]
		if f.faction == faction:
			return f.duplicate_spec()
	for t: TOETemplate in toe_templates.values():
		if t.faction == faction:
			return ForceSpec.from_toe(self, t.id)
	return null
