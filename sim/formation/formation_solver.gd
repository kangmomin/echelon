class_name FormationSolver
extends RefCounted
## 대형 슬롯 계산. PLAN 17.1 의 1~3단계(가이드 기준 목표 위치 산출)를 담당한다.
##
## 좌표계(PLAN DECISIONS D-002):
##   월드: x = 동쪽(+), y = 북쪽(+), 단위 m.
##   heading: 라디안, 0 = 북쪽, 시계방향이 +(나침반 방위와 같음).
##   로컬: x = 진행 방향 오른쪽(+), y = 진행 방향 앞(+).

## 정의된 슬롯보다 하위 단위가 많을 때 마지막 슬롯 뒤로 덧붙이는 간격(m).
const TRAIL_SPACING_M: float = 50.0


## 하위 단위 count 개의 로컬 슬롯 오프셋. 지형 간격 배율을 적용한다.
static func local_offsets(def: FormationDef, count: int, terrain: StringName = &"open") -> PackedVector2Array:
	var out := PackedVector2Array()
	if count <= 0 or def.slots.is_empty():
		return out
	var base: PackedVector2Array = _pick_slots(def, count)
	var scale: float = def.spacing_scale.get(terrain, 1.0)
	out.resize(count)
	for i: int in count:
		out[i] = base[i] * scale
	return out


## 가이드 위치·방향에 맞춘 월드 좌표 슬롯.
static func world_slots(def: FormationDef, count: int, origin: Vector2, heading: float,
		terrain: StringName = &"open") -> PackedVector2Array:
	var local: PackedVector2Array = local_offsets(def, count, terrain)
	var out := PackedVector2Array()
	out.resize(local.size())
	for i: int in local.size():
		out[i] = local_to_world(local[i], origin, heading)
	return out


## heading 방향의 전방 단위 벡터.
static func forward_vector(heading: float) -> Vector2:
	return Vector2(sin(heading), cos(heading))


## heading 방향의 오른쪽 단위 벡터.
static func right_vector(heading: float) -> Vector2:
	return Vector2(cos(heading), -sin(heading))


static func local_to_world(local: Vector2, origin: Vector2, heading: float) -> Vector2:
	return origin + right_vector(heading) * local.x + forward_vector(heading) * local.y


static func world_to_local(world: Vector2, origin: Vector2, heading: float) -> Vector2:
	var rel: Vector2 = world - origin
	return Vector2(rel.dot(right_vector(heading)), rel.dot(forward_vector(heading)))


## 방향 벡터 → heading(라디안, 0 = 북, 시계방향 +).
static func heading_from_vector(dir: Vector2) -> float:
	return atan2(dir.x, dir.y)


## 슬롯 세트 선택 규칙:
##   1) count 개짜리가 정의돼 있으면 그대로 쓴다.
##   2) 아니면 count 이상인 가장 작은 세트를 앞에서부터 자른다.
##   3) 그것도 없으면 가장 큰 세트 뒤로 TRAIL_SPACING_M 간격 종렬을 덧붙인다.
static func _pick_slots(def: FormationDef, count: int) -> PackedVector2Array:
	if def.slots.has(count):
		return def.slots[count]
	var best: int = -1
	var largest: int = -1
	for k: int in def.slots.keys():
		if k >= count and (best == -1 or k < best):
			best = k
		largest = maxi(largest, k)
	if best != -1:
		return def.slots[best].slice(0, count)
	var pts: PackedVector2Array = def.slots[largest].duplicate()
	var last: Vector2 = pts[pts.size() - 1]
	while pts.size() < count:
		last += Vector2(0.0, -TRAIL_SPACING_M)
		pts.append(last)
	return pts
