class_name WeaponDef
extends RefCounted
## 무기 정의. data/weapons/*.json. PLAN 16.2.

const TYPES: Array[String] = ["KE", "HEAT", "ATGM", "HE", "MG", "AC", "SAM"]
const TARGETS: Array[String] = ["vehicle", "infantry", "air"]

var id: StringName
var weapon_type: StringName
var range_min_m: float
var range_max_m: float
## 관통력(mm RHA 등가).
var penetration: float
## km 당 관통력 감소율(0~1).
var pen_falloff_per_km: float
var damage: float
var accuracy_base: float
var accuracy_at_max_range: float
var rof_rpm: float
var suppression: float
var fire_on_move: bool
var targets: PackedStringArray
var he_radius_m: float
## 간접사격 무기(박격포 등). 직접 교전에 쓰지 않고 화력 임무로만 쓴다.
var indirect: bool = false
## 상부 공격(재블린 등): 상부 장갑으로 판정.
var top_attack: bool = false
## 한 번 쏠 때 소모하는 탄수(기관총 점사).
var burst: int = 1


static func from_dict(d: Dictionary, r: DefReader) -> WeaponDef:
	var w := WeaponDef.new()
	w.id = StringName(r.req_string(d, "id"))
	w.weapon_type = StringName(r.req_string(d, "type"))
	r.check_one_of(w.weapon_type, TYPES, "type")

	var range_raw: Dictionary = r.req_dict(d, "range_m")
	w.range_min_m = r.req_number(range_raw, "min")
	w.range_max_m = r.req_number(range_raw, "max")
	if w.range_max_m <= w.range_min_m:
		r.error("range_m.max(%s) 는 range_m.min(%s) 보다 커야 함" % [w.range_max_m, w.range_min_m])

	w.penetration = r.req_number(d, "penetration")
	r.check_range(w.penetration, 0.0, 5000.0, "penetration")
	w.pen_falloff_per_km = r.opt_number(d, "pen_falloff_per_km", 0.0)
	r.check_range(w.pen_falloff_per_km, 0.0, 1.0, "pen_falloff_per_km")
	w.damage = r.req_number(d, "damage")

	var acc: Dictionary = r.req_dict(d, "accuracy")
	w.accuracy_base = r.req_number(acc, "base")
	w.accuracy_at_max_range = r.req_number(acc, "at_max_range")
	r.check_range(w.accuracy_base, 0.0, 1.0, "accuracy.base")
	r.check_range(w.accuracy_at_max_range, 0.0, 1.0, "accuracy.at_max_range")

	w.rof_rpm = r.req_number(d, "rof_rpm")
	r.check_range(w.rof_rpm, 0.1, 6000.0, "rof_rpm")
	w.suppression = r.req_number(d, "suppression")
	r.check_range(w.suppression, 0.0, 100.0, "suppression")
	w.fire_on_move = r.req_bool(d, "fire_on_move")
	w.targets = r.req_string_array(d, "targets")
	for t: String in w.targets:
		r.check_one_of(t, TARGETS, "targets")
	w.he_radius_m = r.opt_number(d, "he_radius_m", 0.0)
	w.indirect = r.opt_bool(d, "indirect", false)
	w.top_attack = r.opt_bool(d, "top_attack", false)
	w.burst = r.opt_int(d, "burst", 1)
	if w.burst < 1:
		r.error("burst 는 1 이상")
	return w


## 사격 간격(초).
func interval_sec() -> float:
	return 60.0 / rof_rpm * burst


## 거리 d(m)에서의 관통력.
func penetration_at(d: float) -> float:
	return penetration * maxf(0.0, 1.0 - pen_falloff_per_km * d / 1000.0)


## 거리 d(m)에서의 기본 명중률(최대 사거리에서 at_max_range 로 선형 감소).
func accuracy_at(d: float) -> float:
	var t: float = clampf((d - range_min_m) / maxf(range_max_m - range_min_m, 1.0), 0.0, 1.0)
	return lerpf(accuracy_base, accuracy_at_max_range, t)


func can_target(kind: StringName) -> bool:
	return targets.has(String(kind))
