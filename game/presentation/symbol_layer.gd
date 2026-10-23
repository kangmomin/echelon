class_name SymbolLayer
extends Control
## 2D 오버레이: NATO 부호풍 제대 부호, 선택 표시, 경로·목적지·슬롯, 명령 전파 표시. PLAN 12장.
## 카메라 높이에 따라 엘리먼트 → 소대 → 중대 부호로 합친다(500 m / 2200 m).

const PLATOON_LEVEL_HEIGHT: float = 450.0
const COMPANY_LEVEL_HEIGHT: float = 2200.0
const FRAME_W: float = 30.0
const FRAME_H: float = 20.0
const PICK_RADIUS_PX: float = 18.0
const FRIENDLY_FILL: Color = Color(0.50, 0.72, 1.0, 0.92)
const HOSTILE_FILL: Color = Color(1.0, 0.50, 0.45, 0.92)
const FRAME_COLOR: Color = Color(0.05, 0.05, 0.08)
const SELECT_COLOR: Color = Color(1.0, 0.93, 0.35)
const PATH_COLOR: Color = Color(1.0, 1.0, 1.0, 0.75)
const PROPAGATING_COLOR: Color = Color(1.0, 0.8, 0.3, 0.9)
const STALE_ALPHA: float = 0.45

enum Level { ELEMENT, PLATOON, COMPANY }

var battle: Battle
var rts_camera: RtsCamera
var player_side: StringName = &"blue"
## 선택된 제대들.
var selection: Array[Unit] = []
## 적 부호를 보고된 것만 그릴지.
var fog: bool = false
## 우클릭 드래그 미리보기(명령 방향).
var drag_from: Vector2 = Vector2.INF
var drag_to: Vector2 = Vector2.INF
## 커서 옆 명령 미리보기.
var preview_text: String = ""
var preview_pos: Vector2 = Vector2.ZERO
## 선택이 없어도 미리보기를 그릴지(화력 요청 모드).
var preview_force: bool = false

var _font: Font
## 이번 프레임에 그린 부호(선택 판정용): [{"pos": Vector2, "unit": Unit}]
var _drawn: Array[Dictionary] = []


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	# 프로젝트 테마의 한글 글꼴(웹 빌드에는 시스템 글꼴 대체가 없다).
	_font = get_theme_default_font()


func _process(_delta: float) -> void:
	queue_redraw()


func current_level() -> int:
	var h: float = rts_camera.height_above_ground()
	if h < PLATOON_LEVEL_HEIGHT:
		return Level.ELEMENT
	if h < COMPANY_LEVEL_HEIGHT:
		return Level.PLATOON
	return Level.COMPANY


## 화면 좌표에서 가장 가까운 아군 제대(없으면 null).
func pick_unit(screen_pos: Vector2, side: StringName = &"") -> Unit:
	var best: Unit = null
	var best_d: float = PICK_RADIUS_PX
	for item: Dictionary in _drawn:
		var u: Unit = item["unit"]
		if side != &"" and u.side != side:
			continue
		var item_pos: Vector2 = item["pos"]
		var d: float = item_pos.distance_to(screen_pos)
		if d < best_d:
			best_d = d
			best = u
	return best


func _draw() -> void:
	_drawn.clear()
	if battle == null or rts_camera == null or rts_camera.camera == null:
		return
	var level: int = current_level()
	var alpha: float = battle.world.get_interpolation_alpha()
	_draw_control_points()
	_draw_engineering()
	if battle.deploying:
		_draw_deploy_zone()
	for side: StringName in Battle.SIDES:
		var tf: TaskForce = battle.task_forces.get(side)
		if tf == null:
			continue
		var hostile: bool = side != player_side
		for u: Unit in tf.all_units():
			if not u.on_map or u.level == Unit.Level.TASK_FORCE or u.is_destroyed():
				continue
			if level == Level.ELEMENT and u.level == Unit.Level.PLATOON:
				_draw_elements(u as Platoon, hostile, alpha)
			elif level == Level.PLATOON and u.level == Unit.Level.PLATOON:
				_draw_unit(u, hostile, alpha)
			elif level == Level.COMPANY:
				if u.level == Unit.Level.COMPANY or (u.level == Unit.Level.PLATOON and not (u as Platoon).follows_parent()):
					_draw_unit(u, hostile, alpha)
	for u: Unit in selection:
		_draw_selection_overlay(u)
	if preview_text != "" and (not selection.is_empty() or preview_force):
		_text(preview_pos + Vector2(18, 26), preview_text, 13, Color(1.0, 0.92, 0.6))
	if drag_from != Vector2.INF and drag_to != Vector2.INF:
		var a: Vector2 = _screen(drag_from)
		var b: Vector2 = _screen(drag_to)
		draw_line(a, b, SELECT_COLOR, 2.0)
		_draw_arrow_head(a, b, SELECT_COLOR)


## 거점: 반경 원(소유 진영 색), 이름, 경합 표시. PLAN 11.1.
func _draw_control_points() -> void:
	if battle.scoring == null:
		return
	for ps: ScoringSystem.PointState in battle.scoring.points:
		var c: Vector2 = _screen(ps.def.pos)
		if c == Vector2.INF:
			continue
		var edge: Vector2 = _screen(ps.def.pos + Vector2(ps.def.radius_m, 0.0))
		var r: float = c.distance_to(edge) if edge != Vector2.INF else 20.0
		var col: Color = Color(0.85, 0.85, 0.85)
		if ps.owner != &"":
			col = FRIENDLY_FILL if ps.owner == player_side else HOSTILE_FILL
		if ps.contested and Engine.get_process_frames() % 40 < 20:
			col = SELECT_COLOR
		draw_arc(c, r, 0.0, TAU, 48, Color(col, 0.85), 2.0)
		draw_circle(c, r, Color(col, 0.06))
		var flag := PackedVector2Array([c + Vector2(-7, -10), c + Vector2(7, -10), c + Vector2(7, 4), c + Vector2(-7, 4)])
		draw_colored_polygon(flag, Color(col, 0.95))
		flag.append(flag[0])
		draw_polyline(flag, FRAME_COLOR, 1.5)
		var label: String = ps.def.display_name
		if ps.contested:
			label += " (경합)"
		_text(c + Vector2(-40, 20), label, 13, Color(1, 1, 1, 0.95))


## 공병: 플레이어 진영이 아는 지뢰 지대(점선 원 + ×), 아군 진지(톱니 원).
func _draw_engineering() -> void:
	if battle.engineers == null:
		return
	for m: EngineerSystem.Minefield in battle.engineers.known_minefields(player_side):
		var c: Vector2 = _screen(m.pos)
		if c == Vector2.INF:
			continue
		var edge: Vector2 = _screen(m.pos + Vector2(m.radius, 0.0))
		var r: float = c.distance_to(edge) if edge != Vector2.INF else 14.0
		var col: Color = FRIENDLY_FILL if m.side == player_side else HOSTILE_FILL
		for i: int in 16:
			if i % 2 == 0:
				draw_arc(c, r, TAU * i / 16.0, TAU * (i + 1) / 16.0, 4, col, 2.0)
		draw_circle(c, r, Color(col, 0.08))
		for o: Vector2 in [Vector2(-0.4, -0.2), Vector2(0.3, -0.35), Vector2(0.0, 0.3), Vector2(-0.35, 0.35), Vector2(0.4, 0.2)]:
			var p: Vector2 = c + o * r
			draw_line(p + Vector2(-3, -3), p + Vector2(3, 3), col, 1.6)
			draw_line(p + Vector2(-3, 3), p + Vector2(3, -3), col, 1.6)
		_text(c + Vector2(-30, -r - 6), "지뢰 %d" % m.charges, 12, col)
	for fp: EngineerSystem.FieldPosition in battle.engineers.positions:
		if fp.side != player_side:
			continue
		var c2: Vector2 = _screen(fp.pos)
		if c2 == Vector2.INF:
			continue
		var edge2: Vector2 = _screen(fp.pos + Vector2(fp.radius, 0.0))
		var r2: float = c2.distance_to(edge2) if edge2 != Vector2.INF else 16.0
		var pts := PackedVector2Array()
		for i: int in 33:
			var a: float = TAU * i / 32.0
			pts.append(c2 + Vector2(cos(a), sin(a)) * (r2 + (3.0 if i % 2 == 0 else -2.0)))
		draw_polyline(pts, Color(0.85, 0.75, 0.45, 0.9), 1.6)
		_text(c2 + Vector2(-14, 4), "진지", 12, Color(0.95, 0.85, 0.55))


## 배치 단계: 아군 배치 구역(채운 사각형 + 테두리)과 증원 진입 지점.
func _draw_deploy_zone() -> void:
	var setup: ScenarioDef.SideSetup = battle.scenario.sides.get(player_side)
	if setup == null:
		return
	var z: Rect2 = setup.deploy_zone
	var corners: Array[Vector2] = [z.position, Vector2(z.end.x, z.position.y), z.end, Vector2(z.position.x, z.end.y)]
	var pts := PackedVector2Array()
	for c: Vector2 in corners:
		# 가장자리를 잘게 나눠 지형 높이를 따라가게 한다.
		pts.append(_screen(c))
	for p: Vector2 in pts:
		if p == Vector2.INF:
			return
	var edges := PackedVector2Array()
	for i: int in corners.size():
		var a: Vector2 = corners[i]
		var b2: Vector2 = corners[(i + 1) % corners.size()]
		for k: int in 16:
			var sp: Vector2 = _screen(a.lerp(b2, k / 16.0))
			if sp != Vector2.INF:
				edges.append(sp)
	if edges.size() >= 3:
		draw_colored_polygon(edges, Color(0.4, 0.7, 1.0, 0.10))
		edges.append(edges[0])
		draw_polyline(edges, Color(0.55, 0.8, 1.0, 0.9), 2.0)
	var label_pos: Vector2 = _screen(Vector2(z.get_center().x, z.end.y if z.get_center().y < battle.terrain.size_m.y * 0.5 else z.position.y))
	if label_pos != Vector2.INF:
		_text(label_pos + Vector2(-40, -8), "배치 구역", 14, Color(0.7, 0.88, 1.0))
	for i: int in setup.entry_points.size():
		var ep: Vector2 = _screen(battle.terrain.clamp_to_map(setup.entry_points[i], 20.0))
		if ep != Vector2.INF:
			draw_circle(ep, 6.0, Color(0.55, 0.8, 1.0, 0.9))
			_text(ep + Vector2(8, -4), "증원 진입 %d" % (i + 1), 12, Color(0.7, 0.88, 1.0))


# --- 부호 ----------------------------------------------------------------

func _draw_unit(u: Unit, hostile: bool, alpha: float) -> void:
	var center: Vector2 = _unit_world_pos(u, alpha)
	var stale: bool = false
	if hostile and fog:
		var known: Dictionary = _known_info(u)
		if known.is_empty():
			return
		center = known["pos"]
		stale = known["stale"]
	var sp: Vector2 = _screen(center)
	if sp == Vector2.INF:
		return
	var selected: bool = selection.has(u)
	_draw_frame(sp, u, hostile, selected, stale)
	_drawn.append({"pos": sp, "unit": u})


func _draw_elements(p: Platoon, hostile: bool, alpha: float) -> void:
	var fill: Color = HOSTILE_FILL if hostile else FRIENDLY_FILL
	var selected: bool = selection.has(p) or (p.parent != null and selection.has(p.parent))
	for e: Element in p.elements:
		if not e.on_map or e.is_mounted():
			continue
		var wp: Vector2 = e.prev_pos.lerp(e.pos, alpha)
		var stale: bool = false
		if hostile and fog and e.is_alive() and not battle.detection.seen_recently(player_side, e.id, battle.tick()):
			# 지금 안 보이면 대대에 보고된 마지막 위치를 흐리게.
			var tf: TaskForce = battle.task_forces[player_side]
			if not tf.known_enemies.has(e.id):
				continue
			var info: Dictionary = tf.known_enemies[e.id]
			wp = info["pos"]
			stale = true
		var sp: Vector2 = _screen(wp)
		if sp == Vector2.INF:
			continue
		var c: Color = fill if e.is_alive() else Color(0.3, 0.3, 0.3, 0.8)
		if stale:
			c.a = STALE_ALPHA
		var h: float = lerp_angle(e.prev_heading, e.heading, alpha)
		if e.is_infantry():
			draw_circle(sp, 5.0, c)
			draw_arc(sp, 5.0, 0.0, TAU, 12, SELECT_COLOR if selected else FRAME_COLOR, 1.5)
		else:
			var tip: Vector2 = _screen(wp + FormationSolver.forward_vector(h) * 12.0)
			var dir: Vector2 = (tip - sp).normalized() if tip != Vector2.INF and tip.distance_to(sp) > 0.01 else Vector2.UP
			if not dir.is_finite() or dir.length() < 0.5:
				dir = Vector2.UP
			var right := Vector2(-dir.y, dir.x)
			var pts := PackedVector2Array([sp + dir * 8.0, sp - dir * 5.0 + right * 5.0, sp - dir * 5.0 - right * 5.0])
			# 삼각형은 삼각분할이 필요 없는 draw_primitive 로(축퇴 다각형 오류 방지).
			draw_primitive(pts, PackedColorArray([c, c, c]), PackedVector2Array())
			pts.append(pts[0])
			draw_polyline(pts, SELECT_COLOR if selected else FRAME_COLOR, 1.5)
		if e == p.leader():
			_text(sp + Vector2(9, -6), p.label(), 12, Color(1, 1, 1, 0.95))
		_drawn.append({"pos": sp, "unit": p})


func _draw_frame(sp: Vector2, u: Unit, hostile: bool, selected: bool, stale: bool) -> void:
	var fill: Color = HOSTILE_FILL if hostile else FRIENDLY_FILL
	if stale:
		fill.a = STALE_ALPHA
	var outline: Color = SELECT_COLOR if selected else FRAME_COLOR
	var width: float = 2.5 if selected else 1.5
	if u.detached:
		width += 1.5
	var r := Rect2(sp - Vector2(FRAME_W, FRAME_H) * 0.5, Vector2(FRAME_W, FRAME_H))
	if hostile:
		var d := PackedVector2Array([sp + Vector2(0, -15), sp + Vector2(17, 0), sp + Vector2(0, 15), sp + Vector2(-17, 0)])
		draw_colored_polygon(d, fill)
		d.append(d[0])
		draw_polyline(d, outline, width)
		r = Rect2(sp - Vector2(10, 7), Vector2(20, 14))
	else:
		draw_rect(r, fill, true)
		draw_rect(r, outline, false, width)
	_draw_glyph(r, u.kind)
	_draw_echelon_mark(Vector2(sp.x, r.position.y - (6.0 if not hostile else 10.0)), u.level)
	# 이름·전투력·통신.
	_text(Vector2(sp.x - 50, r.end.y + 13), u.label(), 12, Color(1, 1, 1, 0.95 if not stale else 0.5), 100,
		HORIZONTAL_ALIGNMENT_CENTER)
	if not hostile:
		var str_ratio: float = clampf(u.strength(), 0.0, 1.0)
		var bar := Rect2(Vector2(sp.x - FRAME_W * 0.5, r.end.y + 16), Vector2(FRAME_W, 3))
		draw_rect(bar, Color(0, 0, 0, 0.6), true)
		draw_rect(Rect2(bar.position, Vector2(FRAME_W * str_ratio, 3)), Color(1.0 - str_ratio, 0.4 + 0.5 * str_ratio, 0.2), true)
		_draw_comms(Vector2(r.end.x + 4, r.position.y), u.comms_state)


## 외곽선 있는 글자(밝은 지형 위에서도 읽히게).
func _text(pos: Vector2, text: String, font_size: int, color: Color, width: float = -1.0,
		align: HorizontalAlignment = HORIZONTAL_ALIGNMENT_LEFT) -> void:
	draw_string_outline(_font, pos, text, align, width, font_size, 4, Color(0, 0, 0, 0.85 * color.a))
	draw_string(_font, pos, text, align, width, font_size, color)


func _draw_glyph(r: Rect2, kind: StringName) -> void:
	var c: Color = FRAME_COLOR
	var cx: float = r.get_center().x
	var cy: float = r.get_center().y
	match kind:
		&"tank":
			_draw_oval(r.get_center(), r.size.x * 0.32, r.size.y * 0.26, c)
		&"mech":
			_draw_oval(r.get_center(), r.size.x * 0.32, r.size.y * 0.26, c)
			draw_line(r.position, r.end, c, 1.2)
			draw_line(Vector2(r.position.x, r.end.y), Vector2(r.end.x, r.position.y), c, 1.2)
		&"inf":
			draw_line(r.position, r.end, c, 1.2)
			draw_line(Vector2(r.position.x, r.end.y), Vector2(r.end.x, r.position.y), c, 1.2)
		&"recon":
			draw_line(Vector2(r.position.x, r.end.y), Vector2(r.end.x, r.position.y), c, 1.4)
		&"mortar":
			draw_circle(Vector2(cx, cy + 3), 2.5, c)
			draw_line(Vector2(cx, cy + 1), Vector2(cx, r.position.y + 2), c, 1.4)
		&"supply":
			draw_line(Vector2(r.position.x, r.end.y - 5), Vector2(r.end.x, r.end.y - 5), c, 1.4)
		&"fo":
			var t := PackedVector2Array([Vector2(cx, r.position.y + 3), Vector2(cx + 6, r.end.y - 3), Vector2(cx - 6, r.end.y - 3), Vector2(cx, r.position.y + 3)])
			draw_polyline(t, c, 1.4)
		&"aa":
			draw_arc(Vector2(cx, r.end.y - 2), 7.0, PI, TAU, 10, c, 1.4)
		&"wheeled":
			# 차륜형 보병: 기계화 부호 + 바퀴 두 개.
			_draw_oval(r.get_center(), r.size.x * 0.32, r.size.y * 0.26, c)
			draw_line(r.position, r.end, c, 1.2)
			draw_line(Vector2(r.position.x, r.end.y), Vector2(r.end.x, r.position.y), c, 1.2)
			draw_circle(Vector2(cx - 5, r.end.y + 3), 1.8, c)
			draw_circle(Vector2(cx + 5, r.end.y + 3), 1.8, c)
		&"at":
			# 대전차: 뒤집힌 V.
			draw_polyline(PackedVector2Array([Vector2(r.position.x + 3, r.end.y - 2), Vector2(cx, r.position.y + 2),
				Vector2(r.end.x - 3, r.end.y - 2)]), c, 1.5)
		&"sniper":
			draw_arc(r.get_center(), 5.5, 0.0, TAU, 14, c, 1.3)
			draw_line(Vector2(cx - 8, cy), Vector2(cx + 8, cy), c, 1.0)
			draw_line(Vector2(cx, cy - 8), Vector2(cx, cy + 8), c, 1.0)
		&"engineer":
			# 공병: ㅠ 모양(교량 부호).
			draw_line(Vector2(cx - 8, cy - 3), Vector2(cx + 8, cy - 3), c, 1.5)
			for x: float in [-8.0, 0.0, 8.0]:
				draw_line(Vector2(cx + x, cy - 3), Vector2(cx + x, cy + 4), c, 1.5)
		&"aviation":
			# 회전익: 나비넥타이.
			draw_colored_polygon(PackedVector2Array([Vector2(cx, cy), Vector2(cx - 9, cy - 5), Vector2(cx - 9, cy + 5)]), c)
			draw_colored_polygon(PackedVector2Array([Vector2(cx, cy), Vector2(cx + 9, cy - 5), Vector2(cx + 9, cy + 5)]), c)
		_:
			var text: String = {&"hq": "HQ", &"cp": "CP", &"ew": "EW"}.get(kind, String(kind).left(2).to_upper())
			draw_string(_font, Vector2(r.position.x, cy + 4), text, HORIZONTAL_ALIGNMENT_CENTER, r.size.x, 10, c)


func _draw_echelon_mark(p: Vector2, level: int) -> void:
	match level:
		Unit.Level.PLATOON:
			for i: int in 3:
				draw_circle(p + Vector2((i - 1) * 5.0, 0), 1.6, Color.WHITE)
		Unit.Level.COMPANY:
			draw_line(p + Vector2(0, -3), p + Vector2(0, 3), Color.WHITE, 2.0)
		Unit.Level.TASK_FORCE:
			draw_line(p + Vector2(-2, -3), p + Vector2(-2, 3), Color.WHITE, 2.0)
			draw_line(p + Vector2(2, -3), p + Vector2(2, 3), Color.WHITE, 2.0)


func _draw_comms(p: Vector2, state: int) -> void:
	var colors: Array[Color] = [Color(0.4, 1.0, 0.4), Color(1.0, 0.8, 0.2), Color(1.0, 0.3, 0.3)]
	var bars: int = 3 - state
	for i: int in 3:
		var h: float = 3.0 + i * 2.5
		var c: Color = colors[state] if i < bars else Color(1, 1, 1, 0.2)
		draw_rect(Rect2(p + Vector2(i * 3.5, 10 - h), Vector2(2.5, h)), c, true)


func _draw_oval(c: Vector2, rx: float, ry: float, col: Color) -> void:
	var pts := PackedVector2Array()
	for i: int in 17:
		var a: float = TAU * i / 16.0
		pts.append(c + Vector2(cos(a) * rx, sin(a) * ry))
	draw_polyline(pts, col, 1.4)


# --- 선택 표시 -----------------------------------------------------------

func _draw_selection_overlay(u: Unit) -> void:
	var g: Guide = u.guide
	# 경로.
	if g.is_moving():
		var pts := PackedVector2Array()
		var a: Vector2 = _screen(g.pos)
		if a != Vector2.INF:
			pts.append(a)
		for i: int in range(g.path_index, g.path.size()):
			var sp: Vector2 = _screen(g.path[i])
			if sp != Vector2.INF:
				pts.append(sp)
		if pts.size() >= 2:
			draw_polyline(pts, PATH_COLOR, 2.0)
			draw_circle(pts[pts.size() - 1], 5.0, PATH_COLOR)
	# 대형 슬롯 고스트(가까이 볼 때만).
	if current_level() == Level.ELEMENT and u is Platoon:
		for e: Element in (u as Platoon).formation_members():
			var sp: Vector2 = _screen(e.move_target)
			if sp != Vector2.INF:
				draw_line(sp + Vector2(-3, -3), sp + Vector2(3, 3), Color(1, 1, 1, 0.5), 1.0)
				draw_line(sp + Vector2(-3, 3), sp + Vector2(3, -3), Color(1, 1, 1, 0.5), 1.0)
	# 방어 방향.
	if u.posture == &"defending" and not is_nan(u.defend_facing):
		var c: Vector2 = _screen(u.guide.pos)
		var t: Vector2 = _screen(u.guide.pos + FormationSolver.forward_vector(u.defend_facing) * 120.0)
		if c != Vector2.INF and t != Vector2.INF:
			draw_line(c, t, Color(0.6, 1.0, 0.6, 0.8), 2.0)
			_draw_arrow_head(c, t, Color(0.6, 1.0, 0.6, 0.8))
	# 전파 중인 명령(M2): 점선 + 남은 시간.
	_draw_pending_orders(u)


func _draw_pending_orders(u: Unit) -> void:
	if battle.order_system == null:
		return
	var pending: Array = battle.order_system.pending_for_unit(u.id)
	for item: Variant in pending:
		var o: Order = item
		if not o.has_pos():
			continue
		var a: Vector2 = _screen(u.guide.pos)
		var b: Vector2 = _screen(o.get_pos())
		if a == Vector2.INF or b == Vector2.INF:
			continue
		_draw_dashed(a, b, PROPAGATING_COLOR)
		var remain: float = maxf(0.0, (o.eta_tick() - battle.world.tick) / float(SimConst.TICK_RATE))
		var text: String = "%s  %.0f초" % [o.type_name(), remain] if o.blocked_since_tick < 0 else "%s  통신 대기" % o.type_name()
		_text(b + Vector2(8, -8), text, 13, PROPAGATING_COLOR)
		draw_arc(b, 6.0, 0.0, TAU, 12, PROPAGATING_COLOR, 2.0)


func _draw_dashed(a: Vector2, b: Vector2, c: Color) -> void:
	var seg_len: float = a.distance_to(b)
	if seg_len < 1.0:
		return
	var dir: Vector2 = (b - a) / seg_len
	var t: float = 0.0
	while t < seg_len:
		draw_line(a + dir * t, a + dir * minf(t + 8.0, seg_len), c, 2.0)
		t += 14.0


func _draw_arrow_head(a: Vector2, b: Vector2, c: Color) -> void:
	var dir: Vector2 = (b - a).normalized()
	if dir == Vector2.ZERO:
		return
	var right := Vector2(-dir.y, dir.x)
	draw_colored_polygon(PackedVector2Array([b, b - dir * 10.0 + right * 5.0, b - dir * 10.0 - right * 5.0]), c)


# --- 좌표 ----------------------------------------------------------------

func _unit_world_pos(u: Unit, alpha: float) -> Vector2:
	var elems: Array[Element] = u.active_elements()
	if elems.is_empty():
		return u.guide.pos
	var sum := Vector2.ZERO
	var n: int = 0
	for e: Element in elems:
		if e.is_mounted():
			continue
		sum += e.prev_pos.lerp(e.pos, alpha)
		n += 1
	return sum / n if n > 0 else u.guide.pos


## 안개 모드에서 적 제대를 어디에 그릴지: 지금 보이는 엘리먼트가 있으면 실제 위치, 없으면 대대에 보고된 위치(오래되면 흐리게).
func _known_info(u: Unit) -> Dictionary:
	var tf: TaskForce = battle.task_forces.get(player_side)
	if tf == null:
		return {}
	var sum := Vector2.ZERO
	var n: int = 0
	var newest: int = -1000000
	var tick: int = battle.world.tick
	var live_sum := Vector2.ZERO
	var live_n: int = 0
	for e: Element in u.all_elements():
		if e.is_alive() and e.on_map and not e.is_mounted() and battle.detection.seen_recently(player_side, e.id, tick):
			live_sum += e.pos
			live_n += 1
	if live_n > 0:
		return {"pos": live_sum / live_n, "stale": false}
	for e: Element in u.all_elements():
		if tf.known_enemies.has(e.id):
			var info: Dictionary = tf.known_enemies[e.id]
			var info_pos: Vector2 = info["pos"]
			var info_tick: int = info["tick"]
			sum += info_pos
			newest = maxi(newest, info_tick)
			n += 1
	if n == 0:
		return {}
	return {"pos": sum / n, "stale": battle.world.tick - newest > 300}


## 지도 좌표 → 화면 좌표. 카메라 뒤면 Vector2.INF.
func _screen(p: Vector2) -> Vector2:
	var cam: Camera3D = rts_camera.camera
	var w: Vector3 = ViewCoords.to_3d(p, battle.terrain.ground_height(battle.terrain.clamp_to_map(p)) + 2.0)
	if cam.is_position_behind(w):
		return Vector2.INF
	return cam.unproject_position(w)
