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
	p.mounted = p.has_squads()
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
