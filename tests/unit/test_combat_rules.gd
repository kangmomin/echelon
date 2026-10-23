extends GutTest
## 전투 규칙 단위 테스트: 맞은 방향, 관통 분류, LOS, 탐지 확률 요인. PLAN 9장, 17.4.

var b: Battle


func before_all() -> void:
	b = TestFixture.battle(1)


func after_all() -> void:
	b.dispose()


func _tank(side: StringName, pos: Vector2, heading: float) -> Element:
	var p: Platoon = TestFixture.platoon(b, &"us_tank_plt" if side == &"blue" else &"ru_tank_plt", side, pos, heading)
	return p.elements[0]


func test_hit_facing_by_angle() -> void:
	var t: Element = Element.new()
	t.pos = Vector2(1000, 1000)
	t.heading = 0.0  # 북쪽을 봄
	assert_eq(CombatSystem.hit_facing(Vector2(1000, 2000), t, false), &"front")
	assert_eq(CombatSystem.hit_facing(Vector2(2000, 1000), t, false), &"side")
	assert_eq(CombatSystem.hit_facing(Vector2(1000, 0), t, false), &"rear")
	assert_eq(CombatSystem.hit_facing(Vector2(1000, 2000), t, true), &"top", "상부 공격 무기")


func test_penetration_classes_with_real_data() -> void:
	var cat: DataCatalog = TestFixture.catalog()
	var m1: ElementDef = cat.get_element(&"us_m1a2_sepv3")
	var t90_gun: WeaponDef = cat.get_weapon(&"2a46m_apfsds")
	var javelin: WeaponDef = cat.get_weapon(&"javelin")
	var coax: WeaponDef = cat.get_weapon(&"pkt_coax")
	assert_eq(CombatSystem.classify_penetration(t90_gun.penetration_at(2000.0), m1.get_armor(&"front")), &"bounce",
		"2 km 에서 T-90 철갑탄은 M1 정면을 못 뚫음")
	assert_eq(CombatSystem.classify_penetration(t90_gun.penetration_at(2000.0), m1.get_armor(&"side")), &"overmatch",
		"측면은 뚫음")
	assert_eq(CombatSystem.classify_penetration(javelin.penetration_at(1500.0), m1.get_armor(&"top")), &"overmatch",
		"재블린 상부 공격")
	assert_eq(CombatSystem.classify_penetration(coax.penetration_at(300.0), m1.get_armor(&"rear")), &"bounce")
	assert_eq(CombatSystem.classify_penetration(100.0, 100.0), &"marginal")


func test_ridge_blocks_los() -> void:
	# BRAVO 능선(900,1950) 양쪽.
	var smokes: Array[Los.Smoke] = []
	assert_false(Los.check(b.terrain, smokes, 0, Vector2(900, 1450), 2.5, Vector2(900, 2450), 2.0), "능선 너머는 안 보임")
	assert_true(Los.check(b.terrain, smokes, 0, Vector2(2600, 3500), 2.5, Vector2(3400, 3500), 2.0), "평지는 보임")


func test_forest_edge_visible_but_deep_forest_hidden() -> void:
	var smokes: Array[Los.Smoke] = []
	# 남부 숲 서쪽 가장자리 x≈1370 (y 700). 숲 밖 서쪽에서 본다.
	var observer := Vector2(900, 700)
	var edge := _first_forest_point(Vector2(1000, 700), Vector2(1, 0)) + Vector2(15, 0)
	var deep := edge + Vector2(200, 0)
	assert_eq(b.terrain.type_at(edge), Terrain.Type.FOREST)
	assert_true(Los.check(b.terrain, smokes, 0, observer, 2.5, edge, 2.0), "숲 가장자리 15 m 안은 보임")
	assert_false(Los.check(b.terrain, smokes, 0, observer, 2.5, deep, 2.0), "숲 깊이 200 m 는 안 보임")


func _first_forest_point(from: Vector2, dir: Vector2) -> Vector2:
	var p: Vector2 = from
	for i: int in 200:
		if b.terrain.type_at(p) == Terrain.Type.FOREST:
			return p
		p += dir * 5.0
	return p


func test_smoke_blocks_los() -> void:
	var smoke := Los.Smoke.new()
	smoke.pos = Vector2(3000, 3500)
	smoke.radius = 80.0
	smoke.until_tick = 100
	var smokes: Array[Los.Smoke] = [smoke]
	assert_false(Los.check(b.terrain, smokes, 50, Vector2(2600, 3500), 2.5, Vector2(3400, 3500), 2.0))
	assert_true(Los.check(b.terrain, smokes, 150, Vector2(2600, 3500), 2.5, Vector2(3400, 3500), 2.0), "연막이 걷히면 보임")


func test_detection_probability_factors() -> void:
	var obs: Element = _tank(&"blue", Vector2(2600, 3300), 0.0)
	var t: Element = _tank(&"red", Vector2(3400, 3300), PI)
	var d: float = 1000.0
	t.stationary_ticks = 100
	var base: float = b.detection.probability(obs, t, d, 1000)
	t.velocity = Vector2(5, 0)
	var moving: float = b.detection.probability(obs, t, d, 1000)
	t.velocity = Vector2.ZERO
	t.last_fired_tick = 990
	var firing: float = b.detection.probability(obs, t, d, 1000)
	t.last_fired_tick = -100000
	var far: float = b.detection.probability(obs, t, 2200.0, 1000)
	assert_gt(moving, base, "움직이면 잘 보임")
	assert_gt(firing, moving, "쏘면 더 잘 보임")
	assert_lt(far, base, "멀수록 덜 보임")


func test_rng_draws_are_deterministic_per_seed() -> void:
	var r1 := SimRng.new(77)
	var r2 := SimRng.new(77)
	for i: int in 20:
		assert_eq(r1.chance(0.5), r2.chance(0.5))
