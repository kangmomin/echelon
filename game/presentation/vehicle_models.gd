class_name VehicleModels
extends RefCounted
## 병과별 저폴리 모델(상자·원통 조합). 원점 = 바닥 중심, 앞 = -Z. 멀리서도 보이게 실제보다 약 1.3배.
## 표면 0 = 차체(위장색), 표면 1 = 피아 식별판(진영 색). 판독은 부호(SymbolLayer)가 주로 맡는다.

const SCALE: float = 1.3

static var _cache: Dictionary[StringName, ArrayMesh] = {}


static func mesh_for(element_class: StringName) -> ArrayMesh:
	if _cache.has(element_class):
		return _cache[element_class]
	var body: Array[Array] = []
	var marker: Array[Array] = []
	match element_class:
		&"tank":
			_box(body, Vector3(3.6, 1.2, 7.8), Vector3(0, 0.95, 0))          # 차체
			_box(body, Vector3(3.9, 0.9, 7.2), Vector3(0, 0.45, 0))          # 궤도
			_box(body, Vector3(2.6, 0.9, 3.4), Vector3(0, 1.95, 0.4))        # 포탑
			_cyl(body, 0.13, 5.2, Vector3(0, 2.0, -3.6), Vector3(PI * 0.5, 0, 0))  # 포신
			_box(marker, Vector3(1.4, 0.06, 1.4), Vector3(0, 2.43, 0.8))
		&"ifv", &"recon", &"fo":
			_box(body, Vector3(3.3, 1.5, 6.6), Vector3(0, 1.15, 0))
			_box(body, Vector3(3.5, 0.8, 6.0), Vector3(0, 0.4, 0))
			_box(body, Vector3(1.8, 0.7, 2.0), Vector3(0, 2.25, -0.6))
			_cyl(body, 0.07, 2.8, Vector3(0, 2.3, -2.6), Vector3(PI * 0.5, 0, 0))
			if element_class == &"fo":
				_cyl(body, 0.08, 3.5, Vector3(0.9, 3.5, 1.8), Vector3.ZERO)  # 관측 마스트
				_box(body, Vector3(0.8, 0.5, 0.5), Vector3(0.9, 5.2, 1.8))
			_box(marker, Vector3(1.2, 0.06, 1.2), Vector3(0, 1.93, 1.8))
		&"apc", &"cp":
			_box(body, Vector3(3.2, 1.9, 6.6), Vector3(0, 1.35, 0))
			_box(body, Vector3(3.4, 0.7, 6.0), Vector3(0, 0.35, 0))
			if element_class == &"cp":
				_cyl(body, 0.05, 5.0, Vector3(1.2, 4.7, 2.5), Vector3.ZERO)
				_cyl(body, 0.05, 4.0, Vector3(-1.2, 4.2, 2.5), Vector3.ZERO)
				_box(body, Vector3(2.6, 1.6, 2.4), Vector3(0, 1.1, 4.4))      # 천막
			_box(marker, Vector3(1.4, 0.06, 1.4), Vector3(0, 2.33, 0))
		&"mortar":
			_box(body, Vector3(3.2, 1.6, 6.4), Vector3(0, 1.2, 0))
			_box(body, Vector3(3.4, 0.7, 6.0), Vector3(0, 0.35, 0))
			_cyl(body, 0.2, 2.2, Vector3(0, 2.6, 1.5), Vector3(-0.5, 0, 0))   # 박격포 포신(뒤쪽, 위로)
			_box(marker, Vector3(1.2, 0.06, 1.2), Vector3(0, 2.03, -1.6))
		&"truck", &"ew":
			_box(body, Vector3(2.6, 1.8, 2.2), Vector3(0, 1.9, -2.9))        # 운전실
			_box(body, Vector3(2.6, 0.5, 6.4), Vector3(0, 1.0, 0.3))         # 차대
			if element_class == &"ew":
				_box(body, Vector3(2.6, 2.4, 4.6), Vector3(0, 2.45, 1.1))     # 장비함
				_cyl(body, 0.06, 6.0, Vector3(0, 6.6, 2.6), Vector3.ZERO)
				_box(body, Vector3(1.6, 1.6, 0.1), Vector3(0, 7.2, 2.6))      # 안테나 판
			else:
				_box(body, Vector3(2.6, 1.6, 4.4), Vector3(0, 2.05, 1.2))     # 적재함
			for z: float in [-2.6, 0.6, 2.4]:
				for x: float in [-1.15, 1.15]:
					_cyl(body, 0.55, 0.45, Vector3(x, 0.55, z), Vector3(0, 0, PI * 0.5))
			_box(marker, Vector3(1.2, 0.06, 1.2), Vector3(0, 2.83, -2.9))
		&"aa":
			_box(body, Vector3(3.0, 1.6, 6.4), Vector3(0, 1.2, 0))
			_box(body, Vector3(3.2, 0.7, 6.0), Vector3(0, 0.35, 0))
			_box(body, Vector3(2.2, 1.6, 2.2), Vector3(0, 2.8, 0.6))         # 발사 포탑
			_box(body, Vector3(0.6, 0.7, 2.0), Vector3(1.5, 3.0, 0.4))
			_box(body, Vector3(0.6, 0.7, 2.0), Vector3(-1.5, 3.0, 0.4))
			_cyl(body, 0.05, 1.6, Vector3(0, 4.4, 0.8), Vector3.ZERO)
			_box(body, Vector3(1.6, 0.9, 0.1), Vector3(0, 5.2, 0.8))          # 레이더
			_box(marker, Vector3(1.0, 0.06, 1.0), Vector3(0, 2.03, -2.0))
		&"inf_squad":
			# 병사 9명(크게 그린다). 횡대로 흩어진 모습.
			var offsets: Array[Vector2] = [Vector2(0, 0), Vector2(-3.5, 1.5), Vector2(3.5, 1.5), Vector2(-7, 3),
				Vector2(7, 3), Vector2(-1.8, 4), Vector2(1.8, 4.2), Vector2(-5, -1), Vector2(5, -1)]
			for o: Vector2 in offsets:
				_cyl(body, 0.45, 1.6, Vector3(o.x, 0.8, o.y), Vector3.ZERO)
				_sphere(body, 0.38, Vector3(o.x, 1.85, o.y))
			_box(marker, Vector3(0.9, 0.06, 0.9), Vector3(0, 2.3, 0))
		_:
			_box(body, Vector3(3.2, 1.8, 6.4), Vector3(0, 0.9, 0))
			_box(marker, Vector3(1.2, 0.06, 1.2), Vector3(0, 1.83, 0))
	var mesh := ArrayMesh.new()
	_commit(mesh, body)
	_commit(mesh, marker)
	_cache[element_class] = mesh
	return mesh


static func _box(parts: Array[Array], size: Vector3, pos: Vector3) -> void:
	var m := BoxMesh.new()
	m.size = size * SCALE
	parts.append([m, Transform3D(Basis(), pos * SCALE)])


static func _cyl(parts: Array[Array], radius: float, length: float, pos: Vector3, rot: Vector3) -> void:
	var m := CylinderMesh.new()
	m.top_radius = radius * SCALE
	m.bottom_radius = radius * SCALE
	m.height = length * SCALE
	m.radial_segments = 8
	m.rings = 1
	parts.append([m, Transform3D(Basis.from_euler(rot), pos * SCALE)])


static func _sphere(parts: Array[Array], radius: float, pos: Vector3) -> void:
	var m := SphereMesh.new()
	m.radius = radius * SCALE
	m.height = radius * 2.0 * SCALE
	m.radial_segments = 8
	m.rings = 4
	parts.append([m, Transform3D(Basis(), pos * SCALE)])


static func _commit(mesh: ArrayMesh, parts: Array[Array]) -> void:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	for part: Array in parts:
		var pm: PrimitiveMesh = part[0]
		var xf: Transform3D = part[1]
		st.append_from(pm, 0, xf)
	st.commit(mesh)
