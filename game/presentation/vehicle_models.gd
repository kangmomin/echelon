class_name VehicleModels
extends RefCounted
## 장비별 저폴리 모델(상자·쐐기·원통·구 조합). 원점 = 바닥 중심, 앞 = -Z. 멀리서도 보이게 실제보다 약 1.3배.
## 차체와 포탑을 나눠 포탑이 표적을 향해 돌 수 있게 한다. 표면 역할: &"camo"(위장색), &"dark"(궤도·바퀴·창),
## &"marker"(피아 식별판). 판독은 부호(SymbolLayer)가 주로 맡는다.

const SCALE: float = 1.3


class Model:
	extends RefCounted
	var hull: ArrayMesh
	var hull_roles: Array[StringName] = []
	var turret: ArrayMesh = null
	var turret_roles: Array[StringName] = []
	## 포탑 회전축 위치(모델 좌표, 이미 SCALE 적용).
	var turret_pivot: Vector3 = Vector3.ZERO


## 조립 중인 메시: 역할별 조각 목록.
class Parts:
	extends RefCounted
	var by_role: Dictionary[StringName, Array] = {}

	func add(role: StringName, mesh: PrimitiveMesh, xf: Transform3D) -> void:
		if not by_role.has(role):
			by_role[role] = []
		by_role[role].append([mesh, xf])


static var _cache: Dictionary[StringName, Model] = {}


## 장비 id(우선) 또는 병과로 모델을 고른다.
static func model_for(def: ElementDef) -> Model:
	var key: StringName = def.id
	if _cache.has(key):
		return _cache[key]
	var hull := Parts.new()
	var turret := Parts.new()
	var pivot := Vector3.ZERO
	var id: String = String(def.id)
	if id.begins_with("us_m1a2"):
		pivot = _abrams(hull, turret, id.ends_with("tusk"))
	elif id.begins_with("ru_t90") or id.begins_with("ru_t72"):
		pivot = _t90(hull, turret, id.begins_with("ru_t72"))
	elif id == "us_m2a3" or id == "us_m3a3" or id == "us_m7_fist":
		pivot = _bradley(hull, turret, id == "us_m7_fist")
	elif id == "ru_bmp3" or id == "ru_brm3k":
		pivot = _bmp3(hull, turret, id == "ru_brm3k")
	elif id == "ru_bmp2m":
		pivot = _bmp2(hull, turret)
	elif id == "ru_fo_team" or id == "ru_bmp1ksh_cp":
		pivot = _bmp1_variant(hull, turret, id == "ru_bmp1ksh_cp")
	elif id == "ru_2s9":
		pivot = _nona(hull, turret)
	elif id == "ru_tor_m2":
		pivot = _tor(hull, turret)
	elif id == "us_avenger" or id == "us_jtac":
		pivot = _humvee(hull, turret, id == "us_avenger")
	elif def.element_class == &"truck" or def.element_class == &"ew" or id == "ru_fo_team":
		_truck(hull, id)
	elif def.element_class == &"inf_squad":
		_squad(hull)
	elif def.element_class == &"mortar" or def.element_class == &"cp":
		_m113(hull, def.element_class)
	else:
		_m113(hull, &"apc")
	var m := Model.new()
	m.hull = _commit(hull, m.hull_roles)
	if not turret.by_role.is_empty():
		m.turret = _commit(turret, m.turret_roles)
		m.turret_pivot = pivot * SCALE
	_cache[key] = m
	return m


# --- 부품 ----------------------------------------------------------------

static func _xf(pos: Vector3, rot: Vector3 = Vector3.ZERO) -> Transform3D:
	return Transform3D(Basis.from_euler(rot), pos * SCALE)


static func _box(p: Parts, role: StringName, size: Vector3, pos: Vector3, rot: Vector3 = Vector3.ZERO) -> void:
	var m := BoxMesh.new()
	m.size = size * SCALE
	p.add(role, m, _xf(pos, rot))


## 쐐기(경사 장갑): 높이 h, 길이 l 의 삼각 단면을 폭 w 로 민 것. front = true 면 앞쪽(-Z)이 낮다.
static func _wedge(p: Parts, role: StringName, w: float, h: float, l: float, pos: Vector3, front: bool = true) -> void:
	var m := PrismMesh.new()
	m.size = Vector3(l, h, w) * SCALE
	m.left_to_right = 0.0 if front else 1.0
	# PrismMesh 의 삼각 단면은 XY 평면 → Y 축으로 90도 돌려 길이 방향을 Z 로(+X → -Z). 꼭짓점이 -X(=뒤 +Z)면 앞이 낮다.
	p.add(role, m, _xf(pos, Vector3(0, PI * 0.5, 0)))


static func _cyl(p: Parts, role: StringName, radius: float, length: float, pos: Vector3, rot: Vector3, segments: int = 8) -> void:
	var m := CylinderMesh.new()
	m.top_radius = radius * SCALE
	m.bottom_radius = radius * SCALE
	m.height = length * SCALE
	m.radial_segments = segments
	m.rings = 1
	p.add(role, m, _xf(pos, rot))


static func _sphere(p: Parts, role: StringName, radius: Vector3, pos: Vector3) -> void:
	var m := SphereMesh.new()
	m.radius = 1.0
	m.height = 2.0
	m.radial_segments = 10
	m.rings = 5
	p.add(role, m, Transform3D(Basis.from_scale(radius * SCALE), pos * SCALE))


## 포신: 앞(-Z)으로 뻗는 원통.
static func _barrel(p: Parts, radius: float, length: float, pos: Vector3) -> void:
	_cyl(p, &"camo", radius, length, pos + Vector3(0, 0, -length * 0.5), Vector3(PI * 0.5, 0, 0), 8)


## 궤도: 양쪽 궤도 + 보기륜 + 사이드 스커트.
static func _tracks(p: Parts, width: float, length: float, wheels: int, skirt: bool) -> void:
	for sgn: float in [-1.0, 1.0]:
		var x: float = sgn * (width * 0.5 - 0.35)
		_box(p, &"dark", Vector3(0.7, 0.8, length), Vector3(x, 0.45, 0))
		for i: int in wheels:
			var z: float = lerpf(-length * 0.42, length * 0.42, float(i) / maxf(wheels - 1, 1))
			_cyl(p, &"dark", 0.38, 0.5, Vector3(x + sgn * 0.12, 0.42, z), Vector3(0, 0, PI * 0.5), 8)
		if skirt:
			_box(p, &"camo", Vector3(0.12, 0.55, length * 0.85), Vector3(x + sgn * 0.42, 0.75, -length * 0.03))


static func _wheels(p: Parts, width: float, zs: Array, radius: float) -> void:
	for z: float in zs:
		for sgn: float in [-1.0, 1.0]:
			_cyl(p, &"dark", radius, 0.45, Vector3(sgn * (width * 0.5 - 0.1), radius, z), Vector3(0, 0, PI * 0.5), 10)


# --- 장비 ----------------------------------------------------------------

## M1A2 SEPv3: 긴 차체, 낮고 각진 넓은 포탑, 바구니. TUSK 는 반응장갑 스커트.
static func _abrams(h: Parts, t: Parts, tusk: bool) -> Vector3:
	_tracks(h, 3.7, 7.6, 7, true)
	_box(h, &"camo", Vector3(3.5, 0.75, 6.6), Vector3(0, 1.15, 0.35))
	_wedge(h, &"camo", 3.5, 0.75, 1.3, Vector3(0, 1.15, -3.6))      # 경사 전면
	_box(h, &"camo", Vector3(3.3, 0.35, 1.2), Vector3(0, 1.65, 2.9))  # 엔진 덮개
	if tusk:
		for sgn: float in [-1.0, 1.0]:
			_box(h, &"camo", Vector3(0.35, 0.7, 4.0), Vector3(sgn * 2.05, 1.0, -0.6))
	# 포탑(피벗 기준 좌표)
	var pv := Vector3(0, 1.55, 0.3)
	_box(t, &"camo", Vector3(3.3, 0.85, 3.2), Vector3(0, 0.42, 0.2))
	_wedge(t, &"camo", 3.0, 0.85, 1.1, Vector3(0, 0.42, -1.95))
	_box(t, &"dark", Vector3(2.9, 0.45, 0.9), Vector3(0, 0.45, 2.25))  # 바구니
	_box(t, &"camo", Vector3(0.5, 0.5, 0.6), Vector3(0.75, 1.1, 0.4))  # 조종수 관측장치
	_box(t, &"camo", Vector3(0.45, 0.4, 0.45), Vector3(-0.8, 1.05, 0.6))
	_box(t, &"camo", Vector3(0.7, 0.7, 0.5), Vector3(0, 0.45, -2.3))   # 포방패
	_barrel(t, 0.12, 5.4, Vector3(0, 0.45, -2.5))
	_box(t, &"marker", Vector3(1.2, 0.05, 1.0), Vector3(0, 0.88, 0.8))
	return pv


## T-90M / T-72B3: 짧은 차체, 둥근 포탑 돔 + 반응장갑 쐐기.
static func _t90(h: Parts, t: Parts, t72: bool) -> Vector3:
	_tracks(h, 3.5, 6.8, 6, true)
	_box(h, &"camo", Vector3(3.3, 0.65, 5.8), Vector3(0, 1.1, 0.4))
	_wedge(h, &"camo", 3.3, 0.65, 1.3, Vector3(0, 1.1, -3.1))
	_box(h, &"dark", Vector3(0.6, 0.6, 1.6), Vector3(-1.2, 1.7, 2.7))  # 외부 연료통
	var pv := Vector3(0, 1.45, 0.0)
	_sphere(t, &"camo", Vector3(1.55, 0.6, 1.65), Vector3(0, 0.25, 0.1))
	if not t72:
		_box(t, &"camo", Vector3(2.2, 0.55, 1.2), Vector3(0, 0.35, 1.6))  # 후면 바구니(T-90M)
	for sgn: float in [-1.0, 1.0]:
		_wedge(t, &"camo", 0.9, 0.45, 1.0, Vector3(sgn * 0.75, 0.35, -1.35))  # 반응장갑 "눈썹"
	_box(t, &"camo", Vector3(0.45, 0.45, 0.45), Vector3(0.6, 0.85, 0.2))
	_barrel(t, 0.12, 5.6, Vector3(0, 0.4, -1.4))
	_box(t, &"marker", Vector3(1.0, 0.05, 0.9), Vector3(-0.3, 0.86, 0.4))
	return pv


## M2/M3 브래들리, M7 BFIST: 높은 상자 차체, 왼쪽으로 치우친 포탑 + TOW 발사함.
static func _bradley(h: Parts, t: Parts, fist: bool) -> Vector3:
	_tracks(h, 3.4, 6.2, 6, true)
	_box(h, &"camo", Vector3(3.2, 1.35, 5.2), Vector3(0, 1.45, 0.5))
	_wedge(h, &"camo", 3.2, 1.35, 1.2, Vector3(0, 1.45, -2.7))
	_box(h, &"dark", Vector3(2.4, 0.9, 0.15), Vector3(0, 1.4, 3.15))   # 뒷문
	var pv := Vector3(0.35, 2.12, -0.2)
	_box(t, &"camo", Vector3(1.9, 0.7, 2.2), Vector3(0, 0.35, 0))
	_wedge(t, &"camo", 1.9, 0.7, 0.6, Vector3(0, 0.35, -1.4))
	if fist:
		_box(t, &"camo", Vector3(0.8, 0.6, 1.0), Vector3(0.0, 0.95, 0.3))   # 표적 지시 장치
		_box(t, &"dark", Vector3(0.6, 0.35, 0.1), Vector3(0.0, 0.95, -0.25))
	else:
		_barrel(t, 0.07, 2.6, Vector3(0.0, 0.45, -1.5))
		_box(t, &"camo", Vector3(0.55, 0.5, 1.4), Vector3(-1.3, 0.55, 0.1))  # TOW 발사함
	_box(t, &"marker", Vector3(0.9, 0.05, 0.9), Vector3(0.2, 0.73, 0.5))
	return pv


## BMP-3 / BRM-3K: 낮고 긴 차체, 중앙 포탑(100 mm + 30 mm).
static func _bmp3(h: Parts, t: Parts, recon: bool) -> Vector3:
	_tracks(h, 3.3, 6.6, 6, false)
	_box(h, &"camo", Vector3(3.2, 0.9, 5.0), Vector3(0, 1.15, 0.7))
	_wedge(h, &"camo", 3.2, 0.9, 1.8, Vector3(0, 1.15, -2.7))
	var pv := Vector3(0, 1.6, 0.2)
	_box(t, &"camo", Vector3(1.9, 0.55, 1.9), Vector3(0, 0.28, 0))
	_wedge(t, &"camo", 1.9, 0.55, 0.5, Vector3(0, 0.28, -1.2))
	_barrel(t, 0.11, 2.4, Vector3(0, 0.35, -1.3))
	_barrel(t, 0.05, 2.8, Vector3(0.35, 0.3, -1.3))
	if recon:
		_box(t, &"camo", Vector3(0.9, 0.6, 0.15), Vector3(0, 1.0, 0.6))  # 레이더판
	_box(t, &"marker", Vector3(0.8, 0.05, 0.8), Vector3(0, 0.58, 0.4))
	return pv


## BMP-2M: 각진 차체, 긴 30 mm 포.
static func _bmp2(h: Parts, t: Parts) -> Vector3:
	_tracks(h, 3.1, 6.4, 6, false)
	_box(h, &"camo", Vector3(3.0, 0.8, 4.6), Vector3(0, 1.1, 0.8))
	_wedge(h, &"camo", 3.0, 0.8, 1.9, Vector3(0, 1.1, -2.5))
	var pv := Vector3(0, 1.5, 0.0)
	_sphere(t, &"camo", Vector3(1.0, 0.4, 1.0), Vector3(0, 0.15, 0))
	_barrel(t, 0.05, 3.2, Vector3(0, 0.35, -0.8))
	_box(t, &"camo", Vector3(0.3, 0.3, 1.2), Vector3(-0.8, 0.45, 0.0))  # 대전차 미사일 발사관
	_box(t, &"marker", Vector3(0.7, 0.05, 0.7), Vector3(0.2, 0.56, 0.3))
	return pv


## BMP-1 파생(PRP-4M 관측 차량, BMP-1KSh 지휘 차량): BMP 차체 + 관측 포탑 또는 안테나 숲.
static func _bmp1_variant(h: Parts, t: Parts, cp: bool) -> Vector3:
	_tracks(h, 3.0, 6.4, 6, false)
	_box(h, &"camo", Vector3(2.9, 0.8, 4.4), Vector3(0, 1.1, 0.9))
	_wedge(h, &"camo", 2.9, 0.8, 1.9, Vector3(0, 1.1, -2.3))
	var pv := Vector3(0, 1.5, 0.1)
	if cp:
		_box(h, &"camo", Vector3(1.6, 0.45, 1.6), Vector3(0, 1.72, 1.6))
		for x: float in [-0.9, 0.9]:
			_cyl(h, &"dark", 0.05, 4.5, Vector3(x, 3.7, 2.4), Vector3.ZERO)
		_cyl(t, &"dark", 0.04, 9.0, Vector3(0, 4.5, 0), Vector3.ZERO)  # 마스트
		_box(t, &"marker", Vector3(0.8, 0.05, 0.8), Vector3(0, 0.02, 0))
	else:
		_sphere(t, &"camo", Vector3(0.95, 0.42, 0.95), Vector3(0, 0.15, 0))
		_box(t, &"dark", Vector3(0.7, 0.35, 0.15), Vector3(0, 0.3, -0.85))   # 관측 장치 창
		_box(t, &"dark", Vector3(0.9, 0.7, 0.1), Vector3(0, 0.95, 0.4))      # 표적 탐지 레이더
		_box(t, &"marker", Vector3(0.6, 0.05, 0.6), Vector3(0.35, 0.58, 0.2))
	return pv


## 2S9 Nona: 낮은 공수 차체 + 짧은 120 mm 포탑.
static func _nona(h: Parts, t: Parts) -> Vector3:
	_tracks(h, 2.8, 5.8, 5, false)
	_box(h, &"camo", Vector3(2.7, 0.7, 4.8), Vector3(0, 1.0, 0.3))
	_wedge(h, &"camo", 2.7, 0.7, 1.2, Vector3(0, 1.0, -2.5))
	var pv := Vector3(0, 1.35, 0.3)
	_box(t, &"camo", Vector3(1.7, 0.6, 1.8), Vector3(0, 0.3, 0))
	_wedge(t, &"camo", 1.7, 0.6, 0.6, Vector3(0, 0.3, -1.2))
	_cyl(t, &"camo", 0.13, 1.8, Vector3(0, 0.75, -1.2), Vector3(PI * 0.42, 0, 0))  # 들린 포신
	_box(t, &"marker", Vector3(0.8, 0.05, 0.8), Vector3(0, 0.62, 0.4))
	return pv


## Tor-M2: 궤도 차체 + 큰 상자 포탑(발사기 + 레이더).
static func _tor(h: Parts, t: Parts) -> Vector3:
	_tracks(h, 3.3, 6.8, 6, false)
	_box(h, &"camo", Vector3(3.2, 1.1, 6.0), Vector3(0, 1.25, 0.2))
	_wedge(h, &"camo", 3.2, 1.1, 1.0, Vector3(0, 1.25, -3.3))
	var pv := Vector3(0, 1.8, 0.4)
	_box(t, &"camo", Vector3(2.6, 1.9, 2.6), Vector3(0, 0.95, 0))
	_box(t, &"dark", Vector3(2.0, 1.2, 0.12), Vector3(0, 2.4, -0.9))   # 탐지 레이더
	_box(t, &"dark", Vector3(1.2, 0.9, 0.12), Vector3(0, 1.3, -1.35))  # 추적 레이더
	_box(t, &"marker", Vector3(1.0, 0.05, 1.0), Vector3(0, 1.92, 0.6))
	return pv


## HMMWV(JTAC) / 어벤저(스팅어 포드 포탑).
static func _humvee(h: Parts, t: Parts, avenger: bool) -> Vector3:
	_wheels(h, 2.3, [-1.6, 1.5], 0.48)
	_box(h, &"camo", Vector3(2.2, 0.75, 4.6), Vector3(0, 1.05, 0))
	_box(h, &"camo", Vector3(2.1, 0.7, 1.6), Vector3(0, 1.75, -0.2))   # 승무원 칸
	_box(h, &"dark", Vector3(1.9, 0.5, 0.08), Vector3(0, 1.85, -1.0))   # 앞유리
	var pv := Vector3(0, 2.1, 1.3)
	if avenger:
		_box(t, &"camo", Vector3(1.0, 0.8, 1.0), Vector3(0, 0.4, 0))
		for sgn: float in [-1.0, 1.0]:
			_box(t, &"camo", Vector3(0.5, 0.6, 1.3), Vector3(sgn * 0.85, 0.75, -0.1))
		_box(t, &"marker", Vector3(0.6, 0.05, 0.6), Vector3(0, 0.82, 0.2))
	else:
		_cyl(t, &"camo", 0.05, 2.5, Vector3(0, 1.2, 0), Vector3.ZERO)       # 안테나
		_box(t, &"marker", Vector3(0.7, 0.05, 0.7), Vector3(0, 0.05, 0))
	return pv


## 트럭(HEMTT, KamAZ, 재머, 러 관측반 차량).
static func _truck(h: Parts, id: String) -> void:
	var eight: bool = id == "us_hemtt"
	var zs: Array = [-2.8, -1.3, 1.6, 3.0] if eight else [-2.6, 1.4, 2.8]
	_wheels(h, 2.5, zs, 0.6)
	_box(h, &"camo", Vector3(2.5, 1.7, 2.0), Vector3(0, 2.05, -2.9))      # 운전실
	_box(h, &"dark", Vector3(2.2, 0.6, 0.08), Vector3(0, 2.4, -3.92))     # 앞유리
	_box(h, &"dark", Vector3(2.4, 0.35, 6.6), Vector3(0, 1.1, 0.2))       # 차대
	if id == "ru_r330zh":
		_box(h, &"camo", Vector3(2.5, 2.2, 4.4), Vector3(0, 2.4, 1.2))
		_cyl(h, &"dark", 0.08, 6.0, Vector3(0, 6.5, 2.4), Vector3.ZERO)
		_box(h, &"dark", Vector3(1.8, 1.4, 0.1), Vector3(0, 8.6, 2.4))
		_box(h, &"marker", Vector3(1.0, 0.05, 1.0), Vector3(0, 3.53, 0.4))
	elif id == "ru_fo_team":
		_box(h, &"camo", Vector3(2.5, 1.6, 4.4), Vector3(0, 2.1, 1.2))
		_cyl(h, &"dark", 0.08, 4.0, Vector3(0.8, 4.9, 2.6), Vector3.ZERO)
		_box(h, &"dark", Vector3(0.8, 0.5, 0.5), Vector3(0.8, 7.0, 2.6))
		_box(h, &"marker", Vector3(1.0, 0.05, 1.0), Vector3(0, 2.93, 0.4))
	else:
		_box(h, &"camo", Vector3(2.5, 1.5, 4.6), Vector3(0, 2.0, 1.3))      # 적재함 덮개
		_box(h, &"marker", Vector3(1.0, 0.05, 1.0), Vector3(0, 2.93, -2.9))


## M113 계열(지휘소 M1068, 박격포 M1064) / 일반 장갑차.
static func _m113(h: Parts, cls: StringName) -> void:
	_tracks(h, 3.0, 5.2, 5, false)
	_box(h, &"camo", Vector3(2.8, 1.5, 4.4), Vector3(0, 1.45, 0.3))
	_wedge(h, &"camo", 2.8, 1.5, 0.8, Vector3(0, 1.45, -2.3))
	if cls == &"cp":
		_box(h, &"camo", Vector3(2.4, 1.7, 2.2), Vector3(0, 1.25, 3.6))   # 확장 천막
		for x: float in [1.0, -1.0]:
			_cyl(h, &"dark", 0.05, 5.0, Vector3(x, 4.7, 1.8), Vector3.ZERO)
	elif cls == &"mortar":
		_cyl(h, &"camo", 0.2, 2.0, Vector3(0, 2.5, 1.2), Vector3(-0.55, 0, 0))   # 120 mm 박격포
	_box(h, &"marker", Vector3(1.0, 0.05, 1.0), Vector3(0, 2.23, -0.6))


## 보병 분대: 병사 9명(철모·소총), 흩어진 대형.
static func _squad(h: Parts) -> void:
	var offsets: Array[Vector2] = [Vector2(0, 0), Vector2(-3.5, 1.5), Vector2(3.5, 1.5), Vector2(-7, 3),
		Vector2(7, 3), Vector2(-1.8, 4), Vector2(1.8, 4.2), Vector2(-5, -1), Vector2(5, -1)]
	for o: Vector2 in offsets:
		_box(h, &"camo", Vector3(0.55, 0.9, 0.35), Vector3(o.x, 1.15, o.y))        # 몸통
		_box(h, &"dark", Vector3(0.5, 0.75, 0.3), Vector3(o.x, 0.38, o.y))         # 다리
		_sphere(h, &"camo", Vector3(0.24, 0.2, 0.26), Vector3(o.x, 1.78, o.y))     # 철모
		_box(h, &"dark", Vector3(0.08, 0.08, 0.9), Vector3(o.x + 0.25, 1.25, o.y - 0.4))  # 소총
	_box(h, &"marker", Vector3(0.9, 0.05, 0.9), Vector3(0, 2.3, 0))


# --- 조립 ----------------------------------------------------------------

const ROLE_ORDER: Array[StringName] = [&"camo", &"dark", &"marker"]


static func _commit(parts: Parts, roles_out: Array[StringName]) -> ArrayMesh:
	var mesh := ArrayMesh.new()
	for role: StringName in ROLE_ORDER:
		if not parts.by_role.has(role):
			continue
		var st := SurfaceTool.new()
		st.begin(Mesh.PRIMITIVE_TRIANGLES)
		for part: Array in parts.by_role[role]:
			var pm: PrimitiveMesh = part[0]
			var xf: Transform3D = part[1]
			st.append_from(pm, 0, xf)
		st.commit(mesh)
		roles_out.append(role)
	return mesh
