extends SceneTree
## 장비 모델 확인용: 모든 장비 모델을 격자로 늘어놓고 PNG 로 저장한다(렌더러 필요).
## 사용: $GODOT --path . -s res://tools/model_gallery.gd -- --out=/tmp/gallery.png [--yaw=35]

const IDS: Array[String] = ["us_m1a2_sepv3", "us_m1a2_sepv3_tusk", "us_m2a3", "us_m3a3", "us_m7_fist", "us_m1064",
	"us_m1068_cp", "us_hemtt", "us_avenger", "us_jtac", "us_inf_squad", "ru_t90m", "ru_t72b3", "ru_bmp3", "ru_brm3k",
	"ru_bmp2m", "ru_fo_team", "ru_bmp1ksh_cp", "ru_2s9", "ru_tor_m2", "ru_kamaz", "ru_r330zh"]

var _out: String = "user://gallery.png"
var _yaw: float = 35.0
var _frames: int = 0
## 이 줄(0부터)만 가까이서 본다(-1 = 전체).
var _row: int = -1


func _initialize() -> void:
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--out="):
			_out = arg.get_slice("=", 1)
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
	var cols: int = 6
	for i: int in IDS.size():
		var def: ElementDef = catalog.get_element(StringName(IDS[i]))
		if def == null:
			continue
		var m: VehicleModels.Model = VehicleModels.model_for(def)
		var holder := Node3D.new()
		holder.position = Vector3((i % cols) * 16.0, 0.0, (i / cols) * 18.0)
		holder.rotation_degrees.y = _yaw
		root3d.add_child(holder)
		var side: StringName = &"blue" if IDS[i].begins_with("us") else &"red"
		var mats: Dictionary[StringName, Color] = {&"camo": ElementView.CAMO_COLORS[side].lerp(ElementView.SIDE_COLORS[side], 0.15),
			&"dark": ElementView.DARK_COLOR, &"marker": ElementView.SIDE_COLORS[side]}
		var hull := MeshInstance3D.new()
		hull.mesh = m.hull
		_paint(hull, m.hull_roles, mats)
		holder.add_child(hull)
		if m.turret != null:
			var t := MeshInstance3D.new()
			t.mesh = m.turret
			t.position = m.turret_pivot
			_paint(t, m.turret_roles, mats)
			holder.add_child(t)
	var cam := Camera3D.new()
	cam.fov = 40.0
	root3d.add_child(cam)
	if _row >= 0:
		cam.look_at_from_position(Vector3(40, 16, _row * 18.0 + 30.0), Vector3(40, 0, _row * 18.0))
	else:
		cam.look_at_from_position(Vector3(40, 55, 95), Vector3(40, 0, 27))


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
