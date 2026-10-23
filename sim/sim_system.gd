class_name SimSystem
extends RefCounted
## 틱마다 실행되는 시뮬레이션 시스템의 기반 클래스.
## SimWorld 에 등록된 순서대로 실행된다(결정론을 위해 순서가 고정이다).


## 디버그 표시용 이름.
func get_system_name() -> StringName:
	return &"system"


## 한 틱 처리. world.tick 은 지금 처리 중인 틱 번호(0부터)다.
func step(_world: SimWorld) -> void:
	pass


## 결정론 검증용 상태 해시. 상태를 가진 시스템은 반드시 오버라이드한다.
func state_hash() -> int:
	return 0


## true 면 SimWorld.state_hash 에서 뺀다(아직 아무 상태도 없는 선택 시스템용).
func omit_from_hash() -> bool:
	return false
