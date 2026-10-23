class_name Guide
extends RefCounted
## 제대의 가상 기준점. 경로를 따라 움직이고, 하위 단위는 가이드 기준 대형 슬롯을 따라간다. PLAN 17.1.
## 지나온 길(trail)을 기록해 종대·도로 이동 때 "앞 차를 따라가기" 목표로 쓴다.

const TRAIL_SPACING_M: float = 10.0
const TRAIL_MAX_POINTS: int = 300
## 가이드 선회 속도(라디안/초). 너무 빠르면 대형이 휙휙 돈다.
const TURN_RATE: float = PI / 2.0
const ARRIVE_EPS_M: float = 2.0

var pos: Vector2 = Vector2.ZERO
var heading: float = 0.0
var path: PackedVector2Array = PackedVector2Array()
var path_index: int = 0
var speed: float = 0.0
## 도착 후 바라볼 방향. NAN = 지정 없음.
var final_facing: float = NAN
## 지나온 점들(오래된 것부터).
var trail: PackedVector2Array = PackedVector2Array()
var pending: GridPathfinder.PathRequest = null
var destination: Vector2 = Vector2.ZERO
var has_destination: bool = false


func reset(p: Vector2, h: float) -> void:
	pos = p
	heading = h
	path = PackedVector2Array()
	path_index = 0
	speed = 0.0
	trail = PackedVector2Array([p])
	pending = null
	has_destination = false


func set_path(points: PackedVector2Array) -> void:
	path = points
	path_index = 1 if points.size() > 1 else points.size()
	if points.size() > 0:
		destination = points[points.size() - 1]
		has_destination = true


func clear_path() -> void:
	path = PackedVector2Array()
	path_index = 0
	if pending != null:
		pending.cancelled = true
	pending = null


func is_moving() -> bool:
	return path_index < path.size()


func is_waiting_for_path() -> bool:
	return pending != null and not pending.done


func remaining_distance() -> float:
	if not is_moving():
		return 0.0
	var d: float = pos.distance_to(path[path_index])
	for i: int in range(path_index, path.size() - 1):
		d += path[i].distance_to(path[i + 1])
	return d


## 경로를 따라 최대 max_speed 로 dt 동안 이동한다. 이동한 거리를 돌려준다.
func advance(dt: float, max_speed: float) -> float:
	speed = 0.0
	if not is_moving() or max_speed <= 0.0:
		return 0.0
	var budget: float = max_speed * dt
	var moved: float = 0.0
	while budget > 0.0 and path_index < path.size():
		var target: Vector2 = path[path_index]
		var to_t: Vector2 = target - pos
		var d: float = to_t.length()
		if d <= ARRIVE_EPS_M:
			pos = target
			path_index += 1
			continue
		_turn_toward(FormationSolver.heading_from_vector(to_t), dt)
		var step: float = minf(budget, d)
		pos += to_t / d * step
		budget -= step
		moved += step
	speed = moved / dt
	_record_trail()
	return moved


## 경로 없이 target 쪽으로 직접 이동한다(상위 대형 슬롯 추종용). 이동한 거리를 돌려준다.
func move_toward(target: Vector2, dt: float, max_speed: float, face: float) -> float:
	var to_t: Vector2 = target - pos
	var d: float = to_t.length()
	speed = 0.0
	if d <= ARRIVE_EPS_M:
		pos = target if d > 0.0 else pos
		if not is_nan(face):
			_turn_toward(face, dt)
		_record_trail()
		return 0.0
	var step: float = minf(max_speed * dt, d)
	if d > 30.0:
		_turn_toward(FormationSolver.heading_from_vector(to_t), dt)
	elif not is_nan(face):
		_turn_toward(face, dt)
	pos += to_t / d * step
	speed = step / dt
	_record_trail()
	return step


func turn_toward(target_heading: float, dt: float) -> void:
	_turn_toward(target_heading, dt)


## 지나온 길을 따라 dist 만큼 뒤의 점. 기록이 모자라면 heading 반대 방향으로 연장한다.
func trail_point_behind(dist: float) -> Vector2:
	var remaining: float = dist
	var cur: Vector2 = pos
	var i: int = trail.size() - 1
	while i >= 0 and remaining > 0.0:
		var p: Vector2 = trail[i]
		var seg: float = cur.distance_to(p)
		if seg >= remaining:
			return cur.lerp(p, remaining / seg) if seg > 0.0 else p
		remaining -= seg
		cur = p
		i -= 1
	return cur - FormationSolver.forward_vector(heading) * remaining


## 앞으로 지날 경로 위 점들(look_ahead_m 까지, 대략 cell 간격).
func points_ahead(look_ahead_m: float, step_m: float = 10.0) -> PackedVector2Array:
	var out := PackedVector2Array()
	if not is_moving():
		return out
	var cur: Vector2 = pos
	var left: float = look_ahead_m
	var i: int = path_index
	while left > 0.0 and i < path.size():
		var nxt: Vector2 = path[i]
		var seg: float = cur.distance_to(nxt)
		var t: float = 0.0
		while t < seg and left > 0.0:
			out.append(cur.lerp(nxt, t / seg))
			t += step_m
			left -= step_m
		cur = nxt
		i += 1
	return out


func _turn_toward(target_heading: float, dt: float) -> void:
	var diff: float = angle_difference(heading, target_heading)
	var max_turn: float = TURN_RATE * dt
	heading = wrapf(heading + clampf(diff, -max_turn, max_turn), -PI, PI)


func _record_trail() -> void:
	if trail.is_empty() or trail[trail.size() - 1].distance_to(pos) >= TRAIL_SPACING_M:
		trail.append(pos)
		if trail.size() > TRAIL_MAX_POINTS:
			trail = trail.slice(trail.size() - TRAIL_MAX_POINTS)
