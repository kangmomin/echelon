class_name MainMenu
extends Control
## 첫 화면: 스커미시(편성 → 배치 → 전투), 샌드박스, 종료.

signal skirmish_requested
signal sandbox_requested
signal quit_requested


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	UiKit.background(self)
	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(center)
	var p: PanelContainer = UiKit.panel(28)
	center.add_child(p)
	var v := VBoxContainer.new()
	v.add_theme_constant_override(&"separation", 12)
	p.add_child(v)
	v.add_child(UiKit.label("ECHELON", 40, UiKit.ACCENT))
	v.add_child(UiKit.label("제대 지휘 현대전 RTT — 프로토타입", 15, UiKit.TEXT_DIM))
	v.add_child(HSeparator.new())
	v.add_child(UiKit.button("스커미시  (편성 → 배치 → 전투)", func() -> void: skirmish_requested.emit(), 340))
	v.add_child(UiKit.button("샌드박스  (편성 없이 바로 전투)", func() -> void: sandbox_requested.emit(), 340))
	v.add_child(UiKit.button("종료", func() -> void: quit_requested.emit(), 340))
