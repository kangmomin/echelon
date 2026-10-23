class_name Terrain
extends RefCounted
## 지형 그리드. MapDef 를 래스터화해 셀(기본 10 m)마다 지형 종류·높이·경사를 가진다. PLAN 15.1, 17.3.
## 좌표: x = 동, y = 북, 원점 = 남서쪽. 셀 (cx, cy) 의 중심 = ((cx + 0.5) * cell_m, (cy + 0.5) * cell_m).

enum Type { OPEN, ROAD, FOREST, URBAN, WATER }

const TYPE_NAMES: Array[StringName] = [&"open", &"road", &"forest", &"urban", &"water"]
## 지형 종류별 피복 높이(m). LOS 판정에 지면 높이와 더해 쓴다. PLAN 17.4.
const COVER_HEIGHT: Array[float] = [0.0, 0.0, 12.0, 10.0, 0.0]
## 이동 속도 배율(도로는 차량의 도로 속도를 따로 쓴다). [차량, 도보]
const SPEED_FACTOR_VEHICLE: Array[float] = [1.0, 1.0, 0.5, 0.6, 0.0]
const SPEED_FACTOR_FOOT: Array[float] = [1.0, 1.0, 0.8, 0.9, 0.0]
## 이 경사(도)를 넘으면 차량 통행 불가.
const VEHICLE_MAX_SLOPE_DEG: float = 35.0
const FOOT_MAX_SLOPE_DEG: float = 50.0
## 하천 바닥을 주변보다 얼마나 낮출지(m).
const RIVER_DEPTH_M: float = 6.0

const MOBILITY_VEHICLE: StringName = &"vehicle"
const MOBILITY_FOOT: StringName = &"foot"

var map: MapDef
var size_m: Vector2
var cell_m: float
var width: int
var height_cells: int
var types: PackedByteArray
## 셀 중심의 지면 높이(m).
var heights: PackedFloat32Array
## 셀의 최대 경사(도).
var slopes: PackedFloat32Array
## 통행 가능 비트맵(1 = 가능). 자주 쓰는 판정을 함수 호출 없이 배열 조회로 한다.
var passable_vehicle: PackedByteArray
var passable_foot: PackedByteArray
## 차량 통행 여유(셀): 이 셀에서 체비셰프 거리 clearance 미만 안의 셀은 모두 차량 통행 가능(최대 255).
## line_passable 이 짧은 선분을 배열 순회 없이 바로 통과시키는 데 쓴다(성능, M7).
var clearance_vehicle: PackedByteArray
## 다리(하천 30 m 안의 도로) 셀.
var bridge_mask: PackedByteArray


static func build(p_map: MapDef) -> Terrain:
	var t := Terrain.new()
	t.map = p_map
	t.size_m = p_map.size_m
	t.cell_m = p_map.cell_m
	t.width = ceili(p_map.size_m.x / p_map.cell_m)
	t.height_cells = ceili(p_map.size_m.y / p_map.cell_m)
	var n: int = t.width * t.height_cells
	t.types = PackedByteArray()
	t.types.resize(n)
	t.types.fill(Type.OPEN)
	t.heights = PackedFloat32Array()
	t.heights.resize(n)
	t.slopes = PackedFloat32Array()
	t.slopes.resize(n)
	t._build_heights()
	for f: MapDef.Feature in p_map.forests:
		t._fill_polygon(f.points, Type.FOREST)
	for f: MapDef.Feature in p_map.towns:
		t._fill_polygon(f.points, Type.URBAN)
	for f: MapDef.Feature in p_map.rivers:
		t._fill_polyline(f.points, f.width_m, Type.WATER)
		t._carve_river(f)
	for f: MapDef.Feature in p_map.roads:
		t._fill_polyline(f.points, f.width_m, Type.ROAD)
	t._build_slopes()
	t._build_masks()
	t._build_clearance()
	return t


# --- 좌표 ----------------------------------------------------------------

func cell_of(p: Vector2) -> Vector2i:
	return Vector2i(floori(p.x / cell_m), floori(p.y / cell_m))


func cell_center(c: Vector2i) -> Vector2:
	return Vector2((c.x + 0.5) * cell_m, (c.y + 0.5) * cell_m)


func in_bounds(c: Vector2i) -> bool:
	return c.x >= 0 and c.y >= 0 and c.x < width and c.y < height_cells


func contains(p: Vector2) -> bool:
	return p.x >= 0.0 and p.y >= 0.0 and p.x < size_m.x and p.y < size_m.y


func index_of(c: Vector2i) -> int:
	return c.y * width + c.x


func clamp_to_map(p: Vector2, margin: float = 1.0) -> Vector2:
	return Vector2(clampf(p.x, margin, size_m.x - margin), clampf(p.y, margin, size_m.y - margin))


# --- 조회 ----------------------------------------------------------------

## 지도 밖은 WATER(통행 불가)로 취급한다.
func type_at_cell(c: Vector2i) -> int:
	if not in_bounds(c):
		return Type.WATER
	return types[index_of(c)]


func type_at(p: Vector2) -> int:
	return type_at_cell(cell_of(p))


func type_name_at(p: Vector2) -> StringName:
	return TYPE_NAMES[type_at(p)]


func cover_height_at(p: Vector2) -> float:
	return COVER_HEIGHT[type_at(p)]


func slope_at_cell(c: Vector2i) -> float:
	if not in_bounds(c):
		return 90.0
	return slopes[index_of(c)]


## 셀 중심 높이를 쌍선형 보간한 지면 높이.
func ground_height(p: Vector2) -> float:
	var fx: float = p.x / cell_m - 0.5
	var fy: float = p.y / cell_m - 0.5
	var x0: int = clampi(floori(fx), 0, width - 1)
	var y0: int = clampi(floori(fy), 0, height_cells - 1)
	var x1: int = mini(x0 + 1, width - 1)
	var y1: int = mini(y0 + 1, height_cells - 1)
	var tx: float = clampf(fx - x0, 0.0, 1.0)
	var ty: float = clampf(fy - y0, 0.0, 1.0)
	var h00: float = heights[y0 * width + x0]
	var h10: float = heights[y0 * width + x1]
	var h01: float = heights[y1 * width + x0]
	var h11: float = heights[y1 * width + x1]
	return lerpf(lerpf(h00, h10, tx), lerpf(h01, h11, tx), ty)


func is_passable_cell(c: Vector2i, mobility: StringName) -> bool:
	if c.x < 0 or c.y < 0 or c.x >= width or c.y >= height_cells:
		return false
	var mask: PackedByteArray = passable_vehicle if mobility == MOBILITY_VEHICLE else passable_foot
	return mask[c.y * width + c.x] == 1


func is_passable(p: Vector2, mobility: StringName) -> bool:
	var cx: int = floori(p.x / cell_m)
	var cy: int = floori(p.y / cell_m)
	if cx < 0 or cy < 0 or cx >= width or cy >= height_cells:
		return false
	var mask: PackedByteArray = passable_vehicle if mobility == MOBILITY_VEHICLE else passable_foot
	return mask[cy * width + cx] == 1


func is_bridge(p: Vector2) -> bool:
	var cx: int = floori(p.x / cell_m)
	var cy: int = floori(p.y / cell_m)
	if cx < 0 or cy < 0 or cx >= width or cy >= height_cells:
		return false
	return bridge_mask[cy * width + cx] == 1


func speed_factor(type: int, mobility: StringName) -> float:
	return SPEED_FACTOR_VEHICLE[type] if mobility == MOBILITY_VEHICLE else SPEED_FACTOR_FOOT[type]


## a→b 직선이 통행 가능한 셀만 지나는지. 셀 절반 간격으로 샘플링한다.
func line_passable(a: Vector2, b: Vector2, mobility: StringName) -> bool:
	var mask: PackedByteArray = passable_vehicle if mobility == MOBILITY_VEHICLE else passable_foot
	var dist: float = a.distance_to(b)
	# 빠른 길: 시작 셀 주변 여유 안에 선분이 다 들어가면 통과(차량이 통행 가능하면 도보도 가능).
	var ax: int = floori(a.x / cell_m)
	var ay: int = floori(a.y / cell_m)
	if ax >= 0 and ay >= 0 and ax < width and ay < height_cells and not clearance_vehicle.is_empty():
		if dist / cell_m + 1.0 < clearance_vehicle[ay * width + ax]:
			return true
	var steps: int = maxi(1, ceili(dist / (cell_m * 0.5)))
	var inv: float = 1.0 / cell_m
	var dx: float = (b.x - a.x) / steps
	var dy: float = (b.y - a.y) / steps
	var x: float = a.x
	var y: float = a.y
	var last: int = -1
	for i: int in steps + 1:
		var cx: int = floori(x * inv)
		var cy: int = floori(y * inv)
		if cx < 0 or cy < 0 or cx >= width or cy >= height_cells:
			return false
		var idx: int = cy * width + cx
		if idx != last:
			if mask[idx] == 0:
				return false
			last = idx
		x += dx
		y += dy
	return true


## p 가 통행 불가면 가장 가까운 통행 가능 셀 중심을 돌려준다(최대 max_rings 셀 반경). 없으면 p.
func nearest_passable(p: Vector2, mobility: StringName, max_rings: int = 30) -> Vector2:
	var c: Vector2i = cell_of(clamp_to_map(p))
	if is_passable_cell(c, mobility):
		return clamp_to_map(p)
	for ring: int in range(1, max_rings + 1):
		var best: Vector2 = Vector2.INF
		var best_d: float = INF
		for dx: int in range(-ring, ring + 1):
			for dy: int in range(-ring, ring + 1):
				if absi(dx) != ring and absi(dy) != ring:
					continue
				var cc := Vector2i(c.x + dx, c.y + dy)
				if is_passable_cell(cc, mobility):
					var center: Vector2 = cell_center(cc)
					var d: float = center.distance_squared_to(p)
					if d < best_d:
						best_d = d
						best = center
		if best_d < INF:
			return best
	return p


## 반경 안에 특정 종류의 셀이 있는지.
func has_type_near(p: Vector2, type: int, radius_m: float) -> bool:
	var c: Vector2i = cell_of(p)
	var r: int = ceili(radius_m / cell_m)
	for dx: int in range(-r, r + 1):
		for dy: int in range(-r, r + 1):
			if type_at_cell(Vector2i(c.x + dx, c.y + dy)) == type:
				return true
	return false


# --- 생성 ----------------------------------------------------------------

func _build_heights() -> void:
	var noise := FastNoiseLite.new()
	noise.seed = map.height_seed
	noise.noise_type = FastNoiseLite.TYPE_SIMPLEX_SMOOTH
	noise.frequency = map.frequency
	noise.fractal_type = FastNoiseLite.FRACTAL_FBM
	noise.fractal_octaves = map.octaves
	for cy: int in height_cells:
		for cx: int in width:
			var p: Vector2 = cell_center(Vector2i(cx, cy))
			heights[cy * width + cx] = map.base_m + map.amplitude_m * noise.get_noise_2d(p.x, p.y)
	_ridge_bump = PackedFloat32Array()
	_ridge_bump.resize(heights.size())
	for ridge: MapDef.Feature in map.ridges:
		_add_ridge(ridge)
	for i: int in heights.size():
		heights[i] += _ridge_bump[i]
	_ridge_bump = PackedFloat32Array()


## 능선 높이 보정. 겹치는 선분·능선끼리는 큰 값만 쓴다(두 번 더해지지 않게).
## 람다는 지역 Packed 배열을 복사해서 캡처하므로 멤버 배열에 쓴다.
var _ridge_bump: PackedFloat32Array = PackedFloat32Array()


func _add_ridge(ridge: MapDef.Feature) -> void:
	var half: float = ridge.width_m * 0.5
	for i: int in ridge.points.size() - 1:
		_for_cells_near_segment(ridge.points[i], ridge.points[i + 1], half, func(idx: int, dist: float) -> void:
			var bump: float = ridge.height_m * 0.5 * (1.0 + cos(PI * dist / half))
			_ridge_bump[idx] = maxf(_ridge_bump[idx], bump))


func _carve_river(river: MapDef.Feature) -> void:
	var half: float = river.width_m * 1.5
	for i: int in river.points.size() - 1:
		_for_cells_near_segment(river.points[i], river.points[i + 1], half, func(idx: int, dist: float) -> void:
			var depth: float = RIVER_DEPTH_M * (1.0 - clampf(dist / half, 0.0, 1.0))
			heights[idx] = heights[idx] - depth * 0.5)


## 두 번 훑는 체비셰프 거리 변환. 통행 불가 셀 = 0.
func _build_clearance() -> void:
	var n: int = width * height_cells
	var d := PackedInt32Array()
	d.resize(n)
	for i: int in n:
		d[i] = 0 if passable_vehicle[i] == 0 else 255
	for cy: int in height_cells:
		for cx: int in width:
			var i: int = cy * width + cx
			if d[i] == 0:
				continue
			var v: int = d[i]
			if cx == 0 or cy == 0:
				v = 1
			else:
				v = mini(v, mini(mini(d[i - 1], d[i - width]), mini(d[i - width - 1], d[i - width + 1] if cx + 1 < width else 0)) + 1)
			d[i] = v
	for cy: int in range(height_cells - 1, -1, -1):
		for cx: int in range(width - 1, -1, -1):
			var i: int = cy * width + cx
			if d[i] == 0:
				continue
			var v: int = d[i]
			if cx == width - 1 or cy == height_cells - 1:
				v = 1
			else:
				v = mini(v, mini(mini(d[i + 1], d[i + width]), mini(d[i + width + 1], d[i + width - 1] if cx > 0 else 0)) + 1)
			d[i] = v
	clearance_vehicle = PackedByteArray()
	clearance_vehicle.resize(n)
	for i: int in n:
		clearance_vehicle[i] = mini(d[i], 255)


func _build_masks() -> void:
	var n: int = width * height_cells
	passable_vehicle = PackedByteArray()
	passable_vehicle.resize(n)
	passable_foot = PackedByteArray()
	passable_foot.resize(n)
	bridge_mask = PackedByteArray()
	bridge_mask.resize(n)
	for i: int in n:
		var t: int = types[i]
		if t == Type.WATER:
			continue
		passable_vehicle[i] = 1 if t == Type.ROAD or slopes[i] <= VEHICLE_MAX_SLOPE_DEG else 0
		passable_foot[i] = 1 if t == Type.ROAD or slopes[i] <= FOOT_MAX_SLOPE_DEG else 0
	var r: int = ceili(30.0 / cell_m)
	for cy: int in height_cells:
		for cx: int in width:
			var idx: int = cy * width + cx
			if types[idx] != Type.ROAD:
				continue
			var near_water: bool = false
			for dy: int in range(-r, r + 1):
				for dx: int in range(-r, r + 1):
					var x: int = cx + dx
					var y: int = cy + dy
					if x >= 0 and y >= 0 and x < width and y < height_cells and types[y * width + x] == Type.WATER:
						near_water = true
						break
				if near_water:
					break
			if near_water:
				bridge_mask[idx] = 1


func _build_slopes() -> void:
	for cy: int in height_cells:
		for cx: int in width:
			var h: float = heights[cy * width + cx]
			var hx: float = heights[cy * width + mini(cx + 1, width - 1)]
			var hy: float = heights[mini(cy + 1, height_cells - 1) * width + cx]
			var grad: float = maxf(absf(hx - h), absf(hy - h)) / cell_m
			slopes[cy * width + cx] = rad_to_deg(atan(grad))


func _fill_polygon(points: PackedVector2Array, type: int) -> void:
	var bbox := Rect2(points[0], Vector2.ZERO)
	for p: Vector2 in points:
		bbox = bbox.expand(p)
	var c0: Vector2i = cell_of(bbox.position)
	var c1: Vector2i = cell_of(bbox.end)
	for cy: int in range(maxi(c0.y, 0), mini(c1.y, height_cells - 1) + 1):
		for cx: int in range(maxi(c0.x, 0), mini(c1.x, width - 1) + 1):
			if Geometry2D.is_point_in_polygon(cell_center(Vector2i(cx, cy)), points):
				types[cy * width + cx] = type


func _fill_polyline(points: PackedVector2Array, width_m: float, type: int) -> void:
	var half: float = maxf(width_m * 0.5, cell_m * 0.75)
	for i: int in points.size() - 1:
		_for_cells_near_segment(points[i], points[i + 1], half, func(idx: int, _dist: float) -> void:
			types[idx] = type)


## 선분 a-b 에서 radius 이내인 셀마다 fn(셀 인덱스, 거리)를 부른다.
func _for_cells_near_segment(a: Vector2, b: Vector2, radius: float, fn: Callable) -> void:
	var bbox := Rect2(a, Vector2.ZERO).expand(b).grow(radius)
	var c0: Vector2i = cell_of(bbox.position)
	var c1: Vector2i = cell_of(bbox.end)
	for cy: int in range(maxi(c0.y, 0), mini(c1.y, height_cells - 1) + 1):
		for cx: int in range(maxi(c0.x, 0), mini(c1.x, width - 1) + 1):
			var center: Vector2 = cell_center(Vector2i(cx, cy))
			var dist: float = Geometry2D.get_closest_point_to_segment(center, a, b).distance_to(center)
			if dist <= radius:
				fn.call(cy * width + cx, dist)
