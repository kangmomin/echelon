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
	# 양쪽 모두 알릴 이벤트.
	match type:
		&"point_captured":
			var mine: bool = side == player_side
			return ["%s: %s 점령" % ["아군" if mine else "적", str(p.get("name", ""))], GOOD if mine else BAD]
		&"objective_done":
			var mine2: bool = side == player_side
			var gained: int = p.get("vp", 0)
			return ["%s 임무 목표 달성: %s (+%d)" % ["아군" if mine2 else "적", str(p.get("name", "")), gained],
				GOOD if mine2 else BAD]
		&"battle_ended":
			var winner: String = p.get("winner", "")
			if winner == "":
				return ["전투 종료 — 무승부", WARN]
			return ["전투 종료 — %s" % ("승리" if winner == String(player_side) else "패배"), GOOD if winner == String(player_side) else BAD]
		&"cas_shot_down":
			if side != player_side:
				return ["아군 방공이 적 항공기를 격추", GOOD]
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
		&"element_destroyed":
			var eid: int = p.get("element", -1)
			var e: Element = battle.get_element(eid)
			return ["%s: %s 격파됨" % [unit_name, e.def.display_name if e != null else "엘리먼트"], BAD]
		&"routed":
			return ["%s: 패주!" % unit_name, BAD]
		&"rally":
			return ["%s: 재집결" % unit_name, INFO]
		&"order_refused":
			return ["%s: 사기 저하로 공격 명령 거부" % unit_name, WARN]
		&"auto_withdraw":
			return ["%s: 전투력 저하 — 스스로 철수" % unit_name, WARN]
		&"fire_mission":
			var ph: int = p.get("phase", 0)
			var asset: int = p.get("asset", 0)
			if ph == FireSupportSystem.Phase.EFFECT:
				return ["%s: 효력사 시작" % FireSupportSystem.ASSET_NAMES[asset], INFO]
			if ph == FireSupportSystem.Phase.DONE:
				return ["%s: 사격 완료" % FireSupportSystem.ASSET_NAMES[asset], INFO]
			return []
		&"cas_shot_down":
			return ["근접항공지원: 적 방공에 격추됨", BAD]
		&"resupplied":
			return ["%s: 보급 완료" % unit_name, GOOD]
		&"supply_dispatched":
			var tid: int = p.get("target", -1)
			var target: Unit = battle.get_unit(tid)
			return ["%s: %s 로 보급 출발" % [unit_name, target.label() if target != null else "?"], INFO]
		&"supply_returning":
			return ["%s: 보급소로 재보급 복귀" % unit_name, INFO]
		&"repaired":
			return ["%s: 손상 차량 수리 완료" % unit_name, GOOD]
		&"disperse":
			return ["%s: 포격 — 분산 이동" % unit_name, WARN]
		&"reinforcement_arrived":
			return ["%s: 증원 도착" % unit_name, GOOD]
	return []
