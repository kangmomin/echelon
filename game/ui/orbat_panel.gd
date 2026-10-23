class_name OrbatPanel
extends PanelContainer
## 좌측 제대 트리(ORBAT). 대대 → 중대 → 소대, 전투력·통신·현재 임무. PLAN 12.1.
## 클릭 = 선택, 더블클릭 = 카메라 이동.

signal unit_selected(unit: Unit, additive: bool)
signal unit_focused(unit: Unit)

const REFRESH_SEC: float = 0.5
const COMMS_ICON: Array[String] = ["●", "◐", "○"]
const COMMS_COLOR: Array[Color] = [Color(0.45, 1.0, 0.45), Color(1.0, 0.8, 0.25), Color(1.0, 0.35, 0.35)]

var battle: Battle
var side: StringName = &"blue"
var _tree: Tree
var _items: Dictionary[int, TreeItem] = {}
var _timer: float = 0.0
var _suppress_signal: bool = false


func _ready() -> void:
	var style := StyleBoxFlat.new()
	style.bg_color = Hud.PANEL_BG
	style.set_corner_radius_all(4)
	style.set_content_margin_all(6)
	add_theme_stylebox_override(&"panel", style)
	set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
	offset_left = 8
	offset_top = 50
	offset_right = 330
	offset_bottom = 470
	_tree = Tree.new()
	_tree.columns = 3
	_tree.hide_root = false
	_tree.select_mode = Tree.SELECT_MULTI
	_tree.set_column_expand(0, true)
	_tree.set_column_expand(1, false)
	_tree.set_column_custom_minimum_width(1, 46)
	_tree.set_column_expand(2, false)
	_tree.set_column_custom_minimum_width(2, 22)
	_tree.add_theme_font_size_override(&"font_size", 13)
	_tree.focus_mode = Control.FOCUS_NONE
	_tree.multi_selected.connect(_on_multi_selected)
	_tree.item_activated.connect(_on_activated)
	add_child(_tree)
	_rebuild()


func _process(delta: float) -> void:
	_timer += delta
	if _timer >= REFRESH_SEC:
		_timer = 0.0
		_refresh()


## 바깥(지도)에서 선택이 바뀌면 트리 표시도 맞춘다.
func show_selection(units: Array[Unit]) -> void:
	_suppress_signal = true
	_tree.deselect_all()
	for u: Unit in units:
		if _items.has(u.id):
			_items[u.id].select(0)
	_suppress_signal = false


func _rebuild() -> void:
	_tree.clear()
	_items.clear()
	var tf: TaskForce = battle.task_forces.get(side)
	if tf == null:
		return
	var root: TreeItem = _tree.create_item()
	_bind(root, tf)
	if tf.cp_section != null:
		_bind(_tree.create_item(root), tf.cp_section)
	for c: Company in tf.companies:
		var ci: TreeItem = _tree.create_item(root)
		_bind(ci, c)
		if c.hq != null:
			_bind(_tree.create_item(ci), c.hq)
		for p: Platoon in c.platoons:
			_bind(_tree.create_item(ci), p)
	for p: Platoon in tf.organic:
		_bind(_tree.create_item(root), p)
	_refresh()


func _bind(item: TreeItem, u: Unit) -> void:
	item.set_metadata(0, u.id)
	_items[u.id] = item


func _refresh() -> void:
	if battle == null:
		return
	var tf: TaskForce = battle.task_forces.get(side)
	if tf == null:
		return
	if _items.size() != tf.all_units().size():
		_rebuild()
		return
	for uid: int in _items.keys():
		var u: Unit = battle.get_unit(uid)
		var item: TreeItem = _items[uid]
		if u == null:
			continue
		var unit_name: String = u.display_name if u.level != Unit.Level.PLATOON else u.label()
		var status: String = _status(u)
		item.set_text(0, "%s%s" % [unit_name, ("  · " + status) if status != "" else ""])
		item.set_text(1, "%d%%" % roundi(u.strength() * 100.0) if u.level != Unit.Level.TASK_FORCE else "")
		item.set_text(2, COMMS_ICON[u.comms_state] if u.level != Unit.Level.TASK_FORCE else "")
		item.set_custom_color(2, COMMS_COLOR[u.comms_state])
		var dead: bool = u.is_destroyed() and u.level != Unit.Level.TASK_FORCE
		var dim: Color = Color(0.55, 0.55, 0.55) if dead or not u.on_map else Color(0.95, 0.95, 0.95)
		item.set_custom_color(0, dim)
		var s: float = u.strength()
		item.set_custom_color(1, Color(1.0 - s, 0.35 + 0.6 * s, 0.3))


func _status(u: Unit) -> String:
	var tick: int = battle.world.tick
	if not u.on_map:
		return "증원 대기"
	if u.is_destroyed() and u.level != Unit.Level.TASK_FORCE:
		return "전멸"
	if u.is_leaderless(tick):
		return "지휘 공백"
	if u.is_routed(tick):
		return "패주"
	if u.awaiting_follow:
		return "명령 대기"
	if u.detached:
		return "분리"
	if battle.order_system != null and not battle.order_system.pending_for_unit(u.id).is_empty():
		return "명령 전파 중"
	if u.mission != null and not u.mission.is_finished():
		return Hud._order_name(u.mission.type)
	return Hud._posture_name(u.posture) if u.posture != &"idle" else ""


func _on_multi_selected(item: TreeItem, _column: int, selected: bool) -> void:
	if _suppress_signal or not selected:
		return
	var uid: int = item.get_metadata(0)
	var u: Unit = battle.get_unit(uid)
	if u != null and u.level != Unit.Level.TASK_FORCE:
		unit_selected.emit(u, Input.is_key_pressed(KEY_CTRL))


func _on_activated() -> void:
	var item: TreeItem = _tree.get_selected()
	if item == null:
		return
	var uid: int = item.get_metadata(0)
	var u: Unit = battle.get_unit(uid)
	if u != null:
		unit_focused.emit(u)
