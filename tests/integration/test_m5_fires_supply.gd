extends GutTest
## M5 검증: 간접화력과 보급이 지휘망을 탄다. PLAN 14장 M5.
## 완료 기준: 관측반 유무에 따라 포격 정확도·지연이 달라짐. 탄약 떨어진 소대를 보급해 복귀.

const TARGET: Vector2 = Vector2(2000, 2600)


## 청군 박격포 소대 + (선택) 관측반, 적 전차 소대(사격 안 함) 표적.
func _fires_battle(seed_value: int, with_fo: bool) -> Battle:
	var b: Battle = TestFixture.battle_on(TestFixture.flat_map(), seed_value, false)
	var tf: TaskForce = b.task_forces[&"blue"]
	tf.guide.reset(Vector2(2000, 300), 0.0)
	TestFixture.teleport(b, tf.cp_section, Vector2(2000, 300))
	TestFixture.platoon(b, &"us_mortar_plt", &"blue", Vector2(2000, 600), 0.0)
	if with_fo:
		TestFixture.platoon(b, &"us_fo_team", &"blue", Vector2(2000, 1300), 0.0)
	var red: Platoon = TestFixture.platoon(b, &"ru_tank_plt", &"red", TARGET, PI)
	red.sop.under_fire = &"hold"
	for e: Element in red.elements:
		e.ammo.clear()
	b.run_ticks(25)
	return b


func _ok(res: Dictionary) -> bool:
	var v: bool = res.get("ok", false)
	return v


## [평균 효력사 오차(m), 지연(초)]
func _mortar_run(seed_value: int, with_fo: bool) -> Array[float]:
	var b: Battle = _fires_battle(seed_value, with_fo)
	var res: Dictionary = b.fires.request(&"blue", FireSupportSystem.Asset.MORTAR, TARGET)
	assert_true(_ok(res), "요청 성공: %s" % res.get("error", ""))
	var m: FireSupportSystem.Mission = res["mission"]
	var impacts: Array[Vector2] = []
	var t: int = 0
	while t < 3000 and m.phase != FireSupportSystem.Phase.DONE:
		b.step_tick()
		t += 1
		for ev: Dictionary in b.world.events.drain():
			if ev["type"] == &"shell" and m.phase == FireSupportSystem.Phase.EFFECT:
				var pl: Dictionary = ev["payload"]
				var pos: Vector2 = pl["pos"]
				impacts.append(pos)
	b.run_ticks(200)
	for ev: Dictionary in b.world.events.drain():
		if ev["type"] == &"shell":
			var pl: Dictionary = ev["payload"]
			var pos: Vector2 = pl["pos"]
			impacts.append(pos)
	var miss: float = 0.0
	for p: Vector2 in impacts:
		miss += p.distance_to(TARGET)
	var out: Array[float] = [miss / maxf(1.0, impacts.size()), m.delay_sec, float(impacts.size()), float(m.observer_quality)]
	b.dispose()
	return out


func test_forward_observer_makes_mortar_faster_and_tighter() -> void:
	var fo_miss: float = 0.0
	var fo_delay: float = 0.0
	var none_miss: float = 0.0
	var none_delay: float = 0.0
	for s: int in [1, 2, 3]:
		var a: Array[float] = _mortar_run(s, true)
		var c: Array[float] = _mortar_run(s, false)
		assert_eq(a[3], 0.0, "관측반이 관측")
		assert_eq(c[3], 2.0, "관측자 없음 = 지도 좌표 사격")
		assert_gt(a[2], 10.0, "효력사 %d발" % a[2])
		fo_miss += a[0]
		none_miss += c[0]
		fo_delay += a[1]
		none_delay += c[1]
	gut.p("관측반: 평균 오차 %.0f m, 지연 %.0f초 / 없음: %.0f m, %.0f초" % [fo_miss / 3, fo_delay / 3, none_miss / 3, none_delay / 3])
	assert_lt(fo_miss * 1.4, none_miss, "관측반이 있으면 훨씬 정확")
	assert_lt(fo_delay + 20.0, none_delay, "관측반이 있으면 빠름")
	assert_between(fo_delay / 3, 40.0, 95.0, "박격포 지연 45~90초 근처")


func test_artillery_needs_purchased_missions_and_hurts() -> void:
	var b: Battle = _fires_battle(4, true)
	var tf: TaskForce = b.task_forces[&"blue"]
	tf.artillery_missions = 1
	var res: Dictionary = b.fires.request(&"blue", FireSupportSystem.Asset.ARTILLERY, TARGET)
	assert_true(_ok(res), str(res.get("error", "")))
	var m: FireSupportSystem.Mission = res["mission"]
	assert_between(m.delay_sec, 110.0, 190.0, "포병 지연 %.0f초" % m.delay_sec)
	var again: Dictionary = b.fires.request(&"blue", FireSupportSystem.Asset.ARTILLERY, TARGET)
	assert_false(_ok(again), "산 임무를 다 썼다")
	b.run_seconds(m.delay_sec + 120.0)
	assert_eq(m.phase, FireSupportSystem.Phase.DONE)
	var red: Platoon = b.task_forces[&"red"].organic[0]
	var hurt: int = 0
	for e: Element in red.elements:
		if e.state != Element.State.OK:
			hurt += 1
	assert_gt(hurt, 0, "155 mm 효력사로 전차 손상·격파")
	b.dispose()


func test_cas_needs_controller_and_faces_air_defence() -> void:
	var b: Battle = _fires_battle(5, false)
	var tf: TaskForce = b.task_forces[&"blue"]
	tf.cas_sorties = 2
	var res: Dictionary = b.fires.request(&"blue", FireSupportSystem.Asset.CAS, TARGET)
	assert_false(_ok(res), "통제관 없이 CAS 불가")
	TestFixture.platoon(b, &"us_jtac_team", &"blue", Vector2(2000, 1500), 0.0)
	var ada: Platoon = TestFixture.platoon(b, &"ru_ada_section", &"red", TARGET + Vector2(0, 600), PI)
	b.run_ticks(20)
	var r2: Dictionary = b.fires.request(&"blue", FireSupportSystem.Asset.CAS, TARGET)
	assert_true(_ok(r2), str(r2.get("error", "")))
	var m: FireSupportSystem.Mission = r2["mission"]
	assert_eq(m.observer_quality, 0, "JTAC 통제")
	assert_between(m.delay_sec, 85.0, 160.0)
	var sam_before: int = ada.elements[0].ammo.get(&"9m338_sam", 0)
	b.run_seconds(m.delay_sec + 5.0)
	assert_true(m.phase == FireSupportSystem.Phase.DONE or m.phase == FireSupportSystem.Phase.CANCELLED)
	var sam_after: int = ada.elements[0].ammo.get(&"9m338_sam", 0)
	assert_eq(sam_after, sam_before - 1, "방공 미사일이 요격 시도")
	assert_eq(tf.cas_sorties, 1)
	b.dispose()


func test_smoke_blocks_line_of_sight() -> void:
	var b: Battle = _fires_battle(6, true)
	var fo: Element = b.task_forces[&"blue"].organic[1].elements[0]
	assert_true(Los.check(b.terrain, b.smokes, b.tick(), fo.pos, 2.5, TARGET, 2.0), "연막 전에는 보인다")
	var res: Dictionary = b.fires.request(&"blue", FireSupportSystem.Asset.MORTAR, TARGET.lerp(fo.pos, 0.15), true)
	assert_true(_ok(res), str(res.get("error", "")))
	var m: FireSupportSystem.Mission = res["mission"]
	b.run_seconds(m.delay_sec + 70.0)
	assert_gt(b.smokes.size(), 3, "연막탄 %d발" % b.smokes.size())
	assert_false(Los.check(b.terrain, b.smokes, b.tick(), fo.pos, 2.5, TARGET, 2.0), "연막이 시선을 가린다")
	b.dispose()


## 재머가 관측자 통신을 방해하면 화력 요청이 늦어진다.
func test_jammer_slows_fire_requests() -> void:
	var delays: Array[float] = []
	for jam: bool in [false, true]:
		var b: Battle = _fires_battle(7, true)
		if jam:
			TestFixture.platoon(b, &"ru_ew_section", &"red", Vector2(2000, 2100), PI)
			b.run_ticks(25)
		var fo_platoon: Platoon = b.task_forces[&"blue"].organic[1]
		var res: Dictionary = b.fires.request(&"blue", FireSupportSystem.Asset.MORTAR, TARGET)
		assert_true(_ok(res), str(res.get("error", "")))
		var m: FireSupportSystem.Mission = res["mission"]
		delays.append(m.delay_sec - b.fires.FDC_SEC[0])
		if jam:
			assert_ne(fo_platoon.comms_state, Unit.Comms.GOOD, "재머 근처 관측반 통신 저하")
		b.dispose()
	assert_gt(delays[1], delays[0] + 3.0, "재머: %.1f초 > 평시 %.1f초" % [delays[1], delays[0]])


## 완료 기준: 탄약이 떨어진 소대를 보급해 복귀시킨다.
func test_resupply_restores_empty_platoon() -> void:
	var b: Battle = TestFixture.battle_on(TestFixture.flat_map(), 8, false)
	var tf: TaskForce = b.task_forces[&"blue"]
	tf.guide.reset(Vector2(2000, 300), 0.0)
	TestFixture.teleport(b, tf.cp_section, Vector2(2000, 300))
	tf.entry_pos = Vector2(2000, 50)
	var tanks: Platoon = TestFixture.platoon(b, &"us_tank_plt", &"blue", Vector2(2000, 1200), 0.0)
	var trucks: Platoon = TestFixture.platoon(b, &"us_supply_section", &"blue", Vector2(1800, 400), 0.0)
	for e: Element in tanks.elements:
		for k: StringName in e.ammo.keys():
			e.ammo[k] = 0
	tanks.elements[1].state = Element.State.DAMAGED
	tanks.elements[1].damage_kind = &"firepower"
	assert_gt(SupplySystem.platoon_need(tanks), 30.0)
	b.run_ticks(25)
	var o: Order = TestFixture.move(b, tanks, Vector2.ZERO, Order.Type.RESUPPLY)
	assert_ne(o.state, Order.State.CANCELLED)
	b.run_seconds(60.0)
	assert_eq(tanks.posture, &"resupplying", "보급 명령을 받은 소대는 대기")
	assert_true(b.supply.tasks.has(trucks.id), "보급반이 배정됨")
	var start_need: float = SupplySystem.platoon_need(tanks)
	b.run_seconds(240.0)
	assert_lt(SupplySystem.platoon_need(tanks), start_need, "보급이 진행")
	b.run_seconds(240.0)
	assert_almost_eq(SupplySystem.platoon_need(tanks), 0.0, 0.05, "완전 보급")
	assert_eq(tanks.posture, &"idle", "보급이 끝나면 복귀")
	var gun: int = tanks.elements[0].ammo.get(&"m256_apfsds", 0)
	assert_gt(gun, 10, "주포탄 %d발" % gun)
	assert_lt(SupplySystem.section_supply(trucks), SupplySystem.section_capacity(trucks), "트럭 보급량 소모")
	# 수리: 트럭 옆 5분.
	assert_eq(tanks.elements[1].state, Element.State.OK, "트럭 옆에서 5분 넘게 정지 → 수리")
	b.dispose()


## 포격 SOP "분산": 정지한 소대가 포격을 받으면 착탄점에서 멀어진다.
func test_under_fire_sop_disperses() -> void:
	var b: Battle = _fires_battle(9, true)
	var red: Platoon = b.task_forces[&"red"].organic[0]
	red.sop.under_fire = &"disperse"
	var start: Vector2 = red.centroid()
	var res: Dictionary = b.fires.request(&"blue", FireSupportSystem.Asset.MORTAR, TARGET)
	var m: FireSupportSystem.Mission = res["mission"]
	b.run_seconds(m.delay_sec + 60.0)
	assert_gt(red.centroid().distance_to(start), 120.0, "분산 이동")
	b.dispose()
