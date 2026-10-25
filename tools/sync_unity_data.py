from __future__ import annotations

import hashlib
import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "data"
DESTINATION = ROOT / "unity" / "Assets" / "Resources" / "GameData"
MANIFEST = ROOT / "unity" / "Assets" / "Resources" / "GameDataManifest.json"


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> None:
    files = sorted(path for path in SOURCE.rglob("*.json") if path.is_file())
    if not files:
        raise RuntimeError(f"No catalog JSON found under {SOURCE}")
    if DESTINATION.exists():
        shutil.rmtree(DESTINATION)
    DESTINATION.mkdir(parents=True)
    manifest: list[dict[str, str]] = []
    for source in files:
        relative = source.relative_to(SOURCE)
        target = DESTINATION / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        if digest(source) != digest(target):
            raise RuntimeError(f"Catalog copy mismatch: {relative.as_posix()}")
        manifest.append({"path": relative.as_posix(), "sha256": digest(source)})
    copied = sorted(DESTINATION.rglob("*.json"))
    if len(files) != len(copied):
        raise RuntimeError("Catalog file count mismatch after synchronization")
    MANIFEST.write_text(json.dumps({"files": manifest}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"[unity-data] OK files={len(files)} destination={DESTINATION}")


if __name__ == "__main__":
    main()
