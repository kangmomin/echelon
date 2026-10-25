extends SceneTree

const OUT_DIR: String = "res://build/unity_oracle"


func _init() -> void:
	DirAccess.make_dir_recursive_absolute(OUT_DIR)
	var cat: DataCatalog = DataLoader.load_catalog()
	if not cat.is_valid():
		push_error("데이터 오류: %s" % str(cat.errors))
		quit(1)
		return
	var terrain: Terrain = Terrain.build(cat.get_map(&"map01"))
	var pf: GridPathfinder = GridPathfinder.new(terrain)
	var arrays: Dictionary = {}
	arrays["types"] = _write_bytes("types", terrain.types)
	arrays["heights"] = _write_floats("heights", terrain.heights)
	arrays["slopes"] = _write_floats("slopes", terrain.slopes)
	arrays["passable_vehicle"] = _write_bytes("passable_vehicle", terrain.passable_vehicle)
	arrays["passable_foot"] = _write_bytes("passable_foot", terrain.passable_foot)
	arrays["clearance_vehicle"] = _write_bytes("clearance_vehicle", terrain.clearance_vehicle)
	arrays["bridge_mask"] = _write_bytes("bridge_mask", terrain.bridge_mask)
	var samples: Array[Dictionary] = []
	for p: Vector2 in [Vector2(1100, 1140), Vector2(2400, 1200), Vector2(1300, 2550),
		Vector2(3100, 1650), Vector2(1960, 1960), Vector2(2000, 2000), Vector2(1825, 2410),
		Vector2(3000, 3500), Vector2(900, 1950), Vector2(900, 1350)]:
		var c: Vector2i = terrain.cell_of(p)
		var idx: int = terrain.index_of(c)
		samples.append({"pos": [p.x, p.y], "type": terrain.type_at(p), "height": terrain.ground_height(p),
			"slope": terrain.slope_at_cell(c), "vehicle": terrain.is_passable(p, Terrain.MOBILITY_VEHICLE),
			"foot": terrain.is_passable(p, Terrain.MOBILITY_FOOT), "bridge": terrain.is_bridge(p),
			"clearance": terrain.clearance_vehicle[idx]})
	var path_cases: Array[Dictionary] = []
	path_cases.append(_path_case(pf, "river_bridge", Vector2(1600, 600), Vector2(1600, 1700), &"vehicle", &"blue"))
	path_cases.append(_path_case(pf, "open_simplify", Vector2(2600, 3300), Vector2(3600, 3600), &"vehicle", &"blue"))
	path_cases.append(_path_case(pf, "road_forest", Vector2(1200, 2250), Vector2(1450, 2750), &"vehicle", &"blue"))
	path_cases.append(_path_case(pf, "foot_path", Vector2(1600, 600), Vector2(1600, 1700), &"foot", &"blue"))
	path_cases.append(_path_case(pf, "threat_before", Vector2(2600, 3400), Vector2(3600, 3400), &"vehicle", &"red"))
	pf.add_threat(&"red", Vector2(3100, 3400), 200.0, 4.0)
	path_cases.append(_path_case(pf, "threat_after", Vector2(2600, 3400), Vector2(3600, 3400), &"vehicle", &"red"))
	path_cases.append(_path_case(pf, "threat_other_side", Vector2(2600, 3400), Vector2(3600, 3400), &"vehicle", &"blue"))
	pf.decay_threat(0.0)
	var metadata: Dictionary = {"map": "map01", "width": terrain.width, "height": terrain.height_cells,
		"cell_m": terrain.cell_m, "size_m": [terrain.size_m.x, terrain.size_m.y], "arrays": arrays,
		"samples": samples, "path_cases": path_cases}
	var f: FileAccess = FileAccess.open(OUT_DIR + "/map01.json", FileAccess.WRITE)
	if f == null:
		push_error("기준 파일을 쓸 수 없음")
		quit(1)
		return
	f.store_string(JSON.stringify(metadata, "  "))
	f.close()
	print("[unity-oracle] map01 arrays=%d paths=%d -> %s" % [arrays.size(), path_cases.size(), OUT_DIR])
	quit(0)


func _write_bytes(name: String, values: PackedByteArray) -> Dictionary:
	var f: FileAccess = FileAccess.open(OUT_DIR + "/" + name + ".bin", FileAccess.WRITE)
	f.store_buffer(values)
	f.close()
	return {"file": name + ".bin", "count": values.size(), "sha256": _sha256(values)}


func _write_floats(name: String, values: PackedFloat32Array) -> Dictionary:
	var bytes: PackedByteArray = values.to_byte_array()
	return _write_bytes(name, bytes).merged({"count": values.size()}, true)


func _sha256(bytes: PackedByteArray) -> String:
	var ctx := HashingContext.new()
	ctx.start(HashingContext.HASH_SHA256)
	ctx.update(bytes)
	return ctx.finish().hex_encode()


func _path_case(pf: GridPathfinder, name: String, from: Vector2, to: Vector2,
		mobility: StringName, side: StringName) -> Dictionary:
	var terrain: Terrain = pf.terrain
	var start: Vector2 = terrain.nearest_passable(from, mobility)
	var goal: Vector2 = terrain.nearest_passable(to, mobility)
	var grid: AStarGrid2D = pf._grid(side, mobility)
	var raw: PackedVector2Array = grid.get_point_path(terrain.cell_of(start), terrain.cell_of(goal), true)
	var simplified: PackedVector2Array = pf.find_path_now(from, to, mobility, side)
	var raw_cells: Array = []
	var raw_points: Array = []
	for p: Vector2 in raw:
		var c: Vector2i = terrain.cell_of(p)
		raw_cells.append([c.x, c.y])
		raw_points.append([p.x, p.y])
	var final_points: Array = []
	for p: Vector2 in simplified:
		final_points.append([p.x, p.y])
	return {"name": name, "from": [from.x, from.y], "to": [to.x, to.y], "mobility": String(mobility),
		"side": String(side), "raw_cells": raw_cells, "raw_points": raw_points, "simplified": final_points}
