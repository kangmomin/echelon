extends Node
## 앱 루트. 화면 전환(메뉴 → 편성 → 배치 → 전투 → AAR)과 커맨드라인 모드를 맡는다.
##
## 커맨드라인(-- 뒤):
##   --smoke-ticks=N        헤드리스로 N 틱 돌리고 종료(실시간 경로, 4배속)
##   --screenshot=PATH      잠시 뒤 화면을 PNG 로 저장하고 종료(렌더러 필요)
##   --sandbox              메뉴 없이 샌드박스 전투로 바로 시작
##   --nofog                전장의 안개 끔(F9 와 같음)
##   --red-attack=X,Y       시작하자마자 적 중대들에 공격 이동 명령(교전 화면 확인용)

const SMOKE_TIMEOUT_SEC: float = 60.0
const SCREENSHOT_DELAY_FRAMES: int = 30

var catalog: DataCatalog
var battle_scene: BattleScene

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
	start_battle(BattleSetup.sandbox(catalog))


func start_battle(b: Battle) -> void:
	if battle_scene != null:
		battle_scene.queue_free()
	battle_scene = BattleScene.new()
	battle_scene.name = "Battle"
	battle_scene.setup(b)
	add_child(battle_scene)
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
	if _camera_args.has("run_sec"):
		var run_sec: float = _camera_args["run_sec"]
		battle_scene.battle.run_seconds(run_sec)
	if _camera_args.has("select"):
		var select_id: int = _camera_args["select"]
		var u: Unit = battle_scene.battle.get_unit(select_id)
		if u != null:
			battle_scene.call_deferred(&"select_units", [u] as Array[Unit])
