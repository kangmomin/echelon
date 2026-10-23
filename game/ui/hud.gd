class_name Hud
extends Control
## 전투 HUD: 상단 바(시간·배속·승점·지휘 용량), 선택 정보, 명령 패널. PLAN 12.1.
## 시뮬을 직접 바꾸지 않고 신호로 BattleScene 에 요청한다.

signal order_mode_selected(type: int)
signal immediate_order(type: int, params: Dictionary)
signal formation_selected(formation_id: StringName)
signal fire_mode_selected(asset: int, smoke: bool)

const PANEL_BG: Color = Color(0.06, 0.07, 0.09, 0.93)
const ORDER_MODES: Array[Array] = [
	[Order.Type.MOVE, "이동", "M"], [Order.Type.FAST_MOVE, "급속 이동", "G"], [Order.Type.ATTACK_MOVE, "공격 이동", "A"],
	[Order.Type.SEIZE, "확보", "S"], [Order.Type.DEFEND, "방어", "D"], [Order.Type.WITHDRAW, "철수", "X"],
]
const PLATOON_FORMATIONS: Array[StringName] = [&"column", &"wedge", &"line", &"echelon_left", &"echelon_right", &"vee", &"coil"]
const COMPANY_FORMATIONS: Array[StringName] = [&"co_column", &"co_wedge", &"co_line", &"co_echelon_left", &"co_echelon_right", &"co_vee", &"co_box"]

var battle: Battle
var selection: Array[Unit] = []
var order_mode: int = Order.Type.MOVE
## 상단 바에 덧붙일 상태 문자열(승점·지휘 용량 등)을 돌려주는 함수.
var status_extra: Callable = Callable()

var _top_label: Label
var _sel_label: RichTextLabel
var _mode_buttons: Dictionary[int, Button] = {}
var _formation_box: HBoxContainer
var _misc_box: HBoxContainer
var _cmd_panel: PanelContainer
var _fire_label: RichTextLabel
var _fire_buttons: Dictionary[String, Button] = {}
## 현재 화력 요청 모드(-1 = 없음).
var fire_asset: int = -1
var fire_smoke: bool = false


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_build_top_bar()
	_build_selection_panel()
	_build_command_panel()
	_build_fire_panel()
	refresh_selection(selection)


func _process(_delta: float) -> void:
	if battle == null:
		return
	_top_label.text = top_bar_text()
	if Engine.get_process_frames() % 10 == 0:
		_update_selection_text()
		_update_fire_panel()


func top_bar_text() -> String:
	var w: SimWorld = battle.world
	var sec: int = int(w.get_sim_time_sec())
	var parts: PackedStringArray = ["%02d:%02d" % [floori(sec / 60.0), sec % 60], "%.1fx" % w.get_speed()]
	if battle.scoring != null:
		var mine: StringName = battle.player_side
		var theirs: StringName = Battle.enemy_of(mine)
		parts.append("승점 %.0f : %.0f" % [battle.scoring.vp[mine], battle.scoring.vp[theirs]])
		var left: int = maxi(0, roundi(battle.scenario.time_limit_min * 60.0 - w.get_sim_time_sec()))
		parts.append("남은 %02d:%02d" % [floori(left / 60.0), left % 60])
		parts.append("전력 %d%% : %d%%" % [roundi(battle.scoring.value_ratio(mine) * 100.0),
			roundi(battle.scoring.value_ratio(theirs) * 100.0)])
	if battle.deploying:
		parts.append("배치 단계")
	elif w.paused:
		parts.append("일시정지")
	if status_extra.is_valid():
		var extra: String = status_extra.call()
		if extra != "":
			parts.append(extra)
	return "   ".join(parts)


func set_order_mode(type: int) -> void:
	order_mode = type
	for t: int in _mode_buttons.keys():
		_mode_buttons[t].button_pressed = t == type


func refresh_selection(units: Array[Unit]) -> void:
	selection = units
	_update_selection_text()
	_rebuild_formation_buttons()
	_cmd_panel.visible = not selection.is_empty()


# --- 구성 ----------------------------------------------------------------

func _panel(anchor_preset: int) -> PanelContainer:
	var p := PanelContainer.new()
	var style := StyleBoxFlat.new()
	style.bg_color = PANEL_BG
	style.set_corner_radius_all(4)
	style.set_content_margin_all(8)
	p.add_theme_stylebox_override(&"panel", style)
	p.set_anchors_and_offsets_preset(anchor_preset)
	p.mouse_filter = Control.MOUSE_FILTER_STOP
	add_child(p)
	return p


func _build_top_bar() -> void:
	var p: PanelContainer = _panel(Control.PRESET_CENTER_TOP)
	p.offset_left = -360
	p.offset_right = 360
	p.offset_top = 6
	p.offset_bottom = 40
	p.grow_horizontal = Control.GROW_DIRECTION_BOTH
	_top_label = Label.new()
	_top_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_top_label.add_theme_font_size_override(&"font_size", 15)
	p.add_child(_top_label)


func _build_selection_panel() -> void:
	var p: PanelContainer = _panel(Control.PRESET_BOTTOM_LEFT)
	p.offset_left = 8
	p.offset_bottom = -8
	p.offset_top = -170
	p.offset_right = 330
	p.grow_vertical = Control.GROW_DIRECTION_BEGIN
	_sel_label = RichTextLabel.new()
	_sel_label.bbcode_enabled = true
	_sel_label.fit_content = true
	_sel_label.scroll_active = false
	_sel_label.custom_minimum_size = Vector2(310, 150)
	_sel_label.add_theme_font_size_override(&"normal_font_size", 13)
	_sel_label.add_theme_font_size_override(&"bold_font_size", 14)
	p.add_child(_sel_label)


func _build_command_panel() -> void:
	_cmd_panel = _panel(Control.PRESET_CENTER_BOTTOM)
	_cmd_panel.offset_bottom = -8
	_cmd_panel.offset_top = -112
	_cmd_panel.offset_left = -330
	_cmd_panel.offset_right = 380
	_cmd_panel.grow_vertical = Control.GROW_DIRECTION_BEGIN
	var v := VBoxContainer.new()
	_cmd_panel.add_child(v)

	var modes := HBoxContainer.new()
	v.add_child(modes)
	for m: Array in ORDER_MODES:
		var b := Button.new()
		b.text = "%s [%s]" % [m[1], m[2]]
		b.toggle_mode = true
		b.focus_mode = Control.FOCUS_NONE
		var t: int = m[0]
		b.pressed.connect(func() -> void: set_order_mode(t); order_mode_selected.emit(t))
		modes.add_child(b)
		_mode_buttons[t] = b
	var halt := Button.new()
	halt.text = "정지 [H]"
	halt.focus_mode = Control.FOCUS_NONE
	halt.pressed.connect(func() -> void: immediate_order.emit(Order.Type.HALT, {}))
	modes.add_child(halt)

	_formation_box = HBoxContainer.new()
	v.add_child(_formation_box)

	_misc_box = HBoxContainer.new()
	v.add_child(_misc_box)
	_add_misc("경계 이동 [W]", Order.Type.SET_SOP, {"sop": {"technique": "overwatch"}})
	_add_misc("교대 약진 [E]", Order.Type.SET_SOP, {"sop": {"technique": "bounding"}})
	_add_misc("일반 이동 [Q]", Order.Type.SET_SOP, {"sop": {"technique": "traveling"}})
	_add_misc("보급 [R]", Order.Type.RESUPPLY, {})
	_add_misc("탑승", Order.Type.MOUNT, {})
	_add_misc("하차", Order.Type.DISMOUNT, {})
	_add_misc("분리", Order.Type.DETACH, {})
	_add_misc("복귀", Order.Type.REJOIN, {})
	set_order_mode(Order.Type.MOVE)


## 화력 지원 패널(오른쪽 아래): 자산 버튼, 남은 횟수, 진행 중인 임무. PLAN 10.1.
const FIRE_BUTTONS: Array[Array] = [
	["박격포 [Z]", FireSupportSystem.Asset.MORTAR, false], ["박격포 연막 [V]", FireSupportSystem.Asset.MORTAR, true],
	["포병 [C]", FireSupportSystem.Asset.ARTILLERY, false], ["포병 연막", FireSupportSystem.Asset.ARTILLERY, true],
	["근접항공지원 [B]", FireSupportSystem.Asset.CAS, false],
]


func _build_fire_panel() -> void:
	var p: PanelContainer = _panel(Control.PRESET_BOTTOM_RIGHT)
	p.offset_right = -8
	p.offset_bottom = -8
	p.offset_left = -300
	p.offset_top = -230
	p.grow_horizontal = Control.GROW_DIRECTION_BEGIN
	p.grow_vertical = Control.GROW_DIRECTION_BEGIN
	var v := VBoxContainer.new()
	p.add_child(v)
	var title := Label.new()
	title.text = "화력 지원  (좌클릭: 표적, 우클릭: 취소)"
	title.add_theme_font_size_override(&"font_size", 13)
	v.add_child(title)
	var grid := GridContainer.new()
	grid.columns = 2
	v.add_child(grid)
	for item: Array in FIRE_BUTTONS:
		var b := Button.new()
		b.text = item[0]
		b.toggle_mode = true
		b.focus_mode = Control.FOCUS_NONE
		var asset: int = item[1]
		var smoke: bool = item[2]
		b.pressed.connect(func() -> void: set_fire_mode(asset, smoke); fire_mode_selected.emit(asset, smoke))
		grid.add_child(b)
		_fire_buttons["%d_%s" % [asset, smoke]] = b
	_fire_label = RichTextLabel.new()
	_fire_label.bbcode_enabled = true
	_fire_label.fit_content = true
	_fire_label.scroll_active = false
	_fire_label.custom_minimum_size = Vector2(280, 60)
	_fire_label.add_theme_font_size_override(&"normal_font_size", 12)
	v.add_child(_fire_label)


func set_fire_mode(asset: int, smoke: bool) -> void:
	fire_asset = asset
	fire_smoke = smoke
	for key: String in _fire_buttons.keys():
		_fire_buttons[key].button_pressed = asset >= 0 and key == "%d_%s" % [asset, smoke]


func _update_fire_panel() -> void:
	if _fire_label == null or battle == null or battle.fires == null:
		return
	var tf: TaskForce = battle.task_forces.get(battle.player_side)
	if tf == null:
		return
	var lines: PackedStringArray = ["남은 포병 임무 %d   항공 출격 %d" % [tf.artillery_missions, tf.cas_sorties]]
	var tick: int = battle.world.tick
	for m: FireSupportSystem.Mission in battle.fires.active_missions(battle.player_side):
		var t: String = FireSupportSystem.PHASE_NAMES[m.phase]
		if m.phase == FireSupportSystem.Phase.PROCESSING:
			t += " %.0f초" % maxf(0.0, (m.ready_tick - tick) / float(SimConst.TICK_RATE))
		elif m.phase == FireSupportSystem.Phase.EFFECT:
			t += " (남은 %d발)" % m.rounds_left
		lines.append("• %s — %s" % [m.label(), t])
	_fire_label.text = "\n".join(lines)


func _add_misc(text: String, type: int, params: Dictionary) -> void:
	var b := Button.new()
	b.text = text
	b.focus_mode = Control.FOCUS_NONE
	b.pressed.connect(func() -> void: immediate_order.emit(type, params.duplicate(true)))
	_misc_box.add_child(b)


func _rebuild_formation_buttons() -> void:
	if _formation_box == null:
		return
	for c: Node in _formation_box.get_children():
		c.queue_free()
	if selection.is_empty() or battle == null:
		return
	var ids: Array[StringName] = COMPANY_FORMATIONS if selection[0].level == Unit.Level.COMPANY else PLATOON_FORMATIONS
	var label := Label.new()
	label.text = "대형:"
	_formation_box.add_child(label)
	for i: int in ids.size():
		var f: FormationDef = battle.catalog.get_formation(ids[i])
		if f == null:
			continue
		var b := Button.new()
		b.text = "%s [%d]" % [f.display_name, i + 1]
		b.focus_mode = Control.FOCUS_NONE
		b.toggle_mode = true
		b.button_pressed = selection[0].formation == f
		var fid: StringName = f.id
		b.pressed.connect(func() -> void: formation_selected.emit(fid))
		_formation_box.add_child(b)


func _update_selection_text() -> void:
	if _sel_label == null:
		return
	if selection.is_empty() or battle == null:
		_sel_label.text = "[color=#aaa]부대를 선택하세요.\n좌클릭: 선택  우클릭: 명령  우클릭 드래그: 방향 지정\nShift: 명령 대기열  Ctrl: 추가 선택[/color]"
		return
	var u: Unit = selection[0]
	var lines: PackedStringArray = []
	lines.append("[b]%s[/b]  (%s)" % [u.display_name, u.level_name()])
	if selection.size() > 1:
		lines.append("외 %d개 제대 선택" % (selection.size() - 1))
	lines.append("전투력 %d%%   사기 %d   통신 %s" % [roundi(u.strength() * 100.0), roundi(u.morale), u.comms_name()])
	var f_name: String = u.formation.display_name if u.formation != null else "-"
	lines.append("대형 %s   이동 방식 %s" % [f_name, _technique_name(u.sop.technique)])
	var mission: String = _order_name(u.mission.type) if u.mission != null and not u.mission.is_finished() else "없음"
	lines.append("자세 %s   임무 %s" % [_posture_name(u.posture), mission])
	if u.detached:
		lines.append("[color=#ffd166]분리됨 — 중대 지휘에서 빠짐[/color]")
	if u.is_leaderless(battle.world.tick):
		lines.append("[color=#ff6b6b]지휘 공백 — 승계 중[/color]")
	if u.is_reforming(battle.world.tick):
		lines.append("[color=#9fd3ff]대형 전환 중[/color]")
	if u is Platoon and (u as Platoon).has_squads():
		lines.append("보병 %s" % ("탑승" if (u as Platoon).mounted else "하차"))
	lines.append("탄약 %d%%" % roundi(SupplySystem.ammo_ratio(u) * 100.0))
	_sel_label.text = "\n".join(lines)


static func _order_name(type: int) -> String:
	const NAMES: Dictionary[int, String] = {
		Order.Type.MOVE: "이동", Order.Type.FAST_MOVE: "급속 이동", Order.Type.ATTACK_MOVE: "공격 이동",
		Order.Type.SEIZE: "확보", Order.Type.DEFEND: "방어", Order.Type.WITHDRAW: "철수", Order.Type.RESUPPLY: "보급",
		Order.Type.MOUNT: "탑승", Order.Type.DISMOUNT: "하차", Order.Type.SET_FORMATION: "대형 변경",
		Order.Type.SET_SOP: "SOP 변경", Order.Type.CALL_FIRE: "화력 요청", Order.Type.DETACH: "분리",
		Order.Type.REJOIN: "복귀", Order.Type.REORGANIZE: "재편", Order.Type.CANCEL: "취소",
		Order.Type.FOLLOW: "중대 대형 추종", Order.Type.HALT: "정지",
	}
	return NAMES.get(type, "?")


static func _technique_name(t: StringName) -> String:
	return {&"traveling": "일반", &"overwatch": "경계 이동", &"bounding": "교대 약진"}.get(t, String(t))


static func _posture_name(p: StringName) -> String:
	return {&"idle": "대기", &"moving": "이동", &"defending": "방어", &"withdrawing": "철수", &"routed": "패주",
		&"resupplying": "보급 중"}.get(p, String(p))
