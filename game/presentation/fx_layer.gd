class_name FxLayer
extends Node3D
## 전투 효과: 사격 예광선, 피격·격파 섬광. 시뮬 이벤트("shot", "element_destroyed")를 받아 잠깐 그린다.
## 표현 계층 전용 — 시뮬 상태를 바꾸지 않는다. 안개가 켜져 있으면 보이지 않는 적의 사격은 발사 섬광만 그린다.

const TRACER_SEC: float = 0.35
const FLASH_SEC: float = 1.2
const WRECK_SMOKE_SEC: float = 20.0
const COLORS: Dictionary[StringName, Color] = {&"blue": Color(1.0, 0.85, 0.45), &"red": Color(1.0, 0.55, 0.35)}

class Tracer:
	var a: Vector3
	var b: Vector3
	var color: Color
	var age: float = 0.0

class Flash:
	var node: MeshInstance3D
	var age: float = 0.0
	var life: float = FLASH_SEC
	var grow: float = 1.0

var battle: Battle
## 이 진영 기준으로 안 보이는 적의 탄도는 감춘다("" = 다 보임).
var fog_side: StringName = &""
var _tracers: Array[Tracer] = []
var _flashes: Array[Flash] = []
var _mesh: ImmediateMesh
var _mesh_instance: MeshInstance3D
var _line_material: StandardMaterial3D
var _flash_material: StandardMaterial3D
var _smoke_material: StandardMaterial3D
## 연막탄 차장(연한 회색).
var _screen_material: StandardMaterial3D


func setup(p_battle: Battle) -> void:
	battle = p_battle
	_mesh = ImmediateMesh.new()
	_mesh_instance = MeshInstance3D.new()
	_mesh_instance.mesh = _mesh
	_mesh_instance.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	_line_material = StandardMaterial3D.new()
	_line_material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_line_material.vertex_color_use_as_albedo = true
	_line_material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_mesh_instance.material_override = _line_material
	add_child(_mesh_instance)
	_flash_material = StandardMaterial3D.new()
	_flash_material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_flash_material.albedo_color = Color(1.0, 0.7, 0.3, 0.85)
	_flash_material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_screen_material = StandardMaterial3D.new()
	_screen_material.albedo_color = Color(0.85, 0.86, 0.88, 0.75)
	_screen_material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_smoke_material = StandardMaterial3D.new()
	_smoke_material.albedo_color = Color(0.2, 0.2, 0.2, 0.45)
	_smoke_material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA


func on_event(ev: Dictionary) -> void:
	var type: StringName = ev.get("type", &"")
	var pl: Dictionary = ev.get("payload", {})
	match type:
		&"shot":
			var from_id: int = pl["from"]
			var hit: bool = pl.get("hit", false)
			var from_e: Element = battle.get_element(from_id)
			if from_e == null:
				return
			var fp: Vector2 = pl["fp"]
			var tp: Vector2 = pl["tp"]
			var shooter_visible: bool = _visible(from_e)
			if not shooter_visible:
				return
			var t := Tracer.new()
			t.a = ViewCoords.to_3d(fp, battle.terrain.ground_height(fp) + 2.5)
			t.b = ViewCoords.to_3d(tp, battle.terrain.ground_height(tp) + 1.5)
			if not hit:
				# 빗나간 탄은 표적 너머로 조금 흘린다.
				t.b += (t.b - t.a).normalized() * 40.0
			t.color = COLORS.get(from_e.side, Color.WHITE)
			_tracers.append(t)
			if hit:
				_add_flash(tp, 2.5, 0.4, _flash_material, 1.5)
		&"element_destroyed":
			var pos: Vector2 = pl["pos"]
			_add_flash(pos, 6.0, FLASH_SEC, _flash_material, 2.0)
			_add_flash(pos, 5.0, WRECK_SMOKE_SEC, _smoke_material, 2.5)
		&"shell":
			var sp: Vector2 = pl["pos"]
			var smoke: bool = pl.get("smoke", false)
			if smoke:
				var r: float = pl.get("radius", 35.0)
				_add_flash(sp, r * 0.6, 120.0, _screen_material, 1.6)
			else:
				_add_flash(sp, 9.0, 0.8, _flash_material, 2.5)
				_add_flash(sp, 7.0, 6.0, _smoke_material, 2.0)
		&"indirect_fire":
			var from_id2: int = pl.get("from", -1)
			var from_e2: Element = battle.get_element(from_id2)
			if from_e2 != null and _visible(from_e2):
				var mp: Vector2 = pl["pos"]
				_add_flash(mp, 3.0, 0.3, _flash_material, 1.5)
		&"sam_launch":
			var sam_id: int = pl.get("from", -1)
			var sam_e: Element = battle.get_element(sam_id)
			if sam_e != null and _visible(sam_e):
				var t2 := Tracer.new()
				var a2: Vector2 = pl["pos"]
				var b2: Vector2 = pl["to"]
				t2.a = ViewCoords.to_3d(a2, battle.terrain.ground_height(a2) + 3.0)
				t2.b = ViewCoords.to_3d(b2, battle.terrain.ground_height(b2) + 400.0)
				t2.color = Color(1.0, 1.0, 0.85)
				_tracers.append(t2)


func _visible(e: Element) -> bool:
	if fog_side == &"" or e.side == fog_side:
		return true
	return battle.detection.seen_recently(fog_side, e.id, battle.tick())


func _add_flash(pos: Vector2, radius: float, life: float, mat: StandardMaterial3D, grow: float) -> void:
	var sphere := SphereMesh.new()
	sphere.radius = radius
	sphere.height = radius * 2.0
	sphere.radial_segments = 8
	sphere.rings = 4
	var mi := MeshInstance3D.new()
	mi.mesh = sphere
	mi.material_override = mat
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	mi.position = ViewCoords.to_3d(pos, battle.terrain.ground_height(pos) + radius * 0.6)
	add_child(mi)
	var f := Flash.new()
	f.node = mi
	f.life = life
	f.grow = grow
	_flashes.append(f)


func _process(delta: float) -> void:
	if battle == null:
		return
	var dt: float = delta if not battle.world.paused else 0.0
	_mesh.clear_surfaces()
	var alive: Array[Tracer] = []
	for t: Tracer in _tracers:
		t.age += dt
		if t.age < TRACER_SEC:
			alive.append(t)
	_tracers = alive
	if not _tracers.is_empty():
		_mesh.surface_begin(Mesh.PRIMITIVE_LINES)
		for t: Tracer in _tracers:
			# 탄이 날아가는 모습: 짧은 선분이 a→b 로 이동.
			var k: float = t.age / TRACER_SEC
			var head: Vector3 = t.a.lerp(t.b, minf(1.0, k * 1.6))
			var tail: Vector3 = t.a.lerp(t.b, maxf(0.0, k * 1.6 - 0.35))
			var c: Color = t.color
			c.a = 1.0 - k * 0.6
			_mesh.surface_set_color(c)
			_mesh.surface_add_vertex(tail)
			_mesh.surface_set_color(c)
			_mesh.surface_add_vertex(head)
		_mesh.surface_end()
	var keep: Array[Flash] = []
	for f: Flash in _flashes:
		f.age += dt
		if f.age >= f.life:
			f.node.queue_free()
			continue
		var s: float = 1.0 + (f.grow - 1.0) * (f.age / f.life)
		f.node.scale = Vector3.ONE * s
		keep.append(f)
	_flashes = keep
