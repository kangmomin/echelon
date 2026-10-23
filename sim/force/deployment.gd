class_name Deployment
extends RefCounted
## 배치 구역 안 자동 배치와 배치 위치 검사. PLAN 6.1, 11.2.
## 배치 키: "cp"(대대 지휘소), "co:<슬롯>", "org:<i>", "att:<i>". 값은 Vector3(x, y, heading).

## 앞선(적 쪽 구역 경계)에서 중대 가이드까지 거리.
const FRONT_INSET_M: float = 180.0
## 직할·배속 줄이 중대 줄 뒤로 떨어지는 거리.
const SUPPORT_DEPTH_M: float = 480.0
const CP_DEPTH_M: float = 600.0
const EDGE_MARGIN_M: float = 150.0


## 적 방향 heading: 구역 중심에서 지도 중심을 향한다(북/남 중 하나로 맞춤).
static func facing(zone: Rect2, map_size: Vector2) -> float:
	return 0.0 if zone.get_center().y < map_size.y * 0.5 else PI


## 구역 앞선에서 depth 만큼 뒤, 폭 비율 t(0~1) 지점.
static func zone_point(zone: Rect2, heading: float, t: float, depth: float) -> Vector2:
	var inner: Rect2 = zone.grow(-EDGE_MARGIN_M)
	if inner.size.x <= 0.0 or inner.size.y <= 0.0:
		inner = zone
	var x: float = lerpf(inner.position.x, inner.end.x, t)
	var y: float
	if is_zero_approx(heading):
		y = inner.end.y - depth
	else:
		y = inner.position.y + depth
	return Vector2(x, clampf(y, inner.position.y, inner.end.y))


## 비어 있는 배치 키를 채운다(이미 있는 값은 둔다). 초기 배치(0단계) 부대만 대상.
static func auto_fill(spec: ForceSpec, zone: Rect2, map_size: Vector2, terrain: Terrain = null) -> void:
	var h: float = facing(zone, map_size)
	if not spec.deploy.has("cp"):
		spec.deploy["cp"] = _v3(_fix(zone_point(zone, h, 0.5, CP_DEPTH_M), zone, terrain), h)
	var first_wave: Array[ForceSpec.CompanySpec] = []
	for c: ForceSpec.CompanySpec in spec.companies:
		if c.wave() == 0:
			first_wave.append(c)
	for i: int in first_wave.size():
		var key: String = "co:%s" % first_wave[i].slot
		if not spec.deploy.has(key):
			var t: float = (i + 1.0) / (first_wave.size() + 1.0)
			spec.deploy[key] = _v3(_fix(zone_point(zone, h, t, FRONT_INSET_M), zone, terrain), h)
	var support: Array[String] = []
	for i: int in spec.organic.size():
		if spec.organic[i].wave == 0:
			support.append("org:%d" % i)
	for i: int in spec.attachments.size():
		if spec.attachments[i].wave == 0:
			support.append("att:%d" % i)
	for i: int in support.size():
		if not spec.deploy.has(support[i]):
			var t: float = (i + 1.0) / (support.size() + 1.0)
			spec.deploy[support[i]] = _v3(_fix(zone_point(zone, h, t, SUPPORT_DEPTH_M), zone, terrain), h)


static func is_valid_point(p: Vector2, zone: Rect2, terrain: Terrain) -> bool:
	if not zone.has_point(p):
		return false
	return terrain == null or terrain.is_passable(p, Terrain.MOBILITY_VEHICLE)


## 구역 안으로 끌어오고 차량이 설 수 있는 곳으로 옮긴다.
static func clamp_to_zone(p: Vector2, zone: Rect2, terrain: Terrain) -> Vector2:
	return _fix(p, zone, terrain)


static func _fix(p: Vector2, zone: Rect2, terrain: Terrain) -> Vector2:
	var inner: Rect2 = zone.grow(-20.0)
	var q := Vector2(clampf(p.x, inner.position.x, inner.end.x), clampf(p.y, inner.position.y, inner.end.y))
	if terrain != null and not terrain.is_passable(q, Terrain.MOBILITY_VEHICLE):
		q = terrain.nearest_passable(q, Terrain.MOBILITY_VEHICLE)
	return q


static func _v3(p: Vector2, heading: float) -> Vector3:
	return Vector3(p.x, p.y, heading)
