class_name Tutorial
extends Control
## 단계별 튜토리얼. 화면 위쪽 패널에 과제를 보여 주고, 플레이어가 해내면 다음 단계로 넘어간다.
## 전장은 BattleSetup.tutorial(): 청군 전차 중대 + 박격포 소대, 적 BMP-3 소대(공격 단계에서 등장).
## 판정은 BattleScene 의 선택·부대 지정·명령 신호와 시뮬 이벤트, 그리고 카메라·제대 상태를 읽어서 한다(시뮬은 바꾸지 않음,
## 적 등장만 BattleSetup.reveal_platoon 으로 연출).

signal finished(to_menu: bool)

const DONE_DELAY_SEC: float = 1.2
const MARK_MOVE: Color = Color(1.0, 0.85, 0.3)
const MARK_ENEMY: Color = Color(1.0, 0.4, 0.35)

class Step:
	extends RefCounted
	var title: String
	var body: String
	## 통과 판정(인자 없음 → bool). 비어 있으면 "다음" 버튼으로 넘어간다.
	var check: Callable = Callable()
	var enter: Callable = Callable()


var bs: BattleScene
var _steps: Array[Step] = []
var _index: int = -1
var _done_timer: float = -1.0

var _panel: PanelContainer
var _count_label: Label
var _title_label: Label
var _body_label: RichTextLabel
var _next_button: Button

# 판정용 상태
var _company: Company
var _enemy: Platoon
var _cam_start: Vector2
var _cam_dist_start: float
var _saw_platoons: bool = false
var _saw_empty_after_group: bool = false
var _saw_pause: bool = false
var _saw_fast: bool = false
var _saw_hold: bool = false
var _orders: Array[Order] = []
var _fire_requested: bool = false
var _listen_platoon_orders: bool = false
var _platoon_got_order: bool = false
var _markers: Array[Dictionary] = []  # {pos: Vector2, color: Color, text: String, node: Node3D}


func setup(p_bs: BattleScene) -> void:
	bs = p_bs
	var tf: TaskForce = bs.battle.task_forces.get(bs.battle.player_side)
	_company = tf.companies[0] if tf != null and not tf.companies.is_empty() else null
	var red: TaskForce = bs.battle.task_forces.get(Battle.enemy_of(bs.battle.player_side))
	_enemy = red.organic[0] if red != null and not red.organic.is_empty() else null
	bs.player_order.connect(func(o: Order) -> void: _orders.append(o))
	bs.sim_event.connect(_on_sim_event)
	_build_steps()


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_panel = UiKit.panel(12)
	_panel.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP)
	_panel.offset_top = 46
	_panel.offset_left = -330
	_panel.offset_right = 330
	_panel.grow_horizontal = Control.GROW_DIRECTION_BOTH
	_panel.mouse_filter = Control.MOUSE_FILTER_STOP
	add_child(_panel)
	var v := VBoxContainer.new()
	v.add_theme_constant_override(&"separation", 6)
	_panel.add_child(v)
	var head := HBoxContainer.new()
	v.add_child(head)
	_count_label = UiKit.label("", 12, UiKit.TEXT_DIM)
	head.add_child(_count_label)
	_title_label = UiKit.label("", 18, UiKit.ACCENT)
	_title_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	head.add_child(_title_label)
	head.add_child(UiKit.button("건너뛰기", _skip, 80))
	head.add_child(UiKit.button("그만하기", func() -> void: finished.emit(true), 80))
	_body_label = RichTextLabel.new()
	_body_label.bbcode_enabled = true
	_body_label.fit_content = true
	_body_label.scroll_active = false
	_body_label.custom_minimum_size = Vector2(636, 20)
	_body_label.add_theme_font_size_override(&"normal_font_size", 14)
	_body_label.add_theme_font_size_override(&"bold_font_size", 14)
	v.add_child(_body_label)
	_next_button = UiKit.button("다음", _advance, 120)
	_next_button.size_flags_horizontal = Control.SIZE_SHRINK_END
	v.add_child(_next_button)
	_advance()


func _k(key: String) -> String:
	return "[color=#f2cc59][b]%s[/b][/color]" % key


func _step(title: String, body: String, check: Callable = Callable(), enter: Callable = Callable()) -> void:
	var s := Step.new()
	s.title = title
	s.body = body
	s.check = check
	s.enter = enter
	_steps.append(s)


func _build_steps() -> void:
	_step("환영합니다",
		"ECHELON 에서는 차량 하나하나가 아니라 [b]중대·소대[/b]를 지휘합니다. 명령은 무전을 타고 내려가므로 [b]몇십 초 늦게[/b] 실행됩니다.\n"
		+ "이 튜토리얼(약 10분)에서 카메라, 선택, 명령, 대형, 화력 요청을 한 번씩 해 봅니다. 언제든 %s 키로 가이드를 열 수 있습니다." % _k("/"))
	_step("카메라",
		"%s(또는 화살표)로 카메라를 움직이고, %s로 확대·축소해 보세요. %s 드래그로 돌리고 기울일 수 있습니다."
			% [_k("W A S D"), _k("마우스 휠"), _k("가운데 버튼")],
		func() -> bool:
			var cam: RtsCamera = bs.rts_camera
			return cam.focus.distance_to(_cam_start) > 150.0 and absf(cam.distance - _cam_dist_start) / _cam_dist_start > 0.2,
		func() -> void:
			_cam_start = bs.rts_camera.focus
			_cam_dist_start = bs.rts_camera.distance)
	_step("중대 선택",
		"%s를 누르거나 지도에서 [b]A 중대[/b] 부호를 좌클릭하세요. 소대 부호를 [b]더블클릭[/b]해도 소속 중대가 선택됩니다.\n"
			% _k("F1")
		+ "왼쪽 편제 패널에서 이름을 눌러도 됩니다.",
		func() -> bool: return _selected_only(_company))
	_step("하위 제대",
		"%s을 누르면 선택한 중대의 [b]소대들[/b]이 선택됩니다. 그다음 %s으로 다시 중대를 고르세요.\n" % [_k("Tab"), _k("Shift+Tab")]
		+ "평소에는 중대에 명령하고, 꼭 필요할 때만 소대에 직접 명령합니다(그 소대는 중대에서 [b]분리[/b]됩니다).",
		func() -> bool:
			if bs.selection.size() > 1 and bs.selection[0].parent == _company:
				_saw_platoons = true
			return _saw_platoons and _selected_only(_company))
	_step("부대 지정",
		"A 중대를 고른 채 %s로 1번에 지정하고, %s로 선택을 푼 뒤, %s을 눌러 다시 불러오세요. 빠르게 두 번 누르면 카메라가 갑니다."
			% [_k("Ctrl+1"), _k("Esc"), _k("1")],
		func() -> bool:
			var g: Array[Unit] = bs.group_units(1)
			if g.has(_company) and bs.selection.is_empty():
				_saw_empty_after_group = true
			return _saw_empty_after_group and bs.selection.has(_company))
	_step("대형",
		"%s으로 A 중대를 [b]소대 횡대[/b]로 바꾸세요. 화면 아래 대형 버튼을 눌러도 됩니다.\n" % _k("Alt+3")
		+ "횡대는 정면 화력이 가장 크고 포격에 강하지만 느립니다. 종대는 빠르지만 앞으로 쏠 수 있는 차량이 적습니다.\n"
		+ "[color=#a6a9b3]대형 명령도 무전으로 내려가므로 잠시 뒤에 바뀝니다.[/color]",
		func() -> bool: return _company != null and _company.formation != null and _company.formation.id == &"co_line")
	_step("시간 조절",
		"%s로 일시정지하고, %s로 배속을 올려 보세요(%s 로 내림). 마지막에 다시 %s로 재개하세요.\n" % [_k("Space"), _k("="), _k("-"), _k("Space")]
		+ "일시정지 중에도 명령을 내릴 수 있습니다. 복잡할 때는 멈추고 생각하세요.",
		func() -> bool:
			var w: SimWorld = bs.battle.world
			if w.paused:
				_saw_pause = true
			if w.get_speed() > 1.01:
				_saw_fast = true
			return _saw_pause and _saw_fast and not w.paused)
	_step("이동 명령",
		"A 중대를 선택한 상태에서 강 건너 [color=#ffd94d][b]노란 표시[/b][/color]를 %s하세요.\n" % _k("우클릭")
		+ "우클릭하기 전 커서 옆의 [b]\"실행까지 약 N초\"[/b]가 지휘 지연입니다. 우클릭한 채로 끌면 도착 후 바라볼 방향을 정할 수 있습니다.",
		func() -> bool: return _ordered_near(_company, [Order.Type.MOVE, Order.Type.FAST_MOVE, Order.Type.ATTACK_MOVE],
			BattleSetup.TUTORIAL_WAYPOINT, 350.0),
		func() -> void: _add_marker(BattleSetup.TUTORIAL_WAYPOINT, MARK_MOVE, "이동 목표"))
	_step("지휘 지연",
		"명령은 [b]대대 → 중대 → 소대[/b]로 무전을 타고 내려갑니다. 지도의 점선과 숫자가 남은 시간입니다.\n"
		+ "명령이 소대에 닿아 움직이기 시작할 때까지 기다려 보세요(%s로 배속을 올려도 됩니다)." % _k("="),
		func() -> bool: return _platoon_got_order and _company_moving(),
		func() -> void: _listen_platoon_orders = true)
	_step("사격 통제",
		"이동하는 동안 %s를 눌러 [b]사격 중지[/b]로 바꾸고(맞기 전에는 먼저 쏘지 않음 — 매복용), 다시 %s로 [b]사격 자유[/b]로 돌려 놓으세요.\n"
			% [_k("H"), _k("H")]
		+ "왼쪽 아래 선택 정보의 \"사격\" 항목이 바뀝니다(역시 무전으로 전달).",
		func() -> bool:
			if _company == null:
				return false
			if _company.sop.roe == &"hold_fire":
				_saw_hold = true
			return _saw_hold and _company.sop.roe == &"fire_at_will")
	_step("공격 이동",
		"정찰 보고: [color=#ff7a70][b]적 BMP-3 소대[/b][/color]가 BRAVO 능선에 나타났습니다.\n"
		+ "%s를 누른 뒤 빨간 표시를 %s하세요. 공격 이동은 가다가 만나는 적과 교전합니다.\n" % [_k("Q"), _k("우클릭")]
		+ "같은 방식으로 %s 급속 이동, %s 철수, %s 확보, %s 방어를 씁니다." % [_k("F"), _k("B"), _k("Y"), _k("J")],
		func() -> bool: return _ordered_near(null, [Order.Type.ATTACK_MOVE, Order.Type.SEIZE], BattleSetup.TUTORIAL_ENEMY, 700.0),
		func() -> void:
			_remove_markers()
			if _enemy != null and not _enemy.on_map:
				BattleSetup.reveal_platoon(bs.battle, _enemy, BattleSetup.TUTORIAL_ENEMY, PI)
			_add_marker(BattleSetup.TUTORIAL_ENEMY, MARK_ENEMY, "적 BMP-3 소대"))
	_step("화력 지원",
		"%s를 눌러 [b]박격포[/b] 요청 모드로 바꾸고 빨간 표시 근처를 %s하세요.\n" % [_k("T"), _k("좌클릭")]
		+ "%s를 한 번 더 누르면 포병, %s는 연막, %s는 근접항공지원입니다. 표적을 보는 아군(관측자)이 있으면 빠르고 정확합니다."
			% [_k("T"), _k("G"), _k("P")],
		func() -> bool: return _fire_requested)
	_step("적 소대 격파",
		"적 소대를 격파하세요. 오른쪽 위 알림과 왼쪽 편제 패널의 전투력(%)을 보세요.\n"
		+ "손실과 제압이 쌓이면 사기가 떨어져 [b]공격을 거부하거나 패주[/b]합니다. 시간이 오래 걸리면 %s로 배속을 올리세요." % _k("="),
		func() -> bool: return _enemy == null or _enemy.is_destroyed() or _enemy_dead() or not bs.battle.result.is_empty())
	_step("튜토리얼 완료",
		"수고했습니다. 이제 기본 조작을 모두 해 봤습니다.\n"
		+ "• 대대 편성, 보급, 승리 조건은 %s 가이드에 정리돼 있습니다.\n" % _k("/")
		+ "• 메뉴의 [b]스커미시[/b]에서 직접 편성한 대대로 AI 와 한 판(최대 40분)을 해 보세요.",
		Callable(),
		func() -> void:
			_remove_markers()
			_next_button.text = "메뉴로"
			_next_button.pressed.disconnect(_advance)
			_next_button.pressed.connect(func() -> void: finished.emit(true)))


func _advance() -> void:
	_done_timer = -1.0
	_index += 1
	if _index >= _steps.size():
		finished.emit(true)
		return
	var s: Step = _steps[_index]
	_count_label.text = "튜토리얼 %d / %d   " % [_index + 1, _steps.size()]
	_title_label.text = s.title
	_body_label.text = s.body
	_next_button.visible = not s.check.is_valid()
	if s.enter.is_valid():
		s.enter.call()


func _skip() -> void:
	if _index < _steps.size() - 1:
		# 건너뛴 단계의 연출(적 등장 등)은 다음 단계 진입 때 필요하면 다시 한다.
		_advance()


func _process(delta: float) -> void:
	queue_redraw()
	if _index < 0 or _index >= _steps.size():
		return
	var s: Step = _steps[_index]
	if _done_timer >= 0.0:
		_done_timer -= delta
		if _done_timer < 0.0:
			_advance()
		return
	if s.check.is_valid() and s.check.call():
		_title_label.text = s.title + "  ✓"
		_done_timer = DONE_DELAY_SEC


func _on_sim_event(ev: Dictionary) -> void:
	var pl: Dictionary = ev.get("payload", {})
	var type: StringName = ev.get("type", &"")
	if type == &"fire_requested" and pl.get("side", &"") == bs.battle.player_side:
		_fire_requested = true
	elif type == &"order_received" and _listen_platoon_orders and _company != null:
		var uid: int = pl.get("unit", -1)
		var u: Unit = bs.battle.get_unit(uid)
		if u != null and u.parent == _company:
			_platoon_got_order = true


# --- 판정 도우미 -----------------------------------------------------------

func _selected_only(u: Unit) -> bool:
	return u != null and bs.selection.size() == 1 and bs.selection[0] == u


## 이 단계에서 내린 이동 계열 명령 중 target 근처를 향한 것이 있는가. unit 이 주어지면 그 제대(또는 하위 소대) 명령만.
func _ordered_near(unit: Unit, types: Array, target: Vector2, radius: float) -> bool:
	for o: Order in _orders:
		if not types.has(o.type) or not o.has_pos() or o.get_pos().distance_to(target) > radius:
			continue
		if unit == null:
			return true
		var u: Unit = bs.battle.get_unit(o.target_unit_id)
		if u == unit or (u != null and u.parent == unit):
			return true
	return false


func _company_moving() -> bool:
	if _company == null:
		return false
	for e: Element in _company.all_elements():
		if e.is_alive() and e.velocity.length() > 1.0:
			return true
	return false


func _enemy_dead() -> bool:
	for e: Element in _enemy.elements:
		if e.is_alive():
			return false
	return true


# --- 지도 표시 -------------------------------------------------------------

func _add_marker(pos: Vector2, color: Color, text: String) -> void:
	var root := Node3D.new()
	root.position = ViewCoords.to_3d(pos, bs.battle.terrain.ground_height(pos))
	var ring := MeshInstance3D.new()
	var disc := CylinderMesh.new()
	disc.top_radius = 90.0
	disc.bottom_radius = 90.0
	disc.height = 3.0
	disc.radial_segments = 32
	ring.mesh = disc
	var mat := StandardMaterial3D.new()
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat.albedo_color = Color(color.r, color.g, color.b, 0.28)
	mat.no_depth_test = false
	ring.material_override = mat
	ring.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	ring.position.y = 2.0
	root.add_child(ring)
	var beam := MeshInstance3D.new()
	var cyl := CylinderMesh.new()
	cyl.top_radius = 2.5
	cyl.bottom_radius = 2.5
	cyl.height = 160.0
	beam.mesh = cyl
	var bmat := StandardMaterial3D.new()
	bmat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	bmat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	bmat.albedo_color = Color(color.r, color.g, color.b, 0.6)
	beam.material_override = bmat
	beam.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	beam.position.y = 80.0
	root.add_child(beam)
	bs.add_child(root)
	_markers.append({"pos": pos, "color": color, "text": text, "node": root})


func _remove_markers() -> void:
	for m: Dictionary in _markers:
		var n: Node3D = m["node"]
		n.queue_free()
	_markers.clear()


## 표시 이름은 2D 로 그린다(어느 거리에서나 같은 크기, 한글 글꼴).
func _draw() -> void:
	if bs == null or bs.rts_camera == null or bs.rts_camera.camera == null:
		return
	var cam: Camera3D = bs.rts_camera.camera
	var font: Font = get_theme_default_font()
	for m: Dictionary in _markers:
		var pos: Vector2 = m["pos"]
		var world: Vector3 = ViewCoords.to_3d(pos, bs.battle.terrain.ground_height(pos) + 160.0)
		if cam.is_position_behind(world):
			continue
		var sp: Vector2 = cam.unproject_position(world)
		var color: Color = m["color"]
		var text: String = m["text"]
		var w: float = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, 15).x
		draw_rect(Rect2(sp + Vector2(-w * 0.5 - 6, -24), Vector2(w + 12, 22)), Color(0, 0, 0, 0.65))
		draw_string(font, sp + Vector2(-w * 0.5, -8), text, HORIZONTAL_ALIGNMENT_LEFT, -1, 15, color)
