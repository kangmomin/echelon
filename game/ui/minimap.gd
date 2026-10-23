class_name Minimap
extends PanelContainer
## 미니맵: 지형, 거점, 아군 엘리먼트, (안개 기준) 보이는·보고된 적, 카메라 위치. 클릭하면 카메라 이동. PLAN 12.1.

const SIZE_PX: float = 210.0
const REFRESH_SEC: float = 0.25

var battle: Battle
var rts_camera: RtsCamera
var player_side: StringName = &"blue"
var fog: bool = true
var _tex: ImageTexture
var _canvas: Control
var _timer: float = 0.0


func _ready() -> void:
	var style := StyleBoxFlat.new()
	style.bg_color = Hud.PANEL_BG
	style.set_corner_radius_all(4)
	style.set_content_margin_all(5)
	add_theme_stylebox_override(&"panel", style)
	set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_RIGHT)
	offset_right = -8
	offset_bottom = -246
	offset_left = -8 - SIZE_PX - 10
	offset_top = offset_bottom - SIZE_PX - 10
	grow_horizontal = Control.GROW_DIRECTION_BEGIN
	grow_vertical = Control.GROW_DIRECTION_BEGIN
	_canvas = Control.new()
	_canvas.custom_minimum_size = Vector2(SIZE_PX, SIZE_PX)
	_canvas.mouse_filter = Control.MOUSE_FILTER_STOP
	_canvas.draw.connect(_draw_map)
	_canvas.gui_input.connect(_on_input)
	add_child(_canvas)
	_build_texture()


## 지형 종류를 한 번 그려 둔다.
func _build_texture() -> void:
	var n: int = int(SIZE_PX)
	var img := Image.create(n, n, false, Image.FORMAT_RGB8)
	var size_m: Vector2 = battle.terrain.size_m
	for y: int in n:
		for x: int in n:
			var p := Vector2((x + 0.5) / n * size_m.x, (1.0 - (y + 0.5) / n) * size_m.y)
			var t: int = battle.terrain.type_at(p)
			var c: Color = TerrainMesh.COLORS[t]
			var h: float = battle.terrain.ground_height(p)
			c = c.lightened(clampf((h - 40.0) / 120.0, -0.3, 0.3)) if h >= 40.0 else c.darkened(clampf((40.0 - h) / 120.0, 0.0, 0.3))
			img.set_pixel(x, y, c)
	_tex = ImageTexture.create_from_image(img)


func _process(delta: float) -> void:
	_timer += delta
	if _timer >= REFRESH_SEC:
		_timer = 0.0
		_canvas.queue_redraw()


func _to_px(p: Vector2) -> Vector2:
	var size_m: Vector2 = battle.terrain.size_m
	return Vector2(p.x / size_m.x * SIZE_PX, (1.0 - p.y / size_m.y) * SIZE_PX)


func _to_map(px: Vector2) -> Vector2:
	var size_m: Vector2 = battle.terrain.size_m
	return Vector2(px.x / SIZE_PX * size_m.x, (1.0 - px.y / SIZE_PX) * size_m.y)


func _draw_map() -> void:
	if battle == null:
		return
	_canvas.draw_texture(_tex, Vector2.ZERO)
	if battle.scoring != null:
		for ps: ScoringSystem.PointState in battle.scoring.points:
			var col: Color = Color(0.9, 0.9, 0.9)
			if ps.owner != &"":
				col = SymbolLayer.FRIENDLY_FILL if ps.owner == player_side else SymbolLayer.HOSTILE_FILL
			var r: float = ps.def.radius_m / battle.terrain.size_m.x * SIZE_PX
			_canvas.draw_arc(_to_px(ps.def.pos), r, 0.0, TAU, 24, col, 1.5)
	var tick: int = battle.world.tick
	var tf: TaskForce = battle.task_forces.get(player_side)
	for e: Element in battle.elements:
		if not e.on_map or e.is_mounted() or not e.is_alive():
			continue
		var pos: Vector2 = e.pos
		var col: Color = SymbolLayer.FRIENDLY_FILL
		if e.side != player_side:
			col = SymbolLayer.HOSTILE_FILL
			if fog and not battle.detection.seen_recently(player_side, e.id, tick):
				if tf == null or not tf.known_enemies.has(e.id):
					continue
				var info: Dictionary = tf.known_enemies[e.id]
				pos = info["pos"]
				col.a = 0.5
		_canvas.draw_rect(Rect2(_to_px(pos) - Vector2(1.5, 1.5), Vector2(3, 3)), col)
	if rts_camera != null:
		var c: Vector2 = _to_px(rts_camera.focus)
		var half: float = rts_camera.distance * 0.6 / battle.terrain.size_m.x * SIZE_PX
		_canvas.draw_rect(Rect2(c - Vector2(half, half), Vector2(half, half) * 2.0), Color(1, 1, 1, 0.8), false, 1.0)


func _on_input(event: InputEvent) -> void:
	if rts_camera == null:
		return
	var press: bool = event is InputEventMouseButton and (event as InputEventMouseButton).pressed \
		and (event as InputEventMouseButton).button_index == MOUSE_BUTTON_LEFT
	var drag: bool = event is InputEventMouseMotion and ((event as InputEventMouseMotion).button_mask & MOUSE_BUTTON_MASK_LEFT) != 0
	if press or drag:
		var mpos: Vector2 = (event as InputEventMouse).position
		rts_camera.look_at_point(_to_map(mpos))
		accept_event()
