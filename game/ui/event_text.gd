class_name EventText
extends RefCounted
## 시뮬 이벤트 → 알림 문장. 플레이어 진영 이벤트만 문장으로 만든다.

const INFO: Color = Color(0.92, 0.95, 1.0)
const GOOD: Color = Color(0.55, 1.0, 0.6)
const WARN: Color = Color(1.0, 0.82, 0.3)
const BAD: Color = Color(1.0, 0.45, 0.4)


## 알림으로 쓸 [문장, 색]. 알릴 필요가 없으면 빈 배열.
static func describe(battle: Battle, ev: Dictionary, player_side: StringName) -> Array:
	var type: StringName = ev["type"]
	var p: Dictionary = ev["payload"]
	var unit_id: int = p.get("unit", -1)
	var unit: Unit = battle.get_unit(unit_id)
	var side: StringName = p.get("side", unit.side if unit != null else &"")
	if side != player_side and type != &"order_waiting_capacity":
		return []
	var unit_name: String = unit.display_name if unit != null and unit.level != Unit.Level.PLATOON else (unit.label() if unit != null else "?")
	match type:
		&"arrived":
			if unit == null or unit.awaiting_follow:
				return []
			if unit.level == Unit.Level.PLATOON and (unit as Platoon).follows_parent():
				return []
			var order_type: int = p.get("type", 0)
			return ["%s: 목표 도착 (%s)" % [unit_name, Hud._order_name(order_type)], GOOD]
		&"order_received":
			if unit == null or str(p.get("type", "")) == "FOLLOW":
				return []
			return ["%s: 명령 수신" % unit_name, INFO]
		&"order_blocked":
			return ["%s: 통신 두절 — 명령 대기 중" % unit_name, WARN]
		&"order_cancelled":
			return ["%s: 명령 취소 (%s)" % [unit_name, str(p.get("reason", ""))], BAD]
		&"order_waiting_capacity":
			if side != player_side:
				return []
			return ["지휘 용량 부족 — 명령이 대기열에 들어감", WARN]
		&"comms_changed":
			var st: int = p.get("state", 0)
			return ["%s: %s" % [unit_name, "통신 두절" if st == Unit.Comms.CUT else "통신 회복"], BAD if st == Unit.Comms.CUT else GOOD]
		&"commander_lost":
			var seconds: float = p.get("seconds", 0.0)
			return ["%s: 지휘관 손실 — 승계까지 %.0f초" % [unit_name, seconds], BAD]
		&"succession_done":
			return ["%s: 지휘권 승계 완료" % unit_name, INFO]
	return []
