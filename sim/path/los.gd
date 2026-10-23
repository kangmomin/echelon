class_name Los
extends RefCounted
## 가시선(LOS). PLAN 17.4.
## - 시선: 관측자 눈높이(지면 + 2.5 m 차량 / 1.5 m 보병) → 표적(지면 + 2.0 m / 1.0 m).
## - 지형이 시선 위로 올라오면 차단.
## - 숲·시가지 피복(12 m / 10 m)은 양 끝 EDGE_CLEAR_M 안에서는 무시(숲 가장자리에서는 보고 보인다).
##   그 밖에서 피복이 시선을 가리는 구간이 COVER_BLOCK_M 를 넘으면 차단(DECISIONS D-018).
## - 연막: 연막 원(반경, 높이 20 m)을 시선이 지나면 차단.

const EYE_VEHICLE_M: float = 2.5
const EYE_FOOT_M: float = 1.5
const TARGET_VEHICLE_M: float = 2.0
const TARGET_FOOT_M: float = 1.0
const MIN_STEP_M: float = 10.0
const MAX_SAMPLES: int = 48
const EDGE_CLEAR_M: float = 30.0
const COVER_BLOCK_M: float = 60.0
const SMOKE_HEIGHT_M: float = 20.0


## 연막 구름.
class Smoke:
	extends RefCounted
	var pos: Vector2
	var radius: float
	var until_tick: int


static func eye_height(e: Element) -> float:
	return (EYE_FOOT_M if e.is_infantry() else EYE_VEHICLE_M) + e.altitude_m()


static func target_height(e: Element) -> float:
	return (TARGET_FOOT_M if e.is_infantry() else TARGET_VEHICLE_M) + e.altitude_m()


static func between(terrain: Terrain, smokes: Array[Smoke], tick: int, observer: Element, target: Element) -> bool:
	return check(terrain, smokes, tick, observer.pos, eye_height(observer), target.pos, target_height(target))


## a(높이 ah) → b(높이 bh) 시선이 열려 있는지.
static func check(terrain: Terrain, smokes: Array[Smoke], tick: int, a: Vector2, ah: float, b: Vector2, bh: float) -> bool:
	var d: float = a.distance_to(b)
	if d < 1.0:
		return true
	for s: Smoke in smokes:
		if s.until_tick > tick and Geometry2D.get_closest_point_to_segment(s.pos, a, b).distance_to(s.pos) < s.radius:
			return false
	var za: float = terrain.ground_height(a) + ah
	var zb: float = terrain.ground_height(b) + bh
	var n: int = clampi(ceili(d / MIN_STEP_M), 2, MAX_SAMPLES)
	var step: float = d / n
	var cover_run: float = 0.0
	for i: int in range(1, n):
		var t: float = float(i) / n
		var p: Vector2 = a.lerp(b, t)
		var z_line: float = lerpf(za, zb, t)
		var ground: float = terrain.ground_height(p)
		if ground > z_line:
			return false
		var along: float = t * d
		if along < EDGE_CLEAR_M or d - along < EDGE_CLEAR_M:
			continue
		var cover: float = terrain.cover_height_at(p)
		if cover > 0.0 and ground + cover > z_line:
			cover_run += step
			if cover_run > COVER_BLOCK_M:
				return false
	return true
