class_name TerrainMesh
extends Node3D
## 지형 표시. 시뮬 Terrain 을 읽기만 한다(표현 전용 장식은 시뮬에 영향 없음).
## - 지면: 높이맵 메시 + ground.gdshader(지형 종류 마스크 + 절차적 들판·밭 구획·도로·물).
## - 숲: 활엽수(줄기 + 수관)·침엽수 MultiMesh, 개체마다 크기·색 변화.
## - 시가지: 벽 + 박공지붕 집 MultiMesh(도로 방향으로 정렬, 색 변화).
## - 장식(시뮬 밖): 밭 둑 생울타리, 외딴 나무, 길가 농가. 시선·이동을 막지 않는다.

const VERTEX_STEP_M: float = 10.0
## 미니맵 등이 쓰는 지형 종류 대표 색.
const COLORS: Array[Color] = [
	Color(0.43, 0.50, 0.31),  # OPEN
	Color(0.36, 0.36, 0.36),  # ROAD
	Color(0.20, 0.30, 0.16),  # FOREST
	Color(0.52, 0.50, 0.47),  # URBAN
	Color(0.14, 0.27, 0.33),  # WATER
]
const TREE_SPACING_M: float = 13.0
const BUILDING_SPACING_M: float = 24.0
## ground.gdshader 의 밭 격자와 같은 값(생울타리를 밭 둑에 맞춘다).
const FIELD_ANGLE: float = 0.35
const FIELD_SIZE: Vector2 = Vector2(230.0, 150.0)
const HEDGE_STEP_M: float = 6.0
const HEDGE_CHANCE: float = 0.45
const ROAD_CLEAR_M: float = 30.0

const LEAF_COLORS: Array[Color] = [Color(0.14, 0.24, 0.10), Color(0.18, 0.27, 0.11), Color(0.12, 0.21, 0.09),
	Color(0.22, 0.29, 0.12), Color(0.16, 0.23, 0.09)]
const CONIFER_COLORS: Array[Color] = [Color(0.08, 0.16, 0.09), Color(0.10, 0.18, 0.11), Color(0.07, 0.14, 0.08)]
const WALL_COLORS: Array[Color] = [Color(0.86, 0.83, 0.76), Color(0.78, 0.72, 0.62), Color(0.70, 0.55, 0.45),
	Color(0.82, 0.80, 0.78), Color(0.66, 0.62, 0.55)]
const ROOF_COLORS: Array[Color] = [Color(0.48, 0.20, 0.16), Color(0.35, 0.33, 0.32), Color(0.40, 0.27, 0.20),
	Color(0.25, 0.26, 0.29)]

var terrain: Terrain
## 웹(Compatibility)에서는 그림자·개체 수를 줄인다.
var lite: bool = false

var _rng := RandomNumberGenerator.new()
var _road_segments: Array[PackedVector2Array] = []
# 나무·덤불·건물 인스턴스: [위치(Vector3), 회전 y, 크기(Vector3), 색]
var _broadleaf: Array[Array] = []
var _conifer: Array[Array] = []
var _bushes: Array[Array] = []
var _houses: Array[Array] = []


func build(p_terrain: Terrain) -> void:
	terrain = p_terrain
	lite = OS.has_feature("web")
	_rng.seed = 20261023
	for road: MapDef.Feature in terrain.map.roads:
		_road_segments.append(road.points)
	_build_ground()
	_build_rivers()
	_build_roads()
	_scatter_forest()
	_scatter_towns()
	_scatter_hedgerows()
	_scatter_farmsteads()
	_emit_trees()
	_emit_bushes()
	_emit_houses()


# --- 지면 ----------------------------------------------------------------

func _build_ground() -> void:
	var nx: int = ceili(terrain.size_m.x / VERTEX_STEP_M) + 1
	var ny: int = ceili(terrain.size_m.y / VERTEX_STEP_M) + 1
	var verts := PackedVector3Array()
	var normals := PackedVector3Array()
	verts.resize(nx * ny)
	normals.resize(nx * ny)
	var hmin: float = INF
	var hmax: float = -INF
	for j: int in ny:
		for i: int in nx:
			var p := Vector2(minf(i * VERTEX_STEP_M, terrain.size_m.x - 0.5), minf(j * VERTEX_STEP_M, terrain.size_m.y - 0.5))
			var h: float = terrain.ground_height(p)
			hmin = minf(hmin, h)
			hmax = maxf(hmax, h)
			verts[j * nx + i] = ViewCoords.to_3d(p, h)
	for j: int in ny:
		for i: int in nx:
			var hl: float = verts[j * nx + maxi(i - 1, 0)].y
			var hr: float = verts[j * nx + mini(i + 1, nx - 1)].y
			var hd: float = verts[maxi(j - 1, 0) * nx + i].y
			var hu: float = verts[mini(j + 1, ny - 1) * nx + i].y
			# 3D: x = 동, -z = 북. 북쪽(+y 시뮬)이 -z 이므로 z 기울기는 부호가 반대.
			normals[j * nx + i] = Vector3(hl - hr, 2.0 * VERTEX_STEP_M, hu - hd).normalized()
	var indices := PackedInt32Array()
	indices.resize((nx - 1) * (ny - 1) * 6)
	var k: int = 0
	for j: int in ny - 1:
		for i: int in nx - 1:
			var a: int = j * nx + i
			var c: int = a + nx
			indices[k] = a
			indices[k + 1] = c
			indices[k + 2] = a + 1
			indices[k + 3] = a + 1
			indices[k + 4] = c
			indices[k + 5] = c + 1
			k += 6
	var arrays: Array = []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = verts
	arrays[Mesh.ARRAY_NORMAL] = normals
	arrays[Mesh.ARRAY_INDEX] = indices
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	var mat := ShaderMaterial.new()
	mat.shader = load("res://game/presentation/ground.gdshader")
	var mask: ImageTexture = _type_mask_texture()
	mat.set_shader_parameter(&"type_mask", mask)
	mat.set_shader_parameter(&"type_mask_blur", mask)
	mat.set_shader_parameter(&"map_size", terrain.size_m)
	mat.set_shader_parameter(&"height_min", hmin)
	mat.set_shader_parameter(&"height_max", hmax)
	mat.set_shader_parameter(&"field_angle", FIELD_ANGLE)
	mat.set_shader_parameter(&"field_size", FIELD_SIZE)
	mesh.surface_set_material(0, mat)
	var mi := MeshInstance3D.new()
	mi.name = "Ground"
	mi.mesh = mesh
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(mi)


# --- 도로·강 띠(매끈한 곡선, 지형에 붙임) ---------------------------------

const RIBBON_STEP_M: float = 5.0
const ROAD_LIFT_M: float = 0.25
const RIVER_LEVEL_M: float = 2.2


## Chaikin 모서리 깎기 2회 후 RIBBON_STEP_M 간격으로 다시 찍는다.
static func _smooth_polyline(pts: PackedVector2Array, keep_ends: bool = true) -> PackedVector2Array:
	var cur: PackedVector2Array = pts
	for it: int in 2:
		if cur.size() < 3:
			break
		var nxt := PackedVector2Array([cur[0]])
		for i: int in cur.size() - 1:
			var a: Vector2 = cur[i]
			var b: Vector2 = cur[i + 1]
			if i > 0 or not keep_ends:
				nxt.append(a.lerp(b, 0.25))
			if i < cur.size() - 2 or not keep_ends:
				nxt.append(a.lerp(b, 0.75))
		nxt.append(cur[cur.size() - 1])
		cur = nxt
	var out := PackedVector2Array([cur[0]])
	for i: int in cur.size() - 1:
		var a: Vector2 = cur[i]
		var b: Vector2 = cur[i + 1]
		var n: int = maxi(1, ceili(a.distance_to(b) / RIBBON_STEP_M))
		for k: int in range(1, n + 1):
			out.append(a.lerp(b, float(k) / n))
	return out


## 가로 띠 메시. heights[i] 가 NAN 이면 그 자리 지면 높이 + lift 를 쓴다.
func _ribbon(pts: PackedVector2Array, width: float, heights: PackedFloat32Array, lift: float, mat: Material, node_name: String) -> void:
	var verts := PackedVector3Array()
	var uvs := PackedVector2Array()
	var norms := PackedVector3Array()
	var idx := PackedInt32Array()
	var dist: float = 0.0
	var half: float = width * 0.5
	for i: int in pts.size():
		var a: Vector2 = pts[maxi(i - 1, 0)]
		var b: Vector2 = pts[mini(i + 1, pts.size() - 1)]
		var t: Vector2 = (b - a).normalized()
		var side := Vector2(-t.y, t.x)
		if i > 0:
			dist += pts[i].distance_to(pts[i - 1])
		for k: int in 3:
			var u: float = k * 0.5
			var q: Vector2 = pts[i] + side * lerpf(-half, half, u)
			var h: float = heights[i] if not is_nan(heights[i]) else terrain.ground_height(terrain.clamp_to_map(q)) + lift
			verts.append(ViewCoords.to_3d(q, h))
			uvs.append(Vector2(u, dist))
			norms.append(Vector3.UP)
		if i > 0:
			var base: int = (i - 1) * 3
			for k: int in 2:
				var a0: int = base + k
				var b0: int = base + k + 1
				var a1: int = a0 + 3
				var b1: int = b0 + 3
				idx.append_array([a0, b0, a1, b0, b1, a1])
	var arrays: Array = []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = verts
	arrays[Mesh.ARRAY_NORMAL] = norms
	arrays[Mesh.ARRAY_TEX_UV] = uvs
	arrays[Mesh.ARRAY_INDEX] = idx
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	mesh.surface_set_material(0, mat)
	var mi := MeshInstance3D.new()
	mi.name = node_name
	mi.mesh = mesh
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(mi)


func _build_rivers() -> void:
	var mat := ShaderMaterial.new()
	mat.shader = load("res://game/presentation/water.gdshader")
	for river: MapDef.Feature in terrain.map.rivers:
		var pts: PackedVector2Array = _smooth_polyline(river.points)
		var hs := PackedFloat32Array()
		hs.resize(pts.size())
		for i: int in pts.size():
			hs[i] = terrain.ground_height(terrain.clamp_to_map(pts[i])) + RIVER_LEVEL_M
		# 수면은 흐름을 따라 매끈하게.
		var smooth := hs.duplicate()
		for i: int in pts.size():
			var acc: float = 0.0
			var n: int = 0
			for k: int in range(maxi(0, i - 6), mini(pts.size(), i + 7)):
				acc += hs[k]
				n += 1
			smooth[i] = acc / n
		_ribbon(pts, river.width_m * 1.15, smooth, 0.0, mat, "River")


func _build_roads() -> void:
	for road: MapDef.Feature in terrain.map.roads:
		var mat := ShaderMaterial.new()
		mat.shader = load("res://game/presentation/road.gdshader")
		mat.set_shader_parameter(&"center_dash", 1.0 if road.width_m >= 12.0 else 0.0)
		var pts: PackedVector2Array = _smooth_polyline(road.points)
		var hs := PackedFloat32Array()
		hs.resize(pts.size())
		var bridge := PackedByteArray()
		bridge.resize(pts.size())
		for i: int in pts.size():
			var p: Vector2 = terrain.clamp_to_map(pts[i])
			hs[i] = NAN
			bridge[i] = 1 if terrain.is_bridge(p) or terrain.type_at(p) == Terrain.Type.WATER else 0
		# 다리: 연속 구간을 양 끝 높이 중 높은 값으로 평평하게(물 위로).
		var i: int = 0
		while i < pts.size():
			if bridge[i] == 0:
				i += 1
				continue
			var j: int = i
			while j < pts.size() and bridge[j] == 1:
				j += 1
			var h0: float = terrain.ground_height(terrain.clamp_to_map(pts[maxi(i - 1, 0)]))
			var h1: float = terrain.ground_height(terrain.clamp_to_map(pts[mini(j, pts.size() - 1)]))
			var deck: float = maxf(h0, h1) + 0.6
			for k: int in range(i, j):
				hs[k] = deck
			_bridge_rails(pts.slice(i, j), road.width_m * 1.4, deck)
			i = j
		_ribbon(pts, road.width_m * 1.4, hs, ROAD_LIFT_M, mat, "Road")


## 다리 난간: 양옆 낮은 상자.
func _bridge_rails(pts: PackedVector2Array, width: float, deck: float) -> void:
	if pts.size() < 2:
		return
	var a: Vector2 = pts[0]
	var b: Vector2 = pts[pts.size() - 1]
	var dir: Vector2 = (b - a).normalized()
	var side := Vector2(-dir.y, dir.x)
	var length: float = a.distance_to(b) + 8.0
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.55, 0.53, 0.50)
	for sgn: float in [-1.0, 1.0]:
		var box := BoxMesh.new()
		box.size = Vector3(0.6, 1.0, length)
		box.material = mat
		var mi := MeshInstance3D.new()
		mi.mesh = box
		var mid: Vector2 = (a + b) * 0.5 + side * sgn * width * 0.45
		mi.position = ViewCoords.to_3d(mid, deck + 0.5)
		mi.rotation = Vector3(0.0, ViewCoords.heading_to_yaw(FormationSolver.heading_from_vector(dir)), 0.0)
		add_child(mi)
	# 교각 아래 그림자 같은 상판 두께.
	var slab := BoxMesh.new()
	slab.size = Vector3(width * 0.9, 1.2, length)
	slab.material = mat
	var smi := MeshInstance3D.new()
	smi.mesh = slab
	smi.position = ViewCoords.to_3d((a + b) * 0.5, deck - 0.75)
	smi.rotation = Vector3(0.0, ViewCoords.heading_to_yaw(FormationSolver.heading_from_vector(dir)), 0.0)
	add_child(smi)


## 셀별 지형 종류를 RGBA 마스크로(R 도로, G 물, B 숲, A 시가지). 밉맵은 "근처" 판정에 쓴다.
func _type_mask_texture() -> ImageTexture:
	var w: int = terrain.width
	var h: int = terrain.height_cells
	var data := PackedByteArray()
	data.resize(w * h * 4)
	for i: int in w * h:
		var t: int = terrain.types[i]
		data[i * 4] = 255 if t == Terrain.Type.ROAD else 0
		data[i * 4 + 1] = 255 if t == Terrain.Type.WATER else 0
		data[i * 4 + 2] = 255 if t == Terrain.Type.FOREST else 0
		data[i * 4 + 3] = 255 if t == Terrain.Type.URBAN else 0
	var img := Image.create_from_data(w, h, false, Image.FORMAT_RGBA8, data)
	img.generate_mipmaps()
	return ImageTexture.create_from_image(img)


# --- 배치 ----------------------------------------------------------------

func _ground(p: Vector2) -> Vector3:
	return ViewCoords.to_3d(p, terrain.ground_height(p))


func _scatter_forest() -> void:
	var spacing: float = TREE_SPACING_M * (1.4 if lite else 1.0)
	var nx: int = floori(terrain.size_m.x / spacing)
	var ny: int = floori(terrain.size_m.y / spacing)
	for j: int in ny:
		for i: int in nx:
			var p := Vector2((i + 0.5) * spacing + _rng.randf_range(-0.45, 0.45) * spacing,
				(j + 0.5) * spacing + _rng.randf_range(-0.45, 0.45) * spacing)
			if terrain.type_at(p) != Terrain.Type.FOREST:
				continue
			# 숲 가장자리는 조금 성기게.
			if terrain.has_type_near(p, Terrain.Type.OPEN, 15.0) and _rng.randf() < 0.35:
				continue
			_add_tree(p, _rng.randf() < 0.35)


func _add_tree(p: Vector2, conifer: bool) -> void:
	var s: float = _rng.randf_range(0.75, 1.3)
	if conifer:
		var c: Color = CONIFER_COLORS[_rng.randi() % CONIFER_COLORS.size()]
		_conifer.append([_ground(p), _rng.randf() * TAU, Vector3(s, s * _rng.randf_range(0.9, 1.25), s), c])
	else:
		var c2: Color = LEAF_COLORS[_rng.randi() % LEAF_COLORS.size()]
		c2 = c2.lightened(_rng.randf_range(-0.05, 0.08))
		_broadleaf.append([_ground(p), _rng.randf() * TAU, Vector3(s, s * _rng.randf_range(0.85, 1.1), s), c2])


func _scatter_towns() -> void:
	var spacing: float = BUILDING_SPACING_M
	var nx: int = floori(terrain.size_m.x / spacing)
	var ny: int = floori(terrain.size_m.y / spacing)
	for j: int in ny:
		for i: int in nx:
			var p := Vector2((i + 0.5) * spacing + _rng.randf_range(-0.2, 0.2) * spacing,
				(j + 0.5) * spacing + _rng.randf_range(-0.2, 0.2) * spacing)
			if terrain.type_at(p) != Terrain.Type.URBAN or _rng.randf() < 0.18:
				continue
			_add_house(p, _road_angle_near(p), _rng.randf_range(0.8, 1.35))
			# 마당 나무.
			if _rng.randf() < 0.25:
				_add_tree(p + Vector2(_rng.randf_range(-10, 10), _rng.randf_range(-10, 10)), false)


func _add_house(p: Vector2, angle: float, s: float) -> void:
	var size := Vector3(_rng.randf_range(9.0, 14.0), _rng.randf_range(5.5, 8.0), _rng.randf_range(8.0, 11.0)) * s
	var wall: Color = WALL_COLORS[_rng.randi() % WALL_COLORS.size()]
	var roof: Color = ROOF_COLORS[_rng.randi() % ROOF_COLORS.size()]
	var yaw: float = angle + (PI * 0.5 if _rng.randf() < 0.3 else 0.0)
	_houses.append([_ground(p), yaw, size, wall, roof])


## 가장 가까운 도로 구간의 방향(3D yaw). 도로가 멀면 무작위 직각.
func _road_angle_near(p: Vector2) -> float:
	var best_d: float = INF
	var best_dir := Vector2.UP
	for pts: PackedVector2Array in _road_segments:
		for i: int in pts.size() - 1:
			var q: Vector2 = Geometry2D.get_closest_point_to_segment(p, pts[i], pts[i + 1])
			var d: float = q.distance_squared_to(p)
			if d < best_d:
				best_d = d
				best_dir = pts[i + 1] - pts[i]
	if best_d > 300.0 * 300.0:
		return _rng.randi_range(0, 3) * PI * 0.5
	return ViewCoords.heading_to_yaw(FormationSolver.heading_from_vector(best_dir))


## 밭 둑을 따라 생울타리(덤불 줄)와 듬성듬성 큰 나무. 셰이더의 밭 격자와 같은 식으로 칸을 계산한다.
func _scatter_hedgerows() -> void:
	var step: float = HEDGE_STEP_M * (1.6 if lite else 1.0)
	var c: float = cos(FIELD_ANGLE)
	var s: float = sin(FIELD_ANGLE)
	var diag: float = terrain.size_m.length()
	var cols: int = ceili(diag / FIELD_SIZE.x) + 2
	var rows: int = ceili(diag / FIELD_SIZE.y) + 2
	for ci: int in range(-cols, cols):
		var offset: float = fposmod(ci * 0.6180339, 1.0) * FIELD_SIZE.y
		for ri: int in range(-rows, rows):
			# 칸의 회전 좌표계 사각형: x ∈ [ci*W, (ci+1)*W], y ∈ [ri*H - offset, (ri+1)*H - offset]
			var x0: float = ci * FIELD_SIZE.x
			var y0: float = ri * FIELD_SIZE.y - offset
			# 칸마다 두 변(아래·왼쪽)만 맡아 중복을 피한다.
			for edge: int in 2:
				if _edge_hash(ci, ri, edge) > HEDGE_CHANCE:
					continue
				var a := Vector2(x0, y0)
				var b := Vector2(x0 + FIELD_SIZE.x, y0) if edge == 0 else Vector2(x0, y0 + FIELD_SIZE.y)
				var length: float = a.distance_to(b)
				var n: int = floori(length / step)
				for k: int in n:
					var r: Vector2 = a.lerp(b, (k + 0.5) / n)
					# 회전 좌표 → 시뮬 좌표(셰이더의 역회전).
					var p := Vector2(c * r.x - s * r.y, s * r.x + c * r.y)
					if p.x < 5.0 or p.y < 5.0 or p.x > terrain.size_m.x - 5.0 or p.y > terrain.size_m.y - 5.0:
						continue
					if terrain.type_at(p) != Terrain.Type.OPEN:
						continue
					if terrain.has_type_near(p, Terrain.Type.ROAD, ROAD_CLEAR_M) or terrain.has_type_near(p, Terrain.Type.WATER, 20.0):
						continue
					if _rng.randf() < 0.12:
						continue  # 울타리 틈
					if _rng.randf() < 0.07:
						_add_tree(p, false)
					else:
						var bs: float = _rng.randf_range(0.7, 1.3)
						var bc: Color = LEAF_COLORS[_rng.randi() % LEAF_COLORS.size()].darkened(_rng.randf_range(0.0, 0.15))
						_bushes.append([_ground(p), _rng.randf() * TAU, Vector3(bs, bs * _rng.randf_range(0.7, 1.0), bs), bc])


func _edge_hash(ci: int, ri: int, edge: int) -> float:
	return fposmod(sin(ci * 12.9898 + ri * 78.233 + edge * 37.719) * 43758.5453, 1.0)


## 도로변 농가: 도로를 따라 가끔 집 두세 채와 나무.
func _scatter_farmsteads() -> void:
	for pts: PackedVector2Array in _road_segments:
		for i: int in pts.size() - 1:
			var a: Vector2 = pts[i]
			var b: Vector2 = pts[i + 1]
			var length: float = a.distance_to(b)
			var dir: Vector2 = (b - a) / maxf(length, 1.0)
			var side := Vector2(-dir.y, dir.x)
			var t: float = 150.0
			while t < length - 100.0:
				if _rng.randf() < 0.32:
					var sign_v: float = 1.0 if _rng.randf() < 0.5 else -1.0
					var center: Vector2 = a + dir * t + side * sign_v * _rng.randf_range(45.0, 70.0)
					if terrain.type_at(center) == Terrain.Type.OPEN and not terrain.has_type_near(center, Terrain.Type.WATER, 40.0):
						var yaw: float = ViewCoords.heading_to_yaw(FormationSolver.heading_from_vector(dir))
						var count: int = _rng.randi_range(1, 3)
						for k: int in count:
							var off: Vector2 = dir * _rng.randf_range(-18.0, 18.0) + side * sign_v * k * 16.0
							_add_house(center + off, yaw + (PI * 0.5 if k == 2 else 0.0), _rng.randf_range(0.8, 1.1))
						for k: int in _rng.randi_range(2, 5):
							_add_tree(center + Vector2(_rng.randf_range(-35, 35), _rng.randf_range(-35, 35)), _rng.randf() < 0.3)
				t += _rng.randf_range(220.0, 420.0)


# --- 메시 ----------------------------------------------------------------

func _instanced(node_name: String, mesh: Mesh, items: Array[Array], shadows: bool, color_index: int = 3) -> void:
	if items.is_empty():
		return
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.use_colors = true
	mm.mesh = mesh
	mm.instance_count = items.size()
	for i: int in items.size():
		var it: Array = items[i]
		var pos: Vector3 = it[0]
		var yaw: float = it[1]
		var sc: Vector3 = it[2]
		var col: Color = it[color_index]
		mm.set_instance_transform(i, Transform3D(Basis(Vector3.UP, yaw) * Basis.from_scale(sc), pos))
		mm.set_instance_color(i, col)
	var mmi := MultiMeshInstance3D.new()
	mmi.name = node_name
	mmi.multimesh = mm
	mmi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON if shadows and not lite \
		else GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(mmi)


static func _instance_material(roughness: float = 0.95) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.vertex_color_use_as_albedo = true
	m.vertex_color_is_srgb = true
	m.roughness = roughness
	return m


## 메시 조각들을 하나로(위치 offset, 크기).
static func _merge(parts: Array[Array]) -> ArrayMesh:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	for part: Array in parts:
		var m: PrimitiveMesh = part[0]
		var xf: Transform3D = part[1]
		st.append_from(m, 0, xf)
	st.generate_normals()
	return st.commit()


func _emit_trees() -> void:
	# 활엽수: 줄기 + 수관 두 덩이(낮은 폴리곤 구).
	var trunk := CylinderMesh.new()
	trunk.top_radius = 0.35
	trunk.bottom_radius = 0.5
	trunk.height = 4.0
	trunk.radial_segments = 5
	trunk.rings = 1
	var crown := SphereMesh.new()
	crown.radius = 4.2
	crown.height = 7.0
	crown.radial_segments = 7
	crown.rings = 4
	var crown2 := SphereMesh.new()
	crown2.radius = 3.0
	crown2.height = 5.0
	crown2.radial_segments = 6
	crown2.rings = 3
	var broad: ArrayMesh = _merge([[crown, Transform3D(Basis(), Vector3(0, 7.0, 0))],
		[crown2, Transform3D(Basis(), Vector3(1.2, 9.6, 0.6))]])
	broad.surface_set_material(0, _instance_material())
	_instanced("TreesBroadleaf", broad, _broadleaf, true)
	var trunks: Array[Array] = []
	for it: Array in _broadleaf:
		trunks.append([it[0], it[1], it[2], Color(0.30, 0.22, 0.15)])
	var trunk_mesh: ArrayMesh = _merge([[trunk, Transform3D(Basis(), Vector3(0, 2.0, 0))]])
	trunk_mesh.surface_set_material(0, _instance_material())
	if not lite:
		_instanced("TreeTrunks", trunk_mesh, trunks, false)
	# 침엽수: 원뿔 두 단.
	var cone1 := CylinderMesh.new()
	cone1.top_radius = 0.0
	cone1.bottom_radius = 3.6
	cone1.height = 8.0
	cone1.radial_segments = 7
	cone1.rings = 1
	var cone2 := CylinderMesh.new()
	cone2.top_radius = 0.0
	cone2.bottom_radius = 2.6
	cone2.height = 6.5
	cone2.radial_segments = 7
	cone2.rings = 1
	var conifer: ArrayMesh = _merge([[cone1, Transform3D(Basis(), Vector3(0, 5.5, 0))],
		[cone2, Transform3D(Basis(), Vector3(0, 10.0, 0))], [trunk, Transform3D(Basis(), Vector3(0, 1.8, 0))]])
	conifer.surface_set_material(0, _instance_material())
	_instanced("TreesConifer", conifer, _conifer, true)


func _emit_bushes() -> void:
	var bush := SphereMesh.new()
	bush.radius = 2.2
	bush.height = 3.2
	bush.radial_segments = 6
	bush.rings = 3
	var m: ArrayMesh = _merge([[bush, Transform3D(Basis(), Vector3(0, 1.2, 0))]])
	m.surface_set_material(0, _instance_material())
	_instanced("Hedgerows", m, _bushes, false)


func _emit_houses() -> void:
	# 벽(상자)과 지붕(삼각 기둥)을 따로 그려 색을 다르게 준다. 크기는 인스턴스 스케일.
	var body := BoxMesh.new()
	body.size = Vector3(1.0, 1.0, 1.0)
	var walls: ArrayMesh = _merge([[body, Transform3D(Basis(), Vector3(0, 0.5, 0))]])
	walls.surface_set_material(0, _instance_material(0.9))
	var prism := PrismMesh.new()
	prism.size = Vector3(1.08, 0.55, 1.06)
	prism.left_to_right = 0.5
	var roof: ArrayMesh = _merge([[prism, Transform3D(Basis(), Vector3(0, 1.0 + 0.275, 0))]])
	roof.surface_set_material(0, _instance_material(0.8))
	var wall_items: Array[Array] = []
	var roof_items: Array[Array] = []
	for it: Array in _houses:
		var size: Vector3 = it[2]
		wall_items.append([it[0], it[1], size, it[3]])
		roof_items.append([it[0], it[1], size, it[4]])
	_instanced("HouseWalls", walls, wall_items, true)
	_instanced("HouseRoofs", roof, roof_items, true)
