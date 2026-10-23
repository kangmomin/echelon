class_name ForceFactory
extends RefCounted
## 편제 템플릿으로 제대·엘리먼트를 만들어 Battle 에 등록한다. PLAN 6장.
## 생성 순서가 곧 id 순서이므로, 같은 입력이면 같은 id 가 나온다(결정론).

const COMPANY_LETTERS: Array[String] = ["A", "B", "C", "D", "E", "F"]
## 중대본부 반이 중대 가이드 뒤로 떨어지는 거리(m).
const HQ_OFFSET_BEHIND_M: float = 150.0

var battle: Battle
var catalog: DataCatalog


func _init(p_battle: Battle) -> void:
	battle = p_battle
	catalog = p_battle.catalog


## 대대 전투단과 지휘소 반을 만든다. 중대·소대는 따로 붙인다.
func create_task_force(side: StringName, toe: TOETemplate, pos: Vector2, heading: float) -> TaskForce:
	var tf := TaskForce.new()
	tf.side = side
	tf.toe = toe
	tf.faction = toe.faction
	tf.display_name = toe.display_name
	tf.short_name = "TF"
	tf.template_id = toe.id
	tf.kind = &"tf"
	tf.command_capacity_per_min = toe.command_capacity_per_min
	tf.guide.reset(pos, heading)
	battle.register_unit(tf)

	var ids := PackedStringArray()
	for i: int in toe.hq_count:
		ids.append(String(toe.hq_element))
	var cp: Platoon = spawn_section(ids, side, &"cp", "대대 지휘소", "CP", pos, heading, tf)
	cp.is_hq_section = true
	tf.cp_section = cp
	tf.commander = cp.elements[0]
	battle.task_forces[side] = tf
	return tf


## 소대 템플릿으로 소대를 만든다.
func spawn_platoon(template: UnitTemplate, side: StringName, pos: Vector2, heading: float, parent: Unit,
		display: String = "", short: String = "", loadout: StringName = &"") -> Platoon:
	assert(template.echelon == &"platoon")
	var p := Platoon.new()
	p.side = side
	p.template_id = template.id
	p.kind = template.kind
	p.skill = template.skill
	p.loadout = loadout if loadout != &"" else (template.loadouts[0].id if not template.loadouts.is_empty() else &"")
	p.display_name = display if display != "" else template.display_name
	p.short_name = short if short != "" else template.display_name
	p.formation = _formation_or(template.default_formation, &"wedge")
	p.parent = parent
	p.guide.reset(pos, heading)
	battle.register_unit(p)

	for element_id: String in template.elements_for(p.loadout):
		_add_element(p, StringName(element_id))
	var vehicles: Array[Element] = p.vehicles()
	var seat: int = 0
	for squad_id: String in template.passengers_for(p.loadout):
		var squad: Element = _add_element(p, StringName(squad_id))
		while seat < vehicles.size() and vehicles[seat].passengers.size() >= vehicles[seat].def.transport_capacity:
			seat += 1
		if seat < vehicles.size():
			squad.mounted_in = vehicles[seat]
			vehicles[seat].passengers.append(squad)
	# 도보 소대(차량 없음)는 "하차" 상태로 둔다.
	p.mounted = p.has_squads() and p.has_transport()
	if p.kind == &"engineer":
		p.mines_left = EngineerSystem.MINEFIELDS_PER_PLATOON
	p.commander = p.elements[0]
	p.initial_element_count = p.elements.size()
	place_elements(p)
	return p


## 템플릿 없이 엘리먼트 목록으로 반(중대본부·지휘소)을 만든다.
func spawn_section(element_ids: PackedStringArray, side: StringName, kind: StringName, display: String,
		short: String, pos: Vector2, heading: float, parent: Unit) -> Platoon:
	var p := Platoon.new()
	p.side = side
	p.kind = kind
	p.display_name = display
	p.short_name = short
	p.formation = _formation_or(&"line", &"wedge")
	p.parent = parent
	p.guide.reset(pos, heading)
	battle.register_unit(p)
	for element_id: String in element_ids:
		_add_element(p, StringName(element_id))
	p.mounted = false
	p.commander = p.elements[0] if not p.elements.is_empty() else null
	p.initial_element_count = p.elements.size()
	place_elements(p)
	return p


## 중대 템플릿으로 중대를 만든다. platoon_templates 를 주면 템플릿의 소대 목록 대신 쓴다(교차 배속).
func spawn_company(template: UnitTemplate, side: StringName, pos: Vector2, heading: float, parent: Unit,
		index: int, platoon_templates: Array[UnitTemplate] = [], loadouts: Array[StringName] = []) -> Company:
	assert(template.echelon == &"company")
	var c := Company.new()
	c.side = side
	c.template_id = template.id
	c.kind = template.kind
	c.skill = template.skill
	var letter: String = COMPANY_LETTERS[index % COMPANY_LETTERS.size()]
	c.display_name = "%s 중대 (%s)" % [letter, template.display_name]
	c.short_name = letter
	c.formation = _formation_or(template.default_formation, &"co_wedge")
	c.parent = parent
	c.guide.reset(pos, heading)
	battle.register_unit(c)

	var hq_pos: Vector2 = FormationSolver.local_to_world(Vector2(0.0, -HQ_OFFSET_BEHIND_M), pos, heading)
	c.hq = spawn_section(template.hq_elements, side, &"hq", "%s 중대본부" % letter, "%s/HQ" % letter,
		battle.terrain.nearest_passable(hq_pos, Terrain.MOBILITY_VEHICLE), heading, c)
	c.hq.is_hq_section = true
	c.commander = c.hq.elements[0]
	c.xo = c.hq.elements[1] if c.hq.elements.size() > 1 else null

	var plts: Array[UnitTemplate] = platoon_templates.duplicate()
	if plts.is_empty():
		for plt_id: String in template.platoons:
			plts.append(catalog.get_unit_template(StringName(plt_id)))
	var slots: PackedVector2Array = FormationSolver.world_slots(c.formation, plts.size(), pos, heading)
	for i: int in plts.size():
		var lo: StringName = loadouts[i] if i < loadouts.size() else &""
		var p: Platoon = spawn_platoon(plts[i], side, battle.terrain.nearest_passable(slots[i], Terrain.MOBILITY_VEHICLE),
			heading, c, "%s 중대 %d소대" % [letter, i + 1], "%s/%d" % [letter, i + 1], lo)
		c.platoons.append(p)
	c.initial_element_count = c.all_elements().size()
	return c


## 편성 결과(ForceSpec)로 전투단 전체를 만든다. 초기 배치 부대는 spec.deploy(없으면 자동 배치) 위치에,
## 증원 부대는 진입 지점에 지도 밖(on_map = false)으로 만든다. 등장 처리는 증원 시스템이 한다. PLAN 6장, 11.2.
func build_task_force(spec: ForceSpec, side: StringName) -> TaskForce:
	var toe: TOETemplate = catalog.get_toe(spec.toe)
	assert(toe != null, "편제표 없음: %s" % spec.toe)
	var setup: ScenarioDef.SideSetup = battle.scenario.sides.get(side)
	var zone: Rect2 = setup.deploy_zone if setup != null else Rect2(Vector2.ZERO, battle.terrain.size_m)
	var deploy_spec: ForceSpec = spec.duplicate_spec()
	Deployment.auto_fill(deploy_spec, zone, battle.terrain.size_m, battle.terrain)
	var entry: Vector2 = zone.get_center()
	if setup != null and not setup.entry_points.is_empty():
		entry = setup.entry_points[clampi(spec.entry_point, 0, setup.entry_points.size() - 1)]
	entry = battle.terrain.nearest_passable(battle.terrain.clamp_to_map(entry, 30.0), Terrain.MOBILITY_VEHICLE)
	var heading: float = Deployment.facing(zone, battle.terrain.size_m)
	var report: ForceRules.Report = ForceRules.evaluate(catalog, spec, setup.budget if setup != null else 100000)

	var cp: Vector3 = deploy_spec.deploy["cp"]
	var tf: TaskForce = create_task_force(side, toe, Vector2(cp.x, cp.y), cp.z)
	tf.display_name = spec.name if spec.name != "" else toe.display_name
	tf.artillery_missions = spec.artillery_missions
	tf.cas_sorties = spec.cas_sorties
	tf.entry_pos = entry

	for i: int in spec.companies.size():
		var cs: ForceSpec.CompanySpec = spec.companies[i]
		var ut: UnitTemplate = catalog.get_unit_template(cs.template)
		var plts: Array[UnitTemplate] = []
		var los: Array[StringName] = []
		for ps: ForceSpec.PlatoonSpec in cs.platoons:
			plts.append(catalog.get_unit_template(ps.template))
			los.append(ps.loadout)
		var wave: int = cs.wave()
		var key: String = "co:%s" % cs.slot
		var at: Vector3 = deploy_spec.deploy.get(key, Vector3(entry.x, entry.y, heading))
		var c: Company = spawn_company(ut, side, Vector2(at.x, at.y), at.z, tf, i, plts, los)
		c.ad_hoc = report.ad_hoc.has(cs.slot)
		c.phase = wave
		c.hq.phase = wave
		tf.companies.append(c)
		for j: int in c.platoons.size():
			var ps: ForceSpec.PlatoonSpec = cs.platoons[j]
			var p: Platoon = c.platoons[j]
			p.phase = ps.wave
			p.ad_hoc = c.ad_hoc
			if ps.home != cs.slot and ps.home != &"":
				var home_index: int = _slot_index(spec, ps.home)
				var from_letter: String = COMPANY_LETTERS[home_index % COMPANY_LETTERS.size()] if home_index >= 0 else "?"
				p.display_name += " (%s 중대에서 배속)" % from_letter
			if ps.wave > 0:
				set_off_map(p, entry, heading)
		if wave > 0:
			set_off_map(c, entry, heading)
			set_off_map(c.hq, entry, heading)
	var org_specs: Array[ForceSpec.PlatoonSpec] = []
	var org_keys: Array[String] = []
	for i: int in spec.organic.size():
		org_specs.append(spec.organic[i])
		org_keys.append("org:%d" % i)
	for i: int in spec.attachments.size():
		org_specs.append(spec.attachments[i])
		org_keys.append("att:%d" % i)
	for i: int in org_specs.size():
		var ps: ForceSpec.PlatoonSpec = org_specs[i]
		var at: Vector3 = deploy_spec.deploy.get(org_keys[i], Vector3(entry.x, entry.y, heading))
		var p: Platoon = spawn_platoon(catalog.get_unit_template(ps.template), side, Vector2(at.x, at.y), at.z, tf,
			"", "", ps.loadout)
		p.phase = ps.wave
		if org_keys[i].begins_with("att:"):
			p.attached = true
		tf.organic.append(p)
		if ps.wave > 0:
			set_off_map(p, entry, heading)
	return tf


static func _slot_index(spec: ForceSpec, slot_id: StringName) -> int:
	for i: int in spec.companies.size():
		if spec.companies[i].slot == slot_id:
			return i
	return -1


## 증원 대기: 지도 밖으로 빼서 진입 지점에 둔다.
func set_off_map(u: Unit, entry: Vector2, heading: float) -> void:
	u.on_map = false
	u.guide.reset(entry, heading)
	for e: Element in u.all_elements():
		e.on_map = false
		e.pos = entry
		e.prev_pos = entry
		e.heading = heading
		e.prev_heading = heading
		e.velocity = Vector2.ZERO
		e.has_move_target = false


## 배치 단계에서 제대를 옮긴다(중대면 소대·본부까지 대형대로). 시뮬 시간이 흐르기 전에만 쓴다.
func redeploy(u: Unit, pos: Vector2, heading: float) -> void:
	if u is Company:
		var c: Company = u
		c.guide.reset(pos, heading)
		var hq_pos: Vector2 = FormationSolver.local_to_world(Vector2(0.0, -HQ_OFFSET_BEHIND_M), pos, heading)
		if c.hq != null and c.hq.on_map:
			c.hq.guide.reset(battle.terrain.nearest_passable(hq_pos, Terrain.MOBILITY_VEHICLE), heading)
			place_elements(c.hq)
		var plts: Array[Platoon] = []
		for p: Platoon in c.platoons:
			if p.on_map:
				plts.append(p)
		var slots: PackedVector2Array = FormationSolver.world_slots(c.formation, plts.size(), pos, heading)
		for i: int in plts.size():
			plts[i].guide.reset(battle.terrain.nearest_passable(slots[i], Terrain.MOBILITY_VEHICLE), heading)
			place_elements(plts[i])
	elif u is Platoon:
		u.guide.reset(battle.terrain.nearest_passable(pos, Terrain.MOBILITY_VEHICLE), heading)
		place_elements(u as Platoon)
	elif u is TaskForce:
		var tf: TaskForce = u
		tf.guide.reset(pos, heading)
		if tf.cp_section != null:
			tf.cp_section.guide.reset(battle.terrain.nearest_passable(pos, Terrain.MOBILITY_VEHICLE), heading)
			place_elements(tf.cp_section)


## 소대 엘리먼트를 가이드 기준 대형 슬롯에 놓는다.
func place_elements(p: Platoon) -> void:
	var members: Array[Element] = p.formation_members()
	var slots: PackedVector2Array = FormationSolver.world_slots(p.formation, members.size(), p.guide.pos, p.guide.heading)
	for i: int in members.size():
		var e: Element = members[i]
		e.pos = battle.terrain.nearest_passable(slots[i], e.mobility())
		e.prev_pos = e.pos
		e.heading = p.guide.heading
		e.prev_heading = e.heading
		e.rest_heading = e.heading
		e.move_target = e.pos
		e.has_move_target = true
		for passenger: Element in e.passengers:
			passenger.pos = e.pos
			passenger.prev_pos = e.pos
			passenger.heading = e.heading


func _add_element(p: Platoon, element_id: StringName) -> Element:
	var def: ElementDef = catalog.get_element(element_id)
	assert(def != null, "엘리먼트 정의 없음: %s" % element_id)
	var e := Element.new()
	e.def = def
	e.side = p.side
	e.platoon = p
	e.index_in_platoon = p.elements.size()
	e.hp = def.hp
	e.supply_left = def.supply_capacity
	for weapon_id: StringName in def.ammo.keys():
		e.ammo[weapon_id] = def.ammo[weapon_id]
	for weapon_id: String in def.weapons:
		e.reload[StringName(weapon_id)] = 0.0
	e.pos = p.guide.pos
	e.prev_pos = e.pos
	p.elements.append(e)
	battle.register_element(e)
	return e


func _formation_or(id: StringName, fallback: StringName) -> FormationDef:
	var f: FormationDef = catalog.get_formation(id) if id != &"" else null
	return f if f != null else catalog.get_formation(fallback)
