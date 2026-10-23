class_name ForceSpec
extends RefCounted
## 편성 결과: 한 진영의 대대 전투단(TF) 구성. 편성 화면이 만들고, 저장(JSON)·불러오기·전투 생성에 쓴다. PLAN 6장.
## 순수 데이터. 비용·검증은 ForceRules, 전장 생성은 ForceFactory.build_task_force().

const FORMAT_VERSION: int = 1
const WAVE_NAMES: Array[String] = ["초기 배치", "1차 증원 (T+10분)", "2차 증원 (T+20분)"]
const MAX_WAVE: int = 2


class PlatoonSpec:
	extends RefCounted
	var template: StringName
	var loadout: StringName = &""
	## 0 = 초기 배치, 1 = T+10분, 2 = T+20분.
	var wave: int = 0
	## 원래 소속 중대 슬롯(교차 배속 추적). 중대 밖(직할·배속)은 "".
	var home: StringName = &""

	func duplicate_spec() -> PlatoonSpec:
		var p := PlatoonSpec.new()
		p.template = template
		p.loadout = loadout
		p.wave = wave
		p.home = home
		return p

	func to_dict() -> Dictionary:
		var d: Dictionary = {"template": String(template), "wave": wave}
		if loadout != &"":
			d["loadout"] = String(loadout)
		if home != &"":
			d["home"] = String(home)
		return d

	static func from_dict(d: Dictionary) -> PlatoonSpec:
		var p := PlatoonSpec.new()
		p.template = StringName(str(d.get("template", "")))
		p.loadout = StringName(str(d.get("loadout", "")))
		p.wave = clampi(ForceSpec.to_int(d.get("wave", 0)), 0, MAX_WAVE)
		p.home = StringName(str(d.get("home", "")))
		return p


class CompanySpec:
	extends RefCounted
	## TOE 의 중대 슬롯 id.
	var slot: StringName
	var template: StringName
	var platoons: Array[PlatoonSpec] = []

	## 가장 이른 소대의 증원 단계(중대본부도 이때 들어온다).
	func wave() -> int:
		var w: int = MAX_WAVE
		for p: PlatoonSpec in platoons:
			w = mini(w, p.wave)
		return w if not platoons.is_empty() else 0

	func to_dict() -> Dictionary:
		var plts: Array = []
		for p: PlatoonSpec in platoons:
			plts.append(p.to_dict())
		return {"slot": String(slot), "template": String(template), "platoons": plts}

	static func from_dict(d: Dictionary) -> CompanySpec:
		var c := CompanySpec.new()
		c.slot = StringName(str(d.get("slot", "")))
		c.template = StringName(str(d.get("template", "")))
		var raw: Variant = d.get("platoons", [])
		if raw is Array:
			var raw_list: Array = raw
			for item: Variant in raw_list:
				if item is Dictionary:
					var pd: Dictionary = item
					c.platoons.append(PlatoonSpec.from_dict(pd))
		return c


var name: String = "새 전투단"
var faction: StringName = &"US"
var toe: StringName = &""
var companies: Array[CompanySpec] = []
## 대대 직할(정찰·박격포·보급). TOE 의 organic 목록에서 고른다(뺄 수 있음).
var organic: Array[PlatoonSpec] = []
## 배속 자산(관측반·방공·전자전).
var attachments: Array[PlatoonSpec] = []
var artillery_missions: int = 0
var cas_sorties: int = 0
## 배치 위치: 키("co:<slot>", "org:<i>", "att:<i>") → [x, y, heading]. 비어 있으면 자동 배치.
var deploy: Dictionary[String, Vector3] = {}
## 증원 진입 지점(시나리오 entry_points 의 인덱스).
var entry_point: int = 0


## TOE 기본 편성: 필수 중대 슬롯을 첫 허용 유형으로, 직할 전부, 배속 없음.
static func from_toe(catalog: DataCatalog, toe_id: StringName) -> ForceSpec:
	var t: TOETemplate = catalog.get_toe(toe_id)
	var s := ForceSpec.new()
	if t == null:
		return s
	s.toe = t.id
	s.faction = t.faction
	s.name = t.display_name
	for slot: TOETemplate.CompanySlot in t.company_slots:
		if slot.required:
			s.companies.append(make_company(catalog, slot.id, StringName(slot.allowed[0])))
	# 최소 전차 중대 수를 맞춘다: 뒤 슬롯부터 전차 중대를 허용하는 곳을 전차 중대로.
	var min_tank: int = to_int(t.constraints.get("min_tank_co", 0))
	var tanks: int = 0
	for c: CompanySpec in s.companies:
		var ut: UnitTemplate = catalog.get_unit_template(c.template)
		if ut != null and ut.kind == &"tank":
			tanks += 1
	for i: int in range(s.companies.size() - 1, -1, -1):
		if tanks >= min_tank:
			break
		var c: CompanySpec = s.companies[i]
		var cur: UnitTemplate = catalog.get_unit_template(c.template)
		if cur != null and cur.kind == &"tank":
			continue
		for slot: TOETemplate.CompanySlot in t.company_slots:
			if slot.id != c.slot:
				continue
			for allowed: String in slot.allowed:
				var ut: UnitTemplate = catalog.get_unit_template(StringName(allowed))
				if ut != null and ut.kind == &"tank":
					s.companies[i] = make_company(catalog, c.slot, ut.id)
					tanks += 1
					break
	for org_id: String in t.organic:
		var p := PlatoonSpec.new()
		p.template = StringName(org_id)
		s.organic.append(p)
	return s


## 중대 템플릿대로 소대를 채운 중대 사양.
static func make_company(catalog: DataCatalog, slot_id: StringName, template_id: StringName) -> CompanySpec:
	var c := CompanySpec.new()
	c.slot = slot_id
	c.template = template_id
	var ut: UnitTemplate = catalog.get_unit_template(template_id)
	if ut != null:
		for plt_id: String in ut.platoons:
			var p := PlatoonSpec.new()
			p.template = StringName(plt_id)
			p.home = slot_id
			c.platoons.append(p)
	return c


func get_company(slot_id: StringName) -> CompanySpec:
	for c: CompanySpec in companies:
		if c.slot == slot_id:
			return c
	return null


## 소대를 다른 중대로 옮긴다(교차 배속).
func move_platoon(from_slot: StringName, index: int, to_slot: StringName) -> bool:
	var a: CompanySpec = get_company(from_slot)
	var b: CompanySpec = get_company(to_slot)
	if a == null or b == null or a == b or index < 0 or index >= a.platoons.size():
		return false
	var p: PlatoonSpec = a.platoons[index]
	a.platoons.remove_at(index)
	b.platoons.append(p)
	return true


func duplicate_spec() -> ForceSpec:
	return ForceSpec.from_dict(to_dict())


func to_dict() -> Dictionary:
	var co_list: Array = []
	for c: CompanySpec in companies:
		co_list.append(c.to_dict())
	var org: Array = []
	for p: PlatoonSpec in organic:
		org.append(p.to_dict())
	var att: Array = []
	for p: PlatoonSpec in attachments:
		att.append(p.to_dict())
	var dep: Dictionary = {}
	for key: String in deploy.keys():
		var v: Vector3 = deploy[key]
		dep[key] = [v.x, v.y, v.z]
	return {
		"format": FORMAT_VERSION, "name": name, "faction": String(faction), "toe": String(toe),
		"companies": co_list, "organic": org, "attachments": att,
		"support": {"artillery_missions": artillery_missions, "cas_sorties": cas_sorties},
		"deploy": dep, "entry_point": entry_point,
	}


static func from_dict(d: Dictionary) -> ForceSpec:
	var s := ForceSpec.new()
	s.name = str(d.get("name", "전투단"))
	s.faction = StringName(str(d.get("faction", "US")))
	s.toe = StringName(str(d.get("toe", "")))
	for item: Variant in _arr(d, "companies"):
		if item is Dictionary:
			var item_d: Dictionary = item
			s.companies.append(CompanySpec.from_dict(item_d))
	for item: Variant in _arr(d, "organic"):
		if item is Dictionary:
			var item_d: Dictionary = item
			s.organic.append(PlatoonSpec.from_dict(item_d))
	for item: Variant in _arr(d, "attachments"):
		if item is Dictionary:
			var item_d: Dictionary = item
			s.attachments.append(PlatoonSpec.from_dict(item_d))
	var sup: Variant = d.get("support", {})
	if sup is Dictionary:
		var sd: Dictionary = sup
		s.artillery_missions = maxi(0, to_int(sd.get("artillery_missions", 0)))
		s.cas_sorties = maxi(0, to_int(sd.get("cas_sorties", 0)))
	var dep: Variant = d.get("deploy", {})
	if dep is Dictionary:
		var dd: Dictionary = dep
		for key: Variant in dd.keys():
			var v: Variant = dd[key]
			if not (v is Array):
				continue
			var a: Array = v
			if a.size() >= 3:
				s.deploy[str(key)] = Vector3(to_float(a[0]), to_float(a[1]), to_float(a[2]))
	s.entry_point = maxi(0, to_int(d.get("entry_point", 0)))
	return s


static func to_int(v: Variant) -> int:
	if v is int:
		var i: int = v
		return i
	if v is float:
		var f: float = v
		return roundi(f)
	return 0


static func to_float(v: Variant) -> float:
	if v is float or v is int:
		return DefReader.to_float(v)
	return 0.0


static func _arr(d: Dictionary, key: String) -> Array:
	var v: Variant = d.get(key, [])
	if v is Array:
		var a: Array = v
		return a
	return []


func to_json() -> String:
	return JSON.stringify(to_dict(), "  ")


## JSON 문자열에서 읽는다. 실패하면 null.
static func from_json(text: String) -> ForceSpec:
	var json := JSON.new()
	if json.parse(text) != OK or not (json.data is Dictionary):
		return null
	var data: Dictionary = json.data
	return from_dict(data)
