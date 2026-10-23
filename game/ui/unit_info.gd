class_name UnitInfo
extends RefCounted
## 편성 화면의 정보 카드·툴팁 문장. 소대·중대 템플릿(장비 템플릿 반영)에서 역할, 장단점, 능력치 막대,
## 장비별 장갑·속도·탐지·무장(사거리·관통·표적)을 뽑아 BBCode 로 만든다. 데이터만 읽는다.

const KIND_NAMES: Dictionary[StringName, String] = {
	&"tank": "기갑", &"mech": "기계화보병", &"inf": "보병(도보)", &"recon": "정찰", &"mortar": "박격포",
	&"supply": "보급", &"hq": "본부", &"cp": "지휘소", &"fo": "관측", &"aa": "방공", &"ew": "전자전",
	&"engineer": "공병", &"wheeled": "차륜형 보병", &"at": "대전차", &"sniper": "저격", &"aviation": "공격헬기",
}
const CLASS_NAMES: Dictionary[StringName, String] = {
	&"tank": "전차", &"ifv": "보병전투차", &"apc": "장갑차", &"recon": "정찰차", &"mortar": "자주박격포",
	&"aa": "방공", &"cp": "지휘차", &"truck": "트럭", &"inf_squad": "보병", &"fo": "관측", &"ew": "전자전",
	&"heli": "공격헬기", &"eng": "공병 장비", &"at": "대전차 차량",
}
const WEAPON_TYPE_NAMES: Dictionary[StringName, String] = {
	&"KE": "철갑탄", &"HEAT": "성형작약", &"ATGM": "대전차 미사일", &"HE": "고폭", &"MG": "기관총·소총",
	&"AC": "기관포", &"SAM": "지대공 미사일",
}
const TARGET_NAMES: Dictionary[String, String] = {"vehicle": "차량", "infantry": "보병", "air": "헬기"}
## 능력치 막대: [키, 이름].
const RATINGS: Array[Array] = [
	["at", "대전차"], ["inf", "대보병"], ["armor", "방호"], ["mobility", "기동"], ["recon", "정찰·은밀"], ["aa", "대공"],
]
const ACCENT: String = "#f2cc59"
const DIM: String = "#a6a9b3"
const GOOD: String = "#8ce99a"
const BAD: String = "#ff8a80"


## 소대 템플릿 카드(BBCode).
static func platoon_card(cat: DataCatalog, template_id: StringName, loadout: StringName = &"", cost: int = -1) -> String:
	var ut: UnitTemplate = cat.get_unit_template(template_id)
	if ut == null:
		return ""
	var elems: PackedStringArray = ut.elements_for(_loadout_or_default(ut, loadout))
	var pax: PackedStringArray = ut.passengers_for(_loadout_or_default(ut, loadout))
	var t: String = _header(ut, cost if cost >= 0 else _sum_cost(cat, elems, pax))
	var lo: UnitTemplate.Loadout = ut.get_loadout(_loadout_or_default(ut, loadout))
	if lo != null and ut.loadouts.size() > 1:
		t += "[color=%s]장비 템플릿: %s[/color]\n" % [DIM, lo.display_name]
	t += _role_block(ut)
	t += _ratings_block(_ratings(cat, elems, pax))
	t += "\n[b]구성[/b]\n"
	t += _elements_block(cat, elems, "")
	if not pax.is_empty():
		t += _elements_block(cat, pax, "탑승 ")
	return t


## 중대 템플릿 카드.
static func company_card(cat: DataCatalog, template_id: StringName) -> String:
	var ut: UnitTemplate = cat.get_unit_template(template_id)
	if ut == null:
		return ""
	var all := PackedStringArray()
	var all_pax := PackedStringArray()
	all.append_array(ut.hq_elements)
	var plt_counts: Dictionary[String, int] = {}
	for pid: String in ut.platoons:
		var pt: UnitTemplate = cat.get_unit_template(StringName(pid))
		if pt == null:
			continue
		all.append_array(pt.elements)
		all_pax.append_array(pt.passengers)
		plt_counts[pid] = plt_counts.get(pid, 0) + 1
	var t: String = _header(ut, _sum_cost(cat, all, all_pax))
	t += _role_block(ut)
	t += _ratings_block(_ratings(cat, all, all_pax))
	t += "\n[b]구성[/b]  [color=%s](차량 %d, 분대 %d)[/color]\n" % [DIM, _count_vehicles(cat, all), _count_squads(cat, all, all_pax)]
	t += "• 중대 본부: %s\n" % _summary_list(cat, ut.hq_elements)
	for pid: String in plt_counts.keys():
		var pt2: UnitTemplate = cat.get_unit_template(StringName(pid))
		t += "• %s × %d: %s%s\n" % [pt2.display_name, plt_counts[pid], _summary_list(cat, pt2.elements),
			(" + 탑승 " + _summary_list(cat, pt2.passengers)) if not pt2.passengers.is_empty() else ""]
	# 대표 소대 장비 상세(첫 소대 템플릿).
	if not ut.platoons.is_empty():
		var first: UnitTemplate = cat.get_unit_template(StringName(ut.platoons[0]))
		if first != null:
			t += "\n[b]주 장비[/b]\n"
			t += _elements_block(cat, first.elements, "")
			if not first.passengers.is_empty():
				t += _elements_block(cat, first.passengers, "탑승 ")
	return t


## 마우스 툴팁(일반 문자열, 몇 줄).
static func tooltip(cat: DataCatalog, template_id: StringName, loadout: StringName = &"") -> String:
	var ut: UnitTemplate = cat.get_unit_template(template_id)
	if ut == null:
		return ""
	var elems: PackedStringArray
	var pax: PackedStringArray
	if ut.echelon == &"company":
		elems = ut.hq_elements.duplicate()
		pax = PackedStringArray()
		for pid: String in ut.platoons:
			var pt: UnitTemplate = cat.get_unit_template(StringName(pid))
			if pt != null:
				elems.append_array(pt.elements)
				pax.append_array(pt.passengers)
	else:
		elems = ut.elements_for(_loadout_or_default(ut, loadout))
		pax = ut.passengers_for(_loadout_or_default(ut, loadout))
	var lines := PackedStringArray()
	lines.append("%s  (%s, %d점)" % [ut.display_name, KIND_NAMES.get(ut.kind, String(ut.kind)), _sum_cost(cat, elems, pax)])
	if ut.role != "":
		lines.append(ut.role)
	var r: Dictionary = _ratings(cat, elems, pax)
	var parts := PackedStringArray()
	for item: Array in RATINGS:
		var v: int = r.get(item[0], 0)
		parts.append("%s %s" % [item[1], "●".repeat(v) + "○".repeat(5 - v)])
	lines.append("  ".join(parts.slice(0, 3)))
	lines.append("  ".join(parts.slice(3, 6)))
	for p: String in ut.pros:
		lines.append("+ " + p)
	for c: String in ut.cons:
		lines.append("- " + c)
	return "\n".join(lines)


# --- 블록 ----------------------------------------------------------------

static func _header(ut: UnitTemplate, cost: int) -> String:
	return "[font_size=19][color=%s][b]%s[/b][/color][/font_size]\n[color=%s]%s · %s · %d점[/color]\n" % [
		ACCENT, ut.display_name, DIM, KIND_NAMES.get(ut.kind, String(ut.kind)),
		"중대" if ut.echelon == &"company" else "소대", cost]


static func _role_block(ut: UnitTemplate) -> String:
	var t: String = ""
	if ut.role != "":
		t += "\n%s\n" % ut.role
	if not ut.pros.is_empty() or not ut.cons.is_empty():
		t += "\n"
		for p: String in ut.pros:
			t += "[color=%s]▲[/color] %s\n" % [GOOD, p]
		for c: String in ut.cons:
			t += "[color=%s]▼[/color] %s\n" % [BAD, c]
	return t


static func _ratings_block(r: Dictionary) -> String:
	var t: String = "\n[table=2]"
	for item: Array in RATINGS:
		var v: int = r.get(item[0], 0)
		t += "[cell][color=%s]%s[/color]   [/cell][cell][color=%s]%s[/color][color=#4a4d55]%s[/color][/cell]" % [
			DIM, item[1], ACCENT, "■".repeat(v), "■".repeat(5 - v)]
	return t + "[/table]\n"


static func _elements_block(cat: DataCatalog, ids: PackedStringArray, prefix: String) -> String:
	var counts: Dictionary[String, int] = {}
	var order := PackedStringArray()
	for id: String in ids:
		if not counts.has(id):
			order.append(id)
		counts[id] = counts.get(id, 0) + 1
	var t: String = ""
	for id: String in order:
		var ed: ElementDef = cat.get_element(StringName(id))
		if ed == null:
			continue
		t += "\n[b]%s%s × %d[/b]  [color=%s]%s, %d점[/color]\n" % [prefix, ed.display_name, counts[id], DIM,
			CLASS_NAMES.get(ed.element_class, String(ed.element_class)), ed.cost]
		if ed.element_class == &"inf_squad":
			t += "[color=%s]인원 %d · 은폐 %.1f · 관측 %.1f km · 도보 %d km/h[/color]\n" % [DIM, roundi(ed.hp), ed.stealth,
				ed.optics_m / 1000.0, roundi(ed.speed_offroad_kmh)]
		else:
			t += "[color=%s]장갑 정면 %d / 측면 %d / 후면 %d / 상면 %d\n속도 도로 %d · 야지 %d km/h · 관측 %.1f km · 은폐 %.2f%s[/color]\n" % [
				DIM, roundi(ed.get_armor(&"front")), roundi(ed.get_armor(&"side")), roundi(ed.get_armor(&"rear")),
				roundi(ed.get_armor(&"top")), roundi(ed.speed_road_kmh), roundi(ed.speed_offroad_kmh), ed.optics_m / 1000.0,
				ed.stealth, (" · 보병 %d개 분대 탑승" % ed.transport_capacity) if ed.transport_capacity > 0 else ""]
			if ed.element_class == &"heli":
				t += "[color=%s]지형 무시 비행 · 저공 25 m / 고공 90 m(V) · 대공 무기만 맞힐 수 있음[/color]\n" % GOOD
		for wid: String in ed.weapons:
			var w: WeaponDef = cat.get_weapon(StringName(wid))
			if w == null:
				continue
			var rounds: int = ed.ammo.get(w.id, 0)
			t += "  · %s\n" % weapon_line(w, rounds)
	return t


## 무기 한 줄: 이름(종류) 사거리, 관통, 표적, 탄수.
static func weapon_line(w: WeaponDef, ammo: int) -> String:
	var targets := PackedStringArray()
	for tk: String in w.targets:
		var tname: String = TARGET_NAMES.get(tk, tk)
		targets.append(tname)
	var extra := PackedStringArray()
	if w.top_attack:
		extra.append("상부 공격")
	if w.fire_on_move:
		extra.append("이동 사격")
	if w.he_radius_m > 0.0:
		extra.append("폭발 %d m" % roundi(w.he_radius_m))
	if w.indirect:
		extra.append("간접")
	var pen: String = ("관통 %d" % roundi(w.penetration)) if w.penetration >= 20.0 else ""
	return "%s [color=%s](%s)[/color]  %.1f km%s  · %s%s%s" % [w.display_name, DIM, WEAPON_TYPE_NAMES.get(w.weapon_type, String(w.weapon_type)),
		w.range_max_m / 1000.0, ("  · " + pen) if pen != "" else "", "/".join(targets),
		("  · " + ", ".join(extra)) if not extra.is_empty() else "", ("  · %d발" % ammo) if ammo > 0 else ""]


# --- 능력치 --------------------------------------------------------------

## 0~5 점: 대전차, 대보병, 방호, 기동, 정찰·은밀, 대공. 대략적인 비교용 지표(전투 판정과 같은 데이터에서 계산).
static func _ratings(cat: DataCatalog, elems: PackedStringArray, pax: PackedStringArray) -> Dictionary:
	var all := PackedStringArray()
	all.append_array(elems)
	all.append_array(pax)
	var at: float = 0.0
	var inf: float = 0.0
	var armor: float = 0.0
	var mob: float = 9999.0
	var recon: float = 0.0
	var aa: float = 0.0
	var n: int = 0
	var vehicles: int = 0
	for id: String in all:
		var ed: ElementDef = cat.get_element(StringName(id))
		if ed == null:
			continue
		n += 1
		var e_at: float = 0.0
		var e_inf: float = 0.0
		var e_aa: float = 0.0
		for wid: String in ed.weapons:
			var w: WeaponDef = cat.get_weapon(StringName(wid))
			if w == null or w.indirect:
				continue
			if w.can_target(&"vehicle"):
				# 상부 공격은 전차 상면 장갑을 노리므로 주력전차를 뚫는 것으로 친다.
				var pen: float = maxf(w.penetration, 900.0) if w.top_attack else w.penetration
				var v: float = clampf(pen / 900.0, 0.0, 1.25) * clampf(w.range_max_m / 4000.0, 0.25, 1.0)
				e_at = maxf(e_at, v)
			if w.can_target(&"infantry"):
				var rate: float = clampf(w.rof_rpm * w.damage / 1200.0, 0.1, 1.5)
				e_inf += rate * (1.0 + w.he_radius_m / 8.0) * clampf(w.range_max_m / 1500.0, 0.3, 1.5)
			if w.can_target(&"air"):
				var aa_v: float = 0.4
				if w.weapon_type == &"SAM":
					aa_v = 1.0
				elif ed.element_class == &"aa":
					aa_v = 0.8
				e_aa = maxf(e_aa, aa_v)
		at += e_at
		inf += minf(e_inf, 2.5)
		aa = maxf(aa, e_aa)
		recon = maxf(recon, ed.optics_m / 4500.0 * (0.6 + ed.stealth))
		if ed.element_class != &"inf_squad":
			vehicles += 1
			armor += ed.get_armor(&"front") / 900.0 * 0.7 + ed.get_armor(&"side") / 350.0 * 0.3
		if ed.element_class == &"heli":
			mob = minf(mob, 2.5)
		elif ed.element_class == &"inf_squad" and pax.has(id):
			pass  # 탑승 분대는 차량 속도로 움직인다
		else:
			mob = minf(mob, (ed.speed_road_kmh / 100.0) * 0.5 + (ed.speed_offroad_kmh / 50.0) * 0.5)
	if n == 0:
		return {}
	var size_f: float = clampf(float(n) / 4.0, 0.7, 1.3)
	var out: Dictionary = {}
	out["at"] = _stars(at / n * size_f * 1.1)
	out["inf"] = _stars(inf / n * size_f * 0.4)
	out["armor"] = _stars(armor / maxf(vehicles, 1) * (float(vehicles) / n) + (0.15 if vehicles < n else 0.0))
	out["mobility"] = _stars(mob if mob < 9000.0 else 0.0)
	out["recon"] = _stars(recon)
	out["aa"] = _stars(aa)
	return out


static func _stars(x: float) -> int:
	return clampi(roundi(x * 5.0), 0, 5)


static func _loadout_or_default(ut: UnitTemplate, loadout: StringName) -> StringName:
	if loadout != &"":
		return loadout
	return ut.loadouts[0].id if not ut.loadouts.is_empty() else &""


static func _sum_cost(cat: DataCatalog, elems: PackedStringArray, pax: PackedStringArray) -> int:
	var c: int = 0
	for id: String in elems:
		var ed: ElementDef = cat.get_element(StringName(id))
		c += ed.cost if ed != null else 0
	for id: String in pax:
		var ed2: ElementDef = cat.get_element(StringName(id))
		c += ed2.cost if ed2 != null else 0
	return c


static func _summary_list(cat: DataCatalog, ids: PackedStringArray) -> String:
	var counts: Dictionary[String, int] = {}
	var order := PackedStringArray()
	for id: String in ids:
		if not counts.has(id):
			order.append(id)
		counts[id] = counts.get(id, 0) + 1
	var parts := PackedStringArray()
	for id: String in order:
		var ed: ElementDef = cat.get_element(StringName(id))
		parts.append("%s %d" % [ed.display_name if ed != null else id, counts[id]])
	return ", ".join(parts)


static func _count_vehicles(cat: DataCatalog, ids: PackedStringArray) -> int:
	var n: int = 0
	for id: String in ids:
		var ed: ElementDef = cat.get_element(StringName(id))
		if ed != null and ed.element_class != &"inf_squad":
			n += 1
	return n


static func _count_squads(cat: DataCatalog, ids: PackedStringArray, pax: PackedStringArray) -> int:
	var n: int = pax.size()
	for id: String in ids:
		var ed: ElementDef = cat.get_element(StringName(id))
		if ed != null and ed.element_class == &"inf_squad":
			n += 1
	return n
