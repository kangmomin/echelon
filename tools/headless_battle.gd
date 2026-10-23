extends SceneTree
## 헤드리스 AI 대 AI 한 판. 사용: godot --headless --path . -s res://tools/headless_battle.gd -- [--seed=N] [--out=PATH] [--quiet]
##   [--blue=편성.json] [--red=편성.json]  (없으면 data/forces 기본 편성)
## 2분마다 진행 상황, 끝나면 AAR JSON 을 출력(또는 --out 파일로 저장). PLAN 18.4.

func _init() -> void:
	var seed_value: int = -1
	var out_path: String = ""
	var quiet: bool = false
	var blue_path: String = ""
	var red_path: String = ""
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--seed="):
			seed_value = arg.get_slice("=", 1).to_int()
		elif arg.begins_with("--out="):
			out_path = arg.get_slice("=", 1)
		elif arg == "--quiet":
			quiet = true
		elif arg.begins_with("--blue="):
			blue_path = arg.get_slice("=", 1)
		elif arg.begins_with("--red="):
			red_path = arg.get_slice("=", 1)
	var cat: DataCatalog = DataLoader.load_catalog()
	if not cat.is_valid():
		print("데이터 오류: ", cat.errors)
		quit(1)
		return
	var blue: ForceSpec = ForceStore.load_spec(blue_path) if blue_path != "" else cat.default_force(&"US")
	var red: ForceSpec = ForceStore.load_spec(red_path) if red_path != "" else cat.default_force(&"RU")
	if blue == null or red == null:
		print("편성 파일을 읽을 수 없음")
		quit(1)
		return
	var b: Battle = BattleSetup.skirmish(cat, blue, red, &"skirmish_01",
		seed_value, [&"blue", &"red"] as Array[StringName])
	b.begin_battle()
	var start_usec: int = Time.get_ticks_usec()
	var limit: int = roundi(b.scenario.time_limit_min * 60.0 * SimConst.TICK_RATE) + 100
	while b.result.is_empty() and b.tick() < limit:
		b.step_tick()
		if b.tick() % 1200 == 0:
			b.world.events.drain()
			if not quiet:
				var pts: PackedStringArray = []
				for ps: ScoringSystem.PointState in b.scoring.points:
					pts.append("%s:%s" % [ps.def.id.trim_prefix("cp_"), ps.owner if ps.owner != &"" else "-"])
				print("[%02d:00] VP 청 %.1f 적 %.1f | 전력 청 %.0f%% 적 %.0f%% | %s | %.1f s" % [b.tick() / 600,
					b.scoring.vp[&"blue"], b.scoring.vp[&"red"], b.scoring.value_ratio(&"blue") * 100.0,
					b.scoring.value_ratio(&"red") * 100.0, " ".join(pts), (Time.get_ticks_usec() - start_usec) / 1e6])
	var aar: Dictionary = b.stats.summary()
	aar["real_sec"] = (Time.get_ticks_usec() - start_usec) / 1e6
	aar["seed"] = b.world.rng.seed_value
	var text: String = JSON.stringify(aar, "  ")
	if out_path != "":
		var f: FileAccess = FileAccess.open(out_path, FileAccess.WRITE)
		f.store_string(text)
		f.close()
		print("AAR -> ", out_path)
	else:
		print(text)
	print("[result] winner=%s reason=%s t=%.0fs" % [b.result.get("winner", "?"), b.result.get("reason", "?"),
		b.world.get_sim_time_sec()])
	b.dispose()
	quit(0)
