class_name TestFixture
extends RefCounted
## 테스트용 전장 생성 도우미. 카탈로그는 한 번만 읽어 재사용한다.

static var _catalog: DataCatalog = null


static func catalog() -> DataCatalog:
	if _catalog == null:
		_catalog = DataLoader.load_catalog()
	return _catalog


## 빈 전장 + 양측 TF(지휘소만). 부대는 테스트가 붙인다.
static func battle(seed_value: int = 1) -> Battle:
	var cat: DataCatalog = catalog()
	var b: Battle = Battle.create(cat, &"skirmish_01", seed_value)
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
