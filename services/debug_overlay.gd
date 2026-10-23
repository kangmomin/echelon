class_name DebugOverlay
extends CanvasLayer
## F3 디버그 오버레이. PLAN 18.4.
## M0: 틱·시간·배속·틱 처리 시간·데이터 로드 결과. 이후 마일스톤에서 A* 큐, 통신 그래프 등을 추가한다.

const FONT_SIZE: int = 14
const BG_COLOR: Color = Color(0.0, 0.0, 0.0, 0.6)

var _panel: PanelContainer
var _label: Label


func _init() -> void:
	layer = 100


func _ready() -> void:
	_panel = PanelContainer.new()
	_panel.position = Vector2(8.0, 8.0)
	var style := StyleBoxFlat.new()
	style.bg_color = BG_COLOR
	style.set_content_margin_all(8.0)
	_panel.add_theme_stylebox_override(&"panel", style)
	add_child(_panel)

	_label = Label.new()
	_label.add_theme_font_size_override(&"font_size", FONT_SIZE)
	_panel.add_child(_label)


func toggle() -> void:
	visible = not visible


## 줄 단위 텍스트를 표시한다. 보이지 않을 때는 아무것도 하지 않는다.
func set_lines(lines: PackedStringArray) -> void:
	if not visible or _label == null:
		return
	_label.text = "\n".join(lines)


func get_text() -> String:
	return _label.text if _label != null else ""
