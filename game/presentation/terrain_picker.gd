class_name TerrainPicker
extends RefCounted
## 화면 좌표 → 지형 위 지도 좌표. 물리 충돌 없이 카메라 광선을 높이맵에 대고 전진·이분 탐색한다.

const STEP_M: float = 15.0
const MAX_DIST_M: float = 30000.0


## 맞으면 지도 좌표, 못 맞으면 Vector2.INF.
static func pick(camera: Camera3D, screen_pos: Vector2, terrain: Terrain) -> Vector2:
	var origin: Vector3 = camera.project_ray_origin(screen_pos)
	var dir: Vector3 = camera.project_ray_normal(screen_pos)
	var prev_t: float = 0.0
	var t: float = 0.0
	while t < MAX_DIST_M:
		var p3: Vector3 = origin + dir * t
		var p2: Vector2 = ViewCoords.to_sim(p3)
		var ground: float = terrain.ground_height(terrain.clamp_to_map(p2))
		if p3.y <= ground:
			# 이분 탐색으로 정밀화.
			var lo: float = prev_t
			var hi: float = t
			for i: int in 12:
				var mid: float = (lo + hi) * 0.5
				var q: Vector3 = origin + dir * mid
				if q.y <= terrain.ground_height(terrain.clamp_to_map(ViewCoords.to_sim(q))):
					hi = mid
				else:
					lo = mid
			var hit: Vector2 = ViewCoords.to_sim(origin + dir * hi)
			return hit if terrain.contains(hit) else terrain.clamp_to_map(hit)
		prev_t = t
		t += STEP_M * maxf(1.0, t / 2000.0)
	return Vector2.INF
