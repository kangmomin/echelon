class_name ElementDef
extends RefCounted
## 엘리먼트(차량 1대 또는 분대 1개) 정의. data/elements/*.json. PLAN 16.1.

const CLASSES: Array[String] = ["tank", "ifv", "apc", "recon", "mortar", "aa", "cp", "truck", "inf_squad", "fo", "ew",
	"heli", "eng", "at"]
const FACTIONS: Array[String] = ["US", "RU"]
const ARMOR_FACINGS: Array[String] = ["front", "side", "rear", "top"]

var id: StringName
var display_name: String
var faction: StringName
var element_class: StringName
var hp: float
## 방향별 장갑(mm RHA 등가). 키: front, side, rear, top.
var armor: Dictionary[StringName, float] = {}
## 탐지 계수(클수록 잘 보임).
var size: float
## 은폐 0~1.
var stealth: float
var optics_m: float
var speed_road_kmh: float
var speed_offroad_kmh: float
var weapons: PackedStringArray
## 무기 id → 탄수.
var ammo: Dictionary[StringName, int] = {}
var radio_range_m: float
var transport_capacity: int
var crew: int
var cost: int
## 완전 보급에 드는 보급 단위.
var supply_full: int
## 보급 차량이 실을 수 있는 보급 단위(트럭만).
var supply_capacity: int = 0
var loadouts: PackedStringArray


static func from_dict(d: Dictionary, r: DefReader) -> ElementDef:
	var e := ElementDef.new()
	e.id = StringName(r.req_string(d, "id"))
	e.display_name = r.req_string(d, "name")
	e.faction = StringName(r.req_string(d, "faction"))
	r.check_one_of(e.faction, FACTIONS, "faction")
	e.element_class = StringName(r.req_string(d, "class"))
	r.check_one_of(e.element_class, CLASSES, "class")

	e.hp = r.req_number(d, "hp")
	r.check_range(e.hp, 1.0, 10000.0, "hp")

	var armor_raw: Dictionary = r.req_dict(d, "armor")
	for facing: String in ARMOR_FACINGS:
		var v: float = r.req_number(armor_raw, facing)
		r.check_range(v, 0.0, 5000.0, "armor.%s" % facing)
		e.armor[StringName(facing)] = v

	e.size = r.req_number(d, "size")
	r.check_range(e.size, 0.1, 5.0, "size")
	e.stealth = r.req_number(d, "stealth")
	r.check_range(e.stealth, 0.0, 1.0, "stealth")
	e.optics_m = r.req_number(d, "optics_m")
	r.check_range(e.optics_m, 0.0, 10000.0, "optics_m")

	var speed: Dictionary = r.req_dict(d, "speed_kmh")
	e.speed_road_kmh = r.req_number(speed, "road")
	e.speed_offroad_kmh = r.req_number(speed, "offroad")
	r.check_range(e.speed_road_kmh, 0.0, 350.0, "speed_kmh.road")
	r.check_range(e.speed_offroad_kmh, 0.0, 350.0, "speed_kmh.offroad")

	e.weapons = r.req_string_array(d, "weapons")
	var ammo_raw: Dictionary = r.req_dict(d, "ammo")
	for key: Variant in ammo_raw.keys():
		var weapon_id: String = str(key)
		if not e.weapons.has(weapon_id):
			r.error("ammo 의 '%s' 가 weapons 목록에 없음" % weapon_id)
		var rounds: int = r.as_int(ammo_raw[key], "ammo.%s" % weapon_id)
		if rounds < 0:
			r.error("ammo.%s 는 0 이상이어야 함" % weapon_id)
		e.ammo[StringName(weapon_id)] = rounds

	e.radio_range_m = r.req_number(d, "radio_range_m")
	r.check_range(e.radio_range_m, 0.0, 50000.0, "radio_range_m")
	e.transport_capacity = r.opt_int(d, "transport_capacity", 0)
	e.crew = r.req_int(d, "crew")
	e.cost = r.req_int(d, "cost")
	r.check_range(e.cost, 0, 10000, "cost")
	e.supply_full = r.req_int(d, "supply_full")
	e.supply_capacity = r.opt_int(d, "supply_capacity", 0)
	e.loadouts = r.opt_string_array(d, "loadouts")
	return e


func get_armor(facing: StringName) -> float:
	return armor.get(facing, 0.0)
