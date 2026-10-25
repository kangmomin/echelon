extends GutTest
## 데이터 로더·스키마 검증 테스트. PLAN 16장.


func _has_message(messages: PackedStringArray, needle: String) -> bool:
	for m: String in messages:
		if m.contains(needle):
			return true
	return false


func test_project_data_loads_without_errors() -> void:
	var cat: DataCatalog = DataLoader.load_catalog()
	for e: String in cat.errors:
		gut.p("data error: " + e)
	assert_true(cat.is_valid(), "data/ 오류 0개")
	assert_gt(cat.elements.size(), 0)
	assert_gt(cat.weapons.size(), 0)
	assert_gt(cat.formations.size(), 0)
	assert_gt(cat.toe_templates.size(), 0)
	assert_gt(cat.scenarios.size(), 0)


func test_loaded_element_fields_are_typed() -> void:
	var cat: DataCatalog = DataLoader.load_catalog()
	var tank: ElementDef = cat.get_element(&"us_m1a2_sepv3")
	assert_not_null(tank)
	assert_eq(tank.element_class, &"tank")
	assert_eq(tank.get_armor(&"front"), 900.0)
	assert_eq(tank.ammo[&"m256_apfsds"], 22)
	assert_true(tank.weapons.has("m240_coax"))


func test_loaded_scenario_structure() -> void:
	var s: ScenarioDef = DataLoader.load_catalog().get_scenario(&"skirmish_01")
	assert_not_null(s)
	assert_eq(s.sides.size(), 2)
	assert_eq(s.sides[&"blue"].toe, &"us_armor_bn_tf")
	assert_eq(s.control_points.size(), 4)
	assert_eq(s.control_points[0].id, &"cp_fox")
	assert_eq(s.control_points[0].pos, Vector2(2000, 2000), "FOX 는 지도 중앙")
	assert_almost_eq(s.collapse_threshold, 0.25, 0.0001)
	assert_eq(s.objectives[0].point, &"cp_fox")


func test_missing_required_field_is_reported() -> void:
	var r := DefReader.new()
	ElementDef.from_dict({}, r)
	assert_true(r.has_errors())
	assert_true(_has_message(r.errors, "'id'"), "id 누락 보고")
	assert_true(_has_message(r.errors, "'armor'"), "armor 누락 보고")


func test_wrong_type_is_reported() -> void:
	var r := DefReader.new()
	r.req_number({"hp": "abc"}, "hp")
	assert_true(_has_message(r.errors, "'hp'는 숫자여야 함"))


func test_non_integer_is_reported_for_int_field() -> void:
	var r := DefReader.new()
	assert_eq(r.req_int({"crew": 3.5}, "crew"), 3)
	assert_true(r.has_errors())


func test_out_of_range_is_reported() -> void:
	var r := DefReader.new()
	var w: Dictionary = {
		"id": "bad", "type": "KE", "range_m": {"min": 100, "max": 50}, "penetration": 10, "damage": 1,
		"accuracy": {"base": 1.5, "at_max_range": 0.1}, "rof_rpm": 1, "suppression": 1,
		"fire_on_move": true, "targets": ["vehicle"]}
	WeaponDef.from_dict(w, r)
	assert_true(_has_message(r.errors, "range_m.max"))
	assert_true(_has_message(r.errors, "accuracy.base"))


func test_formation_slot_count_mismatch_is_reported() -> void:
	var r := DefReader.new()
	FormationDef.from_dict({
		"id": "bad", "echelon": "platoon",
		"slots": {"4": [[0, 0], [1, 1]]},
		"mods": {"speed": 1, "front_fire": 1, "flank_security": 1, "arty_vuln": 1, "control": 1, "transition_sec": 10},
	}, r)
	assert_true(_has_message(r.errors, "slots[4]"))


func test_element_with_unknown_weapon_fails_reference_check() -> void:
	var cat := DataCatalog.new()
	var e := ElementDef.new()
	e.id = &"ghost"
	e.weapons = PackedStringArray(["no_such_gun"])
	cat.elements[e.id] = e
	var r := DefReader.new()
	DataLoader.validate_references(cat, r)
	assert_true(_has_message(r.errors, "no_such_gun"))


func test_scenario_with_unknown_toe_fails_reference_check() -> void:
	var cat: DataCatalog = DataLoader.load_catalog()
	var s: ScenarioDef = cat.get_scenario(&"skirmish_01")
	s.sides[&"red"].toe = &"no_such_toe"
	var r := DefReader.new()
	DataLoader.validate_references(cat, r)
	assert_true(_has_message(r.errors, "no_such_toe"))


func test_malformed_json_is_reported_not_crashing() -> void:
	var dir: String = "user://test_bad_data"
	DirAccess.make_dir_recursive_absolute(dir.path_join("weapons"))
	var f: FileAccess = FileAccess.open(dir.path_join("weapons/broken.json"), FileAccess.WRITE)
	f.store_string("{ \"id\": \"x\", ")
	f.close()
	var cat: DataCatalog = DataLoader.load_catalog(dir)
	assert_false(cat.is_valid())
	assert_true(_has_message(cat.errors, "JSON 파싱 오류"))
	DirAccess.remove_absolute(dir.path_join("weapons/broken.json"))
