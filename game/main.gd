extends Node
## 앱 루트. 화면 전환(메뉴 → 편성 → 배치 → 전투 → AAR)과 커맨드라인 모드를 맡는다.
##
## 커맨드라인(-- 뒤):
##   --smoke-ticks=N        헤드리스로 N 틱 돌리고 종료(실시간 경로, 4배속)
##   --screenshot=PATH      잠시 뒤 화면을 PNG 로 저장하고 종료(렌더러 필요)
##   --scene=NAME           시작 화면: menu(기본) | builder | skirmish(기본 편성으로 배치 단계) | sandbox
##                          --smoke-ticks / --screenshot 만 주면 sandbox
##   --start                skirmish 배치 단계를 바로 끝내고 전투 시작
##   --nofog                전장의 안개 끔(F9 와 같음)
##   --red-attack=X,Y       시작하자마자 적 중대들에 공격 이동 명령(교전 화면 확인용)
##   --fire=A,X,Y[,smoke]   시작하자마자 화력 요청(A: 0 박격포, 1 포병, 2 CAS)

const SMOKE_TIMEOUT_SEC: float = 60.0
const SCREENSHOT_DELAY_FRAMES: int = 30

var catalog: DataCatalog
var battle_scene: BattleScene
var screen: Control = null
var ui_root: CanvasLayer

var _scene_arg: String = ""
var _auto_start: bool = false

var _smoke_ticks: int = -1
var _smoke_elapsed: float = 0.0
var _screenshot_path: String = ""
var _screenshot_frames: int = SCREENSHOT_DELAY_FRAMES
var _frames: int = 0
var _camera_args: Dictionary = {}


func _ready() -> void:
	_parse_cmdline()
	catalog = DataLoader.load_catalog()
	for msg: String in catalog.errors:
		push_error("[data] " + msg)
	for msg: String in catalog.warnings:
		print("[data] 경고: " + msg)
	print("[data] " + catalog.summary())
	if not catalog.is_valid():
		push_error("데이터 오류로 시작할 수 없습니다.")
		if _smoke_ticks >= 0:
			get_tree().quit(1)
		return
	ui_root = CanvasLayer.new()
	ui_root.name = "Screens"
	add_child(ui_root)
	var first: String = _scene_arg
	if first == "":
		first = "sandbox" if (_smoke_ticks >= 0 or _screenshot_path != "") else "menu"
	match first:
		"builder":
			show_builder()
		"skirmish":
			start_skirmish(catalog.default_force(&"US"))
		"sandbox":
			start_battle(BattleSetup.sandbox(catalog))
		_:
			show_menu()


# --- 화면 전환 -------------------------------------------------------------

func _clear_screens() -> void:
	if screen != null:
		screen.queue_free()
		screen = null
	if battle_scene != null:
		battle_scene.queue_free()
		battle_scene = null


func show_menu() -> void:
	_clear_screens()
	var m := MainMenu.new()
	m.skirmish_requested.connect(show_builder)
	m.sandbox_requested.connect(func() -> void: start_battle(BattleSetup.sandbox(catalog)))
	m.quit_requested.connect(func() -> void: get_tree().quit())
	screen = m
	ui_root.add_child(m)


func show_builder(initial: ForceSpec = null) -> void:
	_clear_screens()
	var fb := ForceBuilder.new()
	fb.setup(catalog, catalog.get_scenario(&"skirmish_01"), initial)
	fb.back_requested.connect(show_menu)
	fb.start_requested.connect(start_skirmish)
	screen = fb
	ui_root.add_child(fb)


## 플레이어 편성으로 스커미시(배치 단계부터). 상대는 다른 진영의 기본 편성.
func start_skirmish(player: ForceSpec) -> void:
	var enemy_faction: StringName = &"RU" if player.faction == &"US" else &"US"
	var enemy: ForceSpec = catalog.default_force(enemy_faction)
	var sc: ScenarioDef = catalog.get_scenario(&"skirmish_01")
	var player_side: StringName = &"blue"
	for side: StringName in sc.sides.keys():
		if sc.sides[side].faction == player.faction:
			player_side = side
	var blue: ForceSpec = player if player_side == &"blue" else enemy
	var red: ForceSpec = enemy if player_side == &"blue" else player
	var b: Battle = BattleSetup.skirmish(catalog, blue, red, sc.id)
	b.player_side = player_side
	start_battle(b, _auto_start)


func start_battle(b: Battle, auto_begin: bool = false) -> void:
	_clear_screens()
	battle_scene = BattleScene.new()
	battle_scene.name = "Battle"
	battle_scene.setup(b)
	add_child(battle_scene)
	if auto_begin:
		battle_scene.start_battle()
	if _smoke_ticks >= 0:
		b.world.set_speed_index(SimConst.SPEED_STEPS.size() - 1)
	if not _camera_args.is_empty():
		_apply_camera_args()
	print("[sim] 전투 시작: seed=%d, 엘리먼트 %d, 제대 %d" % [b.world.rng.seed_value, b.elements.size(), b.units.size()])


func _process(delta: float) -> void:
	_frames += 1
	if _smoke_ticks >= 0 and battle_scene != null:
		_smoke_elapsed += delta
		var b: Battle = battle_scene.battle
		if b.world.tick >= _smoke_ticks:
			print("[smoke] OK tick=%d sim=%.1fs real=%.2fs hash=%d" % [b.world.tick, b.world.get_sim_time_sec(),
				_smoke_elapsed, b.world.state_hash()])
			get_tree().quit(0)
		elif _smoke_elapsed > SMOKE_TIMEOUT_SEC:
			print("[smoke] FAIL timeout tick=%d" % b.world.tick)
			get_tree().quit(2)
	if _screenshot_path != "" and _frames == _screenshot_frames:
		var image: Image = get_viewport().get_texture().get_image()
		var err: Error = image.save_png(_screenshot_path)
		print("[screenshot] %s -> %s" % [error_string(err), ProjectSettings.globalize_path(_screenshot_path)])
		get_tree().quit(0 if err == OK else 1)


func _parse_cmdline() -> void:
	for arg: String in OS.get_cmdline_user_args():
		var value: String = arg.get_slice("=", 1)
		if arg.begins_with("--smoke-ticks="):
			_smoke_ticks = value.to_int()
		elif arg.begins_with("--screenshot="):
			_screenshot_path = value
		elif arg.begins_with("--screenshot-frames="):
			_screenshot_frames = value.to_int()
		elif arg.begins_with("--camera="):
			# x,y,distance,pitch_deg,yaw_deg
			var parts: PackedStringArray = value.split(",")
			_camera_args.merge({"x": parts[0].to_float(), "y": parts[1].to_float(), "d": parts[2].to_float(),
				"pitch": parts[3].to_float() if parts.size() > 3 else 55.0, "yaw": parts[4].to_float() if parts.size() > 4 else 0.0}, true)
		elif arg.begins_with("--select="):
			_camera_args["select"] = value.to_int()
		elif arg.begins_with("--run-sec="):
			_camera_args["run_sec"] = value.to_float()
		elif arg.begins_with("--scene="):
			_scene_arg = value
		elif arg == "--sandbox":
			_scene_arg = "sandbox"
		elif arg == "--start":
			_auto_start = true
		elif arg.begins_with("--fire="):
			# asset(0 박격포·1 포병·2 CAS),x,y[,smoke]
			var fp: PackedStringArray = value.split(",")
			_camera_args["fire"] = [fp[0].to_int(), Vector2(fp[1].to_float(), fp[2].to_float()), fp.size() > 3]
		elif arg == "--nofog":
			_camera_args["nofog"] = true
		elif arg.begins_with("--red-attack="):
			var xy: PackedStringArray = value.split(",")
			_camera_args["red_attack"] = Vector2(xy[0].to_float(), xy[1].to_float())


## 스크린샷 검증용: 카메라 위치·선택·미리 돌릴 시간.
func _apply_camera_args() -> void:
	var cam: RtsCamera = battle_scene.rts_camera
	if _camera_args.has("x"):
		var x: float = _camera_args["x"]
		var y: float = _camera_args["y"]
		var d: float = _camera_args["d"]
		var pitch_deg: float = _camera_args["pitch"]
		var yaw_deg: float = _camera_args["yaw"]
		cam.focus = Vector2(x, y)
		cam.distance = d
		cam.pitch = deg_to_rad(pitch_deg)
		cam.yaw = deg_to_rad(yaw_deg)
		cam.edge_scroll = false
		cam.call_deferred(&"_apply")
	if _camera_args.has("nofog"):
		battle_scene.set_fog(false)
	if _camera_args.has("red_attack"):
		var target: Vector2 = _camera_args["red_attack"]
		var red: TaskForce = battle_scene.battle.task_forces[&"red"]
		for i: int in red.companies.size():
			var o: Order = Order.make(Order.Type.ATTACK_MOVE, red.companies[i].id)
			o.set_pos(target + Vector2(i * 500.0 - 250.0, 0.0))
			battle_scene.battle.issue_order(o)
	if _camera_args.has("fire"):
		var fire: Array = _camera_args["fire"]
		var asset: int = fire[0]
		var pos: Vector2 = fire[1]
		var smoke: bool = fire[2]
		battle_scene.battle.run_ticks(25)  # 지휘망이 한 번 계산되도록
		var res: Dictionary = battle_scene.battle.fires.request(battle_scene.battle.player_side, asset, pos, smoke)
		print("[fire] ", res.get("error", "요청됨"))
	if _camera_args.has("run_sec"):
		var run_sec: float = _camera_args["run_sec"]
		battle_scene.battle.run_seconds(run_sec)
	if _camera_args.has("select"):
		var select_id: int = _camera_args["select"]
		var u: Unit = battle_scene.battle.get_unit(select_id)
		if u != null:
			battle_scene.call_deferred(&"select_units", [u] as Array[Unit])
