class_name Unit
extends RefCounted
## 제대(대대 전투단·중대·소대)의 공통 기반. 지휘관 엘리먼트·명령·대형·SOP 를 가진 논리 노드. PLAN 5장.

enum Level { TASK_FORCE, COMPANY, PLATOON }
enum Comms { GOOD, POOR, CUT }

const LEVEL_NAMES: Array[String] = ["대대 전투단", "중대", "소대"]
const COMMS_NAMES: Array[String] = ["양호", "불량", "두절"]
## 숙련도 → 명령 지연 배율. PLAN 8.1.
const SKILL_DELAY: Dictionary[StringName, float] = {&"recruit": 1.3, &"regular": 1.0, &"veteran": 0.8, &"elite": 0.65}

var id: int = 0
var display_name: String = ""
var short_name: String = ""
var side: StringName = &"blue"
var level: int = Level.PLATOON
var parent: Unit = null
var template_id: StringName = &""
## 병과/유형: tank, mech, inf, recon, mortar, supply, hq, cp, fo, aa, ew ...
var kind: StringName = &""

var guide: Guide = Guide.new()
var formation: FormationDef = null
var sop: Sop = Sop.new()
var skill: StringName = &"regular"

## 지금 실행 중인 명령.
var mission: Order = null
## 받았지만 아직 실행 전인 명령(Shift 대기열).
var queued_orders: Array[Order] = []
## 상위 제대 AI 의 재배치에서 빠진 상태(플레이어 직접 명령). PLAN 5.1.
var detached: bool = false

## 지휘관 엘리먼트(승계로 바뀔 수 있다).
var commander: Element = null
## 지휘 공백이 끝나는 틱(-1 = 공백 아님).
var succession_until_tick: int = -1
## 승계 후 명령 지연 가산(선임 소대장 승계 등).
var succession_delay_mult: float = 1.0
## 승계를 기다리는 후계 엘리먼트.
var pending_successor: Element = null
## 비편제(템플릿 제약을 깬 교차 배속) — 명령 지연 +20%, 재편 불가. PLAN 6.2.
var ad_hoc: bool = false
## 중대가 새 이동 명령을 받아 하위 소대에 번역 명령(FOLLOW)을 보냈고, 아직 이 소대에 닿지 않았다.
var awaiting_follow: bool = false

var morale: float = 100.0
var comms_quality: float = 1.0
var comms_state: int = Comms.GOOD
var comms_hops: int = 1
var reform_until_tick: int = -1
var on_map: bool = true
## 증원 단계: 0 = 초기 배치, 1 = T+10분, 2 = T+20분.
var phase: int = 0
## 중대 안 역할: &"lead" | &"flank" | &"reserve"
var role: StringName = &""
## 자세: &"idle" | &"moving" | &"defending" | &"withdrawing" | &"routed" | &"resupplying"
var posture: StringName = &"idle"
## 방어 방향(NAN = 없음).
var defend_facing: float = NAN
var routed_until_tick: int = -1
var initial_element_count: int = 0
## 마지막 접촉(적 탐지·피격) 틱.
var last_contact_tick: int = -1000000


func children_units() -> Array[Unit]:
	return []


## 지휘관 엘리먼트. 하위 클래스가 승계 규칙에 맞게 오버라이드한다.
func leader() -> Element:
	return commander if commander != null and commander.is_alive() else null


## 대형 오차: 하위 단위가 목표 위치에서 가장 멀리 떨어진 거리(m). 대기 규칙에 쓴다.
var formation_error: float = 0.0
## 하위 단위가 길을 따라 한 줄로 가는 중(도로·병목).
var column_mode: bool = false
## 종대 유지 남은 거리(m). 병목을 만나면 종대 길이만큼 채우고 이동한 만큼 줄인다(꼬리까지 빠져나갈 때까지 유지).
var column_hold_m: float = 0.0
## 가이드는 도착했고 하위 단위 정렬을 기다리는 중. 정렬되면(또는 시간이 지나면) 명령 완료.
var settling_since_tick: int = -1
## 이번 틱 가이드 속도 상한(m/s).
var guide_speed_limit: float = 0.0


func all_elements() -> Array[Element]:
	return []


func alive_elements() -> Array[Element]:
	var out: Array[Element] = []
	for e: Element in all_elements():
		if e.is_alive():
			out.append(e)
	return out


func active_elements() -> Array[Element]:
	var out: Array[Element] = []
	for e: Element in all_elements():
		if e.is_active():
			out.append(e)
	return out


## 전투력(0~1): 초기 엘리먼트 대비 살아 있는 전력.
func strength() -> float:
	var elems: Array[Element] = all_elements()
	if elems.is_empty():
		return 0.0
	var total: float = 0.0
	for e: Element in elems:
		total += e.strength()
	return total / maxf(float(maxi(initial_element_count, elems.size())), 1.0)


func is_destroyed() -> bool:
	return alive_elements().is_empty()


## 전투력 50% 이하 = 전투 불능. PLAN 5.1.
func is_combat_ineffective() -> bool:
	return strength() <= 0.5


func centroid() -> Vector2:
	var elems: Array[Element] = active_elements()
	if elems.is_empty():
		return guide.pos
	var sum: Vector2 = Vector2.ZERO
	for e: Element in elems:
		sum += e.pos
	return sum / elems.size()


func skill_delay_factor() -> float:
	return SKILL_DELAY.get(skill, 1.0) * succession_delay_mult


func is_reforming(tick: int) -> bool:
	return tick < reform_until_tick


func is_leaderless(tick: int) -> bool:
	return tick < succession_until_tick


func is_routed(tick: int) -> bool:
	return tick < routed_until_tick


func level_name() -> String:
	return LEVEL_NAMES[level]


func comms_name() -> String:
	return COMMS_NAMES[comms_state]


## 부모 제대 대형을 따라가는지(소대가 중대 대형 안에 있는 경우).
func follows_parent() -> bool:
	return false


## 대대 전투단(최상위)까지 올라간다.
func task_force() -> TaskForce:
	var u: Unit = self
	while u != null and u.level != Level.TASK_FORCE:
		u = u.parent
	return u as TaskForce


func label() -> String:
	return short_name if short_name != "" else display_name
