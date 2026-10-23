class_name Company
extends Unit
## 중대. 기본 명령 단위. 소대 3~4개 + 중대본부(중대장·XO). PLAN 5장.

## 대형에 들어가는 전투 소대(원래 순서).
var platoons: Array[Platoon] = []
## 중대본부 반(중대장 차량, XO 차량).
var hq: Platoon = null
var xo: Element = null


func _init() -> void:
	level = Level.COMPANY


func children_units() -> Array[Unit]:
	var out: Array[Unit] = []
	if hq != null:
		out.append(hq)
	for p: Platoon in platoons:
		out.append(p)
	return out


func all_elements() -> Array[Element]:
	var out: Array[Element] = []
	if hq != null:
		out.append_array(hq.elements)
	for p: Platoon in platoons:
		out.append_array(p.elements)
	return out


func is_destroyed() -> bool:
	if hq != null and not hq.is_destroyed():
		return false
	for p: Platoon in platoons:
		if not p.is_destroyed():
			return false
	return true


## 지금 중대 대형을 따르는 소대(분리·전멸·지도 밖 제외), 원래 순서.
func formation_platoons() -> Array[Platoon]:
	var out: Array[Platoon] = []
	for p: Platoon in platoons:
		if p.parent == self and not p.detached and p.on_map and not p.is_destroyed():
			out.append(p)
	return out


func leader() -> Element:
	if commander != null and commander.is_alive():
		return commander
	return null


## 중대가 낼 수 있는 기준 속도: 대형 소대 중 가장 느린 소대.
func base_speed() -> float:
	var best: float = INF
	for p: Platoon in formation_platoons():
		var s: float = p.base_speed()
		if s > 0.0:
			best = minf(best, s)
	if hq != null and not hq.is_destroyed():
		var hs: float = hq.base_speed()
		if hs > 0.0:
			best = minf(best, hs)
	return 0.0 if best == INF else best
