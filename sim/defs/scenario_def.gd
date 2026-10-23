class_name ScenarioDef
extends RefCounted
## 스커미시 시나리오 정의. data/scenarios/*.json. PLAN 11장.
## 좌표계: 지도 남서쪽 모서리가 (0, 0), x = 동쪽(+), y = 북쪽(+), 단위 m.

const SIDES: Array[String] = ["blue", "red"]
const OBJECTIVE_TYPES: Array[String] = ["seize", "hold"]


class SideSetup:
	extends RefCounted
	var faction: StringName
	var toe: StringName
	var budget: int
	## 배치 구역(사각형, 지도 좌표).
	var deploy_zone: Rect2
	var entry_points: PackedVector2Array


class ControlPoint:
	extends RefCounted
	var id: StringName
	var display_name: String
	var pos: Vector2
	var radius_m: float
	var vp_per_min: float


class Objective:
	extends RefCounted
	var id: StringName
	var side: StringName
	var objective_type: StringName
	var point: StringName
	## seize: 이 시각(분)까지 확보. hold: 이 시간(분)만큼 유지.
	var minutes: float
	var vp: int


var id: StringName
var display_name: String
var map_id: StringName
var size_m: Vector2
var seed_value: int
var time_limit_min: float
## 전투력이 이 비율 이하로 떨어지면 즉시 패배. PLAN 11.1.
var collapse_threshold: float
var sides: Dictionary[StringName, SideSetup] = {}
var control_points: Array[ControlPoint] = []
var objectives: Array[Objective] = []


static func from_dict(d: Dictionary, r: DefReader) -> ScenarioDef:
	var s := ScenarioDef.new()
	s.id = StringName(r.req_string(d, "id"))
	s.display_name = r.req_string(d, "name")
	s.map_id = StringName(r.req_string(d, "map"))
	s.size_m = r.req_vec2(d, "size_m")
	if s.size_m.x <= 0.0 or s.size_m.y <= 0.0:
		r.error("size_m 은 양수여야 함")
	s.seed_value = r.req_int(d, "seed")
	s.time_limit_min = r.req_number(d, "time_limit_min")
	r.check_range(s.time_limit_min, 1.0, 240.0, "time_limit_min")
	s.collapse_threshold = r.req_number(d, "collapse_threshold")
	r.check_range(s.collapse_threshold, 0.0, 1.0, "collapse_threshold")

	var map_rect := Rect2(Vector2.ZERO, s.size_m)
	var sides_raw: Dictionary = r.req_dict(d, "sides")
	for side_name: String in SIDES:
		var raw: Dictionary = r.req_dict(sides_raw, side_name)
		var side := SideSetup.new()
		side.faction = StringName(r.req_string(raw, "faction"))
		r.check_one_of(side.faction, ElementDef.FACTIONS, "sides.%s.faction" % side_name)
		side.toe = StringName(r.req_string(raw, "toe"))
		side.budget = r.req_int(raw, "budget")
		var zone: Array = r.req_array(raw, "deploy_zone")
		if zone.size() == 2:
			var a: Vector2 = r.as_vec2(zone[0], "sides.%s.deploy_zone[0]" % side_name)
			var b: Vector2 = r.as_vec2(zone[1], "sides.%s.deploy_zone[1]" % side_name)
			side.deploy_zone = Rect2(a, Vector2.ZERO).expand(b)
			if not map_rect.encloses(side.deploy_zone):
				r.error("sides.%s.deploy_zone 이 지도 밖으로 나감" % side_name)
		else:
			r.error("sides.%s.deploy_zone 은 [[x1,y1],[x2,y2]] 형식이어야 함" % side_name)
		var entries: Array = r.req_array(raw, "entry_points")
		for i: int in entries.size():
			var p: Vector2 = r.as_vec2(entries[i], "sides.%s.entry_points[%d]" % [side_name, i])
			if not map_rect.grow(0.01).has_point(p):
				r.error("sides.%s.entry_points[%d] 가 지도 밖임" % [side_name, i])
			side.entry_points.append(p)
		if side.entry_points.is_empty():
			r.error("sides.%s.entry_points 가 비어 있음" % side_name)
		s.sides[StringName(side_name)] = side

	var cp_ids: Dictionary = {}
	var cps_raw: Array = r.req_array(d, "control_points")
	for i: int in cps_raw.size():
		var raw: Dictionary = r.as_dict(cps_raw[i], "control_points[%d]" % i)
		var cp := ControlPoint.new()
		cp.id = StringName(r.req_string(raw, "id"))
		cp.display_name = r.req_string(raw, "name")
		cp.pos = r.req_vec2(raw, "pos")
		cp.radius_m = r.req_number(raw, "radius_m")
		cp.vp_per_min = r.req_number(raw, "vp_per_min")
		if not map_rect.has_point(cp.pos):
			r.error("control_points '%s' 위치가 지도 밖임" % cp.id)
		if cp_ids.has(cp.id):
			r.error("control_points id '%s' 중복" % cp.id)
		cp_ids[cp.id] = true
		s.control_points.append(cp)

	var obj_raw: Array = r.opt_array(d, "objectives")
	for i: int in obj_raw.size():
		var raw: Dictionary = r.as_dict(obj_raw[i], "objectives[%d]" % i)
		var obj := Objective.new()
		obj.id = StringName(r.req_string(raw, "id"))
		obj.side = StringName(r.req_string(raw, "side"))
		r.check_one_of(obj.side, SIDES, "objectives[%d].side" % i)
		obj.objective_type = StringName(r.req_string(raw, "type"))
		r.check_one_of(obj.objective_type, OBJECTIVE_TYPES, "objectives[%d].type" % i)
		obj.point = StringName(r.req_string(raw, "point"))
		if not cp_ids.has(obj.point):
			r.error("objectives[%d].point '%s' 가 control_points 에 없음" % [i, obj.point])
		obj.minutes = r.req_number(raw, "minutes")
		obj.vp = r.req_int(raw, "vp")
		s.objectives.append(obj)
	return s
