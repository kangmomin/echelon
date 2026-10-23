class_name AarScreen
extends Control
## 사후 검토(AAR): 승패와 요인, 손실, 명령 지연 통계, 거점, 교전 타임라인. PLAN 4장.
## 입력은 BattleStats.summary() 딕셔너리(헤드리스 AAR JSON 과 같은 형식).

signal menu_requested
signal close_requested

const CLASS_NAMES: Dictionary[String, String] = {
	"tank": "전차", "ifv": "보병전투차", "recon": "정찰차", "inf_squad": "보병 분대", "mortar": "박격포",
	"cp": "지휘소", "truck": "트럭", "fo": "관측반", "aa": "방공", "ew": "전자전", "apc": "장갑차",
}
const REASONS: Dictionary[String, String] = {"collapse": "전투력 붕괴", "time": "시간 종료(승점)"}

var aar: Dictionary = {}
var player_side: String = "blue"


func setup(p_aar: Dictionary, p_player_side: StringName) -> void:
	aar = p_aar
	player_side = String(p_player_side)


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var dim := ColorRect.new()
	dim.color = Color(0, 0, 0, 0.55)
	dim.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(dim)
	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(center)
	var p: PanelContainer = UiKit.panel(20)
	p.custom_minimum_size = Vector2(980, 640)
	center.add_child(p)
	var v := VBoxContainer.new()
	v.add_theme_constant_override(&"separation", 10)
	p.add_child(v)

	var result: Dictionary = aar.get("result", {})
	var winner: String = result.get("winner", "")
	var reason: String = result.get("reason", "")
	var title: String = "무승부" if winner == "" else ("승리" if winner == player_side else "패배")
	var title_color: Color = UiKit.WARN_COLOR if winner == "" else (UiKit.OK_COLOR if winner == player_side else UiKit.ERR_COLOR)
	v.add_child(UiKit.label("사후 검토 — %s" % title, 26, title_color))
	var dur: float = aar.get("duration_sec", 0.0)
	var contact: float = aar.get("first_contact_sec", -1.0)
	v.add_child(UiKit.label("판정: %s   ·   전투 시간 %s   ·   첫 교전 %s" % [REASONS.get(reason, reason), _mmss(dur),
		_mmss(contact) if contact >= 0.0 else "-"], 14, UiKit.TEXT_DIM))

	var cols := HBoxContainer.new()
	cols.add_theme_constant_override(&"separation", 18)
	cols.size_flags_vertical = Control.SIZE_EXPAND_FILL
	v.add_child(cols)
	var left := UiKit.rich(Vector2(460, 0))
	left.text = _sides_text()
	cols.add_child(left)
	var right := VBoxContainer.new()
	right.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	cols.add_child(right)
	right.add_child(UiKit.label("교전 타임라인", 15, UiKit.ACCENT))
	var scroll := ScrollContainer.new()
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.custom_minimum_size = Vector2(440, 420)
	right.add_child(scroll)
	var tl := UiKit.rich(Vector2(420, 0))
	tl.text = _timeline_text()
	scroll.add_child(tl)

	var nav := HBoxContainer.new()
	nav.add_child(UiKit.button("전장 보기", func() -> void: close_requested.emit(), 140))
	nav.add_child(UiKit.button("메뉴로", func() -> void: menu_requested.emit(), 140))
	v.add_child(nav)


func _sides_text() -> String:
	var t: String = ""
	var sides: Dictionary = aar.get("sides", {})
	for side: String in [player_side, "red" if player_side == "blue" else "blue"]:
		var s: Dictionary = sides.get(side, {})
		var mine: bool = side == player_side
		t += "[b][color=#%s]%s — %s[/color][/b]\n" % [UiKit.hex(Color(0.55, 0.75, 1.0) if mine else Color(1.0, 0.55, 0.5)),
			"아군" if mine else "적", str(s.get("name", ""))]
		var vp: float = s.get("vp", 0.0)
		var ratio: float = s.get("value_ratio", 1.0)
		t += "승점 %.0f   ·   남은 전투력 %d%%\n" % [vp, roundi(ratio * 100.0)]
		var lost: int = s.get("losses_total", 0)
		var total: int = s.get("elements", 0)
		var by_class: Dictionary = s.get("losses", {})
		var parts: PackedStringArray = []
		for k: Variant in by_class.keys():
			var n: int = by_class[k]
			parts.append("%s %d" % [CLASS_NAMES.get(str(k), str(k)), n])
		var damaged: int = s.get("damaged", 0)
		t += "손실 %d / %d  (%s)   손상 %d\n" % [lost, total, ", ".join(parts) if not parts.is_empty() else "없음", damaged]
		var mean: float = s.get("order_delay_mean", 0.0)
		var mx: float = s.get("order_delay_max", 0.0)
		var orders: int = s.get("orders", 0)
		t += "명령 %d건 — 평균 지연 %.0f초, 최대 %.0f초\n" % [orders, mean, mx]
		var fm: int = s.get("fire_missions", 0)
		var cuts: int = s.get("comms_cuts", 0)
		var rs: int = s.get("resupplies", 0)
		t += "화력 요청 %d   ·   통신 두절 %d회   ·   보급 %d회\n\n" % [fm, cuts, rs]
	t += "[b]거점[/b]\n"
	for pt: Variant in aar.get("points", []):
		var pd: Dictionary = pt
		var point_owner: String = pd.get("owner", "")
		var hb: float = pd.get("held_blue_sec", 0.0)
		var hr: float = pd.get("held_red_sec", 0.0)
		t += "%s — %s  (청 %s, 적 %s 보유)\n" % [str(pd.get("name", "")), _owner_name(point_owner), _mmss(hb), _mmss(hr)]
	t += "\n[b]승패 요인[/b]\n" + _factors()
	return t


func _owner_name(point_owner: String) -> String:
	if point_owner == "":
		return "중립"
	return "아군" if point_owner == player_side else "적"


## 간단한 요인 문장: 손실 교환비, 명령 지연 비교, 화력 사용.
func _factors() -> String:
	var sides: Dictionary = aar.get("sides", {})
	var me: Dictionary = sides.get(player_side, {})
	var them: Dictionary = sides.get("red" if player_side == "blue" else "blue", {})
	var lines: PackedStringArray = []
	var my_loss: int = me.get("losses_total", 0)
	var their_loss: int = them.get("losses_total", 0)
	lines.append("• 손실 교환비 %d : %d" % [my_loss, their_loss])
	var my_delay: float = me.get("order_delay_mean", 0.0)
	var their_delay: float = them.get("order_delay_mean", 0.0)
	if absf(my_delay - their_delay) > 3.0:
		lines.append("• 명령 지연이 %s 쪽이 %.0f초 짧았다" % ["아군" if my_delay < their_delay else "적", absf(my_delay - their_delay)])
	var my_cuts: int = me.get("comms_cuts", 0)
	if my_cuts >= 3:
		lines.append("• 통신 두절이 %d회 — 무전 범위와 지휘소 위치를 점검" % my_cuts)
	var my_fire: int = me.get("fire_missions", 0)
	var their_fire: int = them.get("fire_missions", 0)
	if my_fire + their_fire > 0:
		lines.append("• 화력 요청 %d : %d" % [my_fire, their_fire])
	return "\n".join(lines)


func _timeline_text() -> String:
	var t: String = ""
	for item: Variant in aar.get("timeline", []):
		var d: Dictionary = item
		var tick: int = d.get("tick", 0)
		var side: String = d.get("side", "")
		var col: Color = UiKit.TEXT_DIM
		if side != "":
			col = Color(0.6, 0.8, 1.0) if side == player_side else Color(1.0, 0.6, 0.55)
		t += "[color=#%s]%s[/color]  [color=#%s]%s[/color]\n" % [UiKit.hex(UiKit.TEXT_DIM), _mmss(tick / 10.0),
			UiKit.hex(col), str(d.get("text", ""))]
	return t


static func _mmss(sec: float) -> String:
	var s: int = maxi(0, roundi(sec))
	return "%02d:%02d" % [floori(s / 60.0), s % 60]
