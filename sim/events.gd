class_name SimEvents
extends RefCounted
## 시뮬 → 표현 계층 이벤트 큐. PLAN 18.2: 시뮬은 표현 계층을 직접 부르지 않고 여기에 쌓기만 한다.
## 표현 계층은 프레임마다 drain() 으로 한 번에 가져간다.
##
## 이벤트 형식: {"type": StringName, "tick": int, "payload": Dictionary}

var _queue: Array[Dictionary] = []
## 시뮬 안의 구독자(전투 기록 등). 이벤트가 쌓이는 즉시 같은 틱 안에서 불린다. 표현 계층은 쓰지 않는다.
var _listeners: Array[Callable] = []
## 큐 상한: 아무도 drain 하지 않는 헤드리스 실행에서 메모리가 끝없이 늘지 않게 오래된 것을 버린다.
const MAX_QUEUE: int = 20000
const KEEP_AFTER_TRIM: int = 10000


func emit_event(type: StringName, tick: int, payload: Dictionary = {}) -> void:
	var ev: Dictionary = {"type": type, "tick": tick, "payload": payload}
	_queue.append(ev)
	if _queue.size() > MAX_QUEUE:
		_queue = _queue.slice(_queue.size() - KEEP_AFTER_TRIM)
	for l: Callable in _listeners:
		l.call(ev)


func add_listener(listener: Callable) -> void:
	_listeners.append(listener)


func clear_listeners() -> void:
	_listeners.clear()


## 쌓인 이벤트를 전부 꺼내고 큐를 비운다.
func drain() -> Array[Dictionary]:
	var out: Array[Dictionary] = _queue
	_queue = []
	return out


func pending_count() -> int:
	return _queue.size()
