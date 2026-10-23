class_name TerrainMesh
extends Node3D
## 지형 표시: 높이맵 메시(지형 종류별 정점 색) + 숲 나무·마을 건물 MultiMesh. 시뮬 Terrain 을 읽기만 한다.

const VERTEX_STEP_M: float = 20.0
const COLORS: Array[Color] = [
	Color(0.43, 0.50, 0.31),  # OPEN
	Color(0.58, 0.54, 0.45),  # ROAD
	Color(0.20, 0.32, 0.17),  # FOREST
	Color(0.52, 0.50, 0.47),  # URBAN
	Color(0.22, 0.36, 0.50),  # WATER
]
const TREE_SPACING_M: float = 22.0
const BUILDING_SPACING_M: float = 28.0

var terrain: Terrain


func build(p_terrain: Terrain) -> void:
	terrain = p_terrain
	_build_ground()
	_build_trees()
	_build_buildings()


func _build_ground() -> void:
	var nx: int = ceili(terrain.size_m.x / VERTEX_STEP_M) + 1
	var ny: int = ceili(terrain.size_m.y / VERTEX_STEP_M) + 1
	var verts := PackedVector3Array()
	var colors := PackedColorArray()
	var normals := PackedVector3Array()
	verts.resize(nx * ny)
	colors.resize(nx * ny)
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
			var p := Vector2(minf(i * VERTEX_STEP_M, terrain.size_m.x - 0.5), minf(j * VERTEX_STEP_M, terrain.size_m.y - 0.5))
			var t: int = terrain.type_at(p)
			var c: Color = COLORS[t]
			if t == Terrain.Type.OPEN:
				# 높이에 따라 살짝 밝기 변화(능선이 읽히게).
				var k: float = inverse_lerp(hmin, hmax, verts[j * nx + i].y)
				c = c.lerp(Color(0.62, 0.62, 0.42), k * 0.45)
			colors[j * nx + i] = c
			var hl: float = verts[j * nx + maxi(i - 1, 0)].y
			var hr: float = verts[j * nx + mini(i + 1, nx - 1)].y
			var hd: float = verts[maxi(j - 1, 0) * nx + i].y
			var hu: float = verts[mini(j + 1, ny - 1) * nx + i].y
			# 3D: x = 동, -z = 북. 북쪽(+y 시뮬)이 -z 이므로 z 기울기는 부호가 반대.
			normals[j * nx + i] = Vector3(hl - hr, 2.0 * VERTEX_STEP_M, hu - hd).normalized()
	var indices := PackedInt32Array()
	for j: int in ny - 1:
		for i: int in nx - 1:
			var a: int = j * nx + i
			var b: int = a + 1
			var c: int = a + nx
			var d: int = c + 1
			indices.append_array([a, c, b, b, c, d])
	var arrays: Array = []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = verts
	arrays[Mesh.ARRAY_NORMAL] = normals
	arrays[Mesh.ARRAY_COLOR] = colors
	arrays[Mesh.ARRAY_INDEX] = indices
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	var mat := StandardMaterial3D.new()
	mat.vertex_color_use_as_albedo = true
	mat.vertex_color_is_srgb = true
	mat.roughness = 1.0
	mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	mesh.surface_set_material(0, mat)
	var mi := MeshInstance3D.new()
	mi.name = "Ground"
	mi.mesh = mesh
	add_child(mi)


func _build_trees() -> void:
	var points: Array[Vector3] = _scatter(Terrain.Type.FOREST, TREE_SPACING_M, 7)
	var cone := CylinderMesh.new()
	cone.top_radius = 0.0
	cone.bottom_radius = 5.0
	cone.height = 13.0
	cone.radial_segments = 6
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.13, 0.25, 0.12)
	mat.roughness = 1.0
	cone.material = mat
	_add_multimesh("Trees", cone, points, 6.5, Vector3(1.0, 1.0, 1.0))


func _build_buildings() -> void:
	var points: Array[Vector3] = _scatter(Terrain.Type.URBAN, BUILDING_SPACING_M, 11)
	var box := BoxMesh.new()
	box.size = Vector3(14.0, 8.0, 12.0)
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.62, 0.58, 0.54)
	box.material = mat
	_add_multimesh("Buildings", box, points, 4.0, Vector3.ONE)


## 지형 종류 셀에 결정론적 지터를 준 점들.
func _scatter(type: int, spacing: float, salt: int) -> Array[Vector3]:
	var out: Array[Vector3] = []
	var rng := RandomNumberGenerator.new()
	rng.seed = 1000 + salt
	var nx: int = floori(terrain.size_m.x / spacing)
	var ny: int = floori(terrain.size_m.y / spacing)
	for j: int in ny:
		for i: int in nx:
			var p := Vector2((i + 0.5) * spacing + rng.randf_range(-spacing * 0.35, spacing * 0.35),
				(j + 0.5) * spacing + rng.randf_range(-spacing * 0.35, spacing * 0.35))
			if terrain.type_at(p) == type:
				out.append(ViewCoords.to_3d(p, terrain.ground_height(p)))
	return out


func _add_multimesh(node_name: String, mesh: Mesh, points: Array[Vector3], y_offset: float, scale_v: Vector3) -> void:
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.mesh = mesh
	mm.instance_count = points.size()
	var rng := RandomNumberGenerator.new()
	rng.seed = points.size()
	for i: int in points.size():
		var s: float = rng.randf_range(0.75, 1.25)
		var b := Basis(Vector3.UP, rng.randf() * TAU).scaled(scale_v * s)
		mm.set_instance_transform(i, Transform3D(b, points[i] + Vector3(0.0, y_offset * s, 0.0)))
	var mmi := MultiMeshInstance3D.new()
	mmi.name = node_name
	mmi.multimesh = mm
	mmi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(mmi)
