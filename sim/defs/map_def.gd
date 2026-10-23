class_name MapDef
extends RefCounted
## 지도 정의. data/maps/*.json. 높이는 노이즈 + 손으로 그린 능선, 지형은 벡터 피처(도로·하천·숲·마을)로
## 기술하고 로드 시 그리드로 래스터화한다(DECISIONS D-009).
## 좌표: 남서쪽 모서리 (0, 0), x = 동, y = 북, 단위 m.


class Feature:
	extends RefCounted
	var name: String
	var points: PackedVector2Array
	## 선형 피처(도로·하천·능선)의 폭(m).
	var width_m: float = 0.0
	## 능선의 높이(m).
	var height_m: float = 0.0


var id: StringName
var display_name: String
var size_m: Vector2
var cell_m: float

var height_seed: int
var base_m: float
var amplitude_m: float
var frequency: float
var octaves: int

var ridges: Array[Feature] = []
var rivers: Array[Feature] = []
var roads: Array[Feature] = []
var forests: Array[Feature] = []
var towns: Array[Feature] = []


static func from_dict(d: Dictionary, r: DefReader) -> MapDef:
	var m := MapDef.new()
	m.id = StringName(r.req_string(d, "id"))
	m.display_name = r.req_string(d, "name")
	m.size_m = r.req_vec2(d, "size_m")
	m.cell_m = r.req_number(d, "cell_m")
	r.check_range(m.cell_m, 2.0, 100.0, "cell_m")
	if m.size_m.x < m.cell_m or m.size_m.y < m.cell_m:
		r.error("size_m 이 cell_m 보다 작음")

	var h: Dictionary = r.req_dict(d, "height")
	m.height_seed = r.req_int(h, "seed")
	m.base_m = r.req_number(h, "base_m")
	m.amplitude_m = r.req_number(h, "amplitude_m")
	m.frequency = r.req_number(h, "frequency")
	m.octaves = r.opt_int(h, "octaves", 4)

	m.ridges = _read_features(d, "ridges", r, true, true, 2)
	m.rivers = _read_features(d, "rivers", r, true, false, 2)
	m.roads = _read_features(d, "roads", r, true, false, 2)
	m.forests = _read_features(d, "forests", r, false, false, 3)
	m.towns = _read_features(d, "towns", r, false, false, 3)
	return m


static func _read_features(d: Dictionary, key: String, r: DefReader, linear: bool, with_height: bool,
		min_points: int) -> Array[Feature]:
	var out: Array[Feature] = []
	var raw: Array = r.opt_array(d, key)
	for i: int in raw.size():
		var fd: Dictionary = r.as_dict(raw[i], "%s[%d]" % [key, i])
		var f := Feature.new()
		f.name = r.opt_string(fd, "name", "%s_%d" % [key, i])
		var pts: Array = r.req_array(fd, "points")
		for j: int in pts.size():
			f.points.append(r.as_vec2(pts[j], "%s[%d].points[%d]" % [key, i, j]))
		if f.points.size() < min_points:
			r.error("%s[%d] 의 점이 %d개 미만" % [key, i, min_points])
		if linear:
			f.width_m = r.req_number(fd, "width_m")
		if with_height:
			f.height_m = r.req_number(fd, "height_m")
		out.append(f)
	return out
