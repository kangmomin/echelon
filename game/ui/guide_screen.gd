class_name GuideScreen
extends Control
## 게임 안 가이드: 왼쪽 목차, 오른쪽 본문(GuideContent). 메뉴와 전투 중(/ 키) 모두에서 연다.

signal closed

var _body: RichTextLabel
var _buttons: Array[Button] = []
var _sections: Array[Array] = []


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP
	var dim := ColorRect.new()
	dim.color = Color(0, 0, 0, 0.55)
	dim.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(dim)
	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(center)
	var p: PanelContainer = UiKit.panel(18)
	p.custom_minimum_size = Vector2(minf(980.0, get_viewport_rect().size.x - 40.0), minf(640.0, get_viewport_rect().size.y - 40.0))
	center.add_child(p)
	var v := VBoxContainer.new()
	v.add_theme_constant_override(&"separation", 10)
	p.add_child(v)
	var head := HBoxContainer.new()
	v.add_child(head)
	var title: Label = UiKit.label("가이드", 24, UiKit.ACCENT)
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	head.add_child(title)
	head.add_child(UiKit.button("닫기  (Esc)", close, 120))
	v.add_child(HSeparator.new())
	var h := HBoxContainer.new()
	h.size_flags_vertical = Control.SIZE_EXPAND_FILL
	h.add_theme_constant_override(&"separation", 16)
	v.add_child(h)
	var toc := VBoxContainer.new()
	toc.custom_minimum_size.x = 180
	toc.add_theme_constant_override(&"separation", 4)
	h.add_child(toc)
	_sections = GuideContent.sections()
	for i: int in _sections.size():
		var b: Button = UiKit.button(str(_sections[i][0]), func() -> void: show_section(i), 180)
		b.toggle_mode = true
		b.alignment = HORIZONTAL_ALIGNMENT_LEFT
		toc.add_child(b)
		_buttons.append(b)
	_body = RichTextLabel.new()
	_body.bbcode_enabled = true
	_body.scroll_active = true
	_body.selection_enabled = true
	_body.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_body.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_body.add_theme_font_size_override(&"normal_font_size", 15)
	_body.add_theme_font_size_override(&"bold_font_size", 15)
	_body.add_theme_constant_override(&"line_separation", 4)
	_body.add_theme_constant_override(&"table_h_separation", 10)
	_body.add_theme_constant_override(&"table_v_separation", 4)
	h.add_child(_body)
	show_section(0)


func show_section(index: int) -> void:
	index = clampi(index, 0, _sections.size() - 1)
	_body.text = str(_sections[index][1])
	_body.scroll_to_line(0)
	for i: int in _buttons.size():
		_buttons[i].button_pressed = i == index


func close() -> void:
	closed.emit()
	queue_free()


func _input(event: InputEvent) -> void:
	if event is InputEventKey and event.is_pressed() and not event.is_echo():
		var k: InputEventKey = event
		if k.physical_keycode == KEY_ESCAPE or k.physical_keycode == KEY_SLASH:
			close()
		get_viewport().set_input_as_handled()
