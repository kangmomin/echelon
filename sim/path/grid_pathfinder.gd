class_name GridPathfinder
extends RefCounted
## AStarGrid2D 기반 경로탐색. PLAN 17.3.
##
## - 진영(blue/red) × 기동 유형(vehicle/foot)마다 그리드 하나. 진영별로 위협 비용이 다르기 때문이다.
## - 지형 비용은 셀 가중치(set_point_weight_scale), 통행 불가는 solid.
## - 요청은 큐에 쌓고 틱마다 최대 MAX_REQUESTS_PER_TICK 개를 FIFO 로 처리한다(결정론).
## - jumping_enabled 는 가중치를 무시하므로 켜지 않는다.

const MAX_REQUESTS_PER_TICK: int = 2
## [OPEN, ROAD, FOREST, URBAN, WATER]
const WEIGHT_VEHICLE: Array[float] = [1.0, 0.6, 1.8, 1.5, 0.0]
const WEIGHT_FOOT: Array[float] = [1.0, 0.8, 1.2, 1.0, 0.0]
## 경사 10° 를 넘으면 5° 마다 +0.5.
const SLOPE_FREE_DEG: float = 10.0
const SLOPE_STEP_DEG: float = 5.0
const SLOPE_STEP_COST: float = 0.5
## 경로 단순화 때 한 번에 건너뛸 수 있는 최대 점 수.
const SIMPLIFY_LOOKAHEAD: int = 40
## 단순화한 직선이 원래 경로보다 이만큼까지 비싼 셀을 지나도 허용.
const SIMPLIFY_WEIGHT_TOLERANCE: float = 0.05
## 위협 비용 상한(가중치에 더해지는 값).
const THREAT_MAX: float = 4.0

const SIDES: Array[StringName] = [&"blue", &"red"]
const MOBILITIES: Array[StringName] = [&"vehicle", &"foot"]


class PathRequest:
	extends RefCounted
	var from: Vector2
	var to: Vector2
	var mobility: StringName
	var side: StringName
	var points: PackedVector2Array = PackedVector2Array()
	var done: bool = false
	var ok: bool = false
	var cancelled: bool = false


var terrain: Terrain
var requests_processed: int = 0

var _grids: Dictionary[StringName, AStarGrid2D] = {}
var _base_weights: Dictionary[StringName, PackedFloat32Array] = {}
var _threat: Dictionary[StringName, PackedFloat32Array] = {}
var _threat_cells: Dictionary[StringName, Dictionary] = {}
var _queue: Array[PathRequest] = []


func _init(p_terrain: Terrain) -> void:
	terrain = p_terrain
	for mob: StringName in MOBILITIES:
		_base_weights[mob] = _compute_base_weights(mob)
	for side: StringName in SIDES:
		var threat := PackedFloat32Array()
		threat.resize(terrain.width * terrain.height_cells)
		_threat[side] = threat
		_threat_cells[side] = {}


# --- 요청 큐 -------------------------------------------------------------

func request(from: Vector2, to: Vector2, mobility: StringName, side: StringName) -> PathRequest:
	var req := PathRequest.new()
	req.from = from
	req.to = to
	req.mobility = mobility
	req.side = side if SIDES.has(side) else &"blue"
	if mobility == Terrain.MOBILITY_AIR:
		# 헬기: 직선 비행. 대기열을 거치지 않는다.
		req.points = find_path_now(from, to, mobility, side)
		req.ok = req.points.size() >= 2
		req.done = true
		return req
	_queue.append(req)
	return req


func pending_count() -> int:
	return _queue.size()


## 틱마다 호출. 처리한 요청 수를 돌려준다.
func process(budget: int = MAX_REQUESTS_PER_TICK) -> int:
	var n: int = 0
	while n < budget and not _queue.is_empty():
		var req: PathRequest = _queue.pop_front()
		if req.cancelled:
			continue
		req.points = find_path_now(req.from, req.to, req.mobility, req.side)
		req.ok = req.points.size() >= 2
		req.done = true
		n += 1
		requests_processed += 1
	return n


# --- 동기 탐색 -----------------------------------------------------------

## 단순화된 경유점 배열. 첫 점은 from(또는 가까운 통행 가능 점), 끝 점은 to(또는 가장 가까이 갈 수 있는 점).
func find_path_now(from: Vector2, to: Vector2, mobility: StringName, side: StringName = &"blue") -> PackedVector2Array:
	if mobility == Terrain.MOBILITY_AIR:
		return PackedVector2Array([from, terrain.clamp_to_map(to, 20.0)])
	var grid: AStarGrid2D = _grid(side if SIDES.has(side) else &"blue", mobility)
	var start: Vector2 = terrain.nearest_passable(from, mobility)
	var goal: Vector2 = terrain.nearest_passable(to, mobility)
	var c_from: Vector2i = terrain.cell_of(start)
	var c_to: Vector2i = terrain.cell_of(goal)
	if not grid.is_in_boundsv(c_from) or not grid.is_in_boundsv(c_to):
		return PackedVector2Array()
	if c_from == c_to:
		return PackedVector2Array([from, goal])
	var raw: PackedVector2Array = grid.get_point_path(c_from, c_to, true)
	if raw.size() < 2:
		return PackedVector2Array()
	raw[0] = start
	# 부분 경로(목표에 못 닿음)면 마지막 셀 중심에서 끝난다.
	if terrain.cell_of(raw[raw.size() - 1]) == c_to:
		raw[raw.size() - 1] = goal
	var simplified: PackedVector2Array = simplify(raw, mobility)
	if start.distance_to(from) > 0.5 and terrain.is_passable(from, mobility):
		simplified[0] = from
	return simplified


## 문자열 당기기(string pulling): 직선으로 갈 수 있고 더 비싼 지형을 지나지 않으면 중간 점을 건너뛴다.
func simplify(points: PackedVector2Array, mobility: StringName) -> PackedVector2Array:
	var n: int = points.size()
	if n <= 2:
		return points
	var weights: PackedFloat32Array = _base_weights[mobility]
	var out := PackedVector2Array([points[0]])
	var i: int = 0
	var w_at := PackedFloat32Array()
	w_at.resize(n)
	for k: int in n:
		w_at[k] = _weight_at(weights, points[k])
	var prefix_max := PackedFloat32Array()
	prefix_max.resize(SIMPLIFY_LOOKAHEAD + 1)
	while i < n - 1:
		var j: int = mini(n - 1, i + SIMPLIFY_LOOKAHEAD)
		# i 부터의 누적 최대 가중치(성능: j 마다 다시 훑지 않는다).
		var running: float = 0.0
		for k: int in range(i, j + 1):
			running = maxf(running, w_at[k])
			prefix_max[k - i] = running
		while j > i + 1:
			if _segment_ok(points[i], points[j], mobility, weights, prefix_max[j - i] + SIMPLIFY_WEIGHT_TOLERANCE):
				break
			j -= 1
		out.append(points[j])
		i = j
	return out


# --- 위협 비용 -----------------------------------------------------------

## side 가 보기에 pos 반경 radius_m 이 위험하다. 경로 가중치에 amount 를 더한다(상한 THREAT_MAX).
func add_threat(side: StringName, pos: Vector2, radius_m: float, amount: float) -> void:
	if not SIDES.has(side):
		return
	var threat: PackedFloat32Array = _threat[side]
	var cells: Dictionary = _threat_cells[side]
	var c: Vector2i = terrain.cell_of(pos)
	var r: int = ceili(radius_m / terrain.cell_m)
	for dy: int in range(-r, r + 1):
		for dx: int in range(-r, r + 1):
			var cc := Vector2i(c.x + dx, c.y + dy)
			if not terrain.in_bounds(cc) or Vector2(dx, dy).length() * terrain.cell_m > radius_m:
				continue
			var idx: int = terrain.index_of(cc)
			threat[idx] = minf(THREAT_MAX, threat[idx] + amount)
			cells[idx] = true
	_threat[side] = threat
	_apply_cells(side, cells.keys())


## 위협 비용을 factor 배로 줄인다. 0.05 미만이면 지운다.
func decay_threat(factor: float) -> void:
	for side: StringName in SIDES:
		var threat: PackedFloat32Array = _threat[side]
		var cells: Dictionary = _threat_cells[side]
		var keys: Array = cells.keys()
		for key: Variant in keys:
			var idx: int = key
			threat[idx] *= factor
			if threat[idx] < 0.05:
				threat[idx] = 0.0
				cells.erase(idx)
		_threat[side] = threat
		_apply_cells(side, keys)


func threat_at(side: StringName, pos: Vector2) -> float:
	var c: Vector2i = terrain.cell_of(pos)
	if not terrain.in_bounds(c) or not _threat.has(side):
		return 0.0
	return _threat[side][terrain.index_of(c)]


# --- 내부 ----------------------------------------------------------------

func _key(side: StringName, mobility: StringName) -> StringName:
	return StringName("%s:%s" % [side, mobility])


## 그리드는 처음 쓸 때 만든다(진영·기동 유형마다 16만 셀 설정이 0.2초쯤 걸린다).
func _grid(side: StringName, mobility: StringName) -> AStarGrid2D:
	var key: StringName = _key(side, mobility)
	if not _grids.has(key):
		var grid: AStarGrid2D = _make_grid(mobility)
		_grids[key] = grid
		var threat: PackedFloat32Array = _threat[side]
		var weights: PackedFloat32Array = _base_weights[mobility]
		for key2: Variant in (_threat_cells[side] as Dictionary).keys():
			var idx: int = key2
			if weights[idx] > 0.0:
				grid.set_point_weight_scale(Vector2i(idx % terrain.width, floori(float(idx) / terrain.width)), weights[idx] + threat[idx])
	return _grids[key]


func _compute_base_weights(mobility: StringName) -> PackedFloat32Array:
	var table: Array[float] = WEIGHT_VEHICLE if mobility == &"vehicle" else WEIGHT_FOOT
	var out := PackedFloat32Array()
	out.resize(terrain.width * terrain.height_cells)
	for cy: int in terrain.height_cells:
		for cx: int in terrain.width:
			var c := Vector2i(cx, cy)
			var idx: int = terrain.index_of(c)
			if not terrain.is_passable_cell(c, mobility):
				out[idx] = 0.0
				continue
			var t: int = terrain.types[idx]
			var w: float = table[t]
			var slope: float = terrain.slopes[idx]
			if t != Terrain.Type.ROAD and slope > SLOPE_FREE_DEG:
				w += SLOPE_STEP_COST * ceilf((slope - SLOPE_FREE_DEG) / SLOPE_STEP_DEG)
			out[idx] = w
	return out


func _make_grid(mobility: StringName) -> AStarGrid2D:
	var grid := AStarGrid2D.new()
	grid.region = Rect2i(0, 0, terrain.width, terrain.height_cells)
	grid.cell_size = Vector2(terrain.cell_m, terrain.cell_m)
	grid.offset = Vector2(terrain.cell_m * 0.5, terrain.cell_m * 0.5)
	grid.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_ONLY_IF_NO_OBSTACLES
	grid.default_compute_heuristic = AStarGrid2D.HEURISTIC_OCTILE
	grid.default_estimate_heuristic = AStarGrid2D.HEURISTIC_OCTILE
	grid.jumping_enabled = false
	grid.update()
	var weights: PackedFloat32Array = _base_weights[mobility]
	for cy: int in terrain.height_cells:
		for cx: int in terrain.width:
			var w: float = weights[cy * terrain.width + cx]
			var c := Vector2i(cx, cy)
			if w <= 0.0:
				grid.set_point_solid(c, true)
			elif w != 1.0:
				grid.set_point_weight_scale(c, w)
	return grid


func _apply_cells(side: StringName, cell_indices: Array) -> void:
	var threat: PackedFloat32Array = _threat[side]
	for mob: StringName in MOBILITIES:
		var grid_key: StringName = _key(side, mob)
		if not _grids.has(grid_key):
			continue
		var grid: AStarGrid2D = _grids[grid_key]
		var weights: PackedFloat32Array = _base_weights[mob]
		for key: Variant in cell_indices:
			var idx: int = key
			if weights[idx] <= 0.0:
				continue
			grid.set_point_weight_scale(Vector2i(idx % terrain.width, floori(float(idx) / terrain.width)), weights[idx] + threat[idx])


func _weight_at(weights: PackedFloat32Array, p: Vector2) -> float:
	var c: Vector2i = terrain.cell_of(p)
	if not terrain.in_bounds(c):
		return 0.0
	return weights[terrain.index_of(c)]


func _segment_ok(a: Vector2, b: Vector2, mobility: StringName, weights: PackedFloat32Array, max_w: float) -> bool:
	var dist: float = a.distance_to(b)
	var steps: int = maxi(1, ceili(dist / (terrain.cell_m * 0.5)))
	for s: int in steps + 1:
		var p: Vector2 = a.lerp(b, float(s) / steps)
		if not terrain.is_passable(p, mobility):
			return false
		if _weight_at(weights, p) > max_w:
			return false
	return true
