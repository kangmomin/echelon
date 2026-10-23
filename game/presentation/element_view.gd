class_name ElementView
extends Node3D
## 엘리먼트 3D 표시. 시뮬 위치를 틱 사이 보간해서 그린다(PLAN 15장: 표현은 읽기만).
## 장비별 저폴리 모델(VehicleModels): 차체 + 표적을 따라 도는 포탑, 진영별 위장색, 위에 진영 색 식별판.
## 지면 경사를 따라 기울고, 사격 시 포탑이 살짝 밀린다. 판독은 SymbolLayer 의 부호가 주로 맡는다.

const SIDE_COLORS: Dictionary[StringName, Color] = {&"blue": Color(0.32, 0.47, 0.78), &"red": Color(0.78, 0.30, 0.26)}
const DEAD_COLOR: Color = Color(0.10, 0.09, 0.08)
## 진영별 위장색(미 = 올리브·황갈, 러 = 녹색).
const CAMO_COLORS: Dictionary[StringName, Color] = {&"blue": Color(0.33, 0.30, 0.15), &"red": Color(0.17, 0.26, 0.11)}
const DAMAGED_TINT: Color = Color(0.55, 0.45, 0.2)
const DARK_COLOR: Color = Color(0.07, 0.07, 0.06)
## 포탑 선회 속도(rad/s, 게임 시간).
const TURRET_RATE: float = 1.1
const RECOIL_M: float = 0.35
const RECOIL_DECAY: float = 3.0


class View:
	extends RefCounted
	var root: Node3D
	var tilt: Node3D
	var hull: MeshInstance3D
	var turret: MeshInstance3D = null
	var model: VehicleModels.Model
	var turret_yaw: float = 0.0
	var recoil: float = 0.0
	var last_fired: int = -1000000
	var mat_key: String = ""
	var tilt_pos: Vector2 = Vector2(INF, INF)
	var tilt_basis: Basis = Basis.IDENTITY


var battle: Battle
var _views: Dictionary[int, View] = {}
var _materials: Dictionary[String, StandardMaterial3D] = {}
## 전장의 안개 기준 진영. 이 진영이 지금 보고 있는 적만 3D 로 그린다("" = 다 보임).
var fog_side: StringName = &""


func setup(p_battle: Battle) -> void:
	battle = p_battle
	for e: Element in battle.elements:
		_ensure_view(e)


func _process(delta: float) -> void:
	if battle == null:
		return
	var dt: float = 0.0 if battle.world.paused else delta * battle.world.get_speed()
	var alpha: float = battle.world.get_interpolation_alpha()
	for e: Element in battle.elements:
		var v: View = _ensure_view(e)
		var visible_now: bool = e.on_map and not e.is_mounted() and _is_visible_to_player(e)
		v.root.visible = visible_now
		if not visible_now:
			continue
		var p: Vector2 = e.prev_pos.lerp(e.pos, alpha)
		var h: float = lerp_angle(e.prev_heading, e.heading, alpha)
		v.root.position = ViewCoords.to_3d(p, battle.terrain.ground_height(p))
		v.root.rotation = Vector3(0.0, ViewCoords.heading_to_yaw(h), 0.0)
		if e.is_vehicle():
			_update_tilt(v, p, h)
		_update_materials(v, e)
		if v.turret != null:
			_update_turret(v, e, p, h, dt)


func _is_visible_to_player(e: Element) -> bool:
	if fog_side == &"" or e.side == fog_side or not e.is_alive():
		return true
	return battle.detection.seen_recently(fog_side, e.id, battle.tick())


func _ensure_view(e: Element) -> View:
	if _views.has(e.id):
		return _views[e.id]
	var v := View.new()
	v.model = VehicleModels.model_for(e.def)
	v.root = Node3D.new()
	v.root.name = "E%d" % e.id
	v.tilt = Node3D.new()
	v.root.add_child(v.tilt)
	v.hull = MeshInstance3D.new()
	v.hull.mesh = v.model.hull
	v.tilt.add_child(v.hull)
	if v.model.turret != null:
		v.turret = MeshInstance3D.new()
		v.turret.mesh = v.model.turret
		v.turret.position = v.model.turret_pivot
		v.tilt.add_child(v.turret)
	add_child(v.root)
	_views[e.id] = v
	return v


## 지면 경사를 따라 차체를 기울인다(앞뒤·좌우 높이차). 움직였을 때만 다시 잰다.
func _update_tilt(v: View, p: Vector2, h: float) -> void:
	if v.tilt_pos.distance_squared_to(p) < 0.25:
		return
	v.tilt_pos = p
	var fwd := Vector2(sin(h), cos(h))
	var right := Vector2(fwd.y, -fwd.x)
	var t: Terrain = battle.terrain
	var dz: float = t.ground_height(p + fwd * 3.0) - t.ground_height(p - fwd * 3.0)
	var dx: float = t.ground_height(p + right * 1.8) - t.ground_height(p - right * 1.8)
	var pitch: float = atan2(dz, 6.0)
	var roll: float = atan2(dx, 3.6)
	# 모델 좌표: 앞 = -Z, 오른쪽 = +X. 앞이 높으면 X 축 + 회전, 오른쪽이 높으면 Z 축 + 회전.
	v.tilt.basis = Basis.from_euler(Vector3(pitch, 0.0, roll))


func _update_turret(v: View, e: Element, p: Vector2, h: float, dt: float) -> void:
	var want: float = 0.0
	if e.is_alive() and e.target_id >= 0:
		var t: Element = battle.get_element(e.target_id)
		if t != null and t.on_map:
			var d: Vector2 = t.pos - p
			if d.length_squared() > 1.0:
				want = wrapf(-(atan2(d.x, d.y) - h), -PI, PI)
	if e.is_alive():
		var diff: float = wrapf(want - v.turret_yaw, -PI, PI)
		var step: float = TURRET_RATE * dt
		v.turret_yaw = wrapf(v.turret_yaw + clampf(diff, -step, step), -PI, PI)
		if e.last_fired_tick != v.last_fired:
			if v.last_fired > -1000000 and e.last_fired_tick > v.last_fired:
				v.recoil = 1.0
			v.last_fired = e.last_fired_tick
		v.recoil = maxf(v.recoil - RECOIL_DECAY * dt, 0.0)
		var back := Vector3(sin(v.turret_yaw), 0.0, cos(v.turret_yaw)) * RECOIL_M * v.recoil
		v.turret.basis = Basis(Vector3.UP, v.turret_yaw)
		v.turret.position = v.model.turret_pivot + back
	elif e.state == Element.State.DESTROYED:
		# 격파: 포탑이 들리고 틀어진 채 남는다(id 로 고정된 모양).
		var r: float = float((e.id * 7919) % 100) / 100.0
		v.turret.basis = Basis.from_euler(Vector3(0.18 + r * 0.25, v.turret_yaw + (r - 0.5) * 1.2, (r - 0.5) * 0.4))
		v.turret.position = v.model.turret_pivot + Vector3(0.0, 0.35 + r * 0.4, 0.0)


func _update_materials(v: View, e: Element) -> void:
	var key: String = "%s_%d_%s" % [e.side, e.state, e.is_alive()]
	if key == v.mat_key:
		return
	v.mat_key = key
	_apply(v.hull, v.model.hull_roles, e)
	if v.turret != null:
		_apply(v.turret, v.model.turret_roles, e)


func _apply(mi: MeshInstance3D, roles: Array[StringName], e: Element) -> void:
	for i: int in roles.size():
		var mat: StandardMaterial3D
		match roles[i]:
			&"camo":
				mat = _material_for(e)
			&"dark":
				mat = _dark_material(e)
			_:
				mat = _marker_material(e)
		mi.set_surface_override_material(i, mat)


func _marker_material(e: Element) -> StandardMaterial3D:
	var key: String = "marker_%s_%s" % [e.side, e.is_alive()]
	if not _materials.has(key):
		var m := StandardMaterial3D.new()
		var side_color: Color = SIDE_COLORS.get(e.side, Color.WHITE)
		m.albedo_color = side_color if e.is_alive() else DEAD_COLOR
		m.emission_enabled = e.is_alive()
		m.emission = side_color * 0.35
		m.roughness = 0.6
		_materials[key] = m
	return _materials[key]


func _dark_material(e: Element) -> StandardMaterial3D:
	var key: String = "dark_%s" % e.is_alive()
	if not _materials.has(key):
		var m := StandardMaterial3D.new()
		m.albedo_color = DARK_COLOR if e.is_alive() else DEAD_COLOR.darkened(0.4)
		m.roughness = 0.85
		m.metallic_specular = 0.3
		_materials[key] = m
	return _materials[key]


func _material_for(e: Element) -> StandardMaterial3D:
	var key: String
	var color: Color
	var camo: Color = CAMO_COLORS.get(e.side, Color(0.4, 0.4, 0.35))
	if not e.is_alive():
		key = "dead"
		color = DEAD_COLOR
	elif e.state == Element.State.DAMAGED:
		key = "%s_dmg" % e.side
		color = camo.lerp(DAMAGED_TINT, 0.4).darkened(0.25)
	else:
		key = String(e.side)
		# 진영 색을 살짝 섞어 멀리서도 구분되게.
		var side_color: Color = SIDE_COLORS.get(e.side, Color.WHITE)
		color = camo.lerp(side_color, 0.15)
	if e.is_infantry():
		key += "_inf"
		color = color.lerp(Color(0.33, 0.36, 0.24), 0.4)
	if not _materials.has(key):
		var m := StandardMaterial3D.new()
		m.albedo_color = color
		m.roughness = 0.9
		m.metallic_specular = 0.15
		_materials[key] = m
	return _materials[key]
