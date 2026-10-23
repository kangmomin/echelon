class_name UiKit
extends RefCounted
## 화면들(메뉴·편성·AAR)이 같이 쓰는 작은 UI 생성 도우미. 색·여백을 한 곳에서 맞춘다.

const BG: Color = Color(0.09, 0.10, 0.12)
const PANEL: Color = Color(0.06, 0.07, 0.09, 0.95)
const ACCENT: Color = Color(0.95, 0.80, 0.35)
const TEXT_DIM: Color = Color(0.65, 0.67, 0.72)
const OK_COLOR: Color = Color(0.45, 1.0, 0.5)
const WARN_COLOR: Color = Color(1.0, 0.8, 0.3)
const ERR_COLOR: Color = Color(1.0, 0.42, 0.38)


static func panel(margin: int = 10) -> PanelContainer:
	var p := PanelContainer.new()
	var style := StyleBoxFlat.new()
	style.bg_color = PANEL
	style.set_corner_radius_all(4)
	style.set_content_margin_all(margin)
	p.add_theme_stylebox_override(&"panel", style)
	return p


static func label(text: String, size: int = 14, color: Color = Color(0.93, 0.93, 0.95)) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_size_override(&"font_size", size)
	l.add_theme_color_override(&"font_color", color)
	return l


static func button(text: String, on_press: Callable, min_width: float = 0.0) -> Button:
	var b := Button.new()
	b.text = text
	b.focus_mode = Control.FOCUS_NONE
	if min_width > 0.0:
		b.custom_minimum_size.x = min_width
	b.pressed.connect(on_press)
	return b


## 선택지 목록: items = [[표시 문자열, 값], ...]. 값이 같은 항목을 선택한다.
static func option(items: Array, selected: Variant, on_change: Callable, min_width: float = 0.0) -> OptionButton:
	var o := OptionButton.new()
	o.focus_mode = Control.FOCUS_NONE
	if min_width > 0.0:
		o.custom_minimum_size.x = min_width
	for i: int in items.size():
		var item: Array = items[i]
		o.add_item(str(item[0]), i)
		o.set_item_metadata(i, item[1])
		if item[1] == selected:
			o.select(i)
	o.item_selected.connect(func(index: int) -> void: on_change.call(o.get_item_metadata(index)))
	return o


static func rich(min_size: Vector2 = Vector2.ZERO) -> RichTextLabel:
	var r := RichTextLabel.new()
	r.bbcode_enabled = true
	r.fit_content = true
	r.scroll_active = false
	r.custom_minimum_size = min_size
	r.add_theme_font_size_override(&"normal_font_size", 13)
	r.add_theme_font_size_override(&"bold_font_size", 14)
	return r


static func background(parent: Control) -> void:
	var bg := ColorRect.new()
	bg.color = BG
	bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(bg)


static func hex(c: Color) -> String:
	return c.to_html(false)
