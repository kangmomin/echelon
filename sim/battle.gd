class_name Battle
extends RefCounted
## 한 판의 시뮬레이션 전체: 지형, 경로탐색, 진영별 TF, 엘리먼트, 시스템. 노드 없이 돌고 테스트에서 직접 쓴다.
## 표현 계층은 이 객체를 읽기만 하고, 플레이어 입력은 issue_order() 로만 들어온다.

const SIDES: Array[StringName] = [&"blue", &"red"]

var catalog: DataCatalog
var scenario: ScenarioDef
var map: MapDef
var terrain: Terrain
var pathfinder: GridPathfinder
var world: SimWorld
var spatial: SpatialHash = SpatialHash.new(100.0)
var factory: ForceFactory

var elements: Array[Element] = []
var elements_by_id: Dictionary[int, Element] = {}
var units: Array[Unit] = []
var units_by_id: Dictionary[int, Unit] = {}
var task_forces: Dictionary[StringName, TaskForce] = {}
var orders_by_id: Dictionary[int, Order] = {}

var executor: OrderExecutor
var movement: MovementSystem
var comms: CommsNet
var succession: SuccessionSystem
var platoon_ai: PlatoonAI
var detection: DetectionSystem
var combat: CombatSystem
var morale: MoraleSystem
var fires: FireSupportSystem
var supply: SupplySystem
## 연막 구름(M5 화력지원이 만든다).
var smokes: Array[Los.Smoke] = []
## 명령 전파 시스템. null 이면 명령을 즉시 실행한다(이동 테스트용 instant 모드).
var order_system: OrderSystem = null

## 플레이어가 조종하는 진영.
var player_side: StringName = &"blue"
## 배치 단계: 시뮬이 멈춰 있고 플레이어는 배치 구역 안에서 부대를 옮길 수 있다. PLAN 6.1.
var deploying: bool = false

var _next_element_id: int = 1
var _next_unit_id: int = 1
var _next_order_id: int = 1


## 시나리오의 지도로 빈 전장을 만든다. 부대는 따로 생성한다.
static func create(p_catalog: DataCatalog, scenario_id: StringName, seed_override: int = -1,
		instant_orders: bool = false, map_override: MapDef = null) -> Battle:
	var b := Battle.new()
	b.catalog = p_catalog
	b.scenario = p_catalog.get_scenario(scenario_id)
	assert(b.scenario != null, "시나리오 없음: %s" % scenario_id)
	b.map = map_override if map_override != null else p_catalog.get_map(b.scenario.map_id)
	b.terrain = Terrain.build(b.map)
	b.pathfinder = GridPathfinder.new(b.terrain)
	b.world = SimWorld.new(b.scenario.seed_value if seed_override < 0 else seed_override)
	b.factory = ForceFactory.new(b)
	b.executor = OrderExecutor.new(b)
	# 시스템 실행 순서(결정론): 통신 → 승계 → 명령 전파 → 자율 반응 → 이동 → 탐지 → 사격 → 화력지원 → 보급 → 사기.
	b.comms = CommsNet.new(b)
	b.morale = MoraleSystem.new(b)
	b.world.add_system(b.comms)
	b.succession = SuccessionSystem.new(b)
	b.world.add_system(b.succession)
	if not instant_orders:
		b.order_system = OrderSystem.new(b, b.comms)
		b.world.add_system(b.order_system)
	b.platoon_ai = PlatoonAI.new(b)
	b.world.add_system(b.platoon_ai)
	b.movement = MovementSystem.new(b)
	b.world.add_system(b.movement)
	b.detection = DetectionSystem.new(b)
	b.world.add_system(b.detection)
	b.combat = CombatSystem.new(b)
	b.world.add_system(b.combat)
	b.fires = FireSupportSystem.new(b)
	b.world.add_system(b.fires)
	b.supply = SupplySystem.new(b)
	b.world.add_system(b.supply)
	b.world.add_system(b.morale)
	return b


# --- 등록 ----------------------------------------------------------------

func register_element(e: Element) -> void:
	e.id = _next_element_id
	_next_element_id += 1
	elements.append(e)
	elements_by_id[e.id] = e


func register_unit(u: Unit) -> void:
	u.id = _next_unit_id
	_next_unit_id += 1
	units.append(u)
	units_by_id[u.id] = u


func get_unit(unit_id: int) -> Unit:
	return units_by_id.get(unit_id)


func get_element(element_id: int) -> Element:
	return elements_by_id.get(element_id)


func get_order(order_id: int) -> Order:
	return orders_by_id.get(order_id)


static func enemy_of(side: StringName) -> StringName:
	return &"red" if side == &"blue" else &"blue"


func tick() -> int:
	return world.tick


# --- 명령 ----------------------------------------------------------------

## 명령을 발행한다. 전파 시스템이 있으면 지휘망을 타고, 없으면 즉시 실행된다.
func issue_order(order: Order) -> Order:
	order.id = _next_order_id
	_next_order_id += 1
	order.tick_issued = world.tick
	orders_by_id[order.id] = order
	if order_system != null:
		order_system.submit(order)
	else:
		var unit: Unit = get_unit(order.target_unit_id)
		if unit != null:
			order.state = Order.State.RECEIVED
			order.received_tick = world.tick
			executor.execute(unit, order)
	return order


## 하위 명령을 만들 때 id 를 붙여 등록만 한다(전파는 호출자가).
func register_order(order: Order) -> Order:
	order.id = _next_order_id
	_next_order_id += 1
	order.tick_issued = world.tick
	orders_by_id[order.id] = order
	return order


## 순환 참조(제대 ↔ 하위 제대, 엘리먼트 ↔ 소대, 시스템 ↔ 전장)를 끊는다. 전투가 끝나면 반드시 부른다.
func dispose() -> void:
	for u: Unit in units:
		u.parent = null
		u.commander = null
		u.mission = null
		u.queued_orders.clear()
		u.guide.pending = null
		if u is Platoon:
			(u as Platoon).elements.clear()
		elif u is Company:
			(u as Company).platoons.clear()
			(u as Company).hq = null
			(u as Company).xo = null
		elif u is TaskForce:
			var tf: TaskForce = u
			tf.companies.clear()
			tf.organic.clear()
			tf.cp_section = null
	for e: Element in elements:
		e.platoon = null
		e.mounted_in = null
		e.passengers.clear()
		e.path_request = null
	units.clear()
	units_by_id.clear()
	elements.clear()
	elements_by_id.clear()
	task_forces.clear()
	orders_by_id.clear()
	spatial.clear()
	world = SimWorld.new(0)
	executor = null
	movement = null
	order_system = null
	comms = null
	succession = null
	platoon_ai = null
	detection = null
	combat = null
	morale = null
	fires = null
	supply = null
	smokes.clear()
	factory = null


# --- 진행 ----------------------------------------------------------------

## 배치 단계를 끝내고 전투를 시작한다.
func begin_battle() -> void:
	deploying = false
	world.set_paused(false)
	world.events.emit_event(&"battle_started", world.tick, {})


## 배치 단계에서 side 의 제대를 pos 로 옮긴다. 배치 구역 밖이나 지도 밖 부대면 false.
func deploy_unit(u: Unit, pos: Vector2, heading: float) -> bool:
	if not deploying or u == null or not u.on_map:
		return false
	var setup: ScenarioDef.SideSetup = scenario.sides.get(u.side)
	if setup == null:
		return false
	var p: Vector2 = Deployment.clamp_to_zone(pos, setup.deploy_zone, terrain)
	if u is Platoon and (u as Platoon).follows_parent():
		# 중대 안 소대는 중대를 옮긴다.
		u = u.parent
	factory.redeploy(u, p, heading)
	return true


## 제대 자신의 판단으로 내는 명령(자율 반응·보급·증원). 지휘망을 타지 않고 바로 실행한다.
func self_order(u: Unit, type: int, pos: Vector2, params: Dictionary = {}) -> Order:
	var o: Order = Order.make(type, u.id, params)
	o.issuer = &"self"
	o.set_pos(terrain.clamp_to_map(pos, 20.0))
	register_order(o)
	o.state = Order.State.RECEIVED
	o.received_tick = world.tick
	executor.execute(u, o)
	return o


func step_tick() -> void:
	world.step_tick()


func run_ticks(n: int) -> void:
	for i: int in n:
		world.step_tick()


func run_seconds(sec: float) -> void:
	run_ticks(roundi(sec * SimConst.TICK_RATE))


# --- 조회 ----------------------------------------------------------------

func side_elements(side: StringName) -> Array[Element]:
	var out: Array[Element] = []
	for e: Element in elements:
		if e.side == side:
			out.append(e)
	return out


func side_units(side: StringName) -> Array[Unit]:
	var out: Array[Unit] = []
	for u: Unit in units:
		if u.side == side:
			out.append(u)
	return out


## 공간 해시에서 반경 안의 활성 엘리먼트(거리 필터 포함).
func elements_near(pos: Vector2, radius: float) -> Array[Element]:
	var out: Array[Element] = []
	var r2: float = radius * radius
	for item: Variant in spatial.query(pos, radius):
		var e: Element = item
		if e.is_active() and e.pos.distance_squared_to(pos) <= r2:
			out.append(e)
	return out


func rebuild_spatial() -> void:
	spatial.clear()
	for e: Element in elements:
		if e.is_active():
			spatial.insert(e, e.pos)
