class_name SimWorld
extends RefCounted
## 시뮬레이션 루트. 10 Hz 고정 틱, 배속, 일시정지, 시드 RNG, 이벤트 큐를 가진다. PLAN 15장.
##
## 규약(PLAN 18.2): 노드 트리, Engine, Time 에 접근하지 않는다. 시간은 틱 정수로만 다룬다.
## 실시간 경과는 호출자(게임 계층)가 advance(real_delta) 로 넘겨준다.
## 테스트와 헤드리스 시뮬은 step_tick() 을 직접 불러도 된다.

## 지금까지 완료된 틱 수. 시스템이 step() 안에서 읽으면 "지금 처리 중인 틱 번호"다.
var tick: int = 0
var paused: bool = false
var speed_index: int = SimConst.DEFAULT_SPEED_INDEX
var rng: SimRng
var events: SimEvents = SimEvents.new()

var _systems: Array[SimSystem] = []
var _accum: float = 0.0


func _init(p_seed: int = 0) -> void:
	rng = SimRng.new(p_seed)


# --- 시스템 등록 ---------------------------------------------------------

func add_system(system: SimSystem) -> void:
	_systems.append(system)


func get_systems() -> Array[SimSystem]:
	return _systems.duplicate()


# --- 시간 제어 -----------------------------------------------------------

func get_speed() -> float:
	return SimConst.SPEED_STEPS[speed_index]


func set_speed_index(index: int) -> void:
	var clamped: int = clampi(index, 0, SimConst.SPEED_STEPS.size() - 1)
	if clamped == speed_index:
		return
	speed_index = clamped
	events.emit_event(&"speed_changed", tick, {"speed": get_speed()})


func speed_up() -> void:
	set_speed_index(speed_index + 1)


func speed_down() -> void:
	set_speed_index(speed_index - 1)


func set_paused(value: bool) -> void:
	if paused == value:
		return
	paused = value
	events.emit_event(&"paused_changed", tick, {"paused": paused})


func toggle_pause() -> void:
	set_paused(not paused)


## 실시간 경과(초)를 받아 필요한 만큼 틱을 진행하고, 진행한 틱 수를 돌려준다.
## 일시정지 중이면 시간이 쌓이지 않는다. 한 번에 MAX_TICKS_PER_ADVANCE 를 넘게 밀리면 나머지는 버린다.
func advance(real_delta: float) -> int:
	if paused:
		return 0
	_accum += maxf(real_delta, 0.0) * get_speed()
	var ran: int = 0
	while _accum + SimConst.TICK_EPSILON >= SimConst.TICK_DT and ran < SimConst.MAX_TICKS_PER_ADVANCE:
		step_tick()
		_accum = maxf(_accum - SimConst.TICK_DT, 0.0)
		ran += 1
	if ran >= SimConst.MAX_TICKS_PER_ADVANCE:
		# 밀린 시간은 버린다. 남은 조각을 두면 시간 경과 없이 다음 프레임에 틱이 하나 더 돈다.
		_accum = 0.0
	return ran


## 정확히 한 틱 진행한다. 시스템은 등록 순서대로 실행된다.
func step_tick() -> void:
	for system: SimSystem in _systems:
		system.step(self)
	tick += 1


## 표현 계층 보간용: 마지막 틱 이후 다음 틱까지 진행 비율(0~1).
func get_interpolation_alpha() -> float:
	return clampf(_accum / SimConst.TICK_DT, 0.0, 1.0)


func get_sim_time_sec() -> float:
	return tick * SimConst.TICK_DT


# --- 결정론 --------------------------------------------------------------

## 같은 시드 + 같은 명령 로그 → 같은 해시. PLAN 15.3.
func state_hash() -> int:
	var parts: Array = [tick, rng.seed_value, rng.get_state()]
	for system: SimSystem in _systems:
		# 쓰이지 않은 선택 시스템(공병 등)은 해시에서 뺀다: 그 기능을 쓰지 않는 전투의 해시가 그대로 남는다.
		if not system.omit_from_hash():
			parts.append(system.state_hash())
	return hash(parts)
