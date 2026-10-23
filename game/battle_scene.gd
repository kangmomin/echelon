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
var overlay: DebugOverlay
var ui_layer: CanvasLayer

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
	ui_layer.add_child(hud)
	hud.order_mode_selected.connect(func(_t: int) -> void: pass)
	hud.immediate_order.connect(_on_immediate_order)
	hud.formation_selected.connect(_on_formation_selected)

	overlay = DebugOverlay.new()
	overlay.visible = false
	add_child(overlay)


func _exit_tree() -> void:
	if battle != null:
		battle.dispose()


func _process(delta: float) -> void:
	var t0: int = Time.get_ticks_usec()
	var ran: int = battle.world.advance(delta)
	_advance_usec = Time.get_ticks_usec() - t0
	if ran > 0:
		_advance_peak_usec = maxi(_advance_peak_usec, floori(_advance_usec / float(ran)))
	battle.world.events.drain()
	_prune_selection()
	if overlay.visible:
		overlay.set_lines(_debug_lines(ran))


# --- 입력 ----------------------------------------------------------------

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		_on_mouse_button(event as InputEventMouseButton)
	elif event is InputEventMouseMotion and _rclick_world != Vector2.INF:
		var mm: InputEventMouseMotion = event
		if mm.position.distance_to(_rclick_screen) > DRAG_THRESHOLD_PX:
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
			_issue_move_orders(_rclick_world, facing, mb.shift_pressed)
			_rclick_world = Vector2.INF
			symbols.drag_from = Vector2.INF
			symbols.drag_to = Vector2.INF
		get_viewport().set_input_as_handled()


func _on_key(k: InputEventKey) -> void:
	var handled: bool = true
	match k.physical_keycode:
		KEY_SPACE:
			battle.world.toggle_pause()
		KEY_EQUAL, KEY_KP_ADD:
			battle.world.speed_up()
		KEY_MINUS, KEY_KP_SUBTRACT:
			battle.world.speed_down()
		KEY_F10:
			overlay.toggle()
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
