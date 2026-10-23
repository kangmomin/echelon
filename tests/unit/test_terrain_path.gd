extends GutTest
## 지형 래스터화·경로탐색 테스트. PLAN 17.3, 18.3.

var terrain: Terrain
var pf: GridPathfinder


func before_all() -> void:
	var cat: DataCatalog = TestFixture.catalog()
	terrain = Terrain.build(cat.get_map(&"map01"))
	pf = GridPathfinder.new(terrain)


func test_grid_size_matches_map() -> void:
	assert_eq(terrain.width, 400)
	assert_eq(terrain.height_cells, 400)


func test_feature_rasterization() -> void:
	assert_eq(terrain.type_at(Vector2(1100, 1140)), Terrain.Type.WATER, "남강")
	assert_eq(terrain.type_at(Vector2(2400, 1200)), Terrain.Type.ROAD, "DELTA 다리(도로가 하천을 덮음)")
	assert_eq(terrain.type_at(Vector2(1300, 2550)), Terrain.Type.FOREST, "FOX 서쪽 숲")
	assert_eq(terrain.type_at(Vector2(3100, 1650)), Terrain.Type.URBAN, "CHARLIE 마을")
	assert_eq(terrain.type_at(Vector2(3000, 3500)), Terrain.Type.OPEN)


func test_ridge_is_higher_than_surroundings() -> void:
	var on_ridge: float = terrain.ground_height(Vector2(900, 1950))
	var off_ridge: float = terrain.ground_height(Vector2(900, 1350))
	assert_gt(on_ridge - off_ridge, 20.0, "BRAVO 능선이 주변보다 20 m 이상 높음")


func test_water_impassable_bridge_passable() -> void:
	assert_false(terrain.is_passable(Vector2(1100, 1140), Terrain.MOBILITY_VEHICLE))
	assert_true(terrain.is_passable(Vector2(2400, 1200), Terrain.MOBILITY_VEHICLE))
	assert_false(terrain.is_passable(Vector2(-10, 100), Terrain.MOBILITY_VEHICLE), "지도 밖")


func test_nearest_passable_leaves_water() -> void:
	var p: Vector2 = terrain.nearest_passable(Vector2(1100, 1140), Terrain.MOBILITY_VEHICLE)
	assert_true(terrain.is_passable(p, Terrain.MOBILITY_VEHICLE))
	assert_lt(p.distance_to(Vector2(1100, 1140)), 60.0)


func test_path_crosses_river_only_on_a_bridge() -> void:
	var path: PackedVector2Array = pf.find_path_now(Vector2(1600, 600), Vector2(1600, 1700), Terrain.MOBILITY_VEHICLE)
	assert_gt(path.size(), 1)
	assert_eq(path[path.size() - 1], Vector2(1600, 1700))
	for i: int in path.size() - 1:
		assert_true(terrain.line_passable(path[i], path[i + 1], Terrain.MOBILITY_VEHICLE), "구간 %d 통행 가능" % i)
	var bridge_used: bool = false
	for i: int in path.size() - 1:
		for k: int in 21:
			var q: Vector2 = path[i].lerp(path[i + 1], k / 20.0)
			if q.y > 1080.0 and q.y < 1320.0 and terrain.type_at(q) == Terrain.Type.ROAD and terrain.has_type_near(q, Terrain.Type.WATER, 30.0):
				bridge_used = true
	assert_true(bridge_used, "다리를 건넘")


func test_path_is_simplified() -> void:
	var path: PackedVector2Array = pf.find_path_now(Vector2(2600, 3300), Vector2(3600, 3600), Terrain.MOBILITY_VEHICLE)
	assert_lt(path.size(), 20, "개활지 직선 경로는 점이 적어야 함 (%d)" % path.size())


func test_vehicle_prefers_road_over_forest() -> void:
	var path: PackedVector2Array = pf.find_path_now(Vector2(1200, 2250), Vector2(1450, 2750), Terrain.MOBILITY_VEHICLE)
	var forest_m: float = 0.0
	for i: int in path.size() - 1:
		var seg: float = path[i].distance_to(path[i + 1])
		for k: int in 10:
			if terrain.type_at(path[i].lerp(path[i + 1], (k + 0.5) / 10.0)) == Terrain.Type.FOREST:
				forest_m += seg / 10.0
	var straight: float = Vector2(1200, 2250).distance_to(Vector2(1450, 2750))
	assert_lt(forest_m, straight, "숲 통과 거리가 직선 거리보다 짧음 (%.0f m)" % forest_m)


func test_request_queue_processes_fifo_with_budget() -> void:
	var reqs: Array[GridPathfinder.PathRequest] = []
	for i: int in 6:
		reqs.append(pf.request(Vector2(2600, 3300), Vector2(2700 + i * 50, 3500), Terrain.MOBILITY_VEHICLE, &"blue"))
	var budget: int = GridPathfinder.MAX_REQUESTS_PER_TICK
	assert_eq(pf.process(), budget)
	assert_true(reqs[budget - 1].done and not reqs[budget].done, "틱당 %d건, 요청 순서대로" % budget)
	for i: int in 6:
		pf.process()
	assert_true(reqs[5].done and reqs[5].ok)


func test_threat_cost_diverts_path() -> void:
	var a := Vector2(2600, 3400)
	var b := Vector2(3600, 3400)
	var before: PackedVector2Array = pf.find_path_now(a, b, Terrain.MOBILITY_VEHICLE, &"red")
	pf.add_threat(&"red", Vector2(3100, 3400), 200.0, 4.0)
	var after: PackedVector2Array = pf.find_path_now(a, b, Terrain.MOBILITY_VEHICLE, &"red")
	var other_side: PackedVector2Array = pf.find_path_now(a, b, Terrain.MOBILITY_VEHICLE, &"blue")
	assert_eq(other_side.size(), before.size(), "다른 진영 경로는 영향 없음")
	assert_gt(_max_offset(after, 3400.0), 100.0, "위협 지역을 우회")
	pf.decay_threat(0.0)
	assert_eq(pf.threat_at(&"red", Vector2(3100, 3400)), 0.0)


func _max_offset(path: PackedVector2Array, y: float) -> float:
	var m: float = 0.0
	for i: int in path.size() - 1:
		for k: int in 11:
			m = maxf(m, absf(path[i].lerp(path[i + 1], k / 10.0).y - y))
	return m
