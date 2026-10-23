extends GutTest
## M4 검증: 편제표로 전투단을 짜고(포인트·제약·교차 배속·증원 단계), 저장·불러오기 후 지도에 배치한다. PLAN 14장 M4.

var cat: DataCatalog


func before_all() -> void:
	cat = TestFixture.catalog()


func _us() -> ForceSpec:
	return cat.default_force(&"US")


## 완료 기준: 기갑 대대 TF 를 2,000점으로 짜서 저장하고 지도에 배치.
func test_default_us_force_fits_2000_points_and_deploys() -> void:
	var spec: ForceSpec = _us()
	var rep: ForceRules.Report = ForceRules.evaluate(cat, spec, 2000)
	assert_true(rep.is_valid(), "기본 편성 유효: %s" % [rep.errors])
	assert_between(rep.total, 1900, 2000, "포인트 %d" % rep.total)
	assert_gt(rep.sustain_min, 5.0, "예상 지속 교전 시간 %.1f분" % rep.sustain_min)
	# 저장 → 불러오기.
	var path: String = "user://test_forces/tf_roundtrip.json"
	assert_eq(ForceStore.save(spec, path), OK)
	var loaded: ForceSpec = ForceStore.load_spec(path)
	assert_not_null(loaded)
	assert_eq(JSON.stringify(loaded.to_dict()), JSON.stringify(spec.to_dict()), "저장·불러오기 왕복이 같다")
	# 배치.
	var b: Battle = BattleSetup.skirmish(cat, loaded, cat.default_force(&"RU"))
	assert_true(b.deploying, "배치 단계로 시작")
	var tf: TaskForce = b.task_forces[&"blue"]
	var zone: Rect2 = b.scenario.sides[&"blue"].deploy_zone
	var on_map: int = 0
	var off_map: int = 0
	for e: Element in tf.all_elements():
		if e.on_map:
			on_map += 1
			assert_true(zone.grow(30.0).has_point(e.pos), "초기 배치 엘리먼트 %d 는 배치 구역 안 (%s)" % [e.id, e.pos])
		else:
			off_map += 1
	assert_eq(on_map + off_map, rep.element_count, "편성한 엘리먼트 수 그대로 생성")
	assert_eq(off_map, 8, "B 중대 3소대(M2A3 4대 + 분대 4)는 1차 증원")
	assert_eq(tf.companies.size(), 2)
	assert_eq(tf.organic.size(), 5, "직할 3 + 배속 2")
	b.dispose()


func test_budget_and_constraint_errors() -> void:
	var spec: ForceSpec = _us()
	spec.artillery_missions = 6
	spec.attachments.append(ForceSpec.PlatoonSpec.new())
	spec.attachments[2].template = &"us_ada_section"
	var rep: ForceRules.Report = ForceRules.evaluate(cat, spec, 2000)
	assert_false(rep.is_valid(), "포병 6회 + 방공반을 사면 예산 초과 (%d)" % rep.total)
	assert_string_contains(" ".join(rep.errors), "예산 초과")
	# 전차 중대를 기계화 중대로 바꾸면 최소 전차 중대 1 위반.
	var s2: ForceSpec = _us()
	s2.companies[0] = ForceSpec.make_company(cat, &"co_a", &"us_mech_co")
	var rep2: ForceRules.Report = ForceRules.evaluate(cat, s2, 5000)
	assert_false(rep2.is_valid())
	assert_string_contains(" ".join(rep2.errors), "전차 중대가 최소 1")
	# 다른 진영 소대.
	var s3: ForceSpec = _us()
	s3.companies[0].platoons[0].template = &"ru_tank_plt"
	assert_false(ForceRules.evaluate(cat, s3, 5000).is_valid(), "러시아 소대는 넣을 수 없다")
	# 필수 슬롯 비움.
	var s4: ForceSpec = _us()
	s4.companies.remove_at(0)
	assert_string_contains(" ".join(ForceRules.evaluate(cat, s4, 5000).errors), "co_a")
	# 배속 상한(3).
	var s5: ForceSpec = _us()
	for i: int in 2:
		var p := ForceSpec.PlatoonSpec.new()
		p.template = &"us_ada_section"
		s5.attachments.append(p)
	assert_string_contains(" ".join(ForceRules.evaluate(cat, s5, 5000).errors), "배속 자산은 최대 3")


func test_wave_discount_and_loadout_cost() -> void:
	var p := ForceSpec.PlatoonSpec.new()
	p.template = &"us_tank_plt"
	var base: int = ForceRules.platoon_cost(cat, p)
	p.wave = 1
	assert_eq(ForceRules.platoon_cost(cat, p), roundi(base * 0.9), "1차 증원 10% 할인")
	p.wave = 2
	assert_eq(ForceRules.platoon_cost(cat, p), roundi(base * 0.8), "2차 증원 20% 할인")
	p.wave = 0
	p.loadout = &"tusk"
	assert_gt(ForceRules.platoon_cost(cat, p), base, "TUSK 장착형이 더 비싸다")


## 교차 배속: 전차 소대 ↔ 기계화 소대 교환은 편제 안(혼성 팀). 전차 위주 중대가 모자라게 되면 비편제.
func test_cross_attachment_and_ad_hoc() -> void:
	var spec: ForceSpec = _us()
	spec.companies.append(ForceSpec.make_company(cat, &"co_c", &"us_mech_co"))
	# A 전차 중대 1소대 → C 기계화 중대, C 1소대 → A. 둘 다 3개 소대 유지, 전차 위주 중대 1 유지.
	assert_true(spec.move_platoon(&"co_a", 0, &"co_c"))
	assert_true(spec.move_platoon(&"co_c", 0, &"co_a"))
	var rep: ForceRules.Report = ForceRules.evaluate(cat, spec, 5000)
	assert_true(rep.is_valid(), "%s" % [rep.errors])
	assert_true(rep.ad_hoc.is_empty(), "팀 편성은 비편제가 아님: %s" % [rep.ad_hoc])
	# A 의 전차 소대 2개를 C 로 → A 는 소대 1개(비편제), C 는 5개(비편제).
	assert_true(spec.move_platoon(&"co_a", 0, &"co_c"))
	assert_true(spec.move_platoon(&"co_a", 0, &"co_c"))
	var rep2: ForceRules.Report = ForceRules.evaluate(cat, spec, 5000)
	assert_true(rep2.is_valid(), "비편제는 경고일 뿐 편성은 가능: %s" % [rep2.errors])
	assert_true(rep2.ad_hoc.has(&"co_a"), "소대 1개 중대는 비편제")
	assert_true(rep2.ad_hoc.has(&"co_c"), "소대 5개 중대도 비편제")
	assert_false(rep2.ad_hoc.has(&"co_b"))
	# 전투에서는 비편제 중대의 명령 지연이 20% 길다.
	var b: Battle = BattleSetup.skirmish(cat, spec, cat.default_force(&"RU"))
	b.begin_battle()
	var tf: TaskForce = b.task_forces[&"blue"]
	var c_ad_hoc: Company = tf.companies[0]
	var c_ok: Company = tf.companies[1]
	assert_true(c_ad_hoc.ad_hoc)
	assert_false(c_ok.ad_hoc)
	assert_string_contains(tf.companies[2].platoons[3].display_name, "배속", "배속 소대는 이름에 표시")
	b.run_ticks(25)
	var d_ad_hoc: float = b.order_system.estimate_delay_sec(c_ad_hoc, Order.Type.MOVE, false)
	var d_ok: float = b.order_system.estimate_delay_sec(c_ok, Order.Type.MOVE, false)
	assert_gt(d_ad_hoc, d_ok * 1.1, "비편제 지연 %.1f초 > 정상 %.1f초" % [d_ad_hoc, d_ok])
	b.dispose()


func test_deploy_phase_moves_within_zone_only() -> void:
	var b: Battle = BattleSetup.skirmish(cat, _us(), cat.default_force(&"RU"))
	var tf: TaskForce = b.task_forces[&"blue"]
	var c: Company = tf.companies[0]
	var zone: Rect2 = b.scenario.sides[&"blue"].deploy_zone
	assert_true(b.deploy_unit(c, Vector2(1000, 500), 0.0))
	assert_almost_eq(c.guide.pos.distance_to(Vector2(1000, 500)), 0.0, 30.0)
	assert_true(b.deploy_unit(c, Vector2(1000, 3000), 0.0), "구역 밖 지점은 구역 안으로 당겨진다")
	assert_true(zone.has_point(c.guide.pos), "중대 가이드 %s" % c.guide.pos)
	# 중대 안 소대를 고르면 중대가 움직인다.
	assert_true(b.deploy_unit(c.platoons[0], Vector2(2000, 400), 0.0))
	assert_almost_eq(c.guide.pos.distance_to(Vector2(2000, 400)), 0.0, 30.0)
	# 증원 부대는 배치할 수 없다.
	var reserve: Platoon = tf.companies[1].platoons[2]
	assert_false(reserve.on_map)
	assert_false(b.deploy_unit(reserve, Vector2(2000, 400), 0.0))
	b.begin_battle()
	assert_false(b.deploying)
	assert_false(b.deploy_unit(c, Vector2(1500, 400), 0.0), "전투가 시작되면 배치 불가")
	# 증원 부대는 시뮬에서 보이지 않는다(탐지·사격 대상 아님).
	b.run_seconds(5.0)
	for e: Element in reserve.elements:
		assert_false(e.is_active())
	b.dispose()


func test_spec_json_roundtrip_with_cross_attachment_and_deploy() -> void:
	var spec: ForceSpec = _us()
	spec.move_platoon(&"co_a", 1, &"co_b")
	spec.companies[0].platoons[0].loadout = &"tusk"
	spec.deploy["co:co_a"] = Vector3(1200, 500, 0.3)
	spec.cas_sorties = 1
	var text: String = spec.to_json()
	var back: ForceSpec = ForceSpec.from_json(text)
	assert_eq(back.companies[1].platoons.size(), 4)
	assert_eq(back.companies[1].platoons[3].home, &"co_a")
	assert_eq(back.companies[0].platoons[0].loadout, &"tusk")
	assert_almost_eq(back.deploy["co:co_a"].z, 0.3, 0.001)
	assert_eq(back.cas_sorties, 1)
	assert_null(ForceSpec.from_json("{ 깨진"), "깨진 JSON 은 null")


func test_red_default_force_is_valid() -> void:
	var rep: ForceRules.Report = ForceRules.evaluate(cat, cat.default_force(&"RU"), 2000)
	assert_true(rep.is_valid(), "%s" % [rep.errors])
	var toe_default: ForceSpec = ForceSpec.from_toe(cat, &"ru_btg_bmp")
	assert_eq(toe_default.companies.size(), 3, "TOE 기본값은 필수 슬롯만")
	assert_true(ForceRules.evaluate(cat, toe_default, 2000).is_valid())
