class_name SimEvents
extends RefCounted
## 시뮬 → 표현 계층 이벤트 큐. PLAN 18.2: 시뮬은 표현 계층을 직접 부르지 않고 여기에 쌓기만 한다.
## 표현 계층은 프레임마다 drain() 으로 한 번에 가져간다.
##
## 이벤트 형식: {"type": StringName, "tick": int, "payload": Dictionary}

var _queue: Array[Dictionary] = []


func emit_event(type: StringName, tick: int, payload: Dictionary = {}) -> void:
	_queue.append({"type": type, "tick": tick, "payload": payload})


## 쌓인 이벤트를 전부 꺼내고 큐를 비운다.
func drain() -> Array[Dictionary]:
	var out: Array[Dictionary] = _queue
	_queue = []
	return out


func pending_count() -> int:
	return _queue.size()
