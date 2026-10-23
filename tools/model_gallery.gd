extends SceneTree
## 장비 모델 확인용: 모든 장비 모델을 격자로 늘어놓고 PNG 로 저장한다(렌더러 필요).
## 사용: $GODOT --path . -s res://tools/model_gallery.gd -- --out=/tmp/gallery.png [--yaw=35]


var _out: String = "user://gallery.png"
var _yaw: float = 35.0
var _frames: int = 0
## 이 줄(0부터)만 가까이서 본다(-1 = 전체).
var _row: int = -1
## 이 문자열이 든 장비만(예: --only=ah64).
var _only: String = ""


func _initialize() -> void:
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--out="):
			_out = arg.get_slice("=", 1)
		elif arg.begins_with("--only="):
			_only = arg.get_slice("=", 1)
		elif arg.begins_with("--row="):
			_row = arg.get_slice("=", 1).to_int()
		elif arg.begins_with("--yaw="):
			_yaw = arg.get_slice("=", 1).to_float()
	var catalog: DataCatalog = DataLoader.load_catalog()
	var root3d := Node3D.new()
	get_root().add_child(root3d)
	var env := WorldEnvironment.new()
	env.environment = Environment.new()
	env.environment.background_mode = Environment.BG_COLOR
	env.environment.background_color = Color(0.55, 0.6, 0.55)
	env.environment.ambient_light_color = Color(0.6, 0.6, 0.6)
	env.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	root3d.add_child(env)
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-50, 30, 0)
	root3d.add_child(sun)
	var ids: Array = catalog.elements.keys()
	ids.sort()
	if _only != "":
		ids = ids.filter(func(x: StringName) -> bool: return String(x).contains(_only))
	var cols: int = 8
	for i: int in ids.size():
		var def: ElementDef = catalog.get_element(ids[i])
		if def == null:
			continue
		var m: VehicleModels.Model = VehicleModels.model_for(def)
		var holder := Node3D.new()
		holder.position = Vector3((i % cols) * 16.0, 4.0 if def.element_class == &"heli" else 0.0, floori(i / float(cols)) * 18.0)
		holder.rotation_degrees.y = _yaw
		root3d.add_child(holder)
		var side: StringName = &"blue" if String(ids[i]).begins_with("us") else &"red"
		var mats: Dictionary[StringName, Color] = {&"camo": ElementView.CAMO_COLORS[side].lerp(ElementView.SIDE_COLORS[side], 0.15),
			&"dark": ElementView.DARK_COLOR, &"marker": ElementView.SIDE_COLORS[side]}
		var hull := MeshInstance3D.new()
		hull.mesh = m.hull
		_paint(hull, m.hull_roles, mats)
		holder.add_child(hull)
		for rp: Array in [[m.rotor, m.rotor_pivot], [m.rotor2, m.rotor2_pivot]]:
			if rp[0] != null:
				var rmi := MeshInstance3D.new()
				rmi.mesh = rp[0]
				rmi.position = rp[1]
				holder.add_child(rmi)
		if m.turret != null:
			var t := MeshInstance3D.new()
			t.mesh = m.turret
			t.position = m.turret_pivot
			_paint(t, m.turret_roles, mats)
			holder.add_child(t)
	var cam := Camera3D.new()
	cam.fov = 40.0
	root3d.add_child(cam)
	if _only != "":
		var k: int = mini(ids.size(), cols)
		var cx: float = (k - 1) * 8.0
		cam.look_at_from_position(Vector3(cx + 4.0, 9.0 + k * 1.5, 14.0 + k * 3.0), Vector3(cx, 2.0, 0.0))
	elif _row >= 0:
		cam.look_at_from_position(Vector3(56, 14, _row * 18.0 + 26.0), Vector3(56, 0, _row * 18.0))
	else:
		cam.look_at_from_position(Vector3(56, 70, 120), Vector3(56, 0, 40))


func _paint(mi: MeshInstance3D, roles: Array[StringName], mats: Dictionary[StringName, Color]) -> void:
	for i: int in roles.size():
		var mat := StandardMaterial3D.new()
		mat.albedo_color = mats[roles[i]]
		mat.roughness = 0.9
		mi.set_surface_override_material(i, mat)


func _process(_delta: float) -> bool:
	_frames += 1
	if _frames == 8:
		var img: Image = get_root().get_texture().get_image()
		img.save_png(_out)
		print("[gallery] ", _out)
		return true
	return false
