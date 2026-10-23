class_name DefReader
extends RefCounted
## JSON 딕셔너리에서 필드를 꺼내며 타입을 검사하고, 오류·경고를 모은다.
## 실패해도 기본값을 돌려주고 계속 진행하므로, 파일 하나를 읽고 나면 오류 전체를 한 번에 볼 수 있다.

var errors: PackedStringArray = []
var warnings: PackedStringArray = []
## 오류 메시지 앞에 붙는 위치 정보(예: 파일 경로, 정의 id).
var context: String = ""


func error(msg: String) -> void:
	errors.append("%s: %s" % [context, msg] if context != "" else msg)


func warn(msg: String) -> void:
	warnings.append("%s: %s" % [context, msg] if context != "" else msg)


func has_errors() -> bool:
	return not errors.is_empty()


static func is_number(v: Variant) -> bool:
	return typeof(v) == TYPE_FLOAT or typeof(v) == TYPE_INT


# --- 필수 필드 -----------------------------------------------------------

func req_string(d: Dictionary, key: String) -> String:
	if not _require(d, key):
		return ""
	return _as_string(d[key], key)


func req_number(d: Dictionary, key: String) -> float:
	if not _require(d, key):
		return 0.0
	return _as_number(d[key], key)


func req_int(d: Dictionary, key: String) -> int:
	if not _require(d, key):
		return 0
	return _as_int(d[key], key)


func req_bool(d: Dictionary, key: String) -> bool:
	if not _require(d, key):
		return false
	return _as_bool(d[key], key)


func req_dict(d: Dictionary, key: String) -> Dictionary:
	if not _require(d, key):
		return {}
	return _as_dict(d[key], key)


func req_array(d: Dictionary, key: String) -> Array:
	if not _require(d, key):
		return []
	return _as_array(d[key], key)


func req_string_array(d: Dictionary, key: String) -> PackedStringArray:
	if not _require(d, key):
		return PackedStringArray()
	return _as_string_array(d[key], key)


## [x, y] 형식의 2차원 좌표.
func req_vec2(d: Dictionary, key: String) -> Vector2:
	if not _require(d, key):
		return Vector2.ZERO
	return as_vec2(d[key], key)


# --- 선택 필드 -----------------------------------------------------------

func opt_string(d: Dictionary, key: String, default: String = "") -> String:
	return _as_string(d[key], key) if d.has(key) else default


func opt_number(d: Dictionary, key: String, default: float = 0.0) -> float:
	return _as_number(d[key], key) if d.has(key) else default


func opt_int(d: Dictionary, key: String, default: int = 0) -> int:
	return _as_int(d[key], key) if d.has(key) else default


func opt_bool(d: Dictionary, key: String, default: bool = false) -> bool:
	return _as_bool(d[key], key) if d.has(key) else default


func opt_dict(d: Dictionary, key: String) -> Dictionary:
	return _as_dict(d[key], key) if d.has(key) else {}


func opt_array(d: Dictionary, key: String) -> Array:
	return _as_array(d[key], key) if d.has(key) else []


func opt_string_array(d: Dictionary, key: String) -> PackedStringArray:
	return _as_string_array(d[key], key) if d.has(key) else PackedStringArray()


# --- 범위 검사 -----------------------------------------------------------

func check_range(value: float, lo: float, hi: float, what: String) -> void:
	if value < lo or value > hi:
		error("'%s' = %s, 허용 범위 %s ~ %s" % [what, value, lo, hi])


func check_one_of(value: String, allowed: Array, what: String) -> void:
	if not allowed.has(value):
		error("'%s' = '%s', 허용값 %s" % [what, value, allowed])


# --- 값 변환 (중첩 구조용으로 공개) --------------------------------------

func as_vec2(v: Variant, what: String) -> Vector2:
	if typeof(v) != TYPE_ARRAY:
		error("'%s'는 [x, y] 배열이어야 함" % what)
		return Vector2.ZERO
	var arr: Array = v
	if arr.size() != 2 or not is_number(arr[0]) or not is_number(arr[1]):
		error("'%s'는 숫자 2개짜리 [x, y] 배열이어야 함" % what)
		return Vector2.ZERO
	return Vector2(_to_float(arr[0]), _to_float(arr[1]))


func as_dict(v: Variant, what: String) -> Dictionary:
	return _as_dict(v, what)


func as_number(v: Variant, what: String) -> float:
	return _as_number(v, what)


func as_int(v: Variant, what: String) -> int:
	return _as_int(v, what)


# --- 내부 ----------------------------------------------------------------

func _require(d: Dictionary, key: String) -> bool:
	if d.has(key):
		return true
	error("필수 필드 누락 '%s'" % key)
	return false


func _as_string(v: Variant, key: String) -> String:
	if typeof(v) != TYPE_STRING:
		error("'%s'는 문자열이어야 함" % key)
		return ""
	return v


func _as_number(v: Variant, key: String) -> float:
	if not is_number(v):
		error("'%s'는 숫자여야 함" % key)
		return 0.0
	return _to_float(v)


func _as_int(v: Variant, key: String) -> int:
	if not is_number(v):
		error("'%s'는 정수여야 함" % key)
		return 0
	var f: float = _to_float(v)
	if f != floorf(f):
		error("'%s'는 정수여야 함 (값 %s)" % [key, f])
	return int(f)


func _as_bool(v: Variant, key: String) -> bool:
	if typeof(v) != TYPE_BOOL:
		error("'%s'는 true/false 여야 함" % key)
		return false
	return v


func _as_dict(v: Variant, key: String) -> Dictionary:
	if typeof(v) != TYPE_DICTIONARY:
		error("'%s'는 객체({})여야 함" % key)
		return {}
	return v


func _as_array(v: Variant, key: String) -> Array:
	if typeof(v) != TYPE_ARRAY:
		error("'%s'는 배열이어야 함" % key)
		return []
	return v


func _as_string_array(v: Variant, key: String) -> PackedStringArray:
	var out := PackedStringArray()
	for item: Variant in _as_array(v, key):
		if typeof(item) != TYPE_STRING:
			error("'%s'의 원소는 문자열이어야 함" % key)
			continue
		var text: String = item
		out.append(text)
	return out


## 숫자 Variant(int 또는 float)를 float 로. 호출 전에 is_number() 로 확인한다.
static func _to_float(v: Variant) -> float:
	if typeof(v) == TYPE_INT:
		var i: int = v
		return float(i)
	var f: float = v
	return f
