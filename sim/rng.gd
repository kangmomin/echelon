class_name SimRng
extends RefCounted
## 시뮬레이션의 유일한 난수 원천. 시드와 내부 상태를 저장·복원할 수 있다. PLAN 15.3.
## 시뮬 코드는 전역 randf()/randi() 를 쓰지 않고 반드시 이 객체를 통해 난수를 뽑는다.

var seed_value: int = 0
var _rng: RandomNumberGenerator = RandomNumberGenerator.new()


func _init(p_seed: int = 0) -> void:
	seed_value = p_seed
	_rng.seed = p_seed


## [0, 1) 균등 분포.
func next_float() -> float:
	return _rng.randf()


## [from, to] 균등 분포 실수.
func next_range(from: float, to: float) -> float:
	return _rng.randf_range(from, to)


## [from, to] 균등 분포 정수(양 끝 포함).
func next_int(from: int, to: int) -> int:
	return _rng.randi_range(from, to)


## 확률 p 로 true.
func chance(p: float) -> bool:
	return _rng.randf() < p


func get_state() -> int:
	return _rng.state


func set_state(state: int) -> void:
	_rng.state = state
