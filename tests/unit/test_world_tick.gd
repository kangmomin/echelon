extends GutTest
## SimWorld 틱 루프·배속·일시정지·결정론 테스트. PLAN 15.1, 15.3, 18.3.


## 등록 순서와 틱 번호를 기록하는 테스트용 시스템.
class RecorderSystem:
	extends SimSystem
	var tag: String
	var entries: Array[String]

	func _init(p_tag: String, p_entries: Array[String]) -> void:
		tag = p_tag
		entries = p_entries

	func step(world: SimWorld) -> void:
		entries.append("%s@%d" % [tag, world.tick])


## RNG 를 써서 상태가 바뀌는 테스트용 시스템(결정론 검증).
class RandomWalkSystem:
	extends SimSystem
	var pos: Vector2 = Vector2.ZERO

	func step(world: SimWorld) -> void:
		pos += Vector2(world.rng.next_range(-1.0, 1.0), world.rng.next_range(-1.0, 1.0))

	func state_hash() -> int:
		return hash(pos)


func _run_frames(world: SimWorld, frames: int, delta: float) -> int:
	var total: int = 0
	for i: int in frames:
		total += world.advance(delta)
	return total


func test_ten_ticks_per_second_at_1x() -> void:
	var world := SimWorld.new(1)
	assert_eq(_run_frames(world, 60, 1.0 / 60.0), 10)
	assert_eq(world.tick, 10)
	assert_almost_eq(world.get_sim_time_sec(), 1.0, 0.0001)


func test_partial_tick_accumulates() -> void:
	var world := SimWorld.new(1)
	assert_eq(world.advance(0.05), 0, "반 틱은 아직 실행 안 됨")
	assert_almost_eq(world.get_interpolation_alpha(), 0.5, 0.001)
	assert_eq(world.advance(0.05), 1, "누적되어 1틱")


func test_speed_steps_scale_tick_rate() -> void:
	var expected: Array[int] = [5, 10, 20, 40]
	for i: int in SimConst.SPEED_STEPS.size():
		var world := SimWorld.new(1)
		world.set_speed_index(i)
		assert_eq(_run_frames(world, 60, 1.0 / 60.0), expected[i], "speed %sx" % SimConst.SPEED_STEPS[i])


func test_speed_index_is_clamped() -> void:
	var world := SimWorld.new(1)
	for i: int in 10:
		world.speed_up()
	assert_eq(world.speed_index, SimConst.SPEED_STEPS.size() - 1)
	for i: int in 10:
		world.speed_down()
	assert_eq(world.speed_index, 0)


func test_pause_stops_time_and_does_not_accumulate() -> void:
	var world := SimWorld.new(1)
	world.set_paused(true)
	assert_eq(_run_frames(world, 120, 1.0 / 60.0), 0)
	world.set_paused(false)
	assert_eq(world.advance(0.0), 0, "일시정지 중 시간이 쌓이지 않았어야 함")


func test_large_hitch_is_capped() -> void:
	var world := SimWorld.new(1)
	assert_eq(world.advance(5.0), SimConst.MAX_TICKS_PER_ADVANCE)
	assert_lt(world.advance(0.0), 1, "밀린 시간은 버려짐")


func test_negative_delta_is_ignored() -> void:
	var world := SimWorld.new(1)
	assert_eq(world.advance(-1.0), 0)
	assert_eq(world.tick, 0)


func test_systems_run_in_registration_order_with_current_tick() -> void:
	var entries: Array[String] = []
	var world := SimWorld.new(1)
	world.add_system(RecorderSystem.new("a", entries))
	world.add_system(RecorderSystem.new("b", entries))
	world.step_tick()
	world.step_tick()
	assert_eq(entries, ["a@0", "b@0", "a@1", "b@1"] as Array[String])


func test_control_changes_emit_events() -> void:
	var world := SimWorld.new(1)
	world.toggle_pause()
	world.speed_up()
	var events: Array[Dictionary] = world.events.drain()
	assert_eq(events.size(), 2)
	var first: StringName = events[0]["type"]
	var second: StringName = events[1]["type"]
	assert_eq(first, &"paused_changed")
	assert_eq(second, &"speed_changed")
	assert_eq(world.events.pending_count(), 0, "drain 후 비어 있음")


func _hash_after(seed_value: int, ticks: int) -> int:
	var world := SimWorld.new(seed_value)
	world.add_system(RandomWalkSystem.new())
	for i: int in ticks:
		world.step_tick()
	return world.state_hash()


func test_same_seed_gives_same_state_hash() -> void:
	assert_eq(_hash_after(42, 500), _hash_after(42, 500))


func test_different_seed_gives_different_state_hash() -> void:
	assert_ne(_hash_after(42, 500), _hash_after(43, 500))


func test_rng_state_save_and_restore() -> void:
	var rng := SimRng.new(7)
	rng.next_float()
	var saved: int = rng.get_state()
	var a: Array[float] = [rng.next_float(), rng.next_float(), rng.next_float()]
	rng.set_state(saved)
	var b: Array[float] = [rng.next_float(), rng.next_float(), rng.next_float()]
	assert_eq(a, b)
