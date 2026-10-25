extends GutTest
## M2 검증: 중대 명령이 소대로 번역되어 지연을 두고 전파되고, 통신·지휘 용량·승계가 명령에 영향을 준다. PLAN 14장 M2.


var _battles: Array[Battle] = []


func _c2_battle(seed_value: int = 3) -> Battle:
	var b: Battle = TestFixture.battle(seed_value, false)
	_battles.append(b)
	return b


func after_each() -> void:
	for b: Battle in _battles:
		b.dispose()
	_battles.clear()


## 완료 기준: 중대 1개(소대 3)에 확보 명령 → 25~30초 뒤 소대들이 움직인다.
func test_company_order_reaches_platoons_after_about_30_seconds() -> void:
	var b: Battle = _c2_battle()
	var c: Company = TestFixture.company(b, &"us_tank_co", &"blue", Vector2(2700, 450))
	b.run_ticks(25)  # 통신망이 한 번 만들어지도록
	var o: Order = TestFixture.move(b, c, Vector2(2600, 950), Order.Type.SEIZE)
	assert_eq(o.state, Order.State.PROPAGATING)
	assert_eq(o.cost, OrderSystem.COST_MISSION)
	var moved: float = TestFixture.seconds_until_moving(b, c.platoons[0], 60.0)
	assert_between(moved, 25.0, 35.0, "소대가 움직이기까지 %.1f초" % moved)
	assert_almost_eq((o.received_tick - o.tick_issued) / 10.0, 15.0, 0.5, "대대→중대 15초")


func test_direct_platoon_order_detaches_and_costs_double() -> void:
	var b: Battle = _c2_battle()
	var c: Company = TestFixture.company(b, &"us_tank_co", &"blue", Vector2(2700, 450))
	b.run_ticks(25)
	var p: Platoon = c.platoons[1]
	var o: Order = TestFixture.move(b, p, Vector2(2300, 800))
	assert_eq(o.cost, OrderSystem.COST_BYPASS, "소대 직접 명령 = 지휘 용량 2")
	var moved: float = TestFixture.seconds_until_moving(b, p, 60.0)
	assert_between(moved, 22.0, 30.0, "직접 명령 20초 + 실행 5초 (%.1f초)" % moved)
	assert_true(p.detached, "직접 명령을 받은 소대는 분리된다")
	assert_false(c.formation_platoons().has(p))


func test_capacity_limits_orders_per_minute() -> void:
	var b: Battle = _c2_battle()
	var c: Company = TestFixture.company(b, &"us_tank_co", &"blue", Vector2(2700, 450))
	b.run_ticks(25)
	var orders: Array[Order] = []
	for i: int in 8:
		orders.append(TestFixture.move(b, c, Vector2(2600 + i * 10, 900)))
	assert_eq(orders[5].state, Order.State.PROPAGATING, "6번째까지는 바로 전파")
	assert_eq(orders[6].state, Order.State.QUEUED, "7번째는 용량 대기")
	b.run_seconds(11.0)
	assert_ne(orders[6].state, Order.State.QUEUED, "용량이 차면(분당 6 = 10초에 1) 출발")
	assert_eq(orders[7].state, Order.State.QUEUED)


func test_comms_cut_blocks_then_cancels_after_120_seconds() -> void:
	var b: Battle = _c2_battle()
	var c: Company = TestFixture.company(b, &"us_tank_co", &"blue", Vector2(2700, 450))
	var far: Platoon = c.platoons[2]
	TestFixture.teleport(b, far, Vector2(3500, 2600))  # 중대망(전차 무전 1 km) 밖
	b.run_ticks(25)
	assert_eq(far.comms_state, Unit.Comms.CUT, "중대망에서 멀리 떨어진 소대는 통신 두절(대대 지휘소는 중대망을 중계하지 않음)")
	TestFixture.move(b, c, Vector2(2600, 900))
	b.run_seconds(20.0)
	var follow: Order = _find_follow(b, far)
	assert_not_null(follow)
	assert_true(follow.blocked_since_tick >= 0, "두절된 소대로 가는 번역 명령은 대기")
	assert_true(far.awaiting_follow)
	b.run_seconds(125.0)
	assert_eq(follow.state, Order.State.CANCELLED, "120초 넘게 막히면 취소")


func _find_follow(b: Battle, p: Platoon) -> Order:
	var found: Order = null
	for o: Order in b.orders_by_id.values():
		if o.type == Order.Type.FOLLOW and o.target_unit_id == p.id:
			found = o
	return found


func test_relay_adds_delay() -> void:
	var b: Battle = _c2_battle()
	var c: Company = TestFixture.company(b, &"us_tank_co", &"blue", Vector2(2700, 450))
	var p: Platoon = c.platoons[0]
	TestFixture.teleport(b, p, Vector2(3550, 950))  # 중대장 무전(1 km) 밖, 3소대장을 거쳐 중계
	b.comms.rebuild()
	var link: CommsNet.Link = b.comms.link_between(c, p)
	assert_true(link.ok)
	assert_gt(link.relays, 0, "중계 필요")
	var near: CommsNet.Link = b.comms.link_between(c, c.platoons[1])
	assert_eq(near.relays, 0)


func test_company_commander_loss_and_xo_succession() -> void:
	var b: Battle = _c2_battle()
	var c: Company = TestFixture.company(b, &"us_tank_co", &"blue", Vector2(2700, 450))
	b.run_ticks(25)
	var co: Element = c.commander
	var xo: Element = c.xo
	TestFixture.kill(co)
	b.run_ticks(15)
	assert_true(c.is_leaderless(b.tick()), "지휘 공백")
	var o: Order = TestFixture.move(b, c, Vector2(2600, 900))
	b.run_seconds(30.0)
	assert_eq(o.state, Order.State.PROPAGATING, "공백 중에는 명령을 받지 못함")
	b.run_seconds(65.0)
	assert_eq(c.commander, xo, "60~90초 뒤 XO 승계")
	assert_false(c.is_leaderless(b.tick()))
	b.run_seconds(20.0)
	assert_ne(o.state, Order.State.PROPAGATING, "승계 후 명령 수신")


func test_no_xo_means_senior_platoon_leader_and_slower_orders() -> void:
	var b: Battle = _c2_battle()
	var c: Company = TestFixture.company(b, &"ru_tank_co", &"red", Vector2(2000, 3300), PI)
	b.run_ticks(25)
	assert_null(c.xo, "러시아 전차 중대본부는 중대장 1대")
	TestFixture.kill(c.commander)
	b.run_ticks(15)
	var until_sec: float = (c.succession_until_tick - b.tick()) / 10.0
	assert_between(until_sec, 110.0, 181.0, "선임 소대장 승계는 2배 시간 (%.0f초)" % until_sec)
	b.run_seconds(185.0)
	assert_eq(c.commander, c.platoons[0].leader())
	assert_almost_eq(c.skill_delay_factor(), 1.5, 0.01, "이후 명령 지연 +50%")


func test_tf_cp_loss_halves_capacity() -> void:
	var b: Battle = _c2_battle()
	TestFixture.company(b, &"us_tank_co", &"blue", Vector2(2700, 450))
	assert_almost_eq(b.order_system.capacity_max(&"blue"), 6.0, 0.01)
	TestFixture.kill(b.task_forces[&"blue"].cp_section.elements[0])
	b.run_ticks(15)
	assert_almost_eq(b.order_system.capacity_max(&"blue"), 3.0, 0.01)


func test_delay_estimate_matches_actual() -> void:
	var b: Battle = _c2_battle()
	var c: Company = TestFixture.company(b, &"us_tank_co", &"blue", Vector2(2700, 450))
	b.run_ticks(25)
	var est: float = b.order_system.estimate_delay_sec(c, Order.Type.MOVE, false)
	var o: Order = TestFixture.move(b, c, Vector2(2600, 900))
	b.run_seconds(20.0)
	assert_almost_eq(est, (o.received_tick - o.tick_issued) / 10.0, 0.5, "미리보기 지연 = 실제 지연")


func test_c2_battle_is_deterministic() -> void:
	var hashes: Array[int] = []
	for run: int in 2:
		var b: Battle = _c2_battle(9)
		var c: Company = TestFixture.company(b, &"us_tank_co", &"blue", Vector2(2700, 450))
		TestFixture.kill(c.commander)
		TestFixture.move(b, c, Vector2(2600, 900))
		b.run_seconds(120.0)
		hashes.append(b.world.state_hash())
	assert_eq(hashes[0], hashes[1])


func test_cut_platoon_rejoins_when_comms_restored() -> void:
	var b: Battle = _c2_battle()
	var c: Company = TestFixture.company(b, &"us_tank_co", &"blue", Vector2(2700, 450))
	var far: Platoon = c.platoons[2]
	TestFixture.teleport(b, far, Vector2(3500, 2600))
	b.run_ticks(25)
	TestFixture.move(b, c, Vector2(3600, 2000))
	b.run_seconds(40.0)
	assert_true(far.awaiting_follow, "두절 중에는 대기")
	# 중대가 북쪽으로 올라와 무전 범위에 들어오면 번역 명령이 다시 닿는다.
	b.run_seconds(240.0)
	assert_false(far.awaiting_follow, "통신이 회복되면 중대 지휘로 복귀")
