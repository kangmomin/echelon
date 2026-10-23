class_name Notifications
extends VBoxContainer
## 우측 알림 피드. 예: "A 중대: 목표 도착", "2소대: 통신 두절". PLAN 12.1.

const MAX_ITEMS: int = 8
const LIFETIME_SEC: float = 10.0
const FADE_SEC: float = 1.5

var _ages: Dictionary[Label, float] = {}


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT)
	offset_left = -380
	offset_right = -8
	offset_top = 50
	grow_horizontal = Control.GROW_DIRECTION_BEGIN
	alignment = BoxContainer.ALIGNMENT_BEGIN
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_theme_constant_override(&"separation", 4)


func push(text: String, color: Color = Color(0.95, 0.95, 0.95)) -> void:
	var l := Label.new()
	l.text = text
	l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	l.custom_minimum_size = Vector2(360, 0)
	l.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	l.add_theme_font_size_override(&"font_size", 14)
	l.add_theme_color_override(&"font_color", color)
	l.add_theme_color_override(&"font_outline_color", Color(0, 0, 0, 0.9))
	l.add_theme_constant_override(&"outline_size", 5)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(l)
	_ages[l] = 0.0
	while get_child_count() > MAX_ITEMS:
		var old: Label = get_child(0)
		_ages.erase(old)
		old.queue_free()
		remove_child(old)


func _process(delta: float) -> void:
	for l: Label in _ages.keys():
		_ages[l] += delta
		var age: float = _ages[l]
		if age > LIFETIME_SEC:
			l.modulate.a = maxf(0.0, 1.0 - (age - LIFETIME_SEC) / FADE_SEC)
			if age > LIFETIME_SEC + FADE_SEC:
				_ages.erase(l)
				l.queue_free()
