class_name BattleSetup
extends RefCounted
## 전투 시작 구성. M1~M3 의 샌드박스와, M4 이후 편성(TF 빌드)에서 만드는 정식 구성을 둔다.


## 대형 이동·지휘 확인용 샌드박스: 청군 전차 중대·기계화 중대·정찰 소대, 적군 전차 중대.
static func sandbox(catalog: DataCatalog, seed_value: int = -1) -> Battle:
	var b: Battle = Battle.create(catalog, &"skirmish_01", seed_value)
	var blue: TaskForce = b.factory.create_task_force(&"blue", catalog.get_toe(&"us_armor_bn_tf"), Vector2(2000, 200), 0.0)
	var red: TaskForce = b.factory.create_task_force(&"red", catalog.get_toe(&"ru_btg_bmp"), Vector2(2000, 3850), PI)
	blue.companies.append(b.factory.spawn_company(catalog.get_unit_template(&"us_tank_co"), &"blue", Vector2(2700, 500), 0.0, blue, 0))
	blue.companies.append(b.factory.spawn_company(catalog.get_unit_template(&"us_mech_co"), &"blue", Vector2(1100, 450), 0.0, blue, 1))
	blue.organic.append(b.factory.spawn_platoon(catalog.get_unit_template(&"us_scout_plt"), &"blue", Vector2(1900, 800), 0.0, blue))
	red.companies.append(b.factory.spawn_company(catalog.get_unit_template(&"ru_tank_co"), &"red", Vector2(2100, 3300), PI, red, 0))
	red.companies.append(b.factory.spawn_company(catalog.get_unit_template(&"ru_msr_co_bmp3"), &"red", Vector2(1000, 3200), PI, red, 1))
	return b


## 튜토리얼 전장: 청군 전차 중대 + 박격포 소대, 적은 BMP-3 소대 하나(처음엔 지도 밖, 튜토리얼이 꺼낸다).
const TUTORIAL_START: Vector2 = Vector2(750, 650)
const TUTORIAL_WAYPOINT: Vector2 = Vector2(900, 1500)
const TUTORIAL_ENEMY: Vector2 = Vector2(1150, 2050)
const _OFF_MAP_PARK: Vector2 = Vector2(3900, 3950)


static func tutorial(catalog: DataCatalog) -> Battle:
	var b: Battle = Battle.create(catalog, &"skirmish_01", 20261023)
	var blue: TaskForce = b.factory.create_task_force(&"blue", catalog.get_toe(&"us_armor_bn_tf"), Vector2(500, 250), 0.0)
	blue.artillery_missions = 1
	blue.cas_sorties = 1
	blue.companies.append(b.factory.spawn_company(catalog.get_unit_template(&"us_tank_co"), &"blue", TUTORIAL_START, 0.0, blue, 0))
	blue.organic.append(b.factory.spawn_platoon(catalog.get_unit_template(&"us_mortar_plt"), &"blue", Vector2(600, 380), 0.0, blue))
	var red: TaskForce = b.factory.create_task_force(&"red", catalog.get_toe(&"ru_btg_bmp"), Vector2(3700, 3850), PI)
	var enemy: Platoon = b.factory.spawn_platoon(catalog.get_unit_template(&"ru_msr_plt_bmp3"), &"red", _OFF_MAP_PARK, PI, red)
	enemy.phase = 99  # 증원 시스템이 꺼내지 않게
	red.organic.append(enemy)
	b.factory.set_off_map(enemy, _OFF_MAP_PARK, PI)
	return b


## 지도 밖 소대를 pos 에 꺼낸다(튜토리얼·시나리오 연출용). 증원 시스템의 배치와 같은 순서.
static func reveal_platoon(b: Battle, p: Platoon, pos: Vector2, heading: float) -> void:
	p.on_map = true
	for e: Element in p.elements:
		e.on_map = true
	p.guide.reset(pos, heading)
	b.factory.place_elements(p)
	p.posture = &"idle"


## 편성(ForceSpec) 두 개로 스커미시를 만든다. 배치 단계에서 시작하므로 시뮬은 일시정지 상태.
## ai_sides: TF AI 가 지휘할 진영(기본: 적군). AI 끼리 붙이려면 [&"blue", &"red"].
static func skirmish(catalog: DataCatalog, blue: ForceSpec, red: ForceSpec, scenario_id: StringName = &"skirmish_01",
		seed_value: int = -1, ai_sides: Array[StringName] = [&"red"]) -> Battle:
	var b: Battle = Battle.create(catalog, scenario_id, seed_value)
	b.factory.build_task_force(blue, &"blue")
	b.factory.build_task_force(red, &"red")
	b.tf_ai.sides = ai_sides.duplicate()
	b.deploying = true
	b.world.set_paused(true)
	return b
