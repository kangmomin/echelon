from __future__ import annotations

import argparse
import tarfile
from pathlib import Path, PurePosixPath


def main() -> int:
    parser = argparse.ArgumentParser(description="Import a Unity .unitypackage into an Assets directory")
    parser.add_argument("package")
    parser.add_argument("destination")
    args = parser.parse_args()
    archive = Path(args.package).resolve()
    destination = Path(args.destination).resolve()
    pathnames: dict[str, str] = {}
    assets: dict[str, dict[str, tarfile.TarInfo]] = {}
    with tarfile.open(archive, "r:gz") as package:
        for member in package.getmembers():
            if not member.isfile():
                continue
            parts = PurePosixPath(member.name).parts
            if len(parts) < 2:
                continue
            guid, kind = parts[-2], parts[-1]
            if kind == "pathname":
                pathnames[guid] = package.extractfile(member).read().decode("utf-8").strip()
            elif kind in ("asset", "asset.meta"):
                assets.setdefault(guid, {})[kind] = member
        imported = 0
        for guid, relative_text in pathnames.items():
            relative = PurePosixPath(relative_text)
            if relative.is_absolute() or ".." in relative.parts:
                raise RuntimeError(f"Unsafe package path: {relative_text}")
            target = destination.joinpath(*relative.parts).resolve()
            if not target.is_relative_to(destination):
                raise RuntimeError(f"Package path escapes destination: {relative_text}")
            files = assets.get(guid, {})
            if "asset" in files:
                target.parent.mkdir(parents=True, exist_ok=True)
                with package.extractfile(files["asset"]) as source:
                    target.write_bytes(source.read())
            else:
                target.mkdir(parents=True, exist_ok=True)
            if "asset.meta" in files:
                meta = target.with_name(target.name + ".meta")
                meta.parent.mkdir(parents=True, exist_ok=True)
                with package.extractfile(files["asset.meta"]) as source:
                    meta.write_bytes(source.read())
            imported += 1
    print(f"[unity-package] OK assets={imported} destination={destination}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
