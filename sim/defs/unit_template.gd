class_name UnitTemplate
extends RefCounted
## 중대·소대 편제 템플릿. data/units/*.json. PLAN 6장, 16.4.
## 소대: 엘리먼트 목록(+ 탑승 분대), 장비 템플릿(loadouts). 중대: 본부 엘리먼트 + 소대 템플릿 목록.

const ECHELONS: Array[String] = ["platoon", "company"]
const KINDS: Array[String] = ["tank", "mech", "inf", "recon", "mortar", "supply", "hq", "cp", "fo", "aa", "ew", "engineer"]


class Loadout:
	extends RefCounted
	var id: StringName
	var display_name: String
	## 원래 엘리먼트 id → 바꿀 엘리먼트 id.
	var replace: Dictionary[StringName, StringName] = {}


var id: StringName
var display_name: String
var faction: StringName
var echelon: StringName
var kind: StringName
## 소대: 엘리먼트 id 를 순서대로 펼친 목록(0번 = 소대장).
var elements: PackedStringArray = PackedStringArray()
## 소대: 탑승 분대 id 목록(차량 순서대로 한 분대씩 태운다).
var passengers: PackedStringArray = PackedStringArray()
var loadouts: Array[Loadout] = []
var default_formation: StringName = &""
## 중대: 본부 엘리먼트(0번 = 중대장, 1번 = XO).
var hq_elements: PackedStringArray = PackedStringArray()
## 중대: 소대 템플릿 id 목록.
var platoons: PackedStringArray = PackedStringArray()
## 기본 숙련도.
var skill: StringName = &"regular"
## 포인트(로드 후 DataLoader 가 계산).
var cost: int = 0


static func from_dict(d: Dictionary, r: DefReader) -> UnitTemplate:
	var t := UnitTemplate.new()
	t.id = StringName(r.req_string(d, "id"))
	t.display_name = r.req_string(d, "name")
	t.faction = StringName(r.req_string(d, "faction"))
	r.check_one_of(t.faction, ElementDef.FACTIONS, "faction")
	t.echelon = StringName(r.req_string(d, "echelon"))
	r.check_one_of(t.echelon, ECHELONS, "echelon")
	t.kind = StringName(r.req_string(d, "kind"))
	r.check_one_of(t.kind, KINDS, "kind")
	t.default_formation = StringName(r.opt_string(d, "default_formation", ""))
	t.skill = StringName(r.opt_string(d, "skill", "regular"))
	r.check_one_of(t.skill, ["recruit", "regular", "veteran", "elite"], "skill")

	if t.echelon == &"platoon":
		t.elements = _expand(r.req_array(d, "elements"), r, "elements")
		t.passengers = _expand(r.opt_array(d, "passengers"), r, "passengers")
		if t.elements.is_empty():
			r.error("elements 가 비어 있음")
		var loadouts_raw: Array = r.opt_array(d, "loadouts")
		for i: int in loadouts_raw.size():
			var ld: Dictionary = r.as_dict(loadouts_raw[i], "loadouts[%d]" % i)
			var lo := Loadout.new()
			lo.id = StringName(r.req_string(ld, "id"))
			lo.display_name = r.req_string(ld, "name")
			var rep: Dictionary = r.opt_dict(ld, "replace")
			for key: Variant in rep.keys():
				lo.replace[StringName(str(key))] = StringName(str(rep[key]))
			t.loadouts.append(lo)
	else:
		t.hq_elements = _expand(r.req_array(d, "hq"), r, "hq")
		t.platoons = r.req_string_array(d, "platoons")
		if t.platoons.is_empty():
			r.error("platoons 가 비어 있음")
	return t


## [{"element": id, "count": n}, ...] → 펼친 id 목록.
static func _expand(raw: Array, r: DefReader, what: String) -> PackedStringArray:
	var out := PackedStringArray()
	for i: int in raw.size():
		var item: Dictionary = r.as_dict(raw[i], "%s[%d]" % [what, i])
		var element_id: String = r.req_string(item, "element")
		var count: int = r.opt_int(item, "count", 1)
		if count < 1 or count > 12:
			r.error("%s[%d].count 는 1~12" % [what, i])
			continue
		for k: int in count:
			out.append(element_id)
	return out


func get_loadout(loadout_id: StringName) -> Loadout:
	for lo: Loadout in loadouts:
		if lo.id == loadout_id:
			return lo
	return null


## 장비 템플릿을 적용한 엘리먼트 id 목록.
func elements_for(loadout_id: StringName) -> PackedStringArray:
	var lo: Loadout = get_loadout(loadout_id)
	if lo == null:
		return elements
	var out := PackedStringArray()
	for e: String in elements:
		var swapped: StringName = lo.replace.get(StringName(e), StringName(e))
		out.append(String(swapped))
	return out


func passengers_for(loadout_id: StringName) -> PackedStringArray:
	var lo: Loadout = get_loadout(loadout_id)
	if lo == null:
		return passengers
	var out := PackedStringArray()
	for e: String in passengers:
		var swapped: StringName = lo.replace.get(StringName(e), StringName(e))
		out.append(String(swapped))
	return out
