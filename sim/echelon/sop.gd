class_name Sop
extends RefCounted
## 표준 행동 절차(SOP). 제대별로 가지며 하위 제대가 상속한다. PLAN 8.5.

const ROE_VALUES: Array[StringName] = [&"hold_fire", &"fire_at_will", &"designated"]
const CONTACT_VALUES: Array[StringName] = [&"halt_and_fire", &"fire_and_continue", &"assault", &"break_contact"]
const TECHNIQUE_VALUES: Array[StringName] = [&"traveling", &"overwatch", &"bounding"]
const WITHDRAW_VALUES: Array[float] = [0.4, 0.25, 0.0]
const MOUNT_VALUES: Array[StringName] = [&"dismount_on_contact", &"stay_mounted", &"stay_dismounted"]
const UNDER_FIRE_VALUES: Array[StringName] = [&"disperse", &"hold"]
## 헬기 비행 고도.
const ALTITUDE_VALUES: Array[StringName] = [&"low", &"high"]

## 교전 규칙.
var roe: StringName = &"fire_at_will"
## 접촉 시 반응.
var on_contact: StringName = &"fire_and_continue"
## 이동 방식.
var technique: StringName = &"traveling"
## 전투력이 이 비율 이하면 스스로 철수. 0 = 철수 안 함.
var withdraw_at: float = 0.4
## 탑승 보병 정책.
var mount_policy: StringName = &"dismount_on_contact"
## 포격 받을 때.
var under_fire: StringName = &"disperse"
## 헬기 고도(헬기가 아니면 쓰지 않는다).
var altitude: StringName = &"low"


func copy() -> Sop:
	var s := Sop.new()
	s.roe = roe
	s.on_contact = on_contact
	s.technique = technique
	s.withdraw_at = withdraw_at
	s.mount_policy = mount_policy
	s.under_fire = under_fire
	s.altitude = altitude
	return s


## 딕셔너리의 키만 덮어쓴다. 잘못된 값은 무시하고 false 를 돌려준다.
func apply(d: Dictionary) -> bool:
	var ok: bool = true
	for key: Variant in d.keys():
		var k: String = str(key)
		var v: Variant = d[key]
		match k:
			"roe":
				ok = _set_name(v, ROE_VALUES, "roe") and ok
			"on_contact":
				ok = _set_name(v, CONTACT_VALUES, "on_contact") and ok
			"technique":
				ok = _set_name(v, TECHNIQUE_VALUES, "technique") and ok
			"mount_policy":
				ok = _set_name(v, MOUNT_VALUES, "mount_policy") and ok
			"under_fire":
				ok = _set_name(v, UNDER_FIRE_VALUES, "under_fire") and ok
			"altitude":
				ok = _set_name(v, ALTITUDE_VALUES, "altitude") and ok
			"withdraw_at":
				if DefReader.is_number(v):
					var f: float = v
					withdraw_at = clampf(f, 0.0, 1.0)
				else:
					ok = false
			_:
				ok = false
	return ok


func to_dict() -> Dictionary:
	return {
		"roe": String(roe), "on_contact": String(on_contact), "technique": String(technique),
		"withdraw_at": withdraw_at, "mount_policy": String(mount_policy), "under_fire": String(under_fire),
		"altitude": String(altitude),
	}


func _set_name(v: Variant, allowed: Array[StringName], field: String) -> bool:
	if typeof(v) != TYPE_STRING and typeof(v) != TYPE_STRING_NAME:
		return false
	var name := StringName(str(v))
	if not allowed.has(name):
		return false
	set(field, name)
	return true
