class_name ForceRules
extends RefCounted
## 편성 규칙: 포인트 계산, 제약 검사, 비편제(ad hoc) 판정, 예상 지속 교전 시간. PLAN 6.2, 11.2.
##
## - 포인트 = 엘리먼트 cost 합. 증원 단계 할인: 1차 10%, 2차 20%(소대 단위). 중대본부·지휘소는 할인 없음.
## - 지원 구매: 포병 사격 임무·CAS 출격(TOE 단가).
## - 오류(편성 불가): 예산 초과, 필수 슬롯 비움, 허용되지 않은 유형, 진영 불일치, 중대 수 상한 초과,
##   중대 "템플릿" 기준 최소 전차 중대 수 미달, 배속 상한 초과, 소대 없는 중대.
## - 비편제(경고, 허용): 소대 수가 2~4 를 벗어난 중대, 또는 교차 배속 때문에 "구성" 기준 전차 중대 수가
##   최소치 아래로 내려간 경우 → 소대를 주고받은 중대가 비편제(명령 지연 +20%, 재편 불가). DECISIONS D-022.

const WAVE_DISCOUNT: Array[float] = [0.0, 0.1, 0.2]
const MIN_PLATOONS: int = 2
const MAX_PLATOONS: int = 4
## 지속 교전 시간 추정: 교전 중 주무장 가동률.
const DUTY_CYCLE: float = 0.25


class Report:
	extends RefCounted
	var total: int = 0
	var budget: int = 0
	var errors: PackedStringArray = []
	var warnings: PackedStringArray = []
	## 비편제 중대 슬롯.
	var ad_hoc: Array[StringName] = []
	## 항목별 포인트: [{"label": String, "cost": int}]
	var lines: Array[Dictionary] = []
	var sustain_min: float = 0.0
	var element_count: int = 0

	func is_valid() -> bool:
		return errors.is_empty()


## 소대 한 개의 포인트(할인 전).
static func platoon_base_cost(catalog: DataCatalog, p: ForceSpec.PlatoonSpec) -> int:
	var ut: UnitTemplate = catalog.get_unit_template(p.template)
	if ut == null:
		return 0
	var total: int = 0
	for id: String in ut.elements_for(p.loadout):
		var ed: ElementDef = catalog.get_element(StringName(id))
		total += ed.cost if ed != null else 0
	for id: String in ut.passengers_for(p.loadout):
		var ed: ElementDef = catalog.get_element(StringName(id))
		total += ed.cost if ed != null else 0
	return total


static func platoon_cost(catalog: DataCatalog, p: ForceSpec.PlatoonSpec) -> int:
	var base: int = platoon_base_cost(catalog, p)
	return roundi(base * (1.0 - WAVE_DISCOUNT[clampi(p.wave, 0, 2)]))


static func company_hq_cost(catalog: DataCatalog, template_id: StringName) -> int:
	var ut: UnitTemplate = catalog.get_unit_template(template_id)
	if ut == null:
		return 0
	var total: int = 0
	for id: String in ut.hq_elements:
		var ed: ElementDef = catalog.get_element(StringName(id))
		total += ed.cost if ed != null else 0
	return total


static func evaluate(catalog: DataCatalog, spec: ForceSpec, budget: int) -> Report:
	var r := Report.new()
	r.budget = budget
	var toe: TOETemplate = catalog.get_toe(spec.toe)
	if toe == null:
		r.errors.append("편제표 '%s' 가 없습니다." % spec.toe)
		return r
	if toe.faction != spec.faction:
		r.errors.append("편제표 진영(%s)과 전투단 진영(%s)이 다릅니다." % [toe.faction, spec.faction])
	# 지휘소.
	var cp: ElementDef = catalog.get_element(toe.hq_element)
	var cp_cost: int = (cp.cost if cp != null else 0) * toe.hq_count
	r.lines.append({"label": "대대 지휘소", "cost": cp_cost})
	r.total += cp_cost
	r.element_count += toe.hq_count

	_check_companies(catalog, spec, toe, r)
	_check_list(catalog, spec.organic, toe.organic, "직할", r, -1)
	var allowed_att := PackedStringArray()
	var max_att: int = 0
	for slot: TOETemplate.AttachmentSlot in toe.attachment_slots:
		allowed_att.append_array(slot.allowed)
		max_att += slot.max_count
	_check_list(catalog, spec.attachments, allowed_att, "배속", r, max_att)
	# 슬롯별 한도(예: 공격헬기 1개).
	for slot: TOETemplate.AttachmentSlot in toe.attachment_slots:
		var n: int = 0
		for p: ForceSpec.PlatoonSpec in spec.attachments:
			if slot.allowed.has(String(p.template)):
				n += 1
		if n > slot.max_count:
			var names := PackedStringArray()
			for t_id: String in slot.allowed:
				var ut: UnitTemplate = catalog.get_unit_template(StringName(t_id))
				names.append(ut.display_name if ut != null else t_id)
			r.errors.append("배속: %s 은(는) 합쳐서 최대 %d개입니다." % [" / ".join(names), slot.max_count])

	# 지원.
	if spec.artillery_missions > toe.max_artillery:
		r.errors.append("포병 사격 임무는 최대 %d회입니다." % toe.max_artillery)
	if spec.cas_sorties > toe.max_cas:
		r.errors.append("근접항공지원은 최대 %d회입니다." % toe.max_cas)
	var sup: int = spec.artillery_missions * toe.artillery_mission_cost + spec.cas_sorties * toe.cas_sortie_cost
	if sup > 0:
		r.lines.append({"label": "지원 (포병 %d회, CAS %d회)" % [spec.artillery_missions, spec.cas_sorties], "cost": sup})
		r.total += sup

	if r.total > budget:
		r.errors.append("예산 초과: %d / %d 점" % [r.total, budget])
	r.sustain_min = sustain_minutes(catalog, spec)
	return r


static func _check_companies(catalog: DataCatalog, spec: ForceSpec, toe: TOETemplate, r: Report) -> void:
	if spec.companies.size() > toe.max_companies:
		r.errors.append("중대는 최대 %d개입니다." % toe.max_companies)
	var used: Dictionary = {}
	var template_tank_cos: int = 0
	var composed_tank_cos: int = 0
	var cross: bool = false
	for c: ForceSpec.CompanySpec in spec.companies:
		var slot: TOETemplate.CompanySlot = _slot(toe, c.slot)
		var ut: UnitTemplate = catalog.get_unit_template(c.template)
		var label: String = "%s 중대" % _letter(spec, c)
		if slot == null:
			r.errors.append("%s: 편제표에 없는 슬롯 '%s'" % [label, c.slot])
			continue
		if used.has(c.slot):
			r.errors.append("슬롯 '%s' 가 두 번 쓰였습니다." % c.slot)
		used[c.slot] = true
		if ut == null or ut.echelon != &"company" or not slot.allowed.has(String(c.template)):
			r.errors.append("%s: '%s' 는 이 슬롯에 넣을 수 없습니다." % [label, c.template])
			continue
		if ut.kind == &"tank":
			template_tank_cos += 1
		var hq_cost: int = company_hq_cost(catalog, c.template)
		r.total += hq_cost
		r.element_count += ut.hq_elements.size()
		var cost: int = hq_cost
		var tanks: int = 0
		for p: ForceSpec.PlatoonSpec in c.platoons:
			_check_platoon(catalog, p, spec.faction, label, r)
			var pc: int = platoon_cost(catalog, p)
			cost += pc
			r.total += pc
			var pt: UnitTemplate = catalog.get_unit_template(p.template)
			if pt != null and pt.kind == &"tank":
				tanks += 1
			if p.home != c.slot:
				cross = true
		if c.platoons.is_empty():
			r.errors.append("%s 에 소대가 없습니다." % label)
		elif c.platoons.size() < MIN_PLATOONS or c.platoons.size() > MAX_PLATOONS:
			r.warnings.append("%s: 소대 %d개 — 비편제(명령 지연 +20%%, 재편 불가)" % [label, c.platoons.size()])
			r.ad_hoc.append(c.slot)
		if tanks * 2 >= c.platoons.size() and not c.platoons.is_empty():
			composed_tank_cos += 1
		r.lines.append({"label": "%s (%s)" % [label, ut.display_name], "cost": cost})
	for slot: TOETemplate.CompanySlot in toe.company_slots:
		if slot.required and not used.has(slot.id):
			r.errors.append("필수 중대 슬롯 '%s' 가 비어 있습니다." % slot.id)
	var min_tank: int = 0
	if toe.constraints.has("min_tank_co"):
		min_tank = ForceSpec.to_int(toe.constraints["min_tank_co"])
	if template_tank_cos < min_tank:
		r.errors.append("전차 중대가 최소 %d개 필요합니다(현재 %d)." % [min_tank, template_tank_cos])
	elif cross and composed_tank_cos < min_tank:
		# 교차 배속으로 전차 위주 중대가 모자라게 됐다 → 소대를 주고받은 중대 모두 비편제.
		r.warnings.append("교차 배속으로 전차 위주 중대가 %d개(최소 %d) — 해당 중대 비편제" % [composed_tank_cos, min_tank])
		for c: ForceSpec.CompanySpec in spec.companies:
			if _involved_in_cross(spec, c) and not r.ad_hoc.has(c.slot):
				r.ad_hoc.append(c.slot)


static func _involved_in_cross(spec: ForceSpec, c: ForceSpec.CompanySpec) -> bool:
	for p: ForceSpec.PlatoonSpec in c.platoons:
		if p.home != c.slot:
			return true
	for other: ForceSpec.CompanySpec in spec.companies:
		if other == c:
			continue
		for p: ForceSpec.PlatoonSpec in other.platoons:
			if p.home == c.slot:
				return true
	return false


static func _check_list(catalog: DataCatalog, list: Array[ForceSpec.PlatoonSpec], allowed: PackedStringArray,
		what: String, r: Report, max_count: int) -> void:
	if max_count >= 0 and list.size() > max_count:
		r.errors.append("%s 자산은 최대 %d개입니다." % [what, max_count])
	var seen: Dictionary = {}
	for p: ForceSpec.PlatoonSpec in list:
		var ut: UnitTemplate = catalog.get_unit_template(p.template)
		if not allowed.has(String(p.template)):
			r.errors.append("%s: '%s' 는 이 편제표에서 쓸 수 없습니다." % [what, p.template])
			continue
		if max_count < 0 and seen.has(p.template):
			r.errors.append("%s: '%s' 가 중복됐습니다." % [what, p.template])
		seen[p.template] = true
		_check_platoon(catalog, p, ut.faction if ut != null else &"", what, r)
		var cost: int = platoon_cost(catalog, p)
		r.total += cost
		r.lines.append({"label": "%s: %s" % [what, ut.display_name if ut != null else String(p.template)], "cost": cost})


static func _check_platoon(catalog: DataCatalog, p: ForceSpec.PlatoonSpec, faction: StringName, where: String, r: Report) -> void:
	var ut: UnitTemplate = catalog.get_unit_template(p.template)
	if ut == null or ut.echelon != &"platoon":
		r.errors.append("%s: 소대 템플릿 '%s' 가 없습니다." % [where, p.template])
		return
	if ut.faction != faction:
		r.errors.append("%s: '%s' 는 다른 진영 부대입니다." % [where, ut.display_name])
	if p.loadout != &"" and ut.get_loadout(p.loadout) == null:
		r.errors.append("%s: '%s' 에 장비 템플릿 '%s' 가 없습니다." % [where, ut.display_name, p.loadout])
	if p.wave < 0 or p.wave > ForceSpec.MAX_WAVE:
		r.errors.append("%s: 증원 단계 %d 는 0~2 여야 합니다." % [where, p.wave])
	r.element_count += ut.elements_for(p.loadout).size() + ut.passengers_for(p.loadout).size()


static func _slot(toe: TOETemplate, slot_id: StringName) -> TOETemplate.CompanySlot:
	for s: TOETemplate.CompanySlot in toe.company_slots:
		if s.id == slot_id:
			return s
	return null


static func _letter(spec: ForceSpec, c: ForceSpec.CompanySpec) -> String:
	var i: int = spec.companies.find(c)
	return ForceFactory.COMPANY_LETTERS[i % ForceFactory.COMPANY_LETTERS.size()] if i >= 0 else "?"


## 예상 지속 교전 시간(분): 전투 엘리먼트 주무장 탄이 가동률 DUTY_CYCLE 로 떨어지는 시간 평균 ×
## (1 + 보급반이 실은 보급량 / 전투단 완전 보급 필요량). 편성 화면 표시용 근사치. PLAN 6.2.
static func sustain_minutes(catalog: DataCatalog, spec: ForceSpec) -> float:
	var minutes_sum: float = 0.0
	var shooters: int = 0
	var need: int = 0
	var carried: int = 0
	for p: ForceSpec.PlatoonSpec in all_platoons(spec):
		var ut: UnitTemplate = catalog.get_unit_template(p.template)
		if ut == null:
			continue
		var ids: PackedStringArray = ut.elements_for(p.loadout).duplicate()
		ids.append_array(ut.passengers_for(p.loadout))
		for id: String in ids:
			var ed: ElementDef = catalog.get_element(StringName(id))
			if ed == null:
				continue
			carried += ed.supply_capacity
			need += ed.supply_full
			if ed.weapons.is_empty():
				continue
			var w: WeaponDef = catalog.get_weapon(StringName(ed.weapons[0]))
			var rounds: int = ed.ammo.get(w.id, 0) if w != null else 0
			if w == null or rounds <= 0 or w.rof_rpm <= 0.0:
				continue
			minutes_sum += rounds / (w.rof_rpm * DUTY_CYCLE)
			shooters += 1
	if shooters == 0:
		return 0.0
	var base: float = minutes_sum / shooters
	var refills: float = carried / float(need) if need > 0 else 0.0
	return base * (1.0 + refills)


static func all_platoons(spec: ForceSpec) -> Array[ForceSpec.PlatoonSpec]:
	var out: Array[ForceSpec.PlatoonSpec] = []
	for c: ForceSpec.CompanySpec in spec.companies:
		out.append_array(c.platoons)
	out.append_array(spec.organic)
	out.append_array(spec.attachments)
	return out
