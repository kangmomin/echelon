extends SceneTree
## 시뮬 성능 측정: AI 대 AI 스커미시에 중대를 더 붙여 엘리먼트 ~300개로 만들고 시스템별 틱 시간을 잰다. PLAN 15.4, M7.
## 사용: godot --headless --path . -s res://tools/profile_sim.gd -- [--ticks=3000] [--extra=N]

func _init() -> void:
	var ticks: int = 3000
	var extra: int = 3
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--ticks="):
			ticks = arg.get_slice("=", 1).to_int()
		elif arg.begins_with("--extra="):
			extra = arg.get_slice("=", 1).to_int()
	var cat: DataCatalog = DataLoader.load_catalog()
	var t_create: int = Time.get_ticks_usec()
	var b: Battle = BattleSetup.skirmish(cat, cat.default_force(&"US"), cat.default_force(&"RU"), &"skirmish_01", 7,
		[&"blue", &"red"] as Array[StringName])
	# 엘리먼트 수를 늘린다(편성 제약 무시, 성능 측정용).
	for side: StringName in Battle.SIDES:
		var tf: TaskForce = b.task_forces[side]
		var zone: Rect2 = b.scenario.sides[side].deploy_zone
		var heading: float = Deployment.facing(zone, b.terrain.size_m)
		var templates: Array[StringName] = [&"us_tank_co", &"us_mech_co"]
		if side == &"red":
			templates = [&"ru_tank_co", &"ru_msr_co_bmp3"]
		for i: int in extra:
			var pos: Vector2 = Deployment.zone_point(zone, heading, (i + 0.5) / extra, 420.0)
			var c: Company = b.factory.spawn_company(cat.get_unit_template(templates[i % 2]), side,
				b.terrain.nearest_passable(pos, Terrain.MOBILITY_VEHICLE), heading, tf, tf.companies.size())
			tf.companies.append(c)
	print("엘리먼트 %d, 제대 %d, 생성 %.0f ms" % [b.elements.size(), b.units.size(), (Time.get_ticks_usec() - t_create) / 1000.0])
	b.begin_battle()
	var systems: Array = b.world._systems
	var total: Dictionary = {}
	var peak: Dictionary = {}
	var tick_peak: int = 0
	var all_start: int = Time.get_ticks_usec()
	for i: int in ticks:
		var t_tick: int = Time.get_ticks_usec()
		for s: Variant in systems:
			var sys: SimSystem = s
			var t0: int = Time.get_ticks_usec()
			sys.step(b.world)
			var dt: int = Time.get_ticks_usec() - t0
			var key: StringName = sys.get_system_name()
			total[key] = total.get(key, 0) + dt
			peak[key] = maxi(peak.get(key, 0), dt)
		b.world.tick += 1
		tick_peak = maxi(tick_peak, Time.get_ticks_usec() - t_tick)
		if i % 600 == 0:
			b.world.events.drain()
		if not b.result.is_empty():
			print("전투 종료 tick=%d" % b.world.tick)
			ticks = i + 1
			break
	var all_ms: float = (Time.get_ticks_usec() - all_start) / 1000.0
	print("틱 %d: 평균 %.2f ms/틱, 최대 %.2f ms/틱 (10 Hz 1배속 예산 100 ms, 4배속 25 ms)" % [ticks, all_ms / ticks, tick_peak / 1000.0])
	var keys: Array = total.keys()
	keys.sort_custom(func(a: Variant, c: Variant) -> bool: return int(total[a]) > int(total[c]))
	for k: Variant in keys:
		print("  %-14s 평균 %7.3f ms  최대 %7.2f ms" % [k, int(total[k]) / 1000.0 / ticks, int(peak[k]) / 1000.0])
	b.dispose()
	quit(0)
