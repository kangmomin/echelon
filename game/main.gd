extends Node3D
## M0 메인 씬: 데이터 로드 → 시뮬 월드 생성 → 빈 지도 → 디버그 오버레이 → 대형 슬롯 미리보기.
##
## 조작: Space 일시정지, = / - 배속, Tab 미리보기 대형 변경, F3 디버그 오버레이.
## 헤드리스 스모크 테스트: godot --headless --path . -- --smoke-ticks=50
## 스크린샷: godot --path . -- --screenshot=user://m0.png   (렌더러가 있는 환경에서)

const SCENARIO_ID: StringName = &"skirmish_01"
const FALLBACK_MAP_SIZE_M: Vector2 = Vector2(4000.0, 4000.0)
const PREVIEW_ELEMENT_SIZE: Vector3 = Vector3(10.0, 4.0, 16.0)
const SMOKE_TIMEOUT_SEC: float = 30.0
const SCREENSHOT_DELAY_FRAMES: int = 20

## 액션 이름 → 물리 키. 키 설정 UI 가 생기기 전까지 코드로 등록한다(DECISIONS D-004).
const INPUT_BINDINGS: Dictionary[StringName, Key] = {
	&"toggle_debug_overlay": KEY_F3,
	&"toggle_pause": KEY_SPACE,
	&"speed_up": KEY_EQUAL,
	&"speed_down": KEY_MINUS,
	&"preview_next_formation": KEY_TAB,
}

var catalog: DataCatalog
var world: SimWorld
var scenario: ScenarioDef

var _overlay: DebugOverlay
var _map_size: Vector2 = FALLBACK_MAP_SIZE_M
var _preview_root: Node3D
var _preview_formations: Array[FormationDef] = []
var _preview_index: int = 0
var _ticks_last_frame: int = 0
var _advance_usec: int = 0
var _advance_usec_peak: int = 0
var _events_total: int = 0
var _smoke_ticks: int = -1
var _smoke_elapsed: float = 0.0
var _screenshot_path: String = ""
var _frames: int = 0


func _ready() -> void:
	_register_input_actions()
	_parse_cmdline()

	catalog = DataLoader.load_catalog()
	for msg: String in catalog.errors:
		push_error("[data] " + msg)
	for msg: String in catalog.warnings:
		print("[data] 경고: " + msg)
	print("[data] " + catalog.summary())

	scenario = catalog.get_scenario(SCENARIO_ID)
	var seed_value: int = scenario.seed_value if scenario != null else 1
	if scenario != null:
		_map_size = scenario.size_m
	world = SimWorld.new(seed_value)
	print("[sim] SimWorld 생성, seed=%d, %d Hz" % [seed_value, SimConst.TICK_RATE])

	if _smoke_ticks >= 0:
		world.set_speed_index(SimConst.SPEED_STEPS.size() - 1)

	_build_environment()
	_build_ground()
	_build_camera()
	_build_formation_preview()

	_overlay = DebugOverlay.new()
	add_child(_overlay)


func _process(delta: float) -> void:
	var t0: int = Time.get_ticks_usec()
	_ticks_last_frame = world.advance(delta)
	_advance_usec = Time.get_ticks_usec() - t0
	if _ticks_last_frame > 0:
		_advance_usec_peak = maxi(_advance_usec_peak, _advance_usec)
	_events_total += world.events.drain().size()

	_overlay.set_lines(_overlay_lines())

	if _smoke_ticks >= 0:
		_smoke_elapsed += delta
		_check_smoke_done()

	_frames += 1
	if _screenshot_path != "" and _frames == SCREENSHOT_DELAY_FRAMES:
		_save_screenshot()


func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed(&"toggle_debug_overlay"):
		_overlay.toggle()
	elif event.is_action_pressed(&"toggle_pause"):
		world.toggle_pause()
	elif event.is_action_pressed(&"speed_up"):
		world.speed_up()
	elif event.is_action_pressed(&"speed_down"):
		world.speed_down()
	elif event.is_action_pressed(&"preview_next_formation"):
		_preview_index = (_preview_index + 1) % maxi(_preview_formations.size(), 1)
		_rebuild_preview()
	else:
		return
	get_viewport().set_input_as_handled()


# --- 오버레이 ------------------------------------------------------------

func _overlay_lines() -> PackedStringArray:
	var sim_sec: int = int(world.get_sim_time_sec())
	var lines := PackedStringArray([
		"ECHELON M0   FPS %d" % Engine.get_frames_per_second(),
		"tick %d   sim %02d:%02d   speed %.1fx%s" % [
			world.tick, floori(sim_sec / 60.0), sim_sec % 60, world.get_speed(), "   [일시정지]" if world.paused else ""],
		"ticks/frame %d   advance %d us (peak %d us)" % [_ticks_last_frame, _advance_usec, _advance_usec_peak],
		"events drained %d   state hash %d" % [_events_total, world.state_hash()],
		"data: " + catalog.summary(),
	])
	if not _preview_formations.is_empty():
		var f: FormationDef = _preview_formations[_preview_index]
		lines.append("미리보기 대형: %s (%s)   [Tab] 변경" % [f.display_name, f.id])
	lines.append("[Space] 일시정지  [=/-] 배속  [F3] 오버레이")
	return lines


# --- 장면 구성 -----------------------------------------------------------

func _build_environment() -> void:
	var env := Environment.new()
	env.background_mode = Environment.BG_SKY
	var sky := Sky.new()
	sky.sky_material = ProceduralSkyMaterial.new()
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	var world_env := WorldEnvironment.new()
	world_env.environment = env
	add_child(world_env)

	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-55.0, -30.0, 0.0)
	sun.shadow_enabled = true
	add_child(sun)


func _build_ground() -> void:
	var plane := PlaneMesh.new()
	plane.size = _map_size
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.36, 0.45, 0.28)
	plane.material = mat
	var ground := MeshInstance3D.new()
	ground.name = "Ground"
	ground.mesh = plane
	ground.position = ViewCoords.to_3d(_map_size * 0.5)
	add_child(ground)


func _build_camera() -> void:
	var cam := Camera3D.new()
	cam.name = "Camera"
	cam.far = 20000.0
	var target: Vector3 = ViewCoords.to_3d(_map_size * 0.5)
	cam.position = target + Vector3(0.0, 260.0, 300.0)
	add_child(cam)
	cam.look_at(target, Vector3.UP)


func _build_formation_preview() -> void:
	_preview_root = Node3D.new()
	_preview_root.name = "FormationPreview"
	add_child(_preview_root)
	_preview_formations = catalog.formations_for(&"platoon")
	for i: int in _preview_formations.size():
		if _preview_formations[i].id == &"wedge":
			_preview_index = i
	_rebuild_preview()


## 지도 중앙에 소대(엘리먼트 4) 대형 슬롯을 상자로 그린다. 진행 방향은 북쪽.
func _rebuild_preview() -> void:
	for child: Node in _preview_root.get_children():
		child.queue_free()
	if _preview_formations.is_empty():
		return
	var f: FormationDef = _preview_formations[_preview_index]
	var center: Vector2 = _map_size * 0.5
	var slots: PackedVector2Array = FormationSolver.world_slots(f, 4, center, 0.0)
	for i: int in slots.size():
		var box := BoxMesh.new()
		box.size = PREVIEW_ELEMENT_SIZE
		var mat := StandardMaterial3D.new()
		mat.albedo_color = Color(0.25, 0.45, 0.9) if i == 0 else Color(0.75, 0.78, 0.82)
		box.material = mat
		var mi := MeshInstance3D.new()
		mi.mesh = box
		mi.position = ViewCoords.to_3d(slots[i], PREVIEW_ELEMENT_SIZE.y * 0.5)
		mi.rotation.y = ViewCoords.heading_to_yaw(0.0)
		_preview_root.add_child(mi)
	var label := Label3D.new()
	label.text = f.display_name
	label.font_size = 64
	label.pixel_size = 0.25
	label.outline_size = 12
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	label.position = ViewCoords.to_3d(center + Vector2(0.0, 70.0), 25.0)
	_preview_root.add_child(label)


# --- 입력·커맨드라인 -----------------------------------------------------

func _register_input_actions() -> void:
	for action: StringName in INPUT_BINDINGS.keys():
		if InputMap.has_action(action):
			continue
		InputMap.add_action(action)
		var ev := InputEventKey.new()
		ev.physical_keycode = INPUT_BINDINGS[action]
		InputMap.action_add_event(action, ev)


func _parse_cmdline() -> void:
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--smoke-ticks="):
			_smoke_ticks = arg.get_slice("=", 1).to_int()
		elif arg.begins_with("--screenshot="):
			_screenshot_path = arg.get_slice("=", 1)


func _save_screenshot() -> void:
	var image: Image = get_viewport().get_texture().get_image()
	var err: Error = image.save_png(_screenshot_path)
	print("[screenshot] %s -> %s" % [error_string(err), ProjectSettings.globalize_path(_screenshot_path)])
	get_tree().quit(0 if err == OK else 1)


## 스모크 모드: 실시간 경로(advance)로 N 틱을 돌린 뒤 결과를 출력하고 종료한다.
func _check_smoke_done() -> void:
	if world.tick >= _smoke_ticks:
		var ok: bool = catalog.is_valid()
		print("[smoke] %s tick=%d sim=%.1fs real=%.2fs hash=%d data_errors=%d" % [
			"OK" if ok else "FAIL", world.tick, world.get_sim_time_sec(), _smoke_elapsed,
			world.state_hash(), catalog.errors.size()])
		get_tree().quit(0 if ok else 1)
	elif _smoke_elapsed > SMOKE_TIMEOUT_SEC:
		print("[smoke] FAIL timeout tick=%d" % world.tick)
		get_tree().quit(2)
