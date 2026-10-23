extends GutTest
## M1 검증: 소대·중대가 대형을 유지하며 지형(숲·도로·능선·다리)을 지나 목적지에 도착한다. PLAN 14장 M1.

const MAX_STUCK_SEC: float = 10.0


## 가이드가 움직이는 동안 어떤 엘리먼트든 10초 넘게 멈춰 있으면 실패로 본다.
func _run_until_arrived(b: Battle, unit: Unit, max_sec: float) -> Dictionary:
	var stuck_max: int = 0
	var column_seen: bool = false
	var ticks: int = 0
	var limit: int = roundi(max_sec * SimConst.TICK_RATE)
	while ticks < limit:
		b.step_tick()
		ticks += 1
		if unit.column_mode:
			column_seen = true
		if unit.guide.is_moving() or unit.guide.is_waiting_for_path():
			for e: Element in unit.alive_elements():
				if e.is_mounted():
					continue
				stuck_max = maxi(stuck_max, e.stuck_ticks)
		elif unit.mission == null or unit.mission.is_finished():
			break
	# 정렬이 끝날 시간을 준다.
	b.run_seconds(30.0)
	return {"ticks": ticks, "stuck_max": stuck_max, "column_seen": column_seen}


func _max_slot_error(p: Platoon) -> float:
	var worst: float = 0.0
	for e: Element in p.formation_members():
		worst = maxf(worst, e.pos.distance_to(e.move_target))
	return worst


func test_platoon_crosses_river_ridge_and_forest() -> void:
	var b: Battle = TestFixture.battle()
	var p: Platoon = TestFixture.platoon(b, &"us_tank_plt", &"blue", Vector2(600, 300))
	var goal := Vector2(1150, 2450)
	var order: Order = TestFixture.move(b, p, goal)
	var r: Dictionary = _run_until_arrived(b, p, 900.0)
	assert_eq(order.state, Order.State.DONE, "명령 완료")
	assert_lt(p.guide.pos.distance_to(goal), 5.0, "가이드 도착")
	assert_lt(_max_slot_error(p), 30.0, "모든 엘리먼트가 슬롯 30 m 안")
	var stuck_max: int = r["stuck_max"]
	var ticks: int = r["ticks"]
	assert_lt(stuck_max, roundi(MAX_STUCK_SEC * SimConst.TICK_RATE), "10초 넘게 끼인 엘리먼트 없음")
	gut.p("소대 이동 %.0f 초" % (ticks / 10.0))


func test_line_formation_aligns_to_facing() -> void:
	var b: Battle = TestFixture.battle()
	var p: Platoon = TestFixture.platoon(b, &"us_tank_plt", &"blue", Vector2(2700, 3300))
	var facing: float = PI / 2.0  # 동쪽
	TestFixture.move(b, p, Vector2(3000, 3400), Order.Type.DEFEND, facing, {"formation": "line"})
	_run_until_arrived(b, p, 300.0)
	assert_eq(p.formation.id, &"line")
	assert_eq(p.posture, &"defending")
	for e: Element in p.formation_members():
		var local: Vector2 = FormationSolver.world_to_local(e.pos, p.guide.pos, p.guide.heading)
		assert_lt(absf(local.y), 15.0, "횡대: 진행축 방향 편차 작음 (%s)" % local)
		assert_lt(absf(angle_difference(e.heading, facing)), 0.35, "동쪽을 바라봄")


func test_road_march_uses_column() -> void:
	var b: Battle = TestFixture.battle()
	var p: Platoon = TestFixture.platoon(b, &"us_tank_plt", &"blue", Vector2(3370, 60))
	TestFixture.move(b, p, Vector2(3020, 680))
	var r: Dictionary = _run_until_arrived(b, p, 300.0)
	var column_seen: bool = r["column_seen"]
	assert_true(column_seen, "도로 위에서 종대로 전환")


func test_company_crosses_bridge_in_column_and_reforms() -> void:
	var b: Battle = TestFixture.battle()
	var c: Company = TestFixture.company(b, &"us_tank_co", &"blue", Vector2(2700, 450))
	var goal := Vector2(2350, 1900)
	var order: Order = TestFixture.move(b, c, goal)
	var r: Dictionary = _run_until_arrived(b, c, 1200.0)
	assert_eq(order.state, Order.State.DONE, "중대 명령 완료")
	var column_seen: bool = r["column_seen"]
	assert_true(column_seen, "다리 앞에서 종대")
	for p: Platoon in c.platoons:
		assert_lt(p.guide.pos.distance_to(_slot_of(b, c, p)), 60.0, "%s 가 중대 대형 슬롯 근처" % p.short_name)
		assert_lt(_max_slot_error(p), 40.0, "%s 소대 대형 정렬" % p.short_name)
		for e: Element in p.elements:
			assert_true(e.pos.y > 1300.0, "%s 가 강을 건넘" % e.display_name())
	var stuck_max: int = r["stuck_max"]
	var ticks: int = r["ticks"]
	assert_lt(stuck_max, roundi(MAX_STUCK_SEC * SimConst.TICK_RATE), "끼인 엘리먼트 없음")
	gut.p("중대 이동 %.0f 초" % (ticks / 10.0))


func _slot_of(b: Battle, c: Company, p: Platoon) -> Vector2:
	var plts: Array[Platoon] = c.formation_platoons()
	var slots: PackedVector2Array = FormationSolver.world_slots(c.formation, plts.size(), c.guide.pos, c.guide.heading,
		b.terrain.type_name_at(c.guide.pos))
	return slots[plts.find(p)]


func test_formation_change_takes_time() -> void:
	var b: Battle = TestFixture.battle()
	var p: Platoon = TestFixture.platoon(b, &"us_tank_plt", &"blue", Vector2(2700, 3300))
	var o: Order = Order.make(Order.Type.SET_FORMATION, p.id, {"formation": "line"})
	b.issue_order(o)
	assert_eq(p.formation.id, &"line")
	assert_true(p.is_reforming(b.tick()), "전환 중")
	b.run_seconds(14.0)
	assert_false(p.is_reforming(b.tick()), "전환 시간 10초 × 통제 1.3 = 13초")
	b.run_seconds(30.0)
	assert_false(p.is_reforming(b.tick()), "전환 끝")
	assert_lt(_max_slot_error(p), 10.0)


func test_same_seed_same_result() -> void:
	var h: Array[int] = []
	for run: int in 2:
		var b: Battle = TestFixture.battle(5)
		var c: Company = TestFixture.company(b, &"us_tank_co", &"blue", Vector2(2700, 450))
		TestFixture.move(b, c, Vector2(2600, 900))
		b.run_seconds(60.0)
		h.append(b.world.state_hash())
	assert_eq(h[0], h[1])
