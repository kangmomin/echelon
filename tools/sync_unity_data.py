"""data/ 의 JSON 정의를 Unity Resources 로 복사하고 매니페스트를 갱신한다.

사용: tools/sync_unity_data.py          동기화
      tools/sync_unity_data.py --check  복사본이 원본과 같은지만 검사(다르면 종료 코드 1)

Unity 가 붙인 .meta 파일(GUID)은 지우지 않는다. 원본에서 사라진 JSON 만 .meta 와 함께 지운다.
"""
from __future__ import annotations

import hashlib
import json
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "data"
DESTINATION = ROOT / "unity" / "Assets" / "Resources" / "GameData"
MANIFEST = ROOT / "unity" / "Assets" / "Resources" / "GameDataManifest.json"


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def source_files() -> list[Path]:
    files = sorted(path for path in SOURCE.rglob("*.json") if path.is_file())
    if not files:
        raise RuntimeError(f"No catalog JSON found under {SOURCE}")
    return files


def manifest_text(files: list[Path]) -> str:
    entries = [{"path": f.relative_to(SOURCE).as_posix(), "sha256": digest(f)} for f in files]
    return json.dumps({"files": entries}, ensure_ascii=False, indent=2) + "\n"


def differences(files: list[Path]) -> list[str]:
    expected = {f.relative_to(SOURCE).as_posix(): f for f in files}
    copied = {p.relative_to(DESTINATION).as_posix() for p in DESTINATION.rglob("*.json")} if DESTINATION.exists() else set()
    problems = [f"missing or changed: {name}" for name, f in expected.items()
                if name not in copied or digest(f) != digest(DESTINATION / name)]
    problems += [f"not in data/: {name}" for name in sorted(copied - expected.keys())]
    if not MANIFEST.is_file() or json.loads(MANIFEST.read_text(encoding="utf-8")) != json.loads(manifest_text(files)):
        problems.append("GameDataManifest.json is out of date")
    return problems


def sync(files: list[Path]) -> None:
    expected = {f.relative_to(SOURCE).as_posix() for f in files}
    if DESTINATION.exists():
        for stale in DESTINATION.rglob("*.json"):
            if stale.relative_to(DESTINATION).as_posix() not in expected:
                stale.unlink()
                stale.with_name(stale.name + ".meta").unlink(missing_ok=True)
    for source in files:
        target = DESTINATION / source.relative_to(SOURCE)
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists() or digest(source) != digest(target):
            shutil.copyfile(source, target)
    MANIFEST.write_text(manifest_text(files), encoding="utf-8", newline="\n")


def main() -> int:
    files = source_files()
    if "--check" not in sys.argv[1:]:
        sync(files)
    problems = differences(files)
    for problem in problems:
        print(f"[unity-data] {problem}", file=sys.stderr)
    if problems:
        print("[unity-data] run tools/sync_unity_data.sh", file=sys.stderr)
        return 1
    print(f"[unity-data] OK files={len(files)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
