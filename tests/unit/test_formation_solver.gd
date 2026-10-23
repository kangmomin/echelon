extends GutTest
## 대형 슬롯 계산 테스트. PLAN 17.1, 18.3.

const EPS: float = 0.001

var catalog: DataCatalog


func before_all() -> void:
	catalog = DataLoader.load_catalog()


func _wedge() -> FormationDef:
	return catalog.get_formation(&"wedge")


func _assert_points_near(got: PackedVector2Array, expected: PackedVector2Array) -> void:
	assert_eq(got.size(), expected.size(), "슬롯 수")
	for i: int in mini(got.size(), expected.size()):
		assert_almost_eq(got[i].x, expected[i].x, EPS, "slot %d x" % i)
		assert_almost_eq(got[i].y, expected[i].y, EPS, "slot %d y" % i)


## M0 완료 기준: 쐐기 4슬롯 좌표가 PLAN 16.3 과 일치한다.
func test_wedge_4_local_offsets_match_plan() -> void:
	assert_not_null(_wedge(), "data/formations.json 에 wedge 가 있어야 함")
	var got: PackedVector2Array = FormationSolver.local_offsets(_wedge(), 4)
	_assert_points_near(got, PackedVector2Array([
		Vector2(0, 0), Vector2(-60, -50), Vector2(60, -50), Vector2(0, -100)]))


func test_heading_north_is_pure_translation() -> void:
	var origin := Vector2(1000, 2000)
	var got: PackedVector2Array = FormationSolver.world_slots(_wedge(), 4, origin, 0.0)
	_assert_points_near(got, PackedVector2Array([
		Vector2(1000, 2000), Vector2(940, 1950), Vector2(1060, 1950), Vector2(1000, 1900)]))


## 동쪽(heading 90°)을 보면 로컬 오른쪽은 남쪽, 로컬 뒤쪽은 서쪽이다.
func test_heading_east_rotates_clockwise() -> void:
	var got: PackedVector2Array = FormationSolver.world_slots(_wedge(), 4, Vector2.ZERO, deg_to_rad(90.0))
	_assert_points_near(got, PackedVector2Array([
		Vector2(0, 0), Vector2(-50, 60), Vector2(-50, -60), Vector2(-100, 0)]))


func test_world_to_local_inverts_local_to_world() -> void:
	var origin := Vector2(123, -45)
	var heading: float = deg_to_rad(37.0)
	var local := Vector2(-60, -50)
	var world: Vector2 = FormationSolver.local_to_world(local, origin, heading)
	var back: Vector2 = FormationSolver.world_to_local(world, origin, heading)
	assert_almost_eq(back.x, local.x, EPS)
	assert_almost_eq(back.y, local.y, EPS)


func test_heading_from_vector() -> void:
	assert_almost_eq(FormationSolver.heading_from_vector(Vector2(0, 1)), 0.0, EPS, "북")
	assert_almost_eq(FormationSolver.heading_from_vector(Vector2(1, 0)), PI / 2.0, EPS, "동")
	assert_almost_eq(absf(FormationSolver.heading_from_vector(Vector2(0, -1))), PI, EPS, "남")


func test_forest_halves_spacing() -> void:
	var got: PackedVector2Array = FormationSolver.local_offsets(_wedge(), 4, &"forest")
	_assert_points_near(got, PackedVector2Array([
		Vector2(0, 0), Vector2(-30, -25), Vector2(30, -25), Vector2(0, -50)]))


func test_unknown_terrain_uses_scale_one() -> void:
	var got: PackedVector2Array = FormationSolver.local_offsets(_wedge(), 4, &"swamp")
	_assert_points_near(got, FormationSolver.local_offsets(_wedge(), 4, &"open"))


func test_three_elements_use_three_slot_set() -> void:
	var got: PackedVector2Array = FormationSolver.local_offsets(_wedge(), 3)
	_assert_points_near(got, PackedVector2Array([Vector2(0, 0), Vector2(-60, -50), Vector2(60, -50)]))


func _synthetic_def() -> FormationDef:
	var f := FormationDef.new()
	f.id = &"test"
	f.echelon = &"platoon"
	f.slots[4] = PackedVector2Array([Vector2(0, 0), Vector2(0, -50), Vector2(0, -100), Vector2(0, -150)])
	return f


func test_fewer_elements_than_defined_truncates_larger_set() -> void:
	var got: PackedVector2Array = FormationSolver.local_offsets(_synthetic_def(), 2)
	_assert_points_near(got, PackedVector2Array([Vector2(0, 0), Vector2(0, -50)]))


func test_more_elements_than_defined_appends_trailing_slots() -> void:
	var got: PackedVector2Array = FormationSolver.local_offsets(_synthetic_def(), 6)
	assert_eq(got.size(), 6)
	assert_almost_eq(got[4].y, -200.0, EPS)
	assert_almost_eq(got[5].y, -250.0, EPS)


func test_zero_count_returns_empty() -> void:
	assert_eq(FormationSolver.local_offsets(_wedge(), 0).size(), 0)


func test_every_platoon_formation_defines_3_and_4_slots() -> void:
	var platoon: Array[FormationDef] = catalog.formations_for(&"platoon")
	assert_gt(platoon.size(), 5, "소대 대형 6종 이상")
	for f: FormationDef in platoon:
		assert_true(f.slots.has(3) and f.slots.has(4), "%s: 3·4 슬롯" % f.id)
		if f.allowed_moving:
			assert_eq(f.slots[4][0], Vector2.ZERO, "%s: 이동 대형의 슬롯 0 = 가이드 위치" % f.id)
