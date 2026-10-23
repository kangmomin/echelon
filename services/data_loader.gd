class_name DataLoader
extends RefCounted
## data/ 아래 JSON 을 읽어 타입 클래스로 변환하고 검증한다. PLAN 15.1, 16장.
##
## 디렉터리 규칙:
##   data/elements/*.json   파일 하나 = ElementDef 하나
##   data/weapons/*.json    파일 하나 = WeaponDef 하나
##   data/toe/*.json        파일 하나 = TOETemplate 하나
##   data/scenarios/*.json  파일 하나 = ScenarioDef 하나
##   data/units/*.json      파일 하나 = UnitTemplate 하나(중대·소대)
##   data/maps/*.json       파일 하나 = MapDef 하나
##   data/formations.json   {"formations": [FormationDef, ...]}
##   data/forces/*.json     파일 하나 = ForceSpec 하나(기본 편성. id = 파일 이름)
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

	for path: String in list_json(root.path_join("units")):
		var d: Dictionary = _read_object(path, r)
		if d.is_empty():
			continue
		r.context = path
		_put(cat.unit_templates, UnitTemplate.from_dict(d, r), r)

	for path: String in list_json(root.path_join("maps")):
		var d: Dictionary = _read_object(path, r)
		if d.is_empty():
			continue
		r.context = path
		_put(cat.maps, MapDef.from_dict(d, r), r)

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

	for path: String in list_json(root.path_join("forces")):
		var d: Dictionary = _read_object(path, r)
		if d.is_empty():
			continue
		cat.forces[StringName(path.get_file().get_basename())] = ForceSpec.from_dict(d)

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

	for ut: UnitTemplate in cat.unit_templates.values():
		r.context = "unit '%s'" % ut.id
		var cost: int = 0
		var ids: PackedStringArray = ut.elements.duplicate()
		ids.append_array(ut.passengers)
		ids.append_array(ut.hq_elements)
		for lo: UnitTemplate.Loadout in ut.loadouts:
			for to_id: StringName in lo.replace.values():
				ids.append(String(to_id))
		for element_id: String in ids:
			var ed: ElementDef = cat.get_element(StringName(element_id))
			if ed == null:
				r.error("엘리먼트 '%s' 정의가 없음" % element_id)
			elif ed.faction != ut.faction:
				r.error("엘리먼트 '%s' 의 진영(%s)이 템플릿 진영과 다름" % [element_id, ed.faction])
		for element_id: String in ut.elements:
			var ed: ElementDef = cat.get_element(StringName(element_id))
			cost += ed.cost if ed != null else 0
		for element_id: String in ut.passengers:
			var ed: ElementDef = cat.get_element(StringName(element_id))
			cost += ed.cost if ed != null else 0
		if not ut.passengers.is_empty():
			var seats: int = 0
			for element_id: String in ut.elements:
				var ed: ElementDef = cat.get_element(StringName(element_id))
				seats += ed.transport_capacity if ed != null else 0
			if seats < ut.passengers.size():
				r.error("탑승 분대 %d 개를 태울 좌석이 %d 개뿐" % [ut.passengers.size(), seats])
		if ut.default_formation != &"" and cat.get_formation(ut.default_formation) == null:
			r.error("default_formation '%s' 가 없음" % ut.default_formation)
		ut.cost = cost
	# 중대 원가는 소대 원가가 정해진 뒤 계산한다.
	for ut: UnitTemplate in cat.unit_templates.values():
		if ut.echelon != &"company":
			continue
		r.context = "unit '%s'" % ut.id
		var cost: int = 0
		for element_id: String in ut.hq_elements:
			var ed: ElementDef = cat.get_element(StringName(element_id))
			cost += ed.cost if ed != null else 0
		for plt_id: String in ut.platoons:
			var pt: UnitTemplate = cat.get_unit_template(StringName(plt_id))
			if pt == null:
				r.error("소대 템플릿 '%s' 가 없음" % plt_id)
			elif pt.echelon != &"platoon" or pt.faction != ut.faction:
				r.error("'%s' 는 같은 진영의 소대 템플릿이어야 함" % plt_id)
			else:
				cost += pt.cost
		ut.cost = cost

	for t: TOETemplate in cat.toe_templates.values():
		r.context = "toe '%s'" % t.id
		var hq: ElementDef = cat.get_element(t.hq_element)
		if hq == null:
			r.error("hq.element '%s' 정의가 없음" % t.hq_element)
		elif hq.faction != t.faction:
			r.error("hq.element '%s' 의 진영(%s)이 템플릿 진영(%s)과 다름" % [hq.id, hq.faction, t.faction])
		for slot: TOETemplate.CompanySlot in t.company_slots:
			for ref: String in slot.allowed:
				_check_unit_ref(cat, r, ref, &"company", t.faction)
		for ref: String in t.organic:
			_check_unit_ref(cat, r, ref, &"platoon", t.faction)
		for slot: TOETemplate.AttachmentSlot in t.attachment_slots:
			for ref: String in slot.allowed:
				_check_unit_ref(cat, r, ref, &"platoon", t.faction)

	for s: ScenarioDef in cat.scenarios.values():
		r.context = "scenario '%s'" % s.id
		for side_name: StringName in s.sides.keys():
			var side: ScenarioDef.SideSetup = s.sides[side_name]
			var toe: TOETemplate = cat.get_toe(side.toe)
			if toe == null:
				r.error("sides.%s.toe '%s' 정의가 없음" % [side_name, side.toe])
			elif toe.faction != side.faction:
				r.error("sides.%s 의 진영(%s)과 toe 진영(%s)이 다름" % [side_name, side.faction, toe.faction])
		for side_name: StringName in s.sides.keys():
			var side: ScenarioDef.SideSetup = s.sides[side_name]
			for force_id: StringName in cat.forces.keys():
				var spec: ForceSpec = cat.forces[force_id]
				if spec.toe != side.toe:
					continue
				var rep: ForceRules.Report = ForceRules.evaluate(cat, spec, side.budget)
				for msg: String in rep.errors:
					r.error("기본 편성 '%s' (%s): %s" % [force_id, side_name, msg])
		var m: MapDef = cat.get_map(s.map_id)
		if m == null:
			r.error("지도 '%s' 정의가 없음" % s.map_id)
		elif m.size_m != s.size_m:
			r.error("size_m 이 지도(%s)와 다름" % m.size_m)
	r.context = ""


static func _check_unit_ref(cat: DataCatalog, r: DefReader, ref: String, echelon: StringName, faction: StringName) -> void:
	var ut: UnitTemplate = cat.get_unit_template(StringName(ref))
	if ut == null:
		r.error("편제 템플릿 '%s' 가 없음" % ref)
	elif ut.echelon != echelon:
		r.error("'%s' 는 %s 템플릿이어야 함" % [ref, echelon])
	elif ut.faction != faction:
		r.error("'%s' 의 진영이 다름" % ref)


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
