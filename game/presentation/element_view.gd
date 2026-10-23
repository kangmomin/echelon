class_name ElementView
extends Node3D
## 엘리먼트 3D 표시. 시뮬 위치를 틱 사이 보간해서 그린다(PLAN 15장: 표현은 읽기만).
## 프로토타입 아트: 병과별 크기의 상자. 판독성은 SymbolLayer 의 부호가 맡는다.

const SIDE_COLORS: Dictionary[StringName, Color] = {&"blue": Color(0.32, 0.47, 0.78), &"red": Color(0.78, 0.30, 0.26)}
const DEAD_COLOR: Color = Color(0.12, 0.12, 0.12)
const DAMAGED_TINT: Color = Color(0.55, 0.45, 0.2)
## 병과별 상자 크기(폭, 높이, 길이) m. 멀리서도 보이게 실제보다 약간 크다.
const SIZES: Dictionary[StringName, Vector3] = {
	&"tank": Vector3(4.5, 2.6, 9.0), &"ifv": Vector3(4.0, 2.8, 7.5), &"recon": Vector3(4.0, 2.8, 7.5),
	&"apc": Vector3(3.6, 2.6, 7.0), &"mortar": Vector3(3.6, 2.8, 7.0), &"cp": Vector3(3.6, 3.0, 7.0),
	&"truck": Vector3(3.0, 3.4, 9.0), &"fo": Vector3(3.6, 2.8, 7.0), &"aa": Vector3(3.0, 3.0, 6.0),
	&"ew": Vector3(3.0, 3.6, 8.0), &"inf_squad": Vector3(12.0, 1.4, 12.0),
}

var battle: Battle
var _views: Dictionary[int, MeshInstance3D] = {}
var _materials: Dictionary[String, StandardMaterial3D] = {}
## 적을 숨길지(전장의 안개). known 콜백이 true 를 준 적만 보인다.
var fog_side: StringName = &""


func setup(p_battle: Battle) -> void:
	battle = p_battle
	for e: Element in battle.elements:
		_ensure_view(e)


func _process(_delta: float) -> void:
	if battle == null:
		return
	var alpha: float = battle.world.get_interpolation_alpha()
	for e: Element in battle.elements:
		var mi: MeshInstance3D = _ensure_view(e)
		var visible_now: bool = e.on_map and not e.is_mounted() and _is_visible_to_player(e)
		mi.visible = visible_now
		if not visible_now:
			continue
		var p: Vector2 = e.prev_pos.lerp(e.pos, alpha)
		var h: float = lerp_angle(e.prev_heading, e.heading, alpha)
		var size: Vector3 = SIZES.get(e.def.element_class, Vector3(4, 3, 7))
		mi.position = ViewCoords.to_3d(p, battle.terrain.ground_height(p) + size.y * 0.5)
		mi.rotation = Vector3(0.0, ViewCoords.heading_to_yaw(h), 0.0)
		mi.material_override = _material_for(e)


func _is_visible_to_player(e: Element) -> bool:
	if fog_side == &"" or e.side == fog_side or not e.is_alive():
		return true
	var tf: TaskForce = battle.task_forces.get(fog_side)
	return tf != null and tf.known_enemies.has(e.id)


func _ensure_view(e: Element) -> MeshInstance3D:
	if _views.has(e.id):
		return _views[e.id]
	var box := BoxMesh.new()
	box.size = SIZES.get(e.def.element_class, Vector3(4, 3, 7))
	var mi := MeshInstance3D.new()
	mi.mesh = box
	mi.name = "E%d" % e.id
	add_child(mi)
	_views[e.id] = mi
	return mi


func _material_for(e: Element) -> StandardMaterial3D:
	var key: String
	var color: Color
	if not e.is_alive():
		key = "dead"
		color = DEAD_COLOR
	elif e.state == Element.State.DAMAGED:
		key = "%s_dmg" % e.side
		var base: Color = SIDE_COLORS.get(e.side, Color.WHITE)
		color = base.lerp(DAMAGED_TINT, 0.5)
	else:
		key = String(e.side)
		color = SIDE_COLORS.get(e.side, Color.WHITE)
	if e.is_infantry():
		key += "_inf"
		color = color.lerp(Color(0.35, 0.42, 0.25), 0.35)
	if not _materials.has(key):
		var m := StandardMaterial3D.new()
		m.albedo_color = color
		m.roughness = 0.8
		_materials[key] = m
	return _materials[key]
