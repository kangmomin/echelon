class_name TestFixture
extends RefCounted
## 테스트용 전장 생성 도우미. 카탈로그는 한 번만 읽어 재사용한다.

static var _catalog: DataCatalog = null


static func catalog() -> DataCatalog:
	if _catalog == null:
		_catalog = DataLoader.load_catalog()
	return _catalog


## 빈 전장 + 양측 TF(지휘소만). 부대는 테스트가 붙인다.
## instant = true 면 명령이 지휘망 지연 없이 즉시 실행된다(이동·전투 단위 테스트용).
static func battle(seed_value: int = 1, instant: bool = true) -> Battle:
	var cat: DataCatalog = catalog()
	var b: Battle = Battle.create(cat, &"skirmish_01", seed_value, instant)
	b.factory.create_task_force(&"blue", cat.get_toe(&"us_armor_bn_tf"), Vector2(2000, 150), 0.0)
	b.factory.create_task_force(&"red", cat.get_toe(&"ru_btg_bmp"), Vector2(2000, 3850), PI)
	return b


static func platoon(b: Battle, template_id: StringName, side: StringName, pos: Vector2, heading: float = 0.0) -> Platoon:
	var tf: TaskForce = b.task_forces[side]
	var p: Platoon = b.factory.spawn_platoon(b.catalog.get_unit_template(template_id), side, pos, heading, tf)
	tf.organic.append(p)
	return p


static func company(b: Battle, template_id: StringName, side: StringName, pos: Vector2, heading: float = 0.0) -> Company:
	var tf: TaskForce = b.task_forces[side]
	var c: Company = b.factory.spawn_company(b.catalog.get_unit_template(template_id), side, pos, heading, tf, tf.companies.size())
	tf.companies.append(c)
	return c


static func move(b: Battle, unit: Unit, pos: Vector2, type: int = Order.Type.MOVE, facing: float = NAN,
		params: Dictionary = {}) -> Order:
	var o: Order = Order.make(type, unit.id, params.duplicate())
	o.set_pos(pos)
	if not is_nan(facing):
		o.set_facing(facing)
	return b.issue_order(o)


## 엘리먼트를 즉시 격파 처리(테스트용).
static func kill(e: Element) -> void:
	e.state = Element.State.DESTROYED
	for p: Element in e.passengers:
		p.state = Element.State.DESTROYED


## 제대를 통째로 옮긴다(테스트용).
static func teleport(b: Battle, u: Unit, pos: Vector2) -> void:
	u.guide.reset(pos, u.guide.heading)
	if u is Platoon:
		b.factory.place_elements(u as Platoon)


## 첫 엘리먼트가 움직이기 시작할 때까지 걸린 시간(초). 제한 안에 안 움직이면 -1.
static func seconds_until_moving(b: Battle, u: Unit, limit_sec: float) -> float:
	var start: int = b.tick()
	var limit: int = roundi(limit_sec * SimConst.TICK_RATE)
	while b.tick() - start < limit:
		b.step_tick()
		for e: Element in u.alive_elements():
			if not e.is_mounted() and e.velocity.length() > 0.5:
				return (b.tick() - start) / float(SimConst.TICK_RATE)
	return -1.0


## 평지 시험 지도(4×4 km). forest_rect 를 주면 그 사각형이 숲이 된다.
static func flat_map(forest_rect: Rect2 = Rect2()) -> MapDef:
	var m := MapDef.new()
	m.id = &"test_flat"
	m.display_name = "평지"
	m.size_m = Vector2(4000, 4000)
	m.cell_m = 10.0
	m.height_seed = 1
	m.base_m = 40.0
	m.amplitude_m = 0.0
	m.frequency = 0.001
	m.octaves = 1
	if forest_rect.has_area():
		var f := MapDef.Feature.new()
		f.name = "시험 숲"
		f.points = PackedVector2Array([forest_rect.position, Vector2(forest_rect.end.x, forest_rect.position.y),
			forest_rect.end, Vector2(forest_rect.position.x, forest_rect.end.y)])
		m.forests.append(f)
	return m


static func battle_on(map: MapDef, seed_value: int = 1, instant: bool = true) -> Battle:
	var cat: DataCatalog = catalog()
	var b: Battle = Battle.create(cat, &"skirmish_01", seed_value, instant, map)
	b.factory.create_task_force(&"blue", cat.get_toe(&"us_armor_bn_tf"), Vector2(200, 2000), PI / 2.0)
	b.factory.create_task_force(&"red", cat.get_toe(&"ru_btg_bmp"), Vector2(3800, 3800), -PI / 2.0)
	return b


static func losses(u: Unit) -> int:
	var n: int = 0
	for e: Element in u.all_elements():
		if not e.is_alive():
			n += 1
	return n
