class_name RtsCamera
extends Node3D
## RTS 카메라: 피벗(지면 위 초점) + 거리·기울기·회전.
## WASD·화살표 키/화면 가장자리 = 이동(Broken Arrow 와 같은 WASD), 휠 = 확대·축소, 가운데 버튼 드래그 = 회전·기울기.

const MIN_DISTANCE: float = 80.0
const MAX_DISTANCE: float = 6000.0
const PAN_SPEED: float = 1.1  # 거리 비례 배율(초당)
const EDGE_MARGIN_PX: float = 6.0
const ZOOM_STEP: float = 1.15
const MIN_PITCH: float = 0.4363  # 25°
const MAX_PITCH: float = 1.4835  # 85°

var camera: Camera3D
var distance: float = 1400.0
var yaw: float = 0.0
var pitch: float = deg_to_rad(55.0)
var focus: Vector2 = Vector2(2000, 2000)
var bounds: Rect2 = Rect2(0, 0, 4000, 4000)
var terrain: Terrain
var edge_scroll: bool = true

var _rotating: bool = false


func _ready() -> void:
	camera = Camera3D.new()
	camera.far = 30000.0
	camera.near = 1.0
	camera.fov = 50.0
	add_child(camera)
	_apply()


func setup(p_terrain: Terrain, start_focus: Vector2) -> void:
	terrain = p_terrain
	bounds = Rect2(Vector2.ZERO, p_terrain.size_m)
	focus = start_focus
	_apply()


func look_at_point(p: Vector2) -> void:
	focus = p
	_apply()


## 카메라 높이(지면 기준). 부호 집계 단계를 정할 때 쓴다.
func height_above_ground() -> float:
	return distance * sin(pitch)


func _process(delta: float) -> void:
	var move := Vector2.ZERO
	# Ctrl·Alt 조합(부대 지정 등)일 때는 WASD 로 움직이지 않는다.
	var wasd: bool = not Input.is_key_pressed(KEY_CTRL) and not Input.is_key_pressed(KEY_ALT) \
		and get_viewport().gui_get_focus_owner() == null
	if Input.is_physical_key_pressed(KEY_LEFT) or (wasd and Input.is_physical_key_pressed(KEY_A)):
		move.x -= 1.0
	if Input.is_physical_key_pressed(KEY_RIGHT) or (wasd and Input.is_physical_key_pressed(KEY_D)):
		move.x += 1.0
	if Input.is_physical_key_pressed(KEY_UP) or (wasd and Input.is_physical_key_pressed(KEY_W)):
		move.y += 1.0
	if Input.is_physical_key_pressed(KEY_DOWN) or (wasd and Input.is_physical_key_pressed(KEY_S)):
		move.y -= 1.0
	if edge_scroll and DisplayServer.window_is_focused():
		var vp: Viewport = get_viewport()
		var m: Vector2 = vp.get_mouse_position()
		var size: Vector2 = vp.get_visible_rect().size
		if m.x >= 0.0 and m.y >= 0.0 and m.x <= size.x and m.y <= size.y:
			if m.x < EDGE_MARGIN_PX:
				move.x -= 1.0
			elif m.x > size.x - EDGE_MARGIN_PX:
				move.x += 1.0
			if m.y < EDGE_MARGIN_PX:
				move.y += 1.0
			elif m.y > size.y - EDGE_MARGIN_PX:
				move.y -= 1.0
	if move != Vector2.ZERO:
		# 화면 기준 이동을 지도 좌표로: yaw 0 = 북쪽을 봄.
		var fwd := Vector2(sin(yaw), cos(yaw))
		var right := Vector2(cos(yaw), -sin(yaw))
		focus += (right * move.x + fwd * move.y).normalized() * distance * PAN_SPEED * delta
		_apply()


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		var mb: InputEventMouseButton = event
		if mb.button_index == MOUSE_BUTTON_WHEEL_UP and mb.pressed:
			distance = maxf(MIN_DISTANCE, distance / ZOOM_STEP)
			_apply()
		elif mb.button_index == MOUSE_BUTTON_WHEEL_DOWN and mb.pressed:
			distance = minf(MAX_DISTANCE, distance * ZOOM_STEP)
			_apply()
		elif mb.button_index == MOUSE_BUTTON_MIDDLE:
			_rotating = mb.pressed
	elif event is InputEventMouseMotion and _rotating:
		var mm: InputEventMouseMotion = event
		yaw = wrapf(yaw + mm.relative.x * 0.005, -PI, PI)
		pitch = clampf(pitch + mm.relative.y * 0.004, MIN_PITCH, MAX_PITCH)
		_apply()


func _apply() -> void:
	focus = Vector2(clampf(focus.x, bounds.position.x, bounds.end.x), clampf(focus.y, bounds.position.y, bounds.end.y))
	var ground: float = terrain.ground_height(focus) if terrain != null else 0.0
	var target: Vector3 = ViewCoords.to_3d(focus, ground)
	# 초점에서 뒤쪽(-forward)·위로 distance 만큼.
	var back := Vector2(-sin(yaw), -cos(yaw)) * distance * cos(pitch)
	var cam_pos: Vector3 = ViewCoords.to_3d(focus + back, ground + distance * sin(pitch))
	if camera != null:
		camera.global_position = cam_pos
		camera.look_at(target, Vector3.UP)
