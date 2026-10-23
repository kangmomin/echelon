class_name BattleScene
extends Node3D
## 전투 화면. Battle(시뮬)을 만들고 표현 계층(지형·엘리먼트·부호·HUD)을 붙인다.
## 플레이어 입력 → Order → battle.issue_order(). 시뮬 상태는 읽기만 한다.

signal battle_finished(result: Dictionary)
## 시뮬 이벤트(드레인한 순서대로). 튜토리얼 등 표현 쪽 관찰자용.
signal sim_event(ev: Dictionary)
## 플레이어가 내린 명령(지연 전파 전, 발행 시점).
signal player_order(order: Order)

const DOUBLE_CLICK_MS: int = 350
const DRAG_THRESHOLD_PX: float = 18.0
const OBSERVER_NAMES: Array[String] = ["관측반 관측", "일반 엘리먼트 관측", "관측 없음, 지도 좌표 사격"]

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
var minimap: Minimap
var tutorial: Tutorial = null
var guide: GuideScreen = null

var selection: Array[Unit] = []
var _rclick_screen: Vector2 = Vector2.INF
var _rclick_world: Vector2 = Vector2.INF
var _last_click_ms: int = 0
## 부대 지정(Ctrl+숫자): 번호 → 제대 목록.
var _groups: Dictionary[int, Array] = {}
var _last_group: int = -1
var _last_group_ms: int = 0
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
	minimap = Minimap.new()
	minimap.battle = battle
	minimap.rts_camera = rts_camera
	minimap.player_side = battle.player_side
	ui_layer.add_child(minimap)
	hud.order_mode_selected.connect(func(_t: int) -> void: pass)
	hud.immediate_order.connect(_on_immediate_order)
	hud.formation_selected.connect(_on_formation_selected)
	hud.altitude_toggle_requested.connect(toggle_altitude)

	var guide_button: Button = UiKit.button("가이드  /", toggle_guide, 0)
	guide_button.position = Vector2(8, 8)
	guide_button.add_theme_font_size_override(&"font_size", 13)
	ui_layer.add_child(guide_button)

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


## 가이드 화면 열기·닫기. 여는 동안 일시정지했다가, 닫을 때 원래대로.
func toggle_guide() -> void:
	if guide != null:
		guide.close()
		return
	guide = GuideScreen.new()
	var was_paused: bool = battle.world.paused
	battle.world.set_paused(true)
	guide.closed.connect(func() -> void:
		guide = null
		if not was_paused and not battle.deploying and battle.result.is_empty():
			battle.world.set_paused(false))
	ui_layer.add_child(guide)


## 튜토리얼 진행 패널을 붙인다(BattleSetup.tutorial 전장에서).
func start_tutorial() -> Tutorial:
	tutorial = Tutorial.new()
	tutorial.setup(self)
	ui_layer.add_child(tutorial)
	return tutorial


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
	if minimap != null:
		minimap.fog = on


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
		sim_event.emit(ev)
		if ev["type"] == &"battle_ended":
			battle_finished.emit(battle.stats.summary())
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
	if mb.button_index == MOUSE_BUTTON_LEFT and mb.pressed and hud.fire_asset >= 0 and not battle.deploying:
		var w: Vector2 = TerrainPicker.pick(rts_camera.camera, mb.position, battle.terrain)
		if w != Vector2.INF:
			_request_fire(w)
		get_viewport().set_input_as_handled()
		return
	if mb.button_index == MOUSE_BUTTON_RIGHT and mb.pressed and hud.fire_asset >= 0:
		hud.set_fire_mode(-1, false)
		get_viewport().set_input_as_handled()
		return
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


## 단축키는 Broken Arrow 기본 배치를 따른다(공식 매뉴얼 기준). 이 게임에만 있는 명령은 빈 키에 둔다.
## WASD = 카메라(RtsCamera), Q 공격 이동, F 급속 이동, E 명령 취소(정지), H 사격 중지 토글, U 탑승/하차,
## B 철수, T 화력 요청(박격포 → 포병), G 연막(박격포 → 포병), P 근접항공지원, 1~9 부대 지정 선택, Ctrl+1~9 지정,
## Tab 하위 제대 선택(Shift+Tab 상위). BA 에는 있지만 이 게임에 없는 명령(R 후진, Z 질주, X 연막탄, C 기만체, V 고도)은 안내만 한다.
func _on_key(k: InputEventKey) -> void:
	var handled: bool = true
	var code: Key = k.physical_keycode
	# 숫자: 1~9 부대 지정 선택, Ctrl 지정, Shift 추가, Alt 대형.
	if code >= KEY_1 and code <= KEY_9:
		var n: int = code - KEY_1
		if k.alt_pressed:
			_formation_hotkey(n)
		elif k.ctrl_pressed:
			_assign_group(n + 1)
		else:
			_recall_group(n + 1, k.shift_pressed)
		get_viewport().set_input_as_handled()
		return
	match code:
		KEY_SPACE:
			if not battle.deploying:
				battle.world.toggle_pause()
		KEY_EQUAL, KEY_KP_ADD:
			battle.world.speed_up()
		KEY_MINUS, KEY_KP_SUBTRACT:
			battle.world.speed_down()
		KEY_SLASH:
			toggle_guide()
		KEY_F10:
			overlay.toggle()
		KEY_F9:
			set_fog(not fog_enabled)
			notifications.push("전장의 안개 %s" % ("켬" if fog_enabled else "끔"), Color(0.8, 0.8, 0.8))
		KEY_M:
			hud.set_order_mode(Order.Type.MOVE)
		KEY_F:
			hud.set_order_mode(Order.Type.FAST_MOVE)
		KEY_Q:
			hud.set_order_mode(Order.Type.ATTACK_MOVE)
		KEY_Y:
			hud.set_order_mode(Order.Type.SEIZE)
		KEY_J:
			hud.set_order_mode(Order.Type.DEFEND)
		KEY_B:
			hud.set_order_mode(Order.Type.WITHDRAW)
		KEY_E:
			_on_immediate_order(Order.Type.HALT, {})
			hud.set_order_mode(Order.Type.MOVE)
		KEY_H:
			_toggle_hold_fire()
		KEY_U:
			_toggle_mount()
		KEY_L:
			_cycle_technique()
		KEY_K:
			_on_immediate_order(Order.Type.RESUPPLY, {})
		KEY_T:
			_cycle_fire_mode(false)
		KEY_G:
			_cycle_fire_mode(true)
		KEY_P:
			hud.set_fire_mode(FireSupportSystem.Asset.CAS, false)
		KEY_TAB:
			_sub_select(not k.shift_pressed)
		KEY_F1, KEY_F2, KEY_F3, KEY_F4:
			_select_company(code)
		KEY_V:
			toggle_altitude()
		KEY_N:
			hud.set_order_mode(Order.Type.LAY_MINES)
		KEY_I:
			hud.set_order_mode(Order.Type.CLEAR_MINES)
		KEY_O:
			hud.set_order_mode(Order.Type.FORTIFY)
		KEY_R, KEY_Z, KEY_X, KEY_C:
			const MISSING: Dictionary[Key, String] = {KEY_R: "후진", KEY_Z: "질주", KEY_X: "연막탄 발사", KEY_C: "기만체"}
			notifications.push("%s 명령은 이 게임에 아직 없습니다" % MISSING[code], Color(0.7, 0.7, 0.7))
		KEY_ESCAPE:
			if hud.fire_asset >= 0:
				hud.set_fire_mode(-1, false)
				get_viewport().set_input_as_handled()
				return
			selection = []
			_selection_changed()
		_:
			handled = false
	if handled:
		get_viewport().set_input_as_handled()


## V: 선택한 공격헬기 반의 고도를 저공 ↔ 고공으로.
func toggle_altitude() -> void:
	var helis: Array[Unit] = []
	for u: Unit in selection:
		if u.kind == &"aviation":
			helis.append(u)
	if helis.is_empty():
		notifications.push("고도 전환은 공격헬기만 할 수 있습니다", Color(0.7, 0.7, 0.7))
		return
	var high: bool = helis[0].sop.altitude != &"high"
	for u: Unit in helis:
		var o: Order = Order.make(Order.Type.SET_SOP, u.id, {"sop": {"altitude": "high" if high else "low"}})
		o.issuer = &"player"
		battle.issue_order(o)
		player_order.emit(o)
	notifications.push("헬기 고도: %s" % ("고공(멀리 보지만 잘 보임)" if high else "저공(지형 뒤에 숨음)"), Color(0.8, 0.8, 0.8))


## T·G 를 거듭 누르면 박격포 → 포병 → 끔 순서로 바뀐다.
func _cycle_fire_mode(smoke: bool) -> void:
	if hud.fire_asset == FireSupportSystem.Asset.MORTAR and hud.fire_smoke == smoke:
		hud.set_fire_mode(FireSupportSystem.Asset.ARTILLERY, smoke)
	elif hud.fire_asset == FireSupportSystem.Asset.ARTILLERY and hud.fire_smoke == smoke:
		hud.set_fire_mode(-1, false)
	else:
		hud.set_fire_mode(FireSupportSystem.Asset.MORTAR, smoke)


func _toggle_hold_fire() -> void:
	if selection.is_empty():
		return
	var hold: bool = selection[0].sop.roe != &"hold_fire"
	_on_immediate_order(Order.Type.SET_SOP, {"sop": {"roe": "hold_fire" if hold else "fire_at_will"}})
	notifications.push("사격 %s" % ("중지(응사만)" if hold else "자유"), Color(0.8, 0.8, 0.8))


## U: 선택 제대 중 탑승한 보병이 있으면 하차, 아니면 탑승.
func _toggle_mount() -> void:
	var any_mounted: bool = false
	for u: Unit in selection:
		var plts: Array[Unit] = [u]
		plts.append_array(u.children_units())
		for c: Unit in plts:
			if c is Platoon and (c as Platoon).has_squads() and (c as Platoon).mounted:
				any_mounted = true
	_on_immediate_order(Order.Type.DISMOUNT if any_mounted else Order.Type.MOUNT, {})


func _cycle_technique() -> void:
	if selection.is_empty():
		return
	const ORDER: Array[String] = ["traveling", "overwatch", "bounding"]
	var i: int = ORDER.find(String(selection[0].sop.technique))
	var next: String = ORDER[(i + 1) % ORDER.size()]
	_on_immediate_order(Order.Type.SET_SOP, {"sop": {"technique": next}})
	notifications.push("이동 방식: %s" % Hud._technique_name(StringName(next)), Color(0.8, 0.8, 0.8))


# --- 부대 지정(컨트롤 그룹) ------------------------------------------------

## n 번에 지정된 제대(격파된 것 제외).
func group_units(n: int) -> Array[Unit]:
	var out: Array[Unit] = []
	if _groups.has(n):
		for u: Unit in _groups[n]:
			if not u.is_destroyed():
				out.append(u)
	return out


func _assign_group(n: int) -> void:
	if selection.is_empty():
		return
	_groups[n] = selection.duplicate()
	notifications.push("부대 지정 %d: %d개 제대" % [n, selection.size()], Color(0.8, 0.8, 0.8))


func _recall_group(n: int, additive: bool) -> void:
	if not _groups.has(n):
		return
	var units: Array[Unit] = []
	for u: Unit in _groups[n]:
		if not u.is_destroyed():
			units.append(u)
	_groups[n] = units
	if units.is_empty():
		return
	var now: int = Time.get_ticks_msec()
	var double: bool = n == _last_group and now - _last_group_ms < DOUBLE_CLICK_MS
	_last_group = n
	_last_group_ms = now
	if additive:
		for u: Unit in units:
			if not selection.has(u):
				selection.append(u)
	else:
		selection = units.duplicate()
	_selection_changed()
	if double:
		rts_camera.look_at_point(units[0].centroid())


## Tab: 선택한 제대의 하위 제대(중대 → 소대)를 선택. Shift+Tab: 상위 제대.
func _sub_select(down: bool) -> void:
	if selection.is_empty():
		return
	var result: Array[Unit] = []
	for u: Unit in selection:
		if down:
			for c: Unit in u.children_units():
				if not c.is_destroyed() and not result.has(c):
					result.append(c)
		elif u.parent != null and u.parent.level != Unit.Level.TASK_FORCE and not result.has(u.parent):
			result.append(u.parent)
	if not result.is_empty():
		selection = result
		_selection_changed()


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
		player_order.emit(o)
	hud.set_order_mode(Order.Type.MOVE)


func _request_fire(target: Vector2) -> void:
	var asset: int = hud.fire_asset
	var res: Dictionary = battle.fires.request(battle.player_side, asset, target, hud.fire_smoke)
	if res.get("ok", false):
		var m: FireSupportSystem.Mission = res["mission"]
		notifications.push("%s 요청 — 약 %.0f초 뒤 초탄 (%s)" % [m.label(), m.delay_sec, OBSERVER_NAMES[m.observer_quality]],
			EventText.INFO)
	else:
		notifications.push("%s 요청 불가: %s" % [FireSupportSystem.ASSET_NAMES[asset], str(res.get("error", ""))], EventText.BAD)
	hud.set_fire_mode(-1, false)


func _on_immediate_order(type: int, params: Dictionary) -> void:
	for u: Unit in selection:
		var o: Order = Order.make(type, u.id, params.duplicate(true))
		o.issuer = &"player"
		battle.issue_order(o)
		player_order.emit(o)


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
	if hud.fire_asset >= 0:
		var w: Vector2 = TerrainPicker.pick(rts_camera.camera, screen_pos, battle.terrain)
		symbols.preview_force = true
		if w == Vector2.INF:
			symbols.preview_text = ""
			return
		var pv: Dictionary = battle.fires.preview(battle.player_side, hud.fire_asset, w)
		var fire_name: String = FireSupportSystem.ASSET_NAMES[hud.fire_asset] + (" 연막" if hud.fire_smoke else "")
		if pv.has("error"):
			symbols.preview_text = "%s — %s" % [fire_name, str(pv["error"])]
		else:
			var q: int = pv["quality"]
			var sec: float = pv["seconds"]
			symbols.preview_text = "%s — %s, 약 %.0f초" % [fire_name, OBSERVER_NAMES[q], sec]
		return
	symbols.preview_force = false
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
	var web: bool = OS.has_feature("web")
	var env := Environment.new()
	env.background_mode = Environment.BG_SKY
	var sky := Sky.new()
	var sky_mat := ProceduralSkyMaterial.new()
	sky_mat.sky_top_color = Color(0.36, 0.52, 0.72)
	sky_mat.sky_horizon_color = Color(0.70, 0.76, 0.80)
	sky_mat.ground_bottom_color = Color(0.25, 0.27, 0.23)
	sky_mat.ground_horizon_color = Color(0.62, 0.66, 0.66)
	sky_mat.sun_angle_max = 20.0
	sky.sky_material = sky_mat
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.ambient_light_sky_contribution = 0.6
	env.ambient_light_energy = 0.75
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	env.tonemap_exposure = 0.95
	# 공기 원근: 멀수록 푸르스름하게.
	env.fog_enabled = true
	env.fog_light_color = Color(0.66, 0.72, 0.78)
	env.fog_density = 0.00006
	env.fog_sky_affect = 0.3
	if not web:
		# 데스크톱(Forward+) 전용 효과.
		env.ssao_enabled = true
		env.ssao_radius = 3.0
		env.ssao_intensity = 1.6
		env.glow_enabled = true
		env.glow_intensity = 0.25
		env.glow_bloom = 0.04
		env.adjustment_enabled = true
		env.adjustment_saturation = 1.08
		env.adjustment_contrast = 1.04
	var we := WorldEnvironment.new()
	we.environment = env
	add_child(we)
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-42.0, -35.0, 0.0)
	sun.light_color = Color(1.0, 0.95, 0.86)
	sun.light_energy = 1.15
	sun.shadow_enabled = not web
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_PARALLEL_4_SPLITS
	sun.directional_shadow_max_distance = 1800.0
	sun.shadow_blur = 1.5
	add_child(sun)
