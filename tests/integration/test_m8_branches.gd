extends GutTest
## 병과 확장 검증: 공격헬기(공중 이동·고도·대공 교전), 공병(지뢰·제거·진지), 도보 보병, 차륜형.


func _flat() -> Battle:
	return TestFixture.battle_on(TestFixture.flat_map(), 3, true)


func _real() -> Battle:
	return TestFixture.battle(3, true)


# --- 헬기 ----------------------------------------------------------------

func test_helicopter_flies_straight_over_river() -> void:
	# 실제 지도: 남강(y ≈ 1150~1300)을 다리 없이 곧장 건넌다.
	var b: Battle = _real()
	var heli: Platoon = TestFixture.platoon(b, &"us_aviation_sec", &"blue", Vector2(2000, 700), 0.0)
	assert_eq(OrderExecutor.path_mobility(heli), Terrain.MOBILITY_AIR, "헬기 소대는 공중 이동")
	TestFixture.move(b, heli, Vector2(2000, 2000))
	b.run_seconds(40.0)
	var lead: Element = heli.elements[0]
	assert_gt(lead.pos.y, 1800.0, "40초 안에 강 건너 1.3 km 이상 (현재 %.0f)" % lead.pos.y)
	assert_lt(absf(lead.pos.x - 2000.0), 250.0, "직선 비행(다리로 돌지 않음)")
	assert_gt(lead.altitude_m(), 0.0, "공중에 있다")
	b.dispose()


func test_altitude_toggle_changes_height() -> void:
	var b: Battle = _flat()
	var heli: Platoon = TestFixture.platoon(b, &"ru_aviation_sec", &"red", Vector2(2000, 2000), PI)
	var e: Element = heli.elements[0]
	assert_eq(e.altitude_m(), Element.ALTITUDE_LOW_M)
	var o: Order = Order.make(Order.Type.SET_SOP, heli.id, {"sop": {"altitude": "high"}})
	b.issue_order(o)
	assert_eq(e.altitude_m(), Element.ALTITUDE_HIGH_M, "V(고공)")
	b.dispose()


func test_tank_cannot_shoot_helicopter_but_aa_can() -> void:
	var b: Battle = _flat()
	var tank: Platoon = TestFixture.platoon(b, &"us_tank_plt", &"blue", Vector2(2000, 1000), 0.0)
	var aa: Platoon = TestFixture.platoon(b, &"us_ada_section", &"blue", Vector2(1800, 1000), 0.0)
	var heli: Platoon = TestFixture.platoon(b, &"ru_aviation_sec", &"red", Vector2(2000, 2500), PI)
	var t: Element = tank.elements[0]
	var h: Element = heli.elements[0]
	var aa_e: Element = aa.elements[0]
	var d: float = t.pos.distance_to(h.pos)
	for wid: String in t.def.weapons:
		assert_false(b.combat._usable(t, b.catalog.get_weapon(StringName(wid)), h, d), "전차 무기 %s 는 헬기를 못 쏜다" % wid)
	var stinger: WeaponDef = b.catalog.get_weapon(&"stinger")
	assert_true(b.combat._usable(aa_e, stinger, h, aa_e.pos.distance_to(h.pos)), "스팅어는 헬기를 쏜다")
	assert_false(b.combat._usable(aa_e, stinger, t, 100.0), "스팅어는 지상 표적을 못 쏜다")
	b.dispose()


func test_attack_helicopters_kill_tanks_from_standoff() -> void:
	var b: Battle = _flat()
	var heli: Platoon = TestFixture.platoon(b, &"us_aviation_sec", &"blue", Vector2(2000, 800), 0.0)
	var tanks: Platoon = TestFixture.platoon(b, &"ru_tank_plt", &"red", Vector2(2000, 4000 - 600), PI)
	b.run_seconds(120.0)
	var dead: int = 0
	for e: Element in tanks.elements:
		if not e.is_alive():
			dead += 1
	assert_gt(dead, 0, "헬파이어로 2.6 km 밖 전차를 잡는다")
	for e: Element in heli.elements:
		assert_true(e.is_alive(), "방공 없는 전차는 헬기를 못 잡는다")
	b.dispose()


func test_shot_down_helicopter_falls_to_ground() -> void:
	var b: Battle = _flat()
	var heli: Platoon = TestFixture.platoon(b, &"us_aviation_sec", &"blue", Vector2(2000, 1000), 0.0)
	var e: Element = heli.elements[0]
	b.combat.destroy(e, null, b.tick())
	assert_eq(e.altitude_m(), 0.0)
	b.dispose()


# --- 공병 ----------------------------------------------------------------

func test_engineers_lay_minefield_that_hurts_enemy_vehicles() -> void:
	var b: Battle = _flat()
	var eng: Platoon = TestFixture.platoon(b, &"us_engineer_plt", &"blue", Vector2(2000, 1500), 0.0)
	assert_eq(eng.mines_left, EngineerSystem.MINEFIELDS_PER_PLATOON)
	TestFixture.move(b, eng, Vector2(2000, 1700), Order.Type.LAY_MINES)
	b.run_seconds(90.0)
	assert_eq(b.engineers.minefields.size(), 1, "지뢰 지대 1개")
	assert_eq(eng.mines_left, EngineerSystem.MINEFIELDS_PER_PLATOON - 1)
	var mf: EngineerSystem.Minefield = b.engineers.minefields[0]
	var red_knows: bool = mf.known.get(&"red", false)
	assert_false(red_knows, "적은 아직 모른다")
	# 적 전차 소대가 지뢰 지대를 가로지른다(공병은 비켜 둔다).
	TestFixture.teleport(b, eng, Vector2(600, 600))
	var red: Platoon = TestFixture.platoon(b, &"ru_tank_plt", &"red", mf.pos + Vector2(0, 400), PI)
	red.sop.roe = &"hold_fire"
	for e: Element in red.elements:
		e.ammo.clear()
	TestFixture.move(b, red, mf.pos - Vector2(0, 400))
	b.run_seconds(90.0)
	assert_gt(b.engineers.mine_strikes, 0, "지뢰를 밟는다")
	var hurt: int = 0
	for e: Element in red.elements:
		if e.state != Element.State.OK:
			hurt += 1
	assert_gt(hurt, 0, "차량 손상·격파")
	var red_knows2: bool = mf.known.get(&"red", false)
	assert_true(red_knows2 or not b.engineers.minefields.has(mf), "밟으면 알게 된다")
	b.dispose()


func test_engineers_clear_minefield() -> void:
	var b: Battle = _flat()
	var mf: EngineerSystem.Minefield = b.engineers.lay_minefield(&"red", Vector2(2000, 2000))
	var eng: Platoon = TestFixture.platoon(b, &"us_engineer_plt", &"blue", Vector2(2000, 1600), 0.0)
	TestFixture.move(b, eng, Vector2(2000, 1880), Order.Type.CLEAR_MINES)
	b.run_seconds(120.0)
	assert_false(b.engineers.minefields.has(mf), "지뢰 지대 제거")
	b.dispose()


func test_only_engineers_can_lay_mines() -> void:
	var b: Battle = _flat()
	var tanks: Platoon = TestFixture.platoon(b, &"us_tank_plt", &"blue", Vector2(2000, 1500), 0.0)
	var o: Order = TestFixture.move(b, tanks, Vector2(2000, 1700), Order.Type.LAY_MINES)
	assert_eq(o.state, Order.State.CANCELLED)
	b.dispose()


func test_fortified_position_reduces_hits() -> void:
	var b: Battle = _flat()
	var eng: Platoon = TestFixture.platoon(b, &"us_engineer_plt", &"blue", Vector2(2000, 1500), 0.0)
	TestFixture.move(b, eng, Vector2(2000, 1550), Order.Type.FORTIFY)
	b.run_seconds(90.0)
	assert_eq(b.engineers.positions.size(), 1, "진지 1개")
	var inf: Platoon = TestFixture.platoon(b, &"us_light_inf_plt", &"blue", Vector2(2000, 1550), 0.0)
	b.run_seconds(5.0)
	var sq: Element = inf.elements[0]
	assert_almost_eq(b.engineers.position_factor(sq), EngineerSystem.POSITION_FACTOR, 0.001, "진지 안")
	TestFixture.teleport(b, inf, Vector2(3000, 3000))
	b.run_seconds(5.0)
	assert_almost_eq(b.engineers.position_factor(sq), 1.0, 0.001, "진지 밖")
	b.dispose()


# --- 도보·차륜형 --------------------------------------------------------

func test_foot_infantry_platoon_walks() -> void:
	var b: Battle = _flat()
	var inf: Platoon = TestFixture.platoon(b, &"us_light_inf_plt", &"blue", Vector2(2000, 1000), 0.0)
	assert_false(inf.mounted, "차량 없는 소대는 하차 상태")
	TestFixture.move(b, inf, Vector2(2000, 1200))
	b.run_seconds(120.0)
	assert_gt(inf.elements[0].pos.y, 1100.0, "걸어서 이동")
	b.dispose()


func test_wheeled_company_is_faster_on_road_than_mech() -> void:
	var cat: DataCatalog = TestFixture.catalog()
	assert_gt(cat.get_element(&"us_m1126").speed_road_kmh, cat.get_element(&"us_m2a3").speed_road_kmh * 1.4)
	assert_gt(cat.get_element(&"ru_btr82a").speed_road_kmh, cat.get_element(&"ru_bmp3").speed_road_kmh * 1.3)
