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
