class_name ViewCoords
extends RefCounted
## 시뮬 좌표(2D, x = 동, y = 북) ↔ Godot 3D 좌표(X = 동, Y = 위, -Z = 북) 변환.
## 시뮬 heading(0 = 북, 시계방향 +) ↔ Node3D 의 rotation.y(반시계방향 +) 변환도 여기서만 한다.


static func to_3d(sim_pos: Vector2, height: float = 0.0) -> Vector3:
	return Vector3(sim_pos.x, height, -sim_pos.y)


static func to_sim(pos: Vector3) -> Vector2:
	return Vector2(pos.x, -pos.z)


## 시뮬 heading → Node3D.rotation.y. 모델의 앞쪽은 -Z 라고 가정한다.
static func heading_to_yaw(heading: float) -> float:
	return -heading
