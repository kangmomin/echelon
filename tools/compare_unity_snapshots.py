#!/usr/bin/env python3
import argparse
import hashlib
import json
import math
from pathlib import Path
import struct
import sys


MISSING = object()
UNORDERED_KEY_MAPS = False
FLOAT_TOLERANCE = 0.0
RELAXED_COMPARISON = False


def canonical_bytes(value):
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
        allow_nan=False,
    ).encode("utf-8")


def sha256(raw):
    return hashlib.sha256(raw).hexdigest()


def pointer(parent, token):
    escaped = str(token).replace("~", "~0").replace("/", "~1")
    return f"{parent}/{escaped}"


def ignored(path, prefixes):
    return any(path == prefix or path.startswith(prefix + "/") for prefix in prefixes)


def difference(path, expected, actual, message=None):
    result = {
        "pointer": path,
        "expected_present": expected is not MISSING,
        "actual_present": actual is not MISSING,
    }
    if expected is not MISSING:
        result["expected"] = expected
    if actual is not MISSING:
        result["actual"] = actual
    if message:
        result["message"] = message
    return result


def first_difference(expected, actual, path="", prefixes=()):
    if ignored(path, prefixes):
        return None
    if expected is MISSING or actual is MISSING:
        return difference(path, expected, actual)
    if isinstance(expected, dict) and isinstance(actual, dict):
        if FLOAT_TOLERANCE > 0.0 and set(expected) == {"$f64"} and set(actual) == {"$f64"}:
            left = struct.unpack(">d", bytes.fromhex(expected["$f64"]))[0]
            right = struct.unpack(">d", bytes.fromhex(actual["$f64"]))[0]
            if math.isnan(left) and math.isnan(right):
                return None
            if math.isclose(left, right, rel_tol=FLOAT_TOLERANCE, abs_tol=FLOAT_TOLERANCE):
                return None
            return difference(path, expected, actual)
        for key in sorted(set(expected) | set(actual)):
            child = pointer(path, key)
            found = first_difference(
                expected.get(key, MISSING), actual.get(key, MISSING), child, prefixes
            )
            if found is not None:
                return found
        return None
    if isinstance(expected, list) and isinstance(actual, list):
        if UNORDERED_KEY_MAPS and path.rsplit("/", 1)[-1] in ("ammo", "reload", "losses"):
            if all(isinstance(item, dict) and set(item) == {"key", "value"} for item in expected + actual):
                expected = sorted(expected, key=lambda item: item["key"])
                actual = sorted(actual, key=lambda item: item["key"])
        for index in range(max(len(expected), len(actual))):
            left = expected[index] if index < len(expected) else MISSING
            right = actual[index] if index < len(actual) else MISSING
            found = first_difference(left, right, pointer(path, index), prefixes)
            if found is not None:
                return found
        return None
    if type(expected) is not type(actual) or expected != actual:
        return difference(path, expected, actual)
    return None


def load_json(path):
    raw = path.read_bytes()
    value = json.loads(raw.decode("utf-8"))
    errors = []
    try:
        if canonical_bytes(value) != raw:
            errors.append(f"non-canonical bytes: {path}")
    except ValueError as exc:
        errors.append(f"non-canonical number in {path}: {exc}")
    return value, raw, errors


def display(value, limit=480):
    if value is MISSING:
        return "<missing>"
    text = json.dumps(value, ensure_ascii=False, separators=(",", ":"), allow_nan=False)
    return text if len(text) <= limit else text[: limit - 3] + "..."


def artifact_difference(tick, golden_entry, actual_entry, golden_file, actual_file, prefixes):
    golden, golden_raw, errors = load_json(golden_file)
    actual, actual_raw, actual_errors = load_json(actual_file)
    found = first_difference(golden, actual, "", prefixes)
    if found is None and golden_raw != actual_raw and not RELAXED_COMPARISON:
        found = difference(
            "", sha256(golden_raw), sha256(actual_raw),
            "JSON values match but exact canonical bytes differ",
        )
    integrity = list(errors) + list(actual_errors)
    if len(golden_raw) != golden_entry["bytes"]:
        integrity.append(f"golden byte count mismatch at tick {tick}: {golden_file}")
    if len(actual_raw) != actual_entry["bytes"]:
        integrity.append(f"actual byte count mismatch at tick {tick}: {actual_file}")
    if sha256(golden_raw) != golden_entry["sha256"]:
        integrity.append(f"golden snapshot SHA-256 mismatch at tick {tick}: {golden_file}")
    if sha256(actual_raw) != actual_entry["sha256"]:
        integrity.append(f"actual snapshot SHA-256 mismatch at tick {tick}: {actual_file}")
    if found is not None:
        found["tick"] = tick
        found["artifact"] = "snapshot"
        found["golden_file"] = str(golden_file).replace("\\", "/")
        found["actual_file"] = str(actual_file).replace("\\", "/")
    return found, integrity


def compare_seed(golden_root, actual_root, seed, prefixes):
    seed_result = {
        "seed": seed,
        "first_divergence": None,
        "matching_checkpoint_ticks": [],
        "missing_actual_ticks": [],
        "missing_golden_ticks": [],
        "detailed_snapshots_compared": 0,
        "unverified_snapshot_hash_ticks": [],
        "integrity_errors": [],
    }
    golden_dir = golden_root / f"seed_{seed}"
    actual_dir = actual_root / f"seed_{seed}"
    try:
        golden_index, _, index_errors = load_json(golden_dir / "checkpoints.json")
        actual_index, _, actual_index_errors = load_json(actual_dir / "checkpoints.json")
        seed_result["integrity_errors"].extend(index_errors + actual_index_errors)
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        seed_result["error"] = str(exc)
        return seed_result

    golden_entries = {entry["tick"]: (index, entry) for index, entry in enumerate(golden_index["checkpoints"])}
    actual_entries = {entry["tick"]: (index, entry) for index, entry in enumerate(actual_index["checkpoints"])}
    seed_result["missing_actual_ticks"] = sorted(set(golden_entries) - set(actual_entries))
    seed_result["missing_golden_ticks"] = sorted(set(actual_entries) - set(golden_entries))

    for tick in sorted(set(golden_entries) | set(actual_entries)):
        if tick not in actual_entries:
            golden_index_in_array, golden_entry = golden_entries[tick]
            found = difference(
                pointer(f"/checkpoints/{golden_index_in_array}", "tick"), tick, MISSING,
                "checkpoint is missing from actual output",
            )
            found.update({"tick": tick, "artifact": "checkpoints"})
            if seed_result["first_divergence"] is None:
                seed_result["first_divergence"] = found
            continue
        if tick not in golden_entries:
            continue

        golden_index_in_array, golden_entry = golden_entries[tick]
        actual_index_in_array, actual_entry = actual_entries[tick]
        found = None
        golden_detail = golden_entry.get("detail_file", "")
        actual_detail = actual_entry.get("detail_file", "")
        if golden_detail and actual_detail:
            seed_result["detailed_snapshots_compared"] += 1
            found, integrity = artifact_difference(
                tick,
                golden_entry,
                actual_entry,
                golden_dir / golden_detail,
                actual_dir / actual_detail,
                prefixes,
            )
            seed_result["integrity_errors"].extend(integrity)
        elif golden_detail or actual_detail:
            found = difference(
                f"/checkpoints/{golden_index_in_array}/detail_file",
                golden_detail,
                actual_detail,
                "detailed snapshot is available on only one side",
            )
            found.update({"tick": tick, "artifact": "checkpoints"})
        else:
            found = first_difference(
                golden_entry.get("counts"),
                actual_entry.get("counts"),
                f"/checkpoints/{golden_index_in_array}/counts",
                prefixes,
            )
            if found is not None:
                found.update({"tick": tick, "artifact": "checkpoints"})
            native_hashes_ignored = ignored("/engine_hashes", prefixes)
            if native_hashes_ignored:
                seed_result["unverified_snapshot_hash_ticks"].append(tick)
            if found is None and not native_hashes_ignored:
                for field in ("engine_state_hash", "sha256"):
                    found = first_difference(
                        golden_entry.get(field),
                        actual_entry.get(field),
                        f"/checkpoints/{golden_index_in_array}/{field}",
                        prefixes,
                    )
                    if found is not None:
                        found.update({"tick": tick, "artifact": "checkpoints"})
                        break
            if found is None and not native_hashes_ignored and golden_entry.get("bytes") != actual_entry.get("bytes"):
                found = difference(
                    f"/checkpoints/{golden_index_in_array}/bytes",
                    golden_entry.get("bytes"),
                    actual_entry.get("bytes"),
                )
                found.update({"tick": tick, "artifact": "checkpoints"})

        if found is None:
            found = first_difference(
                golden_entry.get("counts"),
                actual_entry.get("counts"),
                f"/checkpoints/{golden_index_in_array}/counts",
                prefixes,
            )
            if found is not None:
                found.update({"tick": tick, "artifact": "checkpoints"})
        if found is None:
            found = first_difference(
                golden_entry.get("reasons"),
                actual_entry.get("reasons"),
                f"/checkpoints/{golden_index_in_array}/reasons",
                prefixes,
            )
            if found is not None:
                found.update({"tick": tick, "artifact": "checkpoints"})
        if found is None:
            found = first_difference(
                golden_entry.get("arrival_event_ticks"),
                actual_entry.get("arrival_event_ticks"),
                f"/checkpoints/{golden_index_in_array}/arrival_event_ticks",
                prefixes,
            )
            if found is not None:
                found.update({"tick": tick, "artifact": "checkpoints"})
        if found is None:
            seed_result["matching_checkpoint_ticks"].append(tick)
        elif seed_result["first_divergence"] is None:
            seed_result["first_divergence"] = found

    try:
        golden_aar, golden_aar_raw, errors = load_json(golden_dir / "aar.json")
        actual_aar, actual_aar_raw, actual_errors = load_json(actual_dir / "aar.json")
        seed_result["integrity_errors"].extend(errors + actual_errors)
        aar_difference = first_difference(golden_aar, actual_aar, "", prefixes)
        if aar_difference is None and golden_aar_raw != actual_aar_raw and FLOAT_TOLERANCE == 0.0:
            aar_difference = difference(
                "", sha256(golden_aar_raw), sha256(actual_aar_raw),
                "AAR JSON values match but exact canonical bytes differ",
            )
        if aar_difference is not None:
            aar_difference.update({
                "tick": actual_index.get("final_tick", golden_index.get("final_tick")),
                "artifact": "aar",
                "golden_file": str(golden_dir / "aar.json").replace("\\", "/"),
                "actual_file": str(actual_dir / "aar.json").replace("\\", "/"),
            })
            seed_result["aar_first_difference"] = aar_difference
            if seed_result["first_divergence"] is None:
                seed_result["first_divergence"] = aar_difference
        final_aar = actual_index.get("final_aar", {})
        if final_aar.get("canonical_sha256") != sha256(actual_aar_raw):
            seed_result["integrity_errors"].append(f"actual final AAR SHA-256 mismatch: {actual_dir / 'aar.json'}")
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        seed_result["integrity_errors"].append(f"AAR comparison failed for seed {seed}: {exc}")

    for field in ("final_tick", "result_tick", "truncated"):
        found = first_difference(
            golden_index.get(field), actual_index.get(field), f"/{field}", prefixes
        )
        if found is not None:
            found.update({
                "tick": actual_index.get("final_tick", golden_index.get("final_tick")),
                "artifact": "checkpoints",
            })
            seed_result.setdefault("terminal_first_difference", found)
            if seed_result["first_divergence"] is None:
                seed_result["first_divergence"] = found
            break
    return seed_result


def parse_seeds(text):
    return [int(part) for part in text.split(",") if part]


def main():
    global UNORDERED_KEY_MAPS, FLOAT_TOLERANCE, RELAXED_COMPARISON
    parser = argparse.ArgumentParser(
        description="Compare Echelon Godot canonical battle snapshots with C# output"
    )
    parser.add_argument("--golden", default="tests/golden/battle")
    parser.add_argument("--actual", default="build/snapshot_unity/csharp")
    parser.add_argument("--seeds", default="1,2,3,4,5,6,7,8")
    parser.add_argument(
        "--ignore-prefix",
        action="append",
        default=[],
        help="ignore a JSON pointer subtree, for example /engine_hashes or /rng/state",
    )
    parser.add_argument(
        "--ignore-key-map-order",
        action="store_true",
        help="compare ammo/reload/losses key-value arrays without their order (diagnostic only)",
    )
    parser.add_argument(
        "--float-tolerance",
        type=float,
        default=0.0,
        help="diagnostic relative/absolute f64 tolerance; zero requires exact bits",
    )
    parser.add_argument("--json", default="", help="write the full machine-readable report")
    args = parser.parse_args()
    UNORDERED_KEY_MAPS = args.ignore_key_map_order
    FLOAT_TOLERANCE = args.float_tolerance
    if FLOAT_TOLERANCE < 0.0:
        parser.error("--float-tolerance must be non-negative")
    golden_root = Path(args.golden)
    actual_root = Path(args.actual)
    prefixes = tuple("/" + prefix.strip("/") for prefix in args.ignore_prefix if prefix)
    RELAXED_COMPARISON = UNORDERED_KEY_MAPS or FLOAT_TOLERANCE > 0.0 or bool(prefixes)
    seeds = parse_seeds(args.seeds)
    reports = [compare_seed(golden_root, actual_root, seed, prefixes) for seed in seeds]
    result = {
        "schema": "echelon.unity_snapshot_comparison.v1",
        "golden": str(golden_root).replace("\\", "/"),
        "actual": str(actual_root).replace("\\", "/"),
        "ignored_prefixes": list(prefixes),
        "unordered_key_maps": UNORDERED_KEY_MAPS,
        "float_tolerance": FLOAT_TOLERANCE,
        "ok": all(
            "error" not in report
            and report["first_divergence"] is None
            and not report["integrity_errors"]
            for report in reports
        ),
        "seeds": reports,
    }
    for report in reports:
        seed = report["seed"]
        if "error" in report:
            print(f"seed {seed}: comparison error: {report['error']}", file=sys.stderr)
            continue
        found = report["first_divergence"]
        if found is None:
            print(
                f"seed {seed}: no structural divergence at compared checkpoints/AAR; "
                f"matching_ticks={len(report['matching_checkpoint_ticks'])} "
                f"detailed={report['detailed_snapshots_compared']} "
                f"metadata_only={len(report['unverified_snapshot_hash_ticks'])}"
            )
        else:
            print(f"seed {seed}: first divergence tick {found.get('tick')} {found['artifact']}")
            print(f"  pointer: {found['pointer'] or '/'}")
            print(f"  expected: {display(found.get('expected', MISSING))}")
            print(f"  actual:   {display(found.get('actual', MISSING))}")
            if "message" in found:
                print(f"  {found['message']}")
        for error in report["integrity_errors"]:
            print(f"seed {seed}: integrity error: {error}", file=sys.stderr)
    if args.json:
        report_path = Path(args.json)
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_text(
            json.dumps(result, ensure_ascii=False, indent=2, allow_nan=False) + "\n",
            encoding="utf-8",
        )
    if any("error" in report for report in reports):
        return 2
    return 0 if result["ok"] else 1


if __name__ == "__main__":
    sys.exit(main())
