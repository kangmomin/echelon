class_name SpatialHash
extends RefCounted
## 균일 격자 공간 해시. 탐지 후보·분리 벡터·거점 판정에서 근처 엘리먼트를 빠르게 찾는다. PLAN 15.4.
## 틱마다 clear() 후 다시 넣는다. 삽입 순서가 결정론적이면 질의 결과 순서도 결정론적이다.

var cell_size: float
var _buckets: Dictionary[Vector2i, Array] = {}


func _init(p_cell_size: float = 100.0) -> void:
	cell_size = p_cell_size


func clear() -> void:
	_buckets.clear()


func key_of(p: Vector2) -> Vector2i:
	return Vector2i(floori(p.x / cell_size), floori(p.y / cell_size))


func insert(item: Object, pos: Vector2) -> void:
	var k: Vector2i = key_of(pos)
	if not _buckets.has(k):
		_buckets[k] = []
	_buckets[k].append(item)


## pos 반경 radius 안의 버킷에 든 항목(거리 필터는 호출자가 한다).
func query(pos: Vector2, radius: float) -> Array:
	var out: Array = []
	var k0: Vector2i = key_of(pos - Vector2(radius, radius))
	var k1: Vector2i = key_of(pos + Vector2(radius, radius))
	for ky: int in range(k0.y, k1.y + 1):
		for kx: int in range(k0.x, k1.x + 1):
			var k := Vector2i(kx, ky)
			if _buckets.has(k):
				out.append_array(_buckets[k])
	return out
