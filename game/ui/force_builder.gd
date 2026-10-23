class_name ForceBuilder
extends Control
## 편성 화면(BA 데크 빌더 대체). 진영 → 대대 편제표 → 중대 슬롯 → 교차 배속 → 직할·배속 → 장비 템플릿 →
## 증원 단계 → 지원 구매. 포인트·제약·비편제·예상 지속 교전 시간을 즉시 보여 준다. PLAN 6장, 12장.
## 상태는 ForceSpec 하나. 바뀔 때마다 가운데 목록을 다시 만든다(항목이 적어 충분히 빠르다).

signal start_requested(spec: ForceSpec)
signal back_requested

const FACTIONS: Array[Array] = [["미 육군", &"US"], ["러시아 지상군", &"RU"]]

var catalog: DataCatalog
var scenario: ScenarioDef
var spec: ForceSpec

var _list: VBoxContainer
var _summary: RichTextLabel
var _budget_bar: ProgressBar
var _budget_label: Label
var _name_edit: LineEdit
var _load_option: OptionButton
var _start_button: Button
var _toast: Label


func setup(p_catalog: DataCatalog, p_scenario: ScenarioDef, initial: ForceSpec = null) -> void:
	catalog = p_catalog
	scenario = p_scenario
	spec = initial.duplicate_spec() if initial != null else catalog.default_force(&"US")


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	UiKit.background(self)
	var root := HBoxContainer.new()
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.offset_left = 12
	root.offset_top = 12
	root.offset_right = -12
	root.offset_bottom = -12
	root.add_theme_constant_override(&"separation", 12)
	add_child(root)
	root.add_child(_build_side_panel())
	var center: PanelContainer = UiKit.panel(10)
	center.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	root.add_child(center)
	var scroll := ScrollContainer.new()
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	center.add_child(scroll)
	_list = VBoxContainer.new()
	_list.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_list.add_theme_constant_override(&"separation", 8)
	scroll.add_child(_list)
	_rebuild()


func _budget() -> int:
	for side: ScenarioDef.SideSetup in scenario.sides.values():
		if side.faction == spec.faction:
			return side.budget
	return 2000


# --- 왼쪽: 요약·저장 -------------------------------------------------------

func _build_side_panel() -> Control:
	var p: PanelContainer = UiKit.panel(12)
	p.custom_minimum_size.x = 380
	var v := VBoxContainer.new()
	v.add_theme_constant_override(&"separation", 8)
	p.add_child(v)
	v.add_child(UiKit.label("전투단 편성", 22, UiKit.ACCENT))
	v.add_child(UiKit.label("시나리오: %s" % scenario.display_name, 13, UiKit.TEXT_DIM))
	var row := HBoxContainer.new()
	row.add_child(UiKit.label("진영", 14))
	row.add_child(UiKit.option(FACTIONS, spec.faction, _on_faction, 200))
	v.add_child(row)
	_budget_label = UiKit.label("", 15)
	v.add_child(_budget_label)
	_budget_bar = ProgressBar.new()
	_budget_bar.show_percentage = false
	_budget_bar.custom_minimum_size.y = 10
	v.add_child(_budget_bar)
	_summary = UiKit.rich(Vector2(350, 0))
	_summary.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_summary.fit_content = false
	_summary.scroll_active = true
	v.add_child(_summary)
	v.add_child(HSeparator.new())
	var name_row := HBoxContainer.new()
	name_row.add_child(UiKit.label("이름", 14))
	_name_edit = LineEdit.new()
	_name_edit.text = spec.name
	_name_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_name_edit.text_changed.connect(func(t: String) -> void: spec.name = t)
	name_row.add_child(_name_edit)
	name_row.add_child(UiKit.button("저장", _on_save))
	v.add_child(name_row)
	var load_row := HBoxContainer.new()
	_load_option = OptionButton.new()
	_load_option.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_load_option.focus_mode = Control.FOCUS_NONE
	load_row.add_child(_load_option)
	load_row.add_child(UiKit.button("불러오기", _on_load))
	v.add_child(load_row)
	var preset_row := HBoxContainer.new()
	preset_row.add_child(UiKit.button("기본 편성", func() -> void: _replace(catalog.default_force(spec.faction))))
	preset_row.add_child(UiKit.button("최소 편성", func() -> void: _replace(ForceSpec.from_toe(catalog, spec.toe))))
	v.add_child(preset_row)
	_toast = UiKit.label("", 13, UiKit.OK_COLOR)
	v.add_child(_toast)
	var nav := HBoxContainer.new()
	nav.add_child(UiKit.button("← 메뉴", func() -> void: back_requested.emit()))
	_start_button = UiKit.button("배치로 →", func() -> void: start_requested.emit(spec.duplicate_spec()), 180)
	nav.add_child(_start_button)
	v.add_child(nav)
	_refresh_load_list()
	return p


func _refresh_load_list() -> void:
	_load_option.clear()
	for path: String in ForceStore.list_saved():
		_load_option.add_item(path.get_file().get_basename())
		_load_option.set_item_metadata(_load_option.item_count - 1, path)
	_load_option.disabled = _load_option.item_count == 0


func _on_save() -> void:
	var path: String = ForceStore.path_for(spec.name)
	var err: Error = ForceStore.save(spec, path)
	_show_toast("저장했습니다: %s" % path.get_file() if err == OK else "저장 실패: %s" % error_string(err), err == OK)
	_refresh_load_list()


func _on_load() -> void:
	if _load_option.selected < 0:
		return
	var path: String = _load_option.get_item_metadata(_load_option.selected)
	var loaded: ForceSpec = ForceStore.load_spec(path)
	if loaded == null:
		_show_toast("불러오기 실패: %s" % path.get_file(), false)
		return
	_replace(loaded)
	_show_toast("불러왔습니다: %s" % path.get_file(), true)


func _show_toast(text: String, ok: bool) -> void:
	_toast.text = text
	_toast.add_theme_color_override(&"font_color", UiKit.OK_COLOR if ok else UiKit.ERR_COLOR)


func _replace(new_spec: ForceSpec) -> void:
	if new_spec == null:
		return
	spec = new_spec
	_name_edit.text = spec.name
	_rebuild()


func _on_faction(faction: Variant) -> void:
	var f: StringName = faction
	if f == spec.faction:
		return
	_replace(catalog.default_force(f))


# --- 가운데: 편성 목록 -----------------------------------------------------

func _rebuild() -> void:
	for child: Node in _list.get_children():
		child.queue_free()
	var toe: TOETemplate = catalog.get_toe(spec.toe)
	if toe == null:
		_list.add_child(UiKit.label("편제표 없음: %s" % spec.toe, 14, UiKit.ERR_COLOR))
		return
	_list.add_child(UiKit.label("%s — 중대 슬롯 %d (최대 %d), 지휘 용량 분당 %.0f" % [toe.display_name,
		toe.company_slots.size(), toe.max_companies, toe.command_capacity_per_min], 15, UiKit.ACCENT))
	for slot: TOETemplate.CompanySlot in toe.company_slots:
		_list.add_child(_company_block(toe, slot))
	_list.add_child(_section_title("대대 직할"))
	for org_id: String in toe.organic:
		_list.add_child(_organic_row(StringName(org_id)))
	_list.add_child(_section_title("배속 자산"))
	for i: int in spec.attachments.size():
		_list.add_child(_platoon_row(spec.attachments[i], "", _remove_attachment.bind(i)))
	_list.add_child(_attachment_adders(toe))
	_list.add_child(_section_title("지원 구매"))
	_list.add_child(_support_row("지도 밖 포병 사격 임무", toe.artillery_mission_cost, toe.max_artillery,
		spec.artillery_missions, func(v: int) -> void: spec.artillery_missions = v))
	_list.add_child(_support_row("근접항공지원 출격", toe.cas_sortie_cost, toe.max_cas,
		spec.cas_sorties, func(v: int) -> void: spec.cas_sorties = v))
	_list.add_child(_section_title("증원 진입 지점"))
	var entries: Array = []
	for side: ScenarioDef.SideSetup in scenario.sides.values():
		if side.faction == spec.faction:
			for i: int in side.entry_points.size():
				var ep: Vector2 = side.entry_points[i]
				entries.append(["진입 %d  (%.1f km, %.1f km)" % [i + 1, ep.x / 1000.0, ep.y / 1000.0], i])
	_list.add_child(UiKit.option(entries, spec.entry_point, _on_entry, 260))
	_update_summary()


func _on_entry(v: Variant) -> void:
	spec.entry_point = v
	_update_summary()


func _section_title(text: String) -> Control:
	var l: Label = UiKit.label(text, 16, UiKit.ACCENT)
	l.custom_minimum_size.y = 28
	l.vertical_alignment = VERTICAL_ALIGNMENT_BOTTOM
	return l


func _company_block(toe: TOETemplate, slot: TOETemplate.CompanySlot) -> Control:
	var box: PanelContainer = UiKit.panel(8)
	var v := VBoxContainer.new()
	box.add_child(v)
	var cs: ForceSpec.CompanySpec = spec.get_company(slot.id)
	var index: int = spec.companies.find(cs)
	var head := HBoxContainer.new()
	var letter: String = ForceFactory.COMPANY_LETTERS[index] if index >= 0 else "-"
	head.add_child(UiKit.label("%s 중대  [%s%s]" % [letter, slot.id, "" if slot.required else ", 선택"], 15))
	var choices: Array = []
	if not slot.required:
		choices.append(["(비움)", &""])
	for t_id: String in slot.allowed:
		var ut: UnitTemplate = catalog.get_unit_template(StringName(t_id))
		choices.append([ut.display_name if ut != null else t_id, StringName(t_id)])
	head.add_child(UiKit.option(choices, cs.template if cs != null else &"", _set_company.bind(toe, slot), 200))
	if cs != null:
		var hq: int = ForceRules.company_hq_cost(catalog, cs.template)
		head.add_child(UiKit.label("본부 %d점" % hq, 13, UiKit.TEXT_DIM))
	v.add_child(head)
	if cs == null:
		return box
	for i: int in cs.platoons.size():
		var ps: ForceSpec.PlatoonSpec = cs.platoons[i]
		var row: HBoxContainer = _platoon_row(ps, "%s/%d" % [letter, i + 1], Callable())
		var moves: Array = [["이동…", &""]]
		for other: ForceSpec.CompanySpec in spec.companies:
			if other != cs:
				moves.append(["→ %s 중대" % ForceFactory.COMPANY_LETTERS[spec.companies.find(other)], other.slot])
		row.add_child(UiKit.option(moves, &"", _on_move_platoon.bind(cs.slot, i), 110))
		v.add_child(row)
	return box


func _on_move_platoon(to: Variant, from_slot: StringName, index: int) -> void:
	var to_slot: StringName = to
	if to_slot != &"":
		spec.move_platoon(from_slot, index, to_slot)
		_rebuild()


func _set_company(template_id: Variant, toe: TOETemplate, slot: TOETemplate.CompanySlot) -> void:
	var tid: StringName = template_id
	var cs: ForceSpec.CompanySpec = spec.get_company(slot.id)
	if tid == &"":
		if cs != null:
			_return_foreign_platoons(cs)
			spec.companies.erase(cs)
	elif cs == null:
		spec.companies.append(ForceSpec.make_company(catalog, slot.id, tid))
		_sort_companies(toe)
	else:
		var fresh: ForceSpec.CompanySpec = ForceSpec.make_company(catalog, slot.id, tid)
		spec.companies[spec.companies.find(cs)] = fresh
		_return_foreign_platoons(cs)
	_rebuild()


## 중대를 바꾸거나 비울 때, 다른 중대에서 받아 온 소대는 원래 중대로 돌려보낸다.
func _return_foreign_platoons(cs: ForceSpec.CompanySpec) -> void:
	for ps: ForceSpec.PlatoonSpec in cs.platoons:
		if ps.home != cs.slot:
			var home: ForceSpec.CompanySpec = spec.get_company(ps.home)
			if home != null:
				home.platoons.append(ps)
	# 이 중대에서 다른 중대로 나간 소대는 그대로 둔다(원 소속 표시만 남음).


func _sort_companies(toe: TOETemplate) -> void:
	var ordered: Array[ForceSpec.CompanySpec] = []
	for slot: TOETemplate.CompanySlot in toe.company_slots:
		var c: ForceSpec.CompanySpec = spec.get_company(slot.id)
		if c != null:
			ordered.append(c)
	spec.companies = ordered


func _platoon_row(ps: ForceSpec.PlatoonSpec, tag: String, on_remove: Callable) -> HBoxContainer:
	var row := HBoxContainer.new()
	row.add_theme_constant_override(&"separation", 6)
	var ut: UnitTemplate = catalog.get_unit_template(ps.template)
	var name_text: String = ut.display_name if ut != null else String(ps.template)
	if tag != "":
		name_text = "%s  %s" % [tag, name_text]
	if ps.home != &"" and spec.get_company(ps.home) != null:
		var home_index: int = spec.companies.find(spec.get_company(ps.home))
		var here: bool = false
		for c: ForceSpec.CompanySpec in spec.companies:
			if c.slot == ps.home and c.platoons.has(ps):
				here = true
		if not here:
			name_text += "  (%s 중대에서 배속)" % ForceFactory.COMPANY_LETTERS[home_index]
	var name_label: Label = UiKit.label(name_text, 14)
	name_label.custom_minimum_size.x = 300
	row.add_child(name_label)
	if ut != null and not ut.loadouts.is_empty():
		var los: Array = []
		for lo: UnitTemplate.Loadout in ut.loadouts:
			los.append([lo.display_name, lo.id])
		var current: StringName = ps.loadout if ps.loadout != &"" else ut.loadouts[0].id
		row.add_child(UiKit.option(los, current, _on_loadout.bind(ps), 150))
	else:
		var spacer := Control.new()
		spacer.custom_minimum_size.x = 150
		row.add_child(spacer)
	var waves: Array = []
	for w: int in ForceSpec.WAVE_NAMES.size():
		waves.append([ForceSpec.WAVE_NAMES[w], w])
	row.add_child(UiKit.option(waves, ps.wave, _on_wave.bind(ps), 170))
	row.add_child(UiKit.label("%d점" % ForceRules.platoon_cost(catalog, ps), 14, UiKit.TEXT_DIM))
	if on_remove.is_valid():
		row.add_child(UiKit.button("빼기", on_remove))
	return row


func _on_loadout(v: Variant, ps: ForceSpec.PlatoonSpec) -> void:
	ps.loadout = v
	_rebuild()


func _on_wave(v: Variant, ps: ForceSpec.PlatoonSpec) -> void:
	ps.wave = v
	_rebuild()


func _organic_row(template_id: StringName) -> HBoxContainer:
	var ps: ForceSpec.PlatoonSpec = null
	for p: ForceSpec.PlatoonSpec in spec.organic:
		if p.template == template_id:
			ps = p
	var row := HBoxContainer.new()
	var check := CheckBox.new()
	check.focus_mode = Control.FOCUS_NONE
	check.button_pressed = ps != null
	check.toggled.connect(_on_organic_toggled.bind(template_id))
	row.add_child(check)
	if ps != null:
		row.add_child(_platoon_row(ps, "", Callable()))
	else:
		var ut: UnitTemplate = catalog.get_unit_template(template_id)
		row.add_child(UiKit.label("%s  (편성 안 함)" % (ut.display_name if ut != null else String(template_id)), 14, UiKit.TEXT_DIM))
	return row


func _attachment_adders(toe: TOETemplate) -> Control:
	var row := HBoxContainer.new()
	var max_att: int = 0
	var allowed := PackedStringArray()
	for slot: TOETemplate.AttachmentSlot in toe.attachment_slots:
		max_att += slot.max_count
		allowed.append_array(slot.allowed)
	row.add_child(UiKit.label("추가 (%d/%d):" % [spec.attachments.size(), max_att], 14, UiKit.TEXT_DIM))
	for t_id: String in allowed:
		var ut: UnitTemplate = catalog.get_unit_template(StringName(t_id))
		var b: Button = UiKit.button("+ %s" % (ut.display_name if ut != null else t_id), _add_attachment.bind(StringName(t_id)))
		b.disabled = spec.attachments.size() >= max_att
		row.add_child(b)
	return row


func _on_organic_toggled(on: bool, template_id: StringName) -> void:
	var ps: ForceSpec.PlatoonSpec = null
	for p: ForceSpec.PlatoonSpec in spec.organic:
		if p.template == template_id:
			ps = p
	if on and ps == null:
		var np := ForceSpec.PlatoonSpec.new()
		np.template = template_id
		spec.organic.append(np)
	elif not on and ps != null:
		spec.organic.erase(ps)
	_rebuild()


func _add_attachment(template_id: StringName) -> void:
	var np := ForceSpec.PlatoonSpec.new()
	np.template = template_id
	spec.attachments.append(np)
	_rebuild()


func _remove_attachment(index: int) -> void:
	if index >= 0 and index < spec.attachments.size():
		spec.attachments.remove_at(index)
	_rebuild()


func _support_row(text: String, unit_cost: int, max_count: int, value: int, setter: Callable) -> Control:
	var row := HBoxContainer.new()
	var l: Label = UiKit.label("%s  (%d점, 최대 %d)" % [text, unit_cost, max_count], 14)
	l.custom_minimum_size.x = 300
	row.add_child(l)
	var spin := SpinBox.new()
	spin.min_value = 0
	spin.max_value = max_count
	spin.value = value
	spin.focus_mode = Control.FOCUS_NONE
	spin.value_changed.connect(_on_support_changed.bind(setter))
	row.add_child(spin)
	return row


func _on_support_changed(v: float, setter: Callable) -> void:
	setter.call(roundi(v))
	_update_summary()


# --- 요약 ----------------------------------------------------------------

func _update_summary() -> void:
	var budget: int = _budget()
	var rep: ForceRules.Report = ForceRules.evaluate(catalog, spec, budget)
	_budget_label.text = "포인트  %d / %d" % [rep.total, budget]
	_budget_label.add_theme_color_override(&"font_color", UiKit.ERR_COLOR if rep.total > budget else Color(0.95, 0.95, 0.95))
	_budget_bar.max_value = budget
	_budget_bar.value = mini(rep.total, budget)
	var t: String = ""
	t += "엘리먼트 %d개  ·  예상 지속 교전 약 [b]%.0f분[/b]\n" % [rep.element_count, rep.sustain_min]
	if rep.errors.is_empty() and rep.warnings.is_empty():
		t += "[color=#%s]편성 가능[/color]\n" % UiKit.hex(UiKit.OK_COLOR)
	for e: String in rep.errors:
		t += "[color=#%s]✖ %s[/color]\n" % [UiKit.hex(UiKit.ERR_COLOR), e]
	for w: String in rep.warnings:
		t += "[color=#%s]▲ %s[/color]\n" % [UiKit.hex(UiKit.WARN_COLOR), w]
	t += "\n[b]포인트 내역[/b]\n"
	for line: Dictionary in rep.lines:
		t += "%s  [color=#%s]%d[/color]\n" % [line["label"], UiKit.hex(UiKit.TEXT_DIM), line["cost"]]
	t += "\n[color=#%s]증원: 1차(T+10분) 10%% 할인, 2차(T+20분) 20%% 할인.\n교차 배속: 소대 줄의 \"이동…\"으로 다른 중대에 붙인다.\n중대당 소대 2~4개를 벗어나면 비편제(명령 지연 +20%%).[/color]" % UiKit.hex(UiKit.TEXT_DIM)
	_summary.text = t
	_start_button.disabled = not rep.is_valid()
