class_name FxLayer
extends Node3D
## 전투 효과. 시뮬 이벤트("shot", "element_destroyed", "shell", "indirect_fire", "sam_launch")와 연막 목록을 읽어
## 포구 섬광·포연, 날아가는 예광탄·미사일(연기 꼬리), 명중 불티·빗나간 흙먼지, 격파 화염구와 불타는 잔해,
## 포탄 흙기둥·먼지 고리·포탄 자국, 연막 차장을 그린다.
## 표현 계층 전용 — 시뮬 상태와 시뮬 난수를 건드리지 않는다(자체 난수 사용). 안개가 켜져 있으면 안 보이는 적의 사격은 감춘다.
##
## 입자는 노드 없이 배열로 굴리고, 층(연기 = 알파 혼합, 불 = 더하기 혼합)마다 MultiMesh 하나로 한 번에 그린다.
## 웹(Compatibility)에서도 같은 코드가 돌고, 입자 상한만 낮춘다.

const COLORS: Dictionary[StringName, Color] = {&"blue": Color(1.0, 0.85, 0.45), &"red": Color(1.0, 0.6, 0.35)}
## 바람(m/s, 3D). 연기가 동남쪽으로 천천히 흐른다.
const WIND: Vector3 = Vector3(2.2, 0.0, 1.2)
const WRECK_SEC: float = 120.0
const SCORCH_SEC: float = 240.0
## 이보다 오래된 이벤트(틱)는 효과 없이 넘긴다.
const STALE_TICKS: int = 30
## 포탄 효과 크기(박격포, 포병, 항공 폭탄).
const SHELL_SCALE: Array[float] = [0.85, 1.2, 1.6]

enum Layer { SMOKE, FIRE }


## 한 층의 입자들(같은 재질). 배열은 같은 번호끼리 한 입자.
class ParticleLayer:
	extends RefCounted
	var cap: int
	var count: int = 0
	var pos := PackedVector3Array()
	var vel := PackedVector3Array()
	var age := PackedFloat32Array()
	var life := PackedFloat32Array()
	var size0 := PackedFloat32Array()
	var size1 := PackedFloat32Array()
	var drag := PackedFloat32Array()
	var grav := PackedFloat32Array()
	var rot := PackedFloat32Array()
	var spin := PackedFloat32Array()
	var seeds := PackedFloat32Array()
	var c0 := PackedColorArray()
	var c1 := PackedColorArray()
	var buf := PackedFloat32Array()
	var mm: MultiMesh
	var node: MultiMeshInstance3D

	func _init(p_cap: int, material: Material) -> void:
		cap = p_cap
		pos.resize(cap)
		vel.resize(cap)
		age.resize(cap)
		life.resize(cap)
		size0.resize(cap)
		size1.resize(cap)
		drag.resize(cap)
		grav.resize(cap)
		rot.resize(cap)
		spin.resize(cap)
		seeds.resize(cap)
		c0.resize(cap)
		c1.resize(cap)
		buf.resize(cap * 20)
		buf.fill(0.0)
		var quad := QuadMesh.new()
		quad.size = Vector2(2.0, 2.0)
		mm = MultiMesh.new()
		mm.transform_format = MultiMesh.TRANSFORM_3D
		mm.use_colors = true
		mm.use_custom_data = true
		mm.mesh = quad
		mm.instance_count = cap
		mm.visible_instance_count = 0
		node = MultiMeshInstance3D.new()
		node.multimesh = mm
		node.material_override = material
		node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		# 입자는 맵 전체에 흩어질 수 있다 — 화면 밖 판정으로 사라지지 않게 넉넉한 경계.
		node.custom_aabb = AABB(Vector3(-2000, -500, -6000), Vector3(8000, 3000, 8000))

	func remove(i: int) -> void:
		var last: int = count - 1
		if i != last:
			pos[i] = pos[last]
			vel[i] = vel[last]
			age[i] = age[last]
			life[i] = life[last]
			size0[i] = size0[last]
			size1[i] = size1[last]
			drag[i] = drag[last]
			grav[i] = grav[last]
			rot[i] = rot[last]
			spin[i] = spin[last]
			seeds[i] = seeds[last]
			c0[i] = c0[last]
			c1[i] = c1[last]
		count = last

	func step(dt: float) -> void:
		var i: int = 0
		while i < count:
			var a: float = age[i] + dt
			if a >= life[i]:
				remove(i)
				continue
			age[i] = a
			var v: Vector3 = vel[i]
			v += (WIND - v) * minf(drag[i] * dt, 1.0)
			v.y -= grav[i] * dt
			vel[i] = v
			pos[i] += v * dt
			i += 1

	func upload() -> void:
		for i: int in count:
			var k: float = age[i] / life[i]
			var grow: float = 1.0 - (1.0 - k) * (1.0 - k)
			var s: float = lerpf(size0[i], size1[i], grow)
			var p: Vector3 = pos[i]
			var c: Color = c0[i].lerp(c1[i], k)
			var o: int = i * 20
			buf[o] = s
			buf[o + 3] = p.x
			buf[o + 5] = s
			buf[o + 7] = p.y
			buf[o + 10] = s
			buf[o + 11] = p.z
			buf[o + 12] = c.r
			buf[o + 13] = c.g
			buf[o + 14] = c.b
			buf[o + 15] = c.a
			buf[o + 16] = rot[i] + spin[i] * age[i]
			buf[o + 17] = seeds[i]
			buf[o + 18] = k
		mm.buffer = buf
		mm.visible_instance_count = count


class Projectile:
	extends RefCounted
	var a: Vector3
	var b: Vector3
	var age: float = 0.0
	var dur: float = 0.3
	var weapon: StringName
	var hit: bool
	var color: Color
	var trail_acc: float = 0.0
	## 뒤쪽 꼬리 길이(비율).
	var tail: float = 0.3
	## 명중/탄착 효과를 그릴지(안개로 표적 위치를 모르면 끈다).
	var show_impact: bool = true


class Wreck:
	extends RefCounted
	var pos: Vector3
	var age: float = 0.0
	var fire_acc: float = 0.0
	var smoke_acc: float = 0.0


class ScreenEmitter:
	extends RefCounted
	var smoke: Los.Smoke
	var center: Vector3
	var acc: float = 0.0
	var started: bool = false


class LightFlash:
	extends RefCounted
	var light: OmniLight3D
	var age: float = 0.0
	var life: float = 0.3
	var energy: float = 6.0


var battle: Battle
## 이 진영 기준으로 안 보이는 적의 탄도는 감춘다("" = 다 보임).
var fog_side: StringName = &""
var lite: bool = false

var _rng := RandomNumberGenerator.new()
var _layers: Array[ParticleLayer] = []
var _projectiles: Array[Projectile] = []
var _wrecks: Array[Wreck] = []
var _screens: Dictionary[int, ScreenEmitter] = {}
var _lights: Array[LightFlash] = []
var _line_mesh: ImmediateMesh
var _scorch_mm: MultiMesh
var _scorch_ages: PackedFloat32Array = PackedFloat32Array()
var _scorch_next: int = 0
var _max_burning: int = 30


func setup(p_battle: Battle) -> void:
	battle = p_battle
	lite = OS.has_feature("web")
	_rng.seed = 0x5EED
	_max_burning = 12 if lite else 30
	var smoke_mat := ShaderMaterial.new()
	smoke_mat.shader = load("res://game/presentation/fx_smoke.gdshader")
	var fire_mat := ShaderMaterial.new()
	fire_mat.shader = load("res://game/presentation/fx_fire.gdshader")
	fire_mat.render_priority = 1
	_layers.append(ParticleLayer.new(1100 if lite else 3200, smoke_mat))
	_layers.append(ParticleLayer.new(500 if lite else 1400, fire_mat))
	for l: ParticleLayer in _layers:
		add_child(l.node)
	# 예광선(선).
	_line_mesh = ImmediateMesh.new()
	var lines := MeshInstance3D.new()
	lines.mesh = _line_mesh
	lines.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	var line_mat := StandardMaterial3D.new()
	line_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	line_mat.vertex_color_use_as_albedo = true
	line_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	line_mat.blend_mode = BaseMaterial3D.BLEND_MODE_ADD
	lines.material_override = line_mat
	add_child(lines)
	# 포탄 자국(땅 위 평면, 고리 버퍼).
	var scorch_cap: int = 60 if lite else 160
	var plane := PlaneMesh.new()
	plane.size = Vector2(2.0, 2.0)
	_scorch_mm = MultiMesh.new()
	_scorch_mm.transform_format = MultiMesh.TRANSFORM_3D
	_scorch_mm.use_colors = true
	_scorch_mm.use_custom_data = true
	_scorch_mm.mesh = plane
	_scorch_mm.instance_count = scorch_cap
	_scorch_ages.resize(scorch_cap)
	_scorch_ages.fill(SCORCH_SEC)
	for i: int in scorch_cap:
		_scorch_mm.set_instance_color(i, Color(0, 0, 0, 0))
		_scorch_mm.set_instance_transform(i, Transform3D(Basis.from_scale(Vector3.ONE * 0.001), Vector3(0, -1000, 0)))
	var scorch_mat := ShaderMaterial.new()
	scorch_mat.shader = load("res://game/presentation/fx_scorch.gdshader")
	scorch_mat.render_priority = -1
	var scorch := MultiMeshInstance3D.new()
	scorch.multimesh = _scorch_mm
	scorch.material_override = scorch_mat
	scorch.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	scorch.custom_aabb = AABB(Vector3(-2000, -500, -6000), Vector3(8000, 3000, 8000))
	add_child(scorch)


# --- 이벤트 -------------------------------------------------------------

func on_event(ev: Dictionary) -> void:
	var type: StringName = ev.get("type", &"")
	var pl: Dictionary = ev.get("payload", {})
	var ev_tick: int = ev.get("tick", battle.tick())
	if battle.tick() - ev_tick > STALE_TICKS:
		# 한꺼번에 밀려온 지난 이벤트(배속 따라잡기·미리 돌리기)는 섬광 없이 결과만 남긴다.
		if type == &"element_destroyed":
			var wp: Vector2 = pl["pos"]
			var w := Wreck.new()
			w.pos = _ground(wp, 2.0)
			w.age = float(battle.tick() - ev_tick) / float(SimConst.TICK_RATE)
			_add_wreck(w)
		return
	match type:
		&"shot":
			_on_shot(pl)
		&"element_destroyed":
			var pos: Vector2 = pl["pos"]
			var eid: int = pl.get("element", -1)
			var dead: Element = battle.get_element(eid)
			if dead != null and dead.def.element_class == &"heli" and dead.platoon != null:
				# 격추된 헬기: 그 고도에서 터지고 잔해는 땅에서 탄다.
				var alt: float = Element.ALTITUDE_HIGH_M if dead.platoon.sop.altitude == &"high" else Element.ALTITUDE_LOW_M
				var air: Vector3 = _ground(pos, alt)
				_flash(air, 14.0, 0.2)
				_fireball(air, 1.2)
				_sparks(air, 14, 1.4)
			_explode_vehicle(pos)
		&"shell":
			var sp: Vector2 = pl["pos"]
			var smoke: bool = pl.get("smoke", false)
			var asset: int = pl.get("asset", 1)
			if smoke:
				_smoke_shell_burst(sp)
			else:
				_shell_impact(sp, asset)
		&"indirect_fire":
			var from_id: int = pl.get("from", -1)
			var src: Element = battle.get_element(from_id)
			if src != null and _visible(src):
				var mp: Vector2 = pl["pos"]
				_artillery_muzzle(src, mp)
		&"sam_launch":
			var sam_id: int = pl.get("from", -1)
			var sam_e: Element = battle.get_element(sam_id)
			if sam_e != null and _visible(sam_e):
				var a2: Vector2 = pl["pos"]
				var b2: Vector2 = pl["to"]
				var pr := Projectile.new()
				pr.a = _ground(a2, 3.0)
				pr.b = _ground(b2, 450.0)
				pr.dur = 2.5
				pr.weapon = &"SAM"
				pr.hit = false
				pr.color = Color(1.0, 0.95, 0.8)
				pr.show_impact = false
				_projectiles.append(pr)
				_muzzle_smoke(pr.a, Vector3.UP, 4, 1.0)
		&"mine_strike":
			var mp2: Vector2 = pl.get("pos", Vector2.ZERO)
			var g: Vector3 = _ground(mp2, 0.5)
			_flash(g + Vector3.UP * 2.0, 10.0, 0.14)
			_dirt(g, 14, 1.2)
			_burst(Layer.SMOKE, g + Vector3.UP * 2.0, 6, 3.0, 9.0, 3.0, 12.0, Color(0.32, 0.27, 0.21, 0.85),
				Color(0.5, 0.47, 0.42, 0.0), 4.0, 6.0, 0.6, -0.3)
			_scorch(mp2, 6.0)
			_light(g + Vector3.UP * 2.0, 6.0, 60.0, 0.25)
		&"cas_shot_down":
			var cp: Vector2 = pl.get("pos", Vector2.ZERO)
			var air: Vector3 = _ground(cp, 450.0)
			_fireball(air, 1.4)
			_burst(Layer.SMOKE, air, 8, 4.0, 18.0, 3.0, 10.0, Color(0.1, 0.1, 0.1, 0.7), Color(0.3, 0.3, 0.3, 0.0), 5.0, 8.0, 0.4, 0.0)


func _on_shot(pl: Dictionary) -> void:
	var from_id: int = pl["from"]
	var to_id: int = pl.get("to", -1)
	var from_e: Element = battle.get_element(from_id)
	if from_e == null:
		return
	var target_e: Element = battle.get_element(to_id)
	var shooter_seen: bool = _visible(from_e)
	var target_seen: bool = target_e != null and _visible(target_e)
	if not shooter_seen and not target_seen:
		return
	var hit: bool = pl.get("hit", false)
	var weapon_v: Variant = pl.get("weapon", &"MG")
	var weapon: StringName = StringName(str(weapon_v))
	var fp: Vector2 = pl["fp"]
	var tp: Vector2 = pl["tp"]
	var d2: Vector2 = tp - fp
	var dist: float = maxf(d2.length(), 1.0)
	var dir2: Vector2 = d2 / dist
	var inf: bool = from_e.is_infantry()
	var muzzle_h: float = (1.3 if inf else 2.9) + from_e.altitude_m()
	var muzzle_fwd: float = 0.5 if inf else 5.0
	var a: Vector3 = _ground(fp + dir2 * muzzle_fwd, muzzle_h)
	var tgt_h: float = 1.0 if (target_e != null and target_e.is_infantry()) else 1.8
	if target_e != null:
		tgt_h += target_e.altitude_m()
	var b: Vector3 = _ground(tp, tgt_h)
	if not hit:
		# 빗나간 탄: 표적 옆·너머 땅에 떨어진다(효과용 난수).
		var spread: float = 6.0 if weapon == &"MG" else 12.0
		var miss2: Vector2 = tp + dir2 * _rng.randf_range(8.0, 45.0) + Vector2(-dir2.y, dir2.x) * _rng.randf_range(-spread, spread)
		b = _ground(miss2, 0.2)
	var dir3: Vector3 = (b - a).normalized()
	if shooter_seen:
		_muzzle(from_e, weapon, a, dir3)
	var pr := Projectile.new()
	pr.a = a
	pr.b = b
	pr.weapon = weapon
	pr.hit = hit
	pr.color = COLORS.get(from_e.side, Color.WHITE)
	pr.show_impact = target_seen or not hit
	match weapon:
		&"KE":
			pr.dur = clampf(dist / 1650.0, 0.08, 2.0)
			pr.tail = 0.25
		&"HEAT", &"HE":
			pr.dur = clampf(dist / 900.0, 0.1, 2.5)
			pr.tail = 0.2
		&"ATGM":
			pr.dur = clampf(dist / 450.0, 0.4, 4.0)
			pr.tail = 0.05
			pr.color = Color(1.0, 0.9, 0.7)
		&"AC":
			pr.dur = clampf(dist / 1100.0, 0.08, 2.0)
			pr.tail = 0.3
		_:
			pr.dur = clampf(dist / 850.0, 0.06, 1.6)
			pr.tail = 0.35
	if not shooter_seen:
		# 쏜 쪽을 모르면 탄도는 숨기고 맞은 자리만.
		pr.age = pr.dur
	_projectiles.append(pr)


# --- 효과 조합 -----------------------------------------------------------

func _muzzle(e: Element, weapon: StringName, a: Vector3, dir: Vector3) -> void:
	match weapon:
		&"KE", &"HEAT", &"HE":
			if e.is_infantry():
				# 휴대 대전차 화기: 작은 섬광 + 뒤 후폭풍.
				_flash(a, 2.0, 0.08)
				_muzzle_smoke(a - dir * 1.5, -dir, 4, 0.8)
			else:
				_flash(a, 5.5, 0.1)
				_burst_dir(Layer.FIRE, a, dir, 3, 18.0, 30.0, 2.5, 1.0, Color(1.0, 0.8, 0.4, 1.0), Color(1.0, 0.3, 0.05, 0.0), 0.1, 0.16)
				_muzzle_smoke(a, dir, 6, 1.4)
				_ground_blast(Vector2(a.x, -a.z), 7, 1.0)
				_light(a, 3.0, 35.0, 0.12)
		&"ATGM":
			_flash(a, 3.0, 0.12)
			_muzzle_smoke(a, -dir, 5, 1.1)
		&"AC":
			_flash(a, 2.2, 0.06)
			_muzzle_smoke(a, dir, 1, 0.5)
		&"SAM":
			_flash(a, 3.0, 0.12)
		_:
			_flash(a, 1.0 if e.is_infantry() else 1.4, 0.05)


func _impact(pr: Projectile) -> void:
	var p: Vector3 = pr.b
	if pr.hit:
		match pr.weapon:
			&"KE":
				_flash(p, 4.5, 0.12)
				_sparks(p, 10, 1.0)
				_burst(Layer.SMOKE, p, 2, 1.5, 3.0, 2.0, 7.0, Color(0.25, 0.24, 0.22, 0.6), Color(0.35, 0.34, 0.32, 0.0), 2.0, 3.5, 1.2, -0.5)
			&"HEAT", &"ATGM":
				_flash(p, 6.0, 0.14)
				_fireball(p, 0.6)
				_sparks(p, 6, 0.8)
				_burst(Layer.SMOKE, p, 4, 2.0, 4.0, 3.0, 10.0, Color(0.08, 0.08, 0.07, 0.7), Color(0.28, 0.27, 0.26, 0.0), 3.0, 5.0, 0.8, -0.8)
				_light(p, 4.0, 45.0, 0.2)
			&"HE":
				_flash(p, 5.0, 0.12)
				_fireball(p, 0.45)
				_burst(Layer.SMOKE, p, 3, 2.0, 5.0, 2.5, 9.0, Color(0.2, 0.19, 0.17, 0.65), Color(0.4, 0.38, 0.35, 0.0), 2.5, 4.0, 0.8, -0.5)
			&"AC":
				_flash(p, 2.2, 0.06)
				_sparks(p, 4, 0.6)
				_burst(Layer.SMOKE, p, 1, 1.0, 2.0, 1.2, 4.0, Color(0.3, 0.29, 0.27, 0.5), Color(0.4, 0.4, 0.38, 0.0), 1.5, 2.5, 1.2, 0.0)
			_:
				_sparks(p, 2, 0.4)
	else:
		match pr.weapon:
			&"KE", &"HEAT", &"ATGM", &"HE":
				var big: float = 1.3 if pr.weapon != &"KE" else 1.0
				if pr.weapon != &"KE":
					_flash(p, 4.0, 0.1)
				_dirt(p, 8, big)
				_burst(Layer.SMOKE, p, 4, 3.0, 7.0, 2.0 * big, 8.0 * big, Color(0.42, 0.36, 0.28, 0.75), Color(0.52, 0.48, 0.42, 0.0), 2.0, 3.5, 0.9, 1.0)
			&"AC":
				_dirt(p, 2, 0.5)
				_burst(Layer.SMOKE, p, 1, 2.0, 4.0, 1.0, 3.5, Color(0.45, 0.4, 0.32, 0.6), Color(0.5, 0.47, 0.42, 0.0), 1.2, 2.0, 1.0, 1.0)
			&"SAM":
				pass
			_:
				if _rng.randf() < 0.5:
					_burst(Layer.SMOKE, p, 1, 1.5, 3.0, 0.5, 1.8, Color(0.45, 0.4, 0.32, 0.55), Color(0.5, 0.47, 0.42, 0.0), 0.6, 1.0, 1.0, 2.0)


## 차량 격파: 큰 섬광, 화염구, 파편 불티, 검은 연기 분출, 그 뒤로 불타는 잔해.
func _explode_vehicle(pos2: Vector2) -> void:
	var p: Vector3 = _ground(pos2, 2.0)
	_flash(p, 14.0, 0.2)
	_fireball(p, 1.3)
	_sparks(p + Vector3.UP, 16, 1.6)
	_burst(Layer.SMOKE, p + Vector3.UP * 2.0, 8, 3.0, 9.0, 3.0, 14.0, Color(0.06, 0.06, 0.05, 0.8), Color(0.25, 0.24, 0.23, 0.0), 5.0, 8.0, 0.5, -1.0)
	_light(p, 10.0, 80.0, 0.45)
	var w := Wreck.new()
	w.pos = p
	_add_wreck(w)


func _add_wreck(w: Wreck) -> void:
	if w.age >= WRECK_SEC:
		return
	_wrecks.append(w)
	if _wrecks.size() > _max_burning:
		_wrecks.pop_front()


## 포탄 탄착: 섬광·화염, 흙기둥, 솟는 먼지 기둥, 땅을 따라 퍼지는 먼지 고리, 남는 연기, 포탄 자국.
## asset 0 박격포, 1 포병, 2 항공 폭탄.
func _shell_impact(pos2: Vector2, asset: int) -> void:
	var s: float = SHELL_SCALE[clampi(asset, 0, 2)]
	var p: Vector3 = _ground(pos2, 0.5)
	_flash(p + Vector3.UP * 3.0, 20.0 * s, 0.18)
	_fireball(p + Vector3.UP * 1.5, 1.1 * s)
	_dirt(p, roundi(26 * s), 2.0 * s)
	# 솟는 먼지 기둥(위로 좁게).
	for i: int in roundi(12 * s):
		var h := Vector2.from_angle(_rng.randf() * TAU) * _rng.randf_range(0.5, 4.0) * s
		var v := Vector3(h.x, _rng.randf_range(8.0, 26.0) * s, h.y)
		_emit(Layer.SMOKE, p + Vector3.UP, v, _rng.randf_range(3.5, 6.5), 3.0 * s, _rng.randf_range(10.0, 16.0) * s,
			Color(0.3, 0.25, 0.19, 0.95), Color(0.52, 0.49, 0.44, 0.0), 0.9, 0.0)
	# 땅을 따라 퍼지는 먼지 고리.
	var ring_n: int = roundi(16 * s)
	for i: int in ring_n:
		var ang: float = TAU * float(i) / float(ring_n) + _rng.randf_range(-0.15, 0.15)
		var v := Vector3(cos(ang), 0.0, sin(ang)) * _rng.randf_range(20.0, 32.0) * s + Vector3.UP * _rng.randf_range(0.5, 2.5)
		_emit(Layer.SMOKE, p + Vector3.UP * 1.5, v, _rng.randf_range(3.0, 5.0), 3.5 * s, 13.0 * s,
			Color(0.48, 0.43, 0.35, 0.75), Color(0.58, 0.55, 0.5, 0.0), 1.5, 0.0)
	# 오래 남는 회색 연기.
	_burst(Layer.SMOKE, p + Vector3.UP * 6.0, 4, 0.5, 2.5, 8.0 * s, 24.0 * s, Color(0.36, 0.35, 0.33, 0.45),
		Color(0.45, 0.45, 0.44, 0.0), 12.0, 18.0, 0.4, -0.4)
	_light(p + Vector3.UP * 3.0, 10.0 * s, 110.0 * s, 0.35)
	_scorch(pos2, 9.0 * s)


func _artillery_muzzle(src: Element, pos2: Vector2) -> void:
	var p: Vector3 = _ground(pos2, 3.0)
	var mortar: bool = src.def.element_class == &"mortar" or src.def.element_class == &"inf_squad"
	var s: float = 0.6 if mortar else 1.0
	_flash(p + Vector3.UP * 2.0, 7.0 * s, 0.12)
	_muzzle_smoke(p + Vector3.UP * 2.0, Vector3.UP, roundi(6 * s) + 1, 1.6 * s)
	_ground_blast(pos2, roundi(8 * s), 1.2 * s)
	_light(p, 4.0, 50.0 * s, 0.15)


## 연막탄 한 발: 터지는 순간 흰 연기 뭉치(차장은 battle.smokes 를 따라 _update_screens 가 유지).
func _smoke_shell_burst(pos2: Vector2) -> void:
	var p: Vector3 = _ground(pos2, 2.0)
	_flash(p, 4.0, 0.1)
	_burst(Layer.SMOKE, p, 6, 3.0, 8.0, 4.0, 14.0, Color(0.9, 0.9, 0.9, 0.85), Color(0.86, 0.87, 0.88, 0.0), 3.0, 6.0, 0.5, -0.3)


# --- 효과 부품 -----------------------------------------------------------

func _flash(p: Vector3, size: float, life: float) -> void:
	_emit(Layer.FIRE, p, Vector3.ZERO, life, size, size * 1.3, Color(1.0, 0.85, 0.55, 1.0), Color(1.0, 0.45, 0.1, 0.0), 0.0, 0.0)


func _fireball(p: Vector3, s: float) -> void:
	for i: int in roundi(6 * s) + 2:
		var v := _rand_dir(0.3) * _rng.randf_range(3.0, 9.0) * s + Vector3.UP * 3.0 * s
		_emit(Layer.FIRE, p + _rand_dir(0.0) * 1.5 * s, v, _rng.randf_range(0.5, 1.0) * maxf(s, 0.6),
			2.5 * s, 7.0 * s, Color(1.0, 0.7, 0.3, 1.0), Color(0.55, 0.12, 0.02, 0.0), 1.5, -2.0)


func _sparks(p: Vector3, n: int, s: float) -> void:
	for i: int in n:
		var v := _rand_dir(0.1) * _rng.randf_range(12.0, 30.0) * s
		_emit(Layer.FIRE, p, v, _rng.randf_range(0.3, 0.8), 0.7, 0.25, Color(1.0, 0.9, 0.6, 1.0), Color(1.0, 0.4, 0.1, 0.0), 0.2, 20.0)


## 흙 덩어리: 위로 튀었다가 떨어지는 어두운 입자.
func _dirt(p: Vector3, n: int, s: float) -> void:
	for i: int in n:
		var horiz := Vector2.from_angle(_rng.randf() * TAU) * _rng.randf_range(2.0, 9.0) * s
		var v := Vector3(horiz.x, _rng.randf_range(16.0, 32.0) * sqrt(s), horiz.y)
		_emit(Layer.SMOKE, p, v, _rng.randf_range(1.4, 2.4), 1.4 * s, 3.6 * s, Color(0.16, 0.12, 0.09, 1.0),
			Color(0.3, 0.26, 0.2, 0.0), 0.15, 24.0)


## 포구 연기: dir 쪽으로 밀려 나가 퍼지는 회색 덩어리.
func _muzzle_smoke(p: Vector3, dir: Vector3, n: int, s: float) -> void:
	for i: int in n:
		var v := dir * _rng.randf_range(4.0, 12.0) * s + _rand_dir(0.0) * 2.0
		_emit(Layer.SMOKE, p, v, _rng.randf_range(2.0, 3.5) * s, 1.5 * s, 7.0 * s, Color(0.6, 0.58, 0.55, 0.6),
			Color(0.66, 0.65, 0.63, 0.0), 1.4, -0.3)


## 포 발사 충격파로 땅에서 이는 먼지 고리.
func _ground_blast(pos2: Vector2, n: int, s: float) -> void:
	var p: Vector3 = _ground(pos2, 0.8)
	for i: int in n:
		var ang: float = TAU * float(i) / float(n) + _rng.randf_range(-0.3, 0.3)
		var v := Vector3(cos(ang), 0.15, sin(ang)) * _rng.randf_range(7.0, 12.0) * s
		_emit(Layer.SMOKE, p, v, _rng.randf_range(1.4, 2.4), 2.5 * s, 8.0 * s, Color(0.55, 0.5, 0.41, 0.5),
			Color(0.6, 0.57, 0.52, 0.0), 1.8, 0.0)


func _burst(layer: Layer, p: Vector3, n: int, v_min: float, v_max: float, size0: float, size1: float, c0: Color, c1: Color,
		life_min: float, life_max: float, drag: float, grav: float) -> void:
	for i: int in n:
		var v := _rand_dir(0.0) * _rng.randf_range(v_min, v_max)
		_emit(layer, p, v, _rng.randf_range(life_min, life_max), size0, size1, c0, c1, drag, grav)


func _burst_dir(layer: Layer, p: Vector3, dir: Vector3, n: int, v_min: float, v_max: float, size0: float, size1: float,
		c0: Color, c1: Color, life_min: float, life_max: float) -> void:
	for i: int in n:
		var v := (dir + _rand_dir(-1.0) * 0.2).normalized() * _rng.randf_range(v_min, v_max)
		_emit(layer, p, v, _rng.randf_range(life_min, life_max), size0, size1, c0, c1, 0.0, 0.0)


## 무작위 단위 벡터. min_up 이상으로 위쪽(y)을 향하게(-1 = 제한 없음).
func _rand_dir(min_up: float) -> Vector3:
	var y: float = _rng.randf_range(maxf(min_up, -1.0), 1.0)
	var ang: float = _rng.randf() * TAU
	var r: float = sqrt(maxf(1.0 - y * y, 0.0))
	return Vector3(cos(ang) * r, y, sin(ang) * r)


func _emit(layer: Layer, p: Vector3, v: Vector3, life: float, size0: float, size1: float, c0: Color, c1: Color,
		drag: float, grav: float) -> void:
	var l: ParticleLayer = _layers[layer]
	if l.count >= l.cap:
		return
	var i: int = l.count
	l.count += 1
	l.pos[i] = p
	l.vel[i] = v
	l.age[i] = 0.0
	l.life[i] = maxf(life, 0.01)
	l.size0[i] = size0
	l.size1[i] = size1
	l.drag[i] = drag
	l.grav[i] = grav
	l.rot[i] = _rng.randf() * TAU
	l.spin[i] = _rng.randf_range(-0.6, 0.6)
	l.seeds[i] = _rng.randf()
	l.c0[i] = c0
	l.c1[i] = c1


func _scorch(pos2: Vector2, radius: float) -> void:
	var i: int = _scorch_next
	_scorch_next = (_scorch_next + 1) % _scorch_mm.instance_count
	var h: float = battle.terrain.ground_height(pos2)
	for o: Vector2 in [Vector2(radius, 0), Vector2(-radius, 0), Vector2(0, radius), Vector2(0, -radius)]:
		h = maxf(h, battle.terrain.ground_height(pos2 + o))
	var b := Basis(Vector3.UP, _rng.randf() * TAU).scaled(Vector3(radius, 1.0, radius))
	_scorch_mm.set_instance_transform(i, Transform3D(b, ViewCoords.to_3d(pos2, h + 0.25)))
	_scorch_mm.set_instance_custom_data(i, Color(0.0, _rng.randf(), 0.0, 0.0))
	_scorch_ages[i] = 0.0


func _light(p: Vector3, energy: float, light_range: float, life: float) -> void:
	if lite:
		return
	var lf: LightFlash
	if _lights.size() >= 6:
		lf = _lights.pop_front()
	else:
		lf = LightFlash.new()
		lf.light = OmniLight3D.new()
		lf.light.light_color = Color(1.0, 0.65, 0.3)
		lf.light.shadow_enabled = false
		add_child(lf.light)
	lf.light.position = p + Vector3.UP * 2.0
	lf.light.omni_range = light_range
	lf.light.light_energy = energy
	lf.light.visible = true
	lf.energy = energy
	lf.life = life
	lf.age = 0.0
	_lights.append(lf)


func _ground(p: Vector2, h: float) -> Vector3:
	return ViewCoords.to_3d(p, battle.terrain.ground_height(p) + h)


func _visible(e: Element) -> bool:
	if fog_side == &"" or e.side == fog_side:
		return true
	return battle.detection.seen_recently(fog_side, e.id, battle.tick())


# --- 매 프레임 ------------------------------------------------------------

func _process(delta: float) -> void:
	if battle == null:
		return
	var dt: float = 0.0 if battle.world.paused else delta * battle.world.get_speed()
	_update_projectiles(dt)
	if dt <= 0.0:
		return
	_update_wrecks(dt)
	_update_screens(dt)
	_update_lights(dt)
	_update_scorch(dt)
	for l: ParticleLayer in _layers:
		l.step(dt)
		l.upload()


func _update_projectiles(dt: float) -> void:
	_line_mesh.clear_surfaces()
	var keep: Array[Projectile] = []
	var drawing: bool = false
	for pr: Projectile in _projectiles:
		pr.age += dt
		var k: float = pr.age / pr.dur
		if k >= 1.0:
			if pr.show_impact:
				_impact(pr)
			continue
		keep.append(pr)
		var head: Vector3 = pr.a.lerp(pr.b, k)
		if pr.weapon == &"SAM" or pr.weapon == &"ATGM":
			# 미사일: 위쪽으로 휜 궤적, 밝은 머리 + 연기 꼬리.
			if pr.weapon == &"SAM":
				head = pr.a.lerp(pr.b, k * k)
			else:
				head.y += sin(k * PI) * minf(pr.a.distance_to(pr.b) * 0.01, 12.0)
			if dt > 0.0:
				_emit(Layer.FIRE, head, Vector3.ZERO, 0.05, 1.8, 1.2, Color(1.0, 0.9, 0.7, 1.0), Color(1.0, 0.6, 0.2, 0.0), 0.0, 0.0)
				pr.trail_acc += dt * 35.0
				while pr.trail_acc >= 1.0:
					pr.trail_acc -= 1.0
					_emit(Layer.SMOKE, head, _rand_dir(-1.0) * 0.6, _rng.randf_range(1.8, 3.0), 0.8, 3.5,
						Color(0.85, 0.85, 0.83, 0.55), Color(0.8, 0.8, 0.8, 0.0), 1.0, -0.2)
			continue
		# 예광탄: 짧은 선 + 밝은 머리.
		if not drawing:
			_line_mesh.surface_begin(Mesh.PRIMITIVE_LINES)
			drawing = true
		var tail: Vector3 = pr.a.lerp(pr.b, maxf(0.0, k - pr.tail))
		var c: Color = pr.color
		_line_mesh.surface_set_color(Color(c.r, c.g, c.b, 0.0))
		_line_mesh.surface_add_vertex(tail)
		_line_mesh.surface_set_color(c)
		_line_mesh.surface_add_vertex(head)
		if dt > 0.0 and pr.weapon != &"MG":
			var glow: float = 1.6 if pr.weapon == &"AC" else 2.6
			_emit(Layer.FIRE, head, Vector3.ZERO, 0.04, glow, glow, Color(c.r, c.g, c.b, 0.9), Color(c.r, c.g, c.b, 0.0), 0.0, 0.0)
	if drawing:
		_line_mesh.surface_end()
	_projectiles = keep


func _update_wrecks(dt: float) -> void:
	var keep: Array[Wreck] = []
	var smoke_scale: float = 0.5 if lite else 1.0
	for w: Wreck in _wrecks:
		w.age += dt
		if w.age >= WRECK_SEC:
			continue
		keep.append(w)
		# 처음 20초는 활활, 45초까지 잦아든다. 연기는 40초 뒤부터 옅어진다.
		var fire_rate: float = 10.0 * clampf(1.0 - (w.age - 20.0) / 25.0, 0.0, 1.0)
		var smoke_rate: float = 3.2 * smoke_scale * clampf(1.0 - (w.age - 40.0) / 80.0, 0.15, 1.0)
		w.fire_acc += fire_rate * dt
		while w.fire_acc >= 1.0:
			w.fire_acc -= 1.0
			var off := Vector3(_rng.randf_range(-1.5, 1.5), 0.0, _rng.randf_range(-1.5, 1.5))
			_emit(Layer.FIRE, w.pos + off, Vector3(0, _rng.randf_range(2.0, 4.5), 0), _rng.randf_range(0.35, 0.7),
				_rng.randf_range(1.2, 2.2), 0.4, Color(1.0, 0.62, 0.22, 0.9), Color(0.8, 0.18, 0.04, 0.0), 0.5, -1.0)
		w.smoke_acc += smoke_rate * dt
		while w.smoke_acc >= 1.0:
			w.smoke_acc -= 1.0
			var off2 := Vector3(_rng.randf_range(-1.0, 1.0), 1.5, _rng.randf_range(-1.0, 1.0))
			_emit(Layer.SMOKE, w.pos + off2, Vector3(0, _rng.randf_range(3.0, 5.0), 0), _rng.randf_range(6.0, 9.0),
				2.2, _rng.randf_range(11.0, 16.0), Color(0.06, 0.055, 0.05, 0.75), Color(0.24, 0.23, 0.22, 0.0), 0.35, -0.6)
	_wrecks = keep


## 연막 차장: 시뮬의 battle.smokes(시야를 실제로 막는 원)를 따라 흰 연기 덩어리를 계속 채운다.
func _update_screens(dt: float) -> void:
	var tick: int = battle.tick()
	var alive_ids: Dictionary[int, bool] = {}
	for sm: Los.Smoke in battle.smokes:
		if sm.until_tick <= tick:
			continue
		var key: int = sm.get_instance_id()
		alive_ids[key] = true
		var em: ScreenEmitter = _screens[key] if _screens.has(key) else null
		if em == null:
			em = ScreenEmitter.new()
			em.smoke = sm
			em.center = _ground(sm.pos, 0.0)
			_screens[key] = em
		var remain_sec: float = float(sm.until_tick - tick) / float(SimConst.TICK_RATE)
		# 목표 덩어리 수 ÷ 평균 수명 = 초당 생성. 끝나기 15초 전부터 줄인다.
		var target: float = clampf(sm.radius * 0.9, 16.0, 50.0) * (0.6 if lite else 1.0)
		var rate: float = target / 16.0 * clampf(remain_sec / 15.0, 0.0, 1.0)
		if not em.started:
			em.started = true
			em.acc = target * 0.7
		em.acc += rate * dt
		while em.acc >= 1.0:
			em.acc -= 1.0
			var off := Vector2.from_angle(_rng.randf() * TAU) * sqrt(_rng.randf()) * sm.radius * 0.85
			var gp: Vector2 = sm.pos + off
			var p: Vector3 = _ground(gp, _rng.randf_range(3.0, 10.0))
			var shade: float = _rng.randf_range(0.8, 0.92)
			var size1: float = clampf(sm.radius * 0.55, 14.0, 28.0) * _rng.randf_range(0.8, 1.2)
			_emit(Layer.SMOKE, p, Vector3(0, _rng.randf_range(0.2, 0.8), 0), _rng.randf_range(13.0, 19.0),
				size1 * 0.5, size1, Color(shade, shade, shade + 0.01, 0.8), Color(shade, shade, shade, 0.0), 0.08, -0.03)
	for key: int in _screens.keys():
		if not alive_ids.has(key):
			_screens.erase(key)


func _update_lights(dt: float) -> void:
	var keep: Array[LightFlash] = []
	for lf: LightFlash in _lights:
		lf.age += dt
		if lf.age >= lf.life:
			lf.light.visible = false
			keep.append(lf)  # 재사용
			continue
		lf.light.light_energy = lf.energy * (1.0 - lf.age / lf.life)
		keep.append(lf)
	_lights = keep


func _update_scorch(dt: float) -> void:
	for i: int in _scorch_ages.size():
		var a: float = _scorch_ages[i]
		if a >= SCORCH_SEC:
			continue
		a += dt
		_scorch_ages[i] = a
		_scorch_mm.set_instance_color(i, Color(0, 0, 0, clampf(1.0 - (a - SCORCH_SEC * 0.6) / (SCORCH_SEC * 0.4), 0.0, 1.0)))
