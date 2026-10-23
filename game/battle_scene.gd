class_name BattleScene
extends Node3D
## 전투 화면. Battle(시뮬)을 만들고 표현 계층(지형·엘리먼트·부호·HUD)을 붙인다.
## 플레이어 입력 → Order → battle.issue_order(). 시뮬 상태는 읽기만 한다.

signal battle_finished(result: Dictionary)

const DOUBLE_CLICK_MS: int = 350
const DRAG_THRESHOLD_PX: float = 18.0

var battle: Battle
var rts_camera: RtsCamera
var symbols: SymbolLayer
var hud: Hud
var element_view: ElementView
var fx: FxLayer
## 전장의 안개(플레이어 진영 기준). F9 로 끈다(디버그).
var fog_enabled: bool = true
var overlay: DebugOverlay
var orbat: OrbatPanel
var notifications: Notifications
var ui_layer: CanvasLayer
var deploy_panel: PanelContainer

var selection: Array[Unit] = []
var _rclick_screen: Vector2 = Vector2.INF
var _rclick_world: Vector2 = Vector2.INF
var _last_click_ms: int = 0
var _advance_usec: int = 0
var _advance_peak_usec: int = 0


## battle 은 미리 만들어 넘긴다(샌드박스·정식 구성·세이브 로드 모두 같은 경로).
func setup(p_battle: Battle) -> void:
	battle = p_battle


func _ready() -> void:
	assert(battle != null, "BattleScene.setup() 먼저")
	_build_environment()
	var tm := TerrainMesh.new()
	tm.name = "Terrain"
	add_child(tm)
	tm.build(battle.terrain)

	element_view = ElementView.new()
	element_view.name = "Elements"
	add_child(element_view)
	element_view.setup(battle)
	fx = FxLayer.new()
	fx.name = "Fx"
	add_child(fx)
	fx.setup(battle)

	rts_camera = RtsCamera.new()
	rts_camera.name = "RtsCamera"
	add_child(rts_camera)
	var tf: TaskForce = battle.task_forces.get(battle.player_side)
	rts_camera.setup(battle.terrain, tf.centroid() + Vector2(0, 400) if tf != null else battle.terrain.size_m * 0.5)

	ui_layer = CanvasLayer.new()
	ui_layer.name = "UI"
	add_child(ui_layer)
	symbols = SymbolLayer.new()
	symbols.battle = battle
	symbols.rts_camera = rts_camera
	symbols.player_side = battle.player_side
	ui_layer.add_child(symbols)
	hud = Hud.new()
	hud.battle = battle
	hud.status_extra = _status_extra
	ui_layer.add_child(hud)
	orbat = OrbatPanel.new()
	orbat.battle = battle
	orbat.side = battle.player_side
	ui_layer.add_child(orbat)
	orbat.unit_selected.connect(_on_orbat_selected)
	orbat.unit_focused.connect(func(u: Unit) -> void: rts_camera.look_at_point(u.centroid()))
	notifications = Notifications.new()
	ui_layer.add_child(notifications)
	hud.order_mode_selected.connect(func(_t: int) -> void: pass)
	hud.immediate_order.connect(_on_immediate_order)
	hud.formation_selected.connect(_on_formation_selected)

	overlay = DebugOverlay.new()
	overlay.visible = false
	add_child(overlay)
	set_fog(fog_enabled)
	if battle.deploying:
		_build_deploy_panel()


func _build_deploy_panel() -> void:
	deploy_panel = UiKit.panel(10)
	deploy_panel.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP)
	deploy_panel.offset_top = 46
	deploy_panel.offset_left = -300
	deploy_panel.offset_right = 300
	deploy_panel.grow_horizontal = Control.GROW_DIRECTION_BOTH
	var v := HBoxContainer.new()
	v.add_theme_constant_override(&"separation", 12)
	deploy_panel.add_child(v)
	var l: Label = UiKit.label("배치 단계 — 부대를 고르고 우클릭(드래그로 방향)으로 배치 구역 안에 옮긴다.", 13)
	l.autowrap_mode = TextServer.AUTOWRAP_WORD
	l.custom_minimum_size.x = 400
	v.add_child(l)
	v.add_child(UiKit.button("전투 시작", start_battle, 120))
	ui_layer.add_child(deploy_panel)


## 배치 단계를 끝내고 시뮬을 돌린다.
func start_battle() -> void:
	if not battle.deploying:
		return
	battle.begin_battle()
	if deploy_panel != null:
		deploy_panel.queue_free()
		deploy_panel = null
	notifications.push("전투 시작", UiKit.ACCENT)


func _deploy_selection(target: Vector2, facing: float) -> void:
	var n: int = selection.size()
	for i: int in n:
		var u: Unit = selection[i]
		var heading: float = facing if not is_nan(facing) else u.guide.heading
		var offset: float = (i - (n - 1) * 0.5) * 350.0
		battle.deploy_unit(u, target + FormationSolver.right_vector(heading) * offset, heading)


func set_fog(on: bool) -> void:
	fog_enabled = on
	var side: StringName = battle.player_side if on else &""
	element_view.fog_side = side
	fx.fog_side = side
	symbols.fog = on


func _exit_tree() -> void:
	if battle != null:
		battle.dispose()


func _process(delta: float) -> void:
	var t0: int = Time.get_ticks_usec()
	var ran: int = battle.world.advance(delta)
	_advance_usec = Time.get_ticks_usec() - t0
	if ran > 0:
		_advance_peak_usec = maxi(_advance_peak_usec, floori(_advance_usec / float(ran)))
	for ev: Dictionary in battle.world.events.drain():
		fx.on_event(ev)
		var line: Array = EventText.describe(battle, ev, battle.player_side)
		if not line.is_empty():
			var text: String = line[0]
			var color: Color = line[1]
			notifications.push(text, color)
	_prune_selection()
	if overlay.visible:
		overlay.set_lines(_debug_lines(ran))


# --- 입력 ----------------------------------------------------------------

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		_on_mouse_button(event as InputEventMouseButton)
	elif event is InputEventMouseMotion:
		var mm: InputEventMouseMotion = event
		_update_preview(mm.position)
		if _rclick_world != Vector2.INF and mm.position.distance_to(_rclick_screen) > DRAG_THRESHOLD_PX:
			var w: Vector2 = TerrainPicker.pick(rts_camera.camera, mm.position, battle.terrain)
			if w != Vector2.INF:
				symbols.drag_from = _rclick_world
				symbols.drag_to = w
	elif event is InputEventKey and event.is_pressed() and not event.is_echo():
		_on_key(event as InputEventKey)


func _on_mouse_button(mb: InputEventMouseButton) -> void:
	if mb.button_index == MOUSE_BUTTON_LEFT and mb.pressed:
		var u: Unit = symbols.pick_unit(mb.position, battle.player_side)
		var now: int = Time.get_ticks_msec()
		if u != null and now - _last_click_ms < DOUBLE_CLICK_MS and u.parent != null and u.parent.level == Unit.Level.COMPANY:
			u = u.parent
		_last_click_ms = now
		if mb.ctrl_pressed and u != null:
			if selection.has(u):
				selection.erase(u)
			else:
				selection.append(u)
		else:
			selection = [u] if u != null else []
		_selection_changed()
		get_viewport().set_input_as_handled()
	elif mb.button_index == MOUSE_BUTTON_RIGHT:
		if mb.pressed:
			_rclick_screen = mb.position
			_rclick_world = TerrainPicker.pick(rts_camera.camera, mb.position, battle.terrain)
		elif _rclick_world != Vector2.INF:
			var facing: float = NAN
			if symbols.drag_to != Vector2.INF and symbols.drag_to.distance_to(_rclick_world) > 10.0:
				facing = FormationSolver.heading_from_vector(symbols.drag_to - _rclick_world)
			if battle.deploying:
				_deploy_selection(_rclick_world, facing)
			else:
				_issue_move_orders(_rclick_world, facing, mb.shift_pressed)
			_rclick_world = Vector2.INF
			symbols.drag_from = Vector2.INF
			symbols.drag_to = Vector2.INF
		get_viewport().set_input_as_handled()


func _on_key(k: InputEventKey) -> void:
	var handled: bool = true
	match k.physical_keycode:
		KEY_SPACE:
			if not battle.deploying:
				battle.world.toggle_pause()
		KEY_EQUAL, KEY_KP_ADD:
			battle.world.speed_up()
		KEY_MINUS, KEY_KP_SUBTRACT:
			battle.world.speed_down()
		KEY_F10:
			overlay.toggle()
		KEY_F9:
			set_fog(not fog_enabled)
			notifications.push("전장의 안개 %s" % ("켬" if fog_enabled else "끔"), Color(0.8, 0.8, 0.8))
		KEY_M:
			hud.set_order_mode(Order.Type.MOVE)
		KEY_G:
			hud.set_order_mode(Order.Type.FAST_MOVE)
		KEY_A:
			hud.set_order_mode(Order.Type.ATTACK_MOVE)
		KEY_S:
			hud.set_order_mode(Order.Type.SEIZE)
		KEY_D:
			hud.set_order_mode(Order.Type.DEFEND)
		KEY_X:
			hud.set_order_mode(Order.Type.WITHDRAW)
		KEY_H:
			_on_immediate_order(Order.Type.HALT, {})
		KEY_Q:
			_on_immediate_order(Order.Type.SET_SOP, {"sop": {"technique": "traveling"}})
		KEY_W:
			_on_immediate_order(Order.Type.SET_SOP, {"sop": {"technique": "overwatch"}})
		KEY_E:
			_on_immediate_order(Order.Type.SET_SOP, {"sop": {"technique": "bounding"}})
		KEY_F1, KEY_F2, KEY_F3, KEY_F4:
			_select_company(k.physical_keycode)
		KEY_1, KEY_2, KEY_3, KEY_4, KEY_5, KEY_6, KEY_7:
			_formation_hotkey(k.physical_keycode - KEY_1)
		KEY_ESCAPE:
			selection = []
			_selection_changed()
		_:
			handled = false
	if handled:
		get_viewport().set_input_as_handled()


# --- 명령 ----------------------------------------------------------------

func _issue_move_orders(target: Vector2, facing: float, append: bool) -> void:
	if selection.is_empty():
		return
	var n: int = selection.size()
	for i: int in n:
		var u: Unit = selection[i]
		var o: Order = Order.make(hud.order_mode, u.id)
		# 여러 제대를 한 번에 보내면 옆으로 벌려 세운다.
		var offset: float = (i - (n - 1) * 0.5) * 250.0
		var heading: float = facing if not is_nan(facing) else FormationSolver.heading_from_vector(target - u.guide.pos)
		o.set_pos(battle.terrain.clamp_to_map(target + FormationSolver.right_vector(heading) * offset))
		if not is_nan(facing):
			o.set_facing(facing)
		if append:
			o.params["append"] = true
		o.issuer = &"player"
		o.bypass = u.level == Unit.Level.PLATOON and (u as Platoon).follows_parent()
		battle.issue_order(o)
	hud.set_order_mode(Order.Type.MOVE)


func _on_immediate_order(type: int, params: Dictionary) -> void:
	for u: Unit in selection:
		var o: Order = Order.make(type, u.id, params.duplicate(true))
		o.issuer = &"player"
		battle.issue_order(o)


func _on_formation_selected(fid: StringName) -> void:
	_on_immediate_order(Order.Type.SET_FORMATION, {"formation": String(fid)})
	hud.refresh_selection(selection)


func _formation_hotkey(index: int) -> void:
	if selection.is_empty():
		return
	var ids: Array[StringName] = Hud.COMPANY_FORMATIONS if selection[0].level == Unit.Level.COMPANY else Hud.PLATOON_FORMATIONS
	if index < ids.size():
		_on_formation_selected(ids[index])


func _select_company(key: int) -> void:
	var tf: TaskForce = battle.task_forces.get(battle.player_side)
	var idx: int = {KEY_F1: 0, KEY_F2: 1, KEY_F3: 2, KEY_F4: 3}.get(key, 0)
	if tf != null and idx < tf.companies.size():
		selection = [tf.companies[idx]]
		_selection_changed()
		rts_camera.look_at_point(tf.companies[idx].centroid())


func select_units(units: Array[Unit]) -> void:
	selection = units
	_selection_changed()


func _selection_changed() -> void:
	symbols.selection = selection
	hud.refresh_selection(selection)
	orbat.show_selection(selection)


func _on_orbat_selected(u: Unit, additive: bool) -> void:
	if additive:
		if not selection.has(u):
			selection.append(u)
	else:
		selection = [u]
	symbols.selection = selection
	hud.refresh_selection(selection)


## 커서 옆에 "명령하면 몇 초 뒤 실행" 미리보기. PLAN 12.2.
func _update_preview(screen_pos: Vector2) -> void:
	symbols.preview_pos = screen_pos
	if battle.deploying:
		symbols.preview_text = "배치: 우클릭으로 옮기기" if not selection.is_empty() else ""
		return
	if selection.is_empty() or battle.order_system == null:
		symbols.preview_text = ""
		return
	var u: Unit = selection[0]
	var bypass: bool = u.level == Unit.Level.PLATOON and (u as Platoon).follows_parent()
	var sec: float = battle.order_system.estimate_delay_sec(u, hud.order_mode, bypass)
	var label: String = Hud._order_name(hud.order_mode)
	if sec < 0.0:
		symbols.preview_text = "%s — 통신 두절, 명령 대기" % label
	else:
		symbols.preview_text = "%s — 실행까지 약 %.0f초%s" % [label, sec, "  (직접 명령: 분리, 용량 2)" if bypass else ""]


func _status_extra() -> String:
	if battle.order_system == null:
		return ""
	var side: StringName = battle.player_side
	var q: int = battle.order_system.capacity_queue_size(side)
	return "지휘 용량 %.1f / %.0f%s" % [battle.order_system.capacity_now(side), battle.order_system.capacity_max(side),
		"  (대기 %d)" % q if q > 0 else ""]


func _prune_selection() -> void:
	var changed: bool = false
	for u: Unit in selection.duplicate():
		if u.is_destroyed():
			selection.erase(u)
			changed = true
	if changed:
		_selection_changed()


# --- 기타 ----------------------------------------------------------------

func _debug_lines(ran: int) -> PackedStringArray:
	var w: SimWorld = battle.world
	return PackedStringArray([
		"FPS %d   tick %d   ticks/frame %d" % [Engine.get_frames_per_second(), w.tick, ran],
		"advance %d us (틱당 최대 %d us)" % [_advance_usec, _advance_peak_usec],
		"엘리먼트 %d   제대 %d   경로 요청 대기 %d (누적 %d)" % [battle.elements.size(), battle.units.size(),
			battle.pathfinder.pending_count(), battle.pathfinder.requests_processed],
		"카메라 높이 %.0f m   부호 단계 %d" % [rts_camera.height_above_ground(), symbols.current_level()],
	])


func _build_environment() -> void:
	var env := Environment.new()
	env.background_mode = Environment.BG_SKY
	var sky := Sky.new()
	var sky_mat := ProceduralSkyMaterial.new()
	sky_mat.ground_bottom_color = Color(0.3, 0.32, 0.28)
	sky_mat.ground_horizon_color = Color(0.55, 0.6, 0.62)
	sky.sky_material = sky_mat
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color(0.62, 0.66, 0.72)
	env.ambient_light_energy = 0.55
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	env.fog_enabled = true
	env.fog_light_color = Color(0.55, 0.6, 0.66)
	env.fog_density = 0.00002
	var we := WorldEnvironment.new()
	we.environment = env
	add_child(we)
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-50.0, -35.0, 0.0)
	sun.light_energy = 1.0
	sun.shadow_enabled = true
	sun.directional_shadow_max_distance = 3000.0
	add_child(sun)
