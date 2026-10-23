extends GutTest
## M6 검증: 거점·임무 목표·승점, 증원 진입, 승패 판정, TF AI, AAR. PLAN 14장 M6.
## 완료 기준: AI 상대 한 판이 끝까지 돌고 승패가 남.

var cat: DataCatalog


func before_all() -> void:
	cat = TestFixture.catalog()


func _res(b: Battle, key: String) -> String:
	var v: String = b.result.get(key, "")
	return v


func _skirmish(ai: Array[StringName], seed_value: int = -1) -> Battle:
	var b: Battle = BattleSetup.skirmish(cat, cat.default_force(&"US"), cat.default_force(&"RU"), &"skirmish_01",
		seed_value, ai)
	b.begin_battle()
	return b


func test_ai_vs_ai_game_finishes_with_result_and_aar() -> void:
	var b: Battle = _skirmish([&"blue", &"red"] as Array[StringName], 11)
	var limit: int = roundi(b.scenario.time_limit_min * 60.0 * SimConst.TICK_RATE) + 50
	while b.result.is_empty() and b.tick() < limit:
		b.step_tick()
		if b.tick() % 600 == 0:
			b.world.events.drain()
	assert_false(b.result.is_empty(), "승패 판정이 남")
	var reason: String = b.result.get("reason", "")
	assert_true(reason == "collapse" or reason == "time", "판정 사유 %s" % reason)
	assert_true(b.world.paused, "끝나면 멈춘다")
	var aar: Dictionary = b.stats.summary()
	gut.p("결과: %s (%s) %.0f초, VP %s" % [b.result.get("winner"), reason, b.world.get_sim_time_sec(), b.result.get("vp")])
	var sides: Dictionary = aar["sides"]
	var blue: Dictionary = sides["blue"]
	var red: Dictionary = sides["red"]
	var blue_orders: int = blue["orders"]
	var red_orders: int = red["orders"]
	assert_gt(blue_orders, 3, "청군 AI 가 명령을 냈다")
	assert_gt(red_orders, 3, "적군 AI 가 명령을 냈다")
	var mean_delay: float = red["order_delay_mean"]
	assert_gt(mean_delay, 5.0, "AI 명령도 지휘망 지연을 겪는다 (%.1f초)" % mean_delay)
	var timeline: Array = aar["timeline"]
	assert_gt(timeline.size(), 3)
	var total_losses: int = blue["losses_total"] + red["losses_total"]
	assert_gt(total_losses, 0, "교전이 있었다")
	assert_true(JSON.stringify(aar).length() > 100, "AAR 은 JSON 으로 직렬화된다")
	b.dispose()


func test_control_point_capture_vp_and_objective() -> void:
	var b: Battle = TestFixture.battle_on(TestFixture.flat_map(), 1)
	var fox: ScoringSystem.PointState = b.scoring.get_point(&"cp_fox")
	var blue: Platoon = TestFixture.platoon(b, &"us_tank_plt", &"blue", fox.def.pos, 0.0)
	for e: Element in blue.elements:
		e.ammo.clear()  # 지휘소끼리 쏘아 붕괴로 끝나지 않게
	b.run_seconds(3.0)
	assert_eq(fox.owner, &"blue", "청군만 있으면 점령")
	var fox_done: bool = b.scoring.objectives_done.get(&"obj_fox", false)
	assert_true(fox_done, "T+25분 전 FOX 확보 임무 달성")
	var vp_after_obj: float = b.scoring.vp[&"blue"]
	assert_almost_eq(vp_after_obj, 30.0, 1.0, "임무 목표 +30")
	b.run_seconds(60.0)
	assert_almost_eq(b.scoring.vp[&"blue"] - vp_after_obj, 1.0, 0.1, "점령 거점은 분당 1점")
	# 적이 들어오면 경합: 소유는 그대로, 승점은 계속.
	var red: Platoon = TestFixture.platoon(b, &"ru_tank_plt", &"red", fox.def.pos + Vector2(100, 100), PI)
	for e: Element in red.elements + blue.elements:
		e.ammo.clear()
	b.run_seconds(3.0)
	assert_true(fox.contested)
	assert_eq(fox.owner, &"blue")
	# 청군이 빠지면 적 점령.
	TestFixture.teleport(b, blue, Vector2(600, 600))
	b.run_seconds(3.0)
	assert_eq(fox.owner, &"red")
	b.dispose()


func test_collapse_ends_battle_immediately() -> void:
	var b: Battle = _skirmish([] as Array[StringName], 3)
	b.run_ticks(5)
	var red: TaskForce = b.task_forces[&"red"]
	var all: Array[Element] = red.all_elements()
	for i: int in roundi(all.size() * 0.9):
		TestFixture.kill(all[i])
	b.run_seconds(2.0)
	assert_eq(_res(b, "winner"), "blue")
	assert_eq(_res(b, "reason"), "collapse")
	assert_true(b.world.paused)
	b.dispose()


func test_time_limit_decides_by_vp() -> void:
	var b: Battle = TestFixture.battle_on(TestFixture.flat_map(), 2)
	var old: float = b.scenario.time_limit_min
	b.scenario.time_limit_min = 1.0
	var bravo: ScoringSystem.PointState = b.scoring.get_point(&"cp_bravo")
	var red: Platoon = TestFixture.platoon(b, &"ru_tank_plt", &"red", bravo.def.pos, PI)
	for e: Element in red.elements:
		e.ammo.clear()
	b.run_seconds(65.0)
	b.scenario.time_limit_min = old
	assert_eq(_res(b, "reason"), "time")
	assert_eq(_res(b, "winner"), "red", "BRAVO 를 가진 적군이 승점으로 승리")
	b.dispose()


func test_reinforcements_arrive_and_join_company() -> void:
	var b: Battle = _skirmish([] as Array[StringName], 4)
	assert_eq(ReinforcementSystem.wave_tick(1), 6000, "1차 증원 T+10분")
	assert_eq(ReinforcementSystem.wave_tick(2), 12000, "2차 증원 T+20분")
	var tf: TaskForce = b.task_forces[&"blue"]
	var a3: Platoon = tf.companies[1].platoons[2]
	assert_false(a3.on_map)
	b.reinforcements._bring_in(&"blue", 1, b.tick())
	assert_true(a3.on_map, "증원 소대 진입")
	for e: Element in a3.elements:
		assert_true(e.is_active())
	var start_d: float = a3.centroid().distance_to(tf.companies[1].guide.pos)
	b.run_seconds(60.0)
	var d: float = a3.centroid().distance_to(tf.companies[1].guide.pos)
	assert_lt(d, start_d - 100.0, "중대 대형으로 합류하러 이동 (%.0f → %.0f m)" % [start_d, d])
	# 적 3중대(1차 증원)도 같은 단계에 들어온다.
	var red_c3: Company = b.task_forces[&"red"].companies[2]
	assert_false(red_c3.on_map)
	b.reinforcements._bring_in(&"red", 1, b.tick())
	assert_true(red_c3.on_map, "중대째 증원")
	assert_true(red_c3.hq.on_map)
	b.dispose()


func test_tf_ai_assigns_points_and_reserves_weak_company() -> void:
	var b: Battle = _skirmish([&"red"] as Array[StringName], 5)
	b.run_ticks(TaskForceAI.EVAL_OFFSET + 1)
	var red: TaskForce = b.task_forces[&"red"]
	var assigned: int = 0
	for c: Company in red.companies:
		if c.on_map:
			assert_true(b.tf_ai.assignment.has(c.id), "%s 에 임무 배정" % c.label())
			assigned += 1
	assert_gt(assigned, 1)
	# 거점이 여러 곳으로 나뉜다.
	var distinct: Dictionary = {}
	for v: StringName in b.tf_ai.assignment.values():
		distinct[v] = true
	assert_gt(distinct.size(), 1, "중대들이 서로 다른 거점으로")
	# 약해진 중대는 예비로.
	var weak: Company = red.companies[0]
	var elems: Array[Element] = weak.all_elements()
	for i: int in roundi(elems.size() * 0.6):
		TestFixture.kill(elems[i])
	b.tf_ai.evaluate(red)
	assert_eq(b.tf_ai.assignment[weak.id], &"reserve", "전투력 50% 이하 중대는 예비")
	b.dispose()
