class_name TOETemplate
extends RefCounted
## 대대 전투단 편제표 템플릿. data/toe/*.json. PLAN 6장, 16.4.
## company_slots / organic / attachment_slots 는 중대·소대 템플릿 id 를 가리킨다.
## support: 편성 때 포인트로 사는 지원(지도 밖 포병 사격 임무, 근접항공지원 출격). PLAN 6.2.


class CompanySlot:
	extends RefCounted
	var id: StringName
	var allowed: PackedStringArray
	var required: bool


class AttachmentSlot:
	extends RefCounted
	var allowed: PackedStringArray
	var max_count: int


var id: StringName
var faction: StringName
var display_name: String
var hq_element: StringName
var hq_count: int
var company_slots: Array[CompanySlot] = []
## 자유 형식 제약. 예: {"min_tank_co": 2}. 해석은 M4 편성 검증기가 한다.
var constraints: Dictionary = {}
var organic: PackedStringArray
var attachment_slots: Array[AttachmentSlot] = []
var command_capacity_per_min: float
var max_companies: int
## 지원 구매 단가·상한.
var artillery_mission_cost: int = 30
var max_artillery: int = 6
var cas_sortie_cost: int = 70
var max_cas: int = 2


static func from_dict(d: Dictionary, r: DefReader) -> TOETemplate:
	var t := TOETemplate.new()
	t.id = StringName(r.req_string(d, "id"))
	t.faction = StringName(r.req_string(d, "faction"))
	r.check_one_of(t.faction, ElementDef.FACTIONS, "faction")
	t.display_name = r.req_string(d, "name")

	var hq: Dictionary = r.req_dict(d, "hq")
	t.hq_element = StringName(r.req_string(hq, "element"))
	t.hq_count = r.req_int(hq, "count")

	var seen: Dictionary = {}
	var slots_raw: Array = r.req_array(d, "company_slots")
	for i: int in slots_raw.size():
		var raw: Dictionary = r.as_dict(slots_raw[i], "company_slots[%d]" % i)
		var slot := CompanySlot.new()
		slot.id = StringName(r.req_string(raw, "id"))
		slot.allowed = r.req_string_array(raw, "allowed")
		slot.required = r.opt_bool(raw, "required", false)
		if slot.allowed.is_empty():
			r.error("company_slots[%d].allowed 가 비어 있음" % i)
		if seen.has(slot.id):
			r.error("company_slots 의 id '%s' 가 중복됨" % slot.id)
		seen[slot.id] = true
		t.company_slots.append(slot)

	t.constraints = r.opt_dict(d, "constraints")
	t.organic = r.opt_string_array(d, "organic")

	var att_raw: Array = r.opt_array(d, "attachment_slots")
	for i: int in att_raw.size():
		var raw: Dictionary = r.as_dict(att_raw[i], "attachment_slots[%d]" % i)
		var slot := AttachmentSlot.new()
		slot.allowed = r.req_string_array(raw, "allowed")
		slot.max_count = r.req_int(raw, "max")
		t.attachment_slots.append(slot)

	var sup: Dictionary = r.opt_dict(d, "support")
	t.artillery_mission_cost = r.opt_int(sup, "artillery_mission", 30)
	t.max_artillery = r.opt_int(sup, "max_artillery", 6)
	t.cas_sortie_cost = r.opt_int(sup, "cas_sortie", 70)
	t.max_cas = r.opt_int(sup, "max_cas", 2)
	t.command_capacity_per_min = r.req_number(d, "command_capacity_per_min")
	r.check_range(t.command_capacity_per_min, 1.0, 20.0, "command_capacity_per_min")
	t.max_companies = r.req_int(d, "max_companies")
	if t.company_slots.size() > t.max_companies:
		r.error("company_slots 수(%d)가 max_companies(%d)를 넘음" % [t.company_slots.size(), t.max_companies])
	return t


## 이 템플릿이 참조하는 중대·소대 템플릿 id 전체.
func referenced_unit_templates() -> PackedStringArray:
	var out := PackedStringArray()
	for slot: CompanySlot in company_slots:
		out.append_array(slot.allowed)
	out.append_array(organic)
	for slot: AttachmentSlot in attachment_slots:
		out.append_array(slot.allowed)
	return out
