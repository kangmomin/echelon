class_name DataLoader
extends RefCounted
## data/ 아래 JSON 을 읽어 타입 클래스로 변환하고 검증한다. PLAN 15.1, 16장.
##
## 디렉터리 규칙:
##   data/elements/*.json   파일 하나 = ElementDef 하나
##   data/weapons/*.json    파일 하나 = WeaponDef 하나
##   data/toe/*.json        파일 하나 = TOETemplate 하나
##   data/scenarios/*.json  파일 하나 = ScenarioDef 하나
##   data/formations.json   {"formations": [FormationDef, ...]}
##
## 검증은 두 단계다. 1) 파일별 필드·타입·범위 검사, 2) 정의 사이 참조 검사(validate_references).
## 오류가 있어도 끝까지 읽어서 전부 보고한다. catalog.is_valid() 로 판정한다.

const DEFAULT_ROOT: String = "res://data"


static func load_catalog(root: String = DEFAULT_ROOT) -> DataCatalog:
	var cat := DataCatalog.new()
	var r := DefReader.new()

	for path: String in list_json(root.path_join("weapons")):
		var d: Dictionary = _read_object(path, r)
		if d.is_empty():
			continue
		r.context = path
		_put(cat.weapons, WeaponDef.from_dict(d, r), r)

	for path: String in list_json(root.path_join("elements")):
		var d: Dictionary = _read_object(path, r)
		if d.is_empty():
			continue
		r.context = path
		_put(cat.elements, ElementDef.from_dict(d, r), r)

	var formations_path: String = root.path_join("formations.json")
	var fd: Dictionary = _read_object(formations_path, r)
	r.context = formations_path
	var list: Array = r.req_array(fd, "formations") if not fd.is_empty() else []
	for i: int in list.size():
		var item: Dictionary = r.as_dict(list[i], "formations[%d]" % i)
		if item.is_empty():
			continue
		r.context = "%s#%s" % [formations_path, item.get("id", i)]
		_put(cat.formations, FormationDef.from_dict(item, r), r)

	for path: String in list_json(root.path_join("toe")):
		var d: Dictionary = _read_object(path, r)
		if d.is_empty():
			continue
		r.context = path
		_put(cat.toe_templates, TOETemplate.from_dict(d, r), r)

	for path: String in list_json(root.path_join("scenarios")):
		var d: Dictionary = _read_object(path, r)
		if d.is_empty():
			continue
		r.context = path
		_put(cat.scenarios, ScenarioDef.from_dict(d, r), r)

	validate_references(cat, r)
	cat.errors = r.errors
	cat.warnings = r.warnings
	return cat


## 정의 사이 참조 검사. 테스트에서 직접 부를 수 있게 공개한다.
static func validate_references(cat: DataCatalog, r: DefReader) -> void:
	for e: ElementDef in cat.elements.values():
		r.context = "element '%s'" % e.id
		for weapon_id: String in e.weapons:
			if not cat.weapons.has(StringName(weapon_id)):
				r.error("무기 '%s' 정의가 없음" % weapon_id)

	for f: FormationDef in cat.formations.values():
		r.context = "formation '%s'" % f.id
		if f.echelon == &"platoon" and not (f.slots.has(3) and f.slots.has(4)):
			r.error("소대 대형은 3·4 엘리먼트 슬롯을 모두 정의해야 함")

	for t: TOETemplate in cat.toe_templates.values():
		r.context = "toe '%s'" % t.id
		var hq: ElementDef = cat.get_element(t.hq_element)
		if hq == null:
			r.error("hq.element '%s' 정의가 없음" % t.hq_element)
		elif hq.faction != t.faction:
			r.error("hq.element '%s' 의 진영(%s)이 템플릿 진영(%s)과 다름" % [hq.id, hq.faction, t.faction])
		# 중대·소대 템플릿(UnitTemplate)은 M4 에서 추가된다. 그 전까지는 경고로만 남긴다.
		var unresolved: PackedStringArray = t.referenced_unit_templates()
		if not unresolved.is_empty():
			r.warn("중대·소대 템플릿 참조 %d개 미검증(M4 에서 UnitTemplate 추가 후 검사)" % unresolved.size())

	for s: ScenarioDef in cat.scenarios.values():
		r.context = "scenario '%s'" % s.id
		for side_name: StringName in s.sides.keys():
			var side: ScenarioDef.SideSetup = s.sides[side_name]
			var toe: TOETemplate = cat.get_toe(side.toe)
			if toe == null:
				r.error("sides.%s.toe '%s' 정의가 없음" % [side_name, side.toe])
			elif toe.faction != side.faction:
				r.error("sides.%s 의 진영(%s)과 toe 진영(%s)이 다름" % [side_name, side.faction, toe.faction])
		var map_dir: String = DEFAULT_ROOT.path_join("maps").path_join(s.map_id)
		if not FileAccess.file_exists(map_dir.path_join("heightmap.png")):
			r.warn("지도 '%s' 의 heightmap.png 가 아직 없음(M1 에서 추가)" % s.map_id)
	r.context = ""


## 디렉터리의 .json 파일 경로를 이름순으로.
static func list_json(dir_path: String) -> PackedStringArray:
	var out := PackedStringArray()
	if not DirAccess.dir_exists_absolute(dir_path):
		return out
	var names: PackedStringArray = DirAccess.get_files_at(dir_path)
	names.sort()
	for file_name: String in names:
		if file_name.ends_with(".json"):
			out.append(dir_path.path_join(file_name))
	return out


## JSON 파일을 읽어 최상위 객체를 돌려준다. 실패하면 오류를 남기고 빈 딕셔너리.
static func _read_object(path: String, r: DefReader) -> Dictionary:
	r.context = path
	if not FileAccess.file_exists(path):
		r.error("파일 없음")
		return {}
	var json := JSON.new()
	var err: Error = json.parse(FileAccess.get_file_as_string(path))
	if err != OK:
		r.error("JSON 파싱 오류 %d행: %s" % [json.get_error_line(), json.get_error_message()])
		return {}
	if typeof(json.data) != TYPE_DICTIONARY:
		r.error("최상위가 객체({})여야 함")
		return {}
	return json.data


## id 중복을 검사하며 사전에 넣는다.
static func _put(target: Dictionary, def: RefCounted, r: DefReader) -> void:
	var def_id: StringName = def.get("id")
	if def_id == &"":
		return
	if target.has(def_id):
		r.error("id '%s' 가 중복 정의됨" % def_id)
		return
	target[def_id] = def
