class_name FormationDef
extends RefCounted
## 대형 정의. data/formations.json. PLAN 7장, 16.3.
## 슬롯은 가이드 기준 로컬 좌표(x 우측 +, y 전방 +), 단위 m. 슬롯 0 은 지휘관(가이드) 자리다.

const ECHELONS: Array[String] = ["platoon", "company"]
const MOD_KEYS: Array[String] = ["speed", "front_fire", "flank_security", "arty_vuln", "control", "transition_sec"]

var id: StringName
var display_name: String
var echelon: StringName
## 하위 단위 수 → 슬롯 배열.
var slots: Dictionary[int, PackedVector2Array] = {}
## 지형 종류 → 간격 배율. 없는 지형은 1.0.
var spacing_scale: Dictionary[StringName, float] = {}
## 보정치. 키는 MOD_KEYS, 추가 키(예: road_speed)도 허용.
var mods: Dictionary[StringName, float] = {}
## false 면 정지 전용 대형(사주 경계).
var allowed_moving: bool = true


static func from_dict(d: Dictionary, r: DefReader) -> FormationDef:
	var f := FormationDef.new()
	f.id = StringName(r.req_string(d, "id"))
	f.display_name = r.opt_string(d, "name", String(f.id))
	f.echelon = StringName(r.req_string(d, "echelon"))
	r.check_one_of(f.echelon, ECHELONS, "echelon")

	var raw_slots: Dictionary = r.req_dict(d, "slots")
	if raw_slots.is_empty():
		r.error("slots 가 비어 있음")
	for key: Variant in raw_slots.keys():
		var key_str: String = str(key)
		if not key_str.is_valid_int() or key_str.to_int() <= 0:
			r.error("slots 의 키 '%s' 는 양의 정수여야 함" % key_str)
			continue
		var count: int = key_str.to_int()
		var pts_raw: Variant = raw_slots[key]
		if typeof(pts_raw) != TYPE_ARRAY:
			r.error("slots[%d] 는 배열이어야 함" % count)
			continue
		var pts_arr: Array = pts_raw
		if pts_arr.size() != count:
			r.error("slots[%d] 의 슬롯 수가 %d 개임" % [count, pts_arr.size()])
		var pts := PackedVector2Array()
		for i: int in pts_arr.size():
			pts.append(r.as_vec2(pts_arr[i], "slots[%d][%d]" % [count, i]))
		f.slots[count] = pts

	var spacing_raw: Dictionary = r.opt_dict(d, "spacing_scale")
	for key: Variant in spacing_raw.keys():
		var v: float = r.as_number(spacing_raw[key], "spacing_scale.%s" % key)
		r.check_range(v, 0.05, 5.0, "spacing_scale.%s" % key)
		f.spacing_scale[StringName(str(key))] = v

	var mods_raw: Dictionary = r.req_dict(d, "mods")
	for key: String in MOD_KEYS:
		f.mods[StringName(key)] = r.req_number(mods_raw, key)
	for key: Variant in mods_raw.keys():
		if not MOD_KEYS.has(str(key)):
			f.mods[StringName(str(key))] = r.as_number(mods_raw[key], "mods.%s" % key)

	f.allowed_moving = r.opt_bool(d, "allowed_moving", true)
	return f


func get_mod(key: StringName, default: float = 1.0) -> float:
	return mods.get(key, default)


## 정의된 슬롯 세트 중 가장 큰 하위 단위 수.
func max_defined_count() -> int:
	var best: int = 0
	for k: int in slots.keys():
		best = maxi(best, k)
	return best
