class_name ForceStore
extends RefCounted
## 편성(ForceSpec) 저장·불러오기. user://forces/*.json. PLAN 6장 "저장 가능한 전투단 파일".

const DIR: String = "user://forces"


static func path_for(file_name: String) -> String:
	var safe: String = file_name.strip_edges().validate_filename()
	if safe == "":
		safe = "tf"
	if not safe.ends_with(".json"):
		safe += ".json"
	return DIR.path_join(safe)


static func save(spec: ForceSpec, path: String) -> Error:
	var dir: String = path.get_base_dir()
	if not DirAccess.dir_exists_absolute(dir):
		var mk: Error = DirAccess.make_dir_recursive_absolute(dir)
		if mk != OK:
			return mk
	var f: FileAccess = FileAccess.open(path, FileAccess.WRITE)
	if f == null:
		return FileAccess.get_open_error()
	f.store_string(spec.to_json())
	f.close()
	return OK


static func load_spec(path: String) -> ForceSpec:
	if not FileAccess.file_exists(path):
		return null
	return ForceSpec.from_json(FileAccess.get_file_as_string(path))


## 저장된 편성 파일 경로(이름순).
static func list_saved() -> PackedStringArray:
	var out := PackedStringArray()
	if not DirAccess.dir_exists_absolute(DIR):
		return out
	var names: PackedStringArray = DirAccess.get_files_at(DIR)
	names.sort()
	for n: String in names:
		if n.ends_with(".json"):
			out.append(DIR.path_join(n))
	return out
