extends GutTest
## M3 검증: 탐지·보고·사격·피해·사기가 맞물려 대형과 지형 선택이 결과를 바꾼다. PLAN 14장 M3.
## 교전 시험(평지 4 km): 청군 기계화 소대(탑승, 횡대 방어) ← 적 전차 소대 공격 이동.

const SEEDS: Array[int] = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]
const FOREST: Rect2 = Rect2(1500, 950, 1000, 400)


## [청 손실, 적 손실]
func _engage(red_formation: String, forest: bool, seed_value: int) -> Array[int]:
	var b: Battle = TestFixture.battle_on(TestFixture.flat_map(FOREST if forest else Rect2()), seed_value)
	var blue: Platoon = TestFixture.platoon(b, &"us_mech_plt", &"blue", Vector2(2000, 1330), 0.0)
	blue.sop.mount_policy = &"stay_mounted"
	TestFixture.move(b, blue, Vector2(2000, 1330), Order.Type.DEFEND, 0.0, {"formation": "line"})
	var red: Platoon = TestFixture.platoon(b, &"ru_tank_plt", &"red", Vector2(2000, 3900), PI)
	TestFixture.move(b, red, Vector2(2000, 1600), Order.Type.ATTACK_MOVE, PI, {"formation": red_formation})
	var t: int = 0
	while t < 4800 and not red.is_destroyed() and not blue.is_destroyed() and not red.routed:
		b.step_tick()
		t += 1
	var out: Array[int] = [TestFixture.losses(blue), TestFixture.losses(red)]
	b.dispose()
	return out


func _sum(red_formation: String, forest: bool) -> Array[int]:
	var total: Array[int] = [0, 0]
	for s: int in SEEDS:
		var r: Array[int] = _engage(red_formation, forest, s)
		total[0] += r[0]
		total[1] += r[1]
	gut.p("%s forest=%s → 청 손실 %d, 적 손실 %d" % [red_formation, forest, total[0], total[1]])
	return total


## 완료 기준: 같은 병력이라도 종대로 들이받으면 횡대보다 교환비가 나쁘다(앞 차량이 뒤 차량의 사격을 가림).
func test_column_attack_trades_worse_than_line() -> void:
	var column: Array[int] = _sum("column", false)
	var line: Array[int] = _sum("line", false)
	var column_ratio: float = column[0] / maxf(1.0, float(column[1]))
	var line_ratio: float = line[0] / maxf(1.0, float(line[1]))
	assert_gt(line_ratio, column_ratio, "횡대 교환비 %.2f > 종대 %.2f (적이 잃은 1대당 청 손실)" % [line_ratio, column_ratio])
	assert_gt(column[1], line[1], "종대 공격 쪽 손실이 더 크다")


## 완료 기준: 숲 가장자리에서 방어하면 탁 트인 곳보다 덜 잃는다.
func test_forest_edge_helps_defender() -> void:
	var open: Array[int] = _sum("wedge", false)
	var forest: Array[int] = _sum("wedge", true)
	assert_lt(forest[0], open[0], "숲 방어 청 손실 %d < 개활지 %d" % [forest[0], open[0]])
	assert_gt(forest[1], open[1], "숲 방어 시 적 손실이 더 크다")


## 보고는 소대 → 중대 → 대대로 지연되어 올라가고, 통신이 끊기면 보류된다.
func test_contact_reports_are_delayed_and_held_when_cut() -> void:
	var b: Battle = TestFixture.battle_on(TestFixture.flat_map(), 2)
	var c: Company = TestFixture.company(b, &"us_tank_co", &"blue", Vector2(600, 2000), PI / 2.0)
	var red: Platoon = TestFixture.platoon(b, &"ru_tank_plt", &"red", Vector2(2300, 2000), -PI / 2.0)
	for e: Element in red.elements:
		e.ammo.clear()  # 사격 없이 탐지만 시험
	for e: Element in c.all_elements():
		e.ammo.clear()
	var tf: TaskForce = b.task_forces[&"blue"]
	var first_seen: int = -1
	var first_known: int = -1
	for i: int in 600:
		b.step_tick()
		if first_seen < 0 and not b.detection.seen[&"blue"].is_empty():
			first_seen = b.tick()
		if first_known < 0 and not tf.known_enemies.is_empty():
			first_known = b.tick()
			break
	assert_gt(first_seen, 0, "청군이 적을 봄")
	assert_gt(first_known, 0, "대대에 보고 도착")
	var delay_sec: float = (first_known - first_seen) / 10.0
	assert_between(delay_sec, 8.0, 25.0, "소대→중대→대대 보고 지연 %.1f초" % delay_sec)
	# 두 번째 적: 통신이 끊긴 소대만 볼 수 있는 곳에.
	var b2: Battle = TestFixture.battle_on(TestFixture.flat_map(), 2)
	var c2: Company = TestFixture.company(b2, &"us_tank_co", &"blue", Vector2(600, 1000), PI / 2.0)
	var cut: Platoon = c2.platoons[2]
	TestFixture.teleport(b2, cut, Vector2(1200, 3300))
	var red2: Platoon = TestFixture.platoon(b2, &"ru_tank_plt", &"red", Vector2(2600, 3300), -PI / 2.0)
	for e: Element in red2.elements:
		e.ammo.clear()
	for e: Element in c2.all_elements():
		e.ammo.clear()
	b2.run_seconds(40.0)
	assert_eq(cut.comms_state, Unit.Comms.CUT)
	assert_false(b2.detection.seen[&"blue"].is_empty(), "두절 소대도 적은 본다")
	assert_true(cut.contacts.size() > 0, "소대 자신은 접촉을 안다")
	assert_true(b2.task_forces[&"blue"].known_enemies.is_empty(), "두절 중에는 대대가 모른다")
	TestFixture.teleport(b2, cut, Vector2(700, 1300))
	b2.run_seconds(30.0)
	assert_false(b2.task_forces[&"blue"].known_enemies.is_empty(), "통신 복구 후 보고가 올라간다")
	b.dispose()
	b2.dispose()


## 탑승 중 IFV 격파 → 분대 50% 사상 후 하차, 제압.
func test_ifv_kill_halves_passengers() -> void:
	var b: Battle = TestFixture.battle_on(TestFixture.flat_map(), 1)
	var p: Platoon = TestFixture.platoon(b, &"us_mech_plt", &"blue", Vector2(2000, 2000), 0.0)
	var ifv: Element = null
	for e: Element in p.vehicles():
		if not e.passengers.is_empty():
			ifv = e
			break
	assert_not_null(ifv, "분대를 태운 IFV")
	var squad: Element = ifv.passengers[0]
	var before: float = squad.hp
	b.combat.destroy(ifv, null, b.tick())
	assert_false(ifv.is_alive())
	assert_false(squad.is_mounted(), "하차")
	assert_eq(squad.hp, floorf(before * 0.5), "인원 절반")
	assert_gt(squad.suppression, 50.0, "강한 제압")
	assert_false(p.mounted, "소대는 하차 상태")
	b.dispose()


## 탄약이 바닥나면 쏘지 않는다.
func test_no_ammo_no_fire() -> void:
	var b: Battle = TestFixture.battle_on(TestFixture.flat_map(), 1)
	var blue: Platoon = TestFixture.platoon(b, &"us_tank_plt", &"blue", Vector2(2000, 1500), 0.0)
	var red: Platoon = TestFixture.platoon(b, &"ru_tank_plt", &"red", Vector2(2000, 3000), PI)
	for e: Element in blue.elements:
		for k: StringName in e.ammo.keys():
			e.ammo[k] = 0
	b.run_seconds(60.0)
	assert_eq(TestFixture.losses(red), 0, "탄이 없는 청군은 적을 못 잡는다")
	for e: Element in blue.elements:
		assert_lt(e.last_fired_tick, 0, "청군 %d 은 쏘지 않음" % e.id)
	assert_gt(b.combat.shots_fired, 0, "적은 쏜다")
	b.dispose()


## 사기 40 이하: 공격 명령 거부, 20 이하: 패주 후 후방으로.
func test_morale_refusal_and_rout() -> void:
	var b: Battle = TestFixture.battle_on(TestFixture.flat_map(), 1)
	var p: Platoon = TestFixture.platoon(b, &"us_tank_plt", &"blue", Vector2(2000, 2000), 0.0)
	p.morale = 35.0
	var o: Order = TestFixture.move(b, p, Vector2(2000, 2600), Order.Type.ATTACK_MOVE)
	b.run_ticks(2)
	assert_eq(o.state, Order.State.CANCELLED, "흔들린 소대는 공격 거부")
	var mv: Order = TestFixture.move(b, p, Vector2(2100, 2000))
	b.run_ticks(2)
	assert_ne(mv.state, Order.State.CANCELLED, "이동은 받는다")
	p.morale = 15.0
	b.run_seconds(3.0)
	assert_true(p.routed, "패주")
	var start: Vector2 = p.centroid()
	b.run_seconds(40.0)
	assert_gt(start.distance_to(p.centroid()), 150.0, "패주 소대는 물러난다")
	b.run_seconds(30.0)
	assert_false(p.routed, "60초 뒤 재집결")
	assert_true(p.morale >= MoraleSystem.AFTER_ROUT)
	b.dispose()
