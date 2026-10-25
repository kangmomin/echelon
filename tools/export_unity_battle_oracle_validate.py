#!/usr/bin/env python3
import argparse
import hashlib
import json
import math
from pathlib import Path
import struct
import sys


def canonical_bytes(value):
    return json.dumps(
        value, ensure_ascii=False, sort_keys=True, separators=(",", ":"), allow_nan=False
    ).encode("utf-8")


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def load(path):
    return json.loads(path.read_bytes().decode("utf-8"))


def without_real_sec_line(raw):
    return b"\n".join(
        line for line in raw.split(b"\n") if b'"real_sec":' not in line
    )


def decode_numbers(value):
    if isinstance(value, list):
        return [decode_numbers(item) for item in value]
    if isinstance(value, dict):
        if set(value) == {"$f64"}:
            return struct.unpack(">d", bytes.fromhex(value["$f64"]))[0]
        if set(value) == {"$i64"}:
            return int(value["$i64"])
        return {key: decode_numbers(item) for key, item in value.items()}
    return value


def equivalent(expected, actual, path="$", errors=None):
    if errors is None:
        errors = []
    if len(errors) >= 50:
        return errors
    if isinstance(expected, dict) and isinstance(actual, dict):
        if set(expected) != set(actual):
            errors.append(f"{path}: object keys differ")
        for key in set(expected) & set(actual):
            equivalent(expected[key], actual[key], f"{path}.{key}", errors)
    elif isinstance(expected, list) and isinstance(actual, list):
        if len(expected) != len(actual):
            errors.append(f"{path}: array lengths {len(expected)} != {len(actual)}")
        for index, (left, right) in enumerate(zip(expected, actual)):
            equivalent(left, right, f"{path}[{index}]", errors)
    elif (
        isinstance(expected, (int, float))
        and not isinstance(expected, bool)
        and isinstance(actual, (int, float))
        and not isinstance(actual, bool)
    ):
        if isinstance(expected, int) and isinstance(actual, int):
            if expected != actual:
                errors.append(f"{path}: {expected} != {actual}")
        elif not math.isclose(expected, actual, rel_tol=1e-12, abs_tol=1e-12):
            errors.append(f"{path}: {expected!r} != {actual!r}")
    elif expected != actual:
        errors.append(f"{path}: {expected!r} != {actual!r}")
    return errors


def validate_number_wrappers(value, path="$"):
    errors = []
    if isinstance(value, float):
        errors.append(f"{path}: unwrapped float in canonical artifact")
    elif isinstance(value, list):
        for index, item in enumerate(value):
            errors.extend(validate_number_wrappers(item, f"{path}[{index}]"))
    elif isinstance(value, dict):
        if set(value) == {"$f64"}:
            token = value["$f64"]
            if not isinstance(token, str) or len(token) != 16:
                errors.append(f"{path}: invalid f64 token")
            else:
                try:
                    bytes.fromhex(token)
                except ValueError:
                    errors.append(f"{path}: non-hex f64 token")
        elif set(value) == {"$i64"}:
            try:
                parsed = int(value["$i64"])
                if not -(2**63) <= parsed < 2**63:
                    errors.append(f"{path}: i64 outside signed 64-bit range")
            except (ValueError, TypeError):
                errors.append(f"{path}: invalid i64 token")
        for key, item in value.items():
            errors.extend(validate_number_wrappers(item, f"{path}.{key}"))
    return errors


def snapshot_counts(snapshot):
    contacts = 0
    known = 0
    for unit in snapshot["units"]:
        if unit["platoon"] is not None:
            contacts += len(unit["platoon"]["contacts"])
        if unit["task_force"] is not None:
            known += len(unit["task_force"]["known_enemies"])
    return {
        "elements": len(snapshot["elements"]),
        "units": len(snapshot["units"]),
        "orders": len(snapshot["orders"]),
        "active_orders": len(snapshot["order_system"]["active_orders"]),
        "pending_paths": len(snapshot["path"]["queue_order"]),
        "contacts": contacts,
        "known_enemies": known,
        "shots_fired": snapshot["combat"]["shots_fired"],
        "hits": snapshot["combat"]["hits"],
        "kills": snapshot["combat"]["kills"],
        "fire_missions": len(snapshot["fires"]["missions"]),
        "shells": len(snapshot["fires"]["shells"]),
        "smokes": len(snapshot["smokes"]),
        "supply_requests": len(snapshot["supply"]["requests"]),
        "supply_tasks": len(snapshot["supply"]["tasks"]),
        "minefields": len(snapshot["engineers"]["minefields"]),
        "field_positions": len(snapshot["engineers"]["positions"]),
        "committed_elements": sum(
            len(side["committed_element_ids"]) for side in snapshot["scoring"]["sides"]
        ),
        "pending_events": snapshot["events"]["pending_count"],
    }


def validate(args):
    root = Path(args.root)
    golden = Path(args.golden)
    baselines = Path(args.baselines)
    project = Path(args.project)
    errors = []
    checked_files = 0
    checked_bytes = 0
    detailed_files = 0
    nan_tokens = 0
    seed_reports = []

    def check(condition, message):
        if not condition:
            errors.append(message)

    def read_artifact(path, canonical=True):
        nonlocal checked_files, checked_bytes, nan_tokens
        check(path.is_file(), f"missing file: {path}")
        if not path.is_file():
            return None, None
        raw = path.read_bytes()
        checked_files += 1
        checked_bytes += len(raw)
        try:
            value = json.loads(raw.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            errors.append(f"invalid JSON {path}: {exc}")
            return None, raw
        if canonical:
            try:
                encoded = canonical_bytes(value)
                check(encoded == raw, f"non-canonical bytes: {path}")
            except ValueError as exc:
                errors.append(f"non-canonical number in {path}: {exc}")
            errors.extend(validate_number_wrappers(value, str(path)))
            nan_tokens += sum(
                1
                for token in raw.split(b'"$f64":"')[1:]
                if token[:16]
                in (b"7ff8000000000000", b"fff8000000000000", b"7ff4000000000000")
            )
        return value, raw

    schema, schema_raw = read_artifact(root / "schema.json")
    run, run_raw = read_artifact(root / "run_manifest.json")
    if schema is None or run is None:
        raise RuntimeError("schema.json and run_manifest.json are required")
    check(schema.get("schema") == "echelon.battle_oracle.schema.v1", "schema id mismatch")
    check(run.get("schema") == "echelon.battle_oracle.run.v1", "run schema id mismatch")
    for name, record in schema.get("records", {}).items():
        fields = record.get("field_order", [])
        types = record.get("types", {})
        check(fields == sorted(fields), f"schema field order is not sorted: {name}")
        check(set(fields) == set(types), f"schema fields/types differ: {name}")
    if args.seeds == 8:
        check(len(run.get("seeds", [])) == 8, "run does not contain all eight seeds")
        check([item["seed"] for item in run.get("seeds", [])] == list(range(1, 9)), "seed order is not 1..8")
    check(run.get("all_baselines_match") is True, "run manifest reports baseline failure")
    engine = run.get("engine", {})
    check(
        (engine.get("major"), engine.get("minor"), engine.get("patch")) == (4, 7, 2),
        "fixtures were not produced by Godot 4.7.2",
    )
    provenance_files = 0
    for group in ("inputs", "simulation_inputs"):
        for item in run.get(group, []):
            source = project / item["file"]
            check(source.is_file(), f"missing provenance input: {source}")
            if source.is_file():
                raw = source.read_bytes()
                provenance_files += 1
                check(len(raw) == item["bytes"], f"provenance size changed: {source}")
                check(sha256(raw) == item["sha256"], f"provenance hash changed: {source}")

    for seed_info in run.get("seeds", []):
        seed = seed_info["seed"]
        seed_dir = root / f"seed_{seed}"
        listed = {}
        for file_info in seed_info["files"]:
            relative = file_info["file"]
            listed[relative] = file_info
            path = seed_dir / relative
            canonical = not relative.endswith(".compat.json")
            value, raw = read_artifact(path, canonical)
            if raw is None:
                continue
            check(len(raw) == file_info["bytes"], f"size mismatch: {path}")
            check(sha256(raw) == file_info["sha256"], f"manifest hash mismatch: {path}")
            if relative != "baseline_diff.json":
                mirror = golden / f"seed_{seed}" / relative
                check(mirror.is_file(), f"missing golden mirror: {mirror}")
                if mirror.is_file():
                    check(mirror.read_bytes() == raw, f"golden mirror differs: {mirror}")
        index, index_raw = read_artifact(seed_dir / "checkpoints.json")
        aar, aar_raw = read_artifact(seed_dir / "aar.json")
        compat, compat_raw = read_artifact(seed_dir / "aar.compat.json", canonical=False)
        if index is None or aar is None or compat is None:
            continue
        check(index.get("seed") == seed, f"seed {seed}: index seed mismatch")
        check(not index.get("truncated"), f"seed {seed}: battle was truncated")
        check(index.get("baseline", {}).get("matches") is True, f"seed {seed}: baseline flag is false")
        baseline_path = baselines / f"aar_{seed}.json"
        check(baseline_path.is_file(), f"missing baseline: {baseline_path}")
        if baseline_path.is_file():
            baseline_raw = baseline_path.read_bytes()
            check(
                without_real_sec_line(baseline_raw) == compat_raw,
                f"seed {seed}: compatibility AAR differs from baseline except real_sec",
            )
            baseline = load(baseline_path)
            baseline.pop("real_sec", None)
            check(compat == baseline, f"seed {seed}: semantic baseline mismatch")
            errors.extend(equivalent(decode_numbers(aar), compat, f"seed {seed} canonical AAR"))
        check("real_sec" not in aar, f"seed {seed}: canonical AAR contains real_sec")
        check("real_sec" not in compat, f"seed {seed}: compatibility AAR contains real_sec")
        check(aar.get("seed") == seed, f"seed {seed}: AAR seed mismatch")
        check(aar.get("result", {}).get("tick") == index["result_tick"], f"seed {seed}: AAR result tick mismatch")
        decoded_aar = decode_numbers(aar)
        check(
            math.isclose(decoded_aar["duration_sec"], index["final_tick"] / 10.0, abs_tol=1e-9),
            f"seed {seed}: AAR duration/final tick mismatch",
        )
        final_aar = index["final_aar"]
        check(final_aar["canonical_sha256"] == sha256(aar_raw), f"seed {seed}: canonical AAR hash mismatch")
        check(final_aar["compatibility_sha256"] == sha256(compat_raw), f"seed {seed}: compat AAR hash mismatch")

        checkpoints = index["checkpoints"]
        ticks = [entry["tick"] for entry in checkpoints]
        check(ticks == sorted(set(ticks)), f"seed {seed}: checkpoint ticks not strictly increasing")
        final_tick = index["final_tick"]
        required = {0, 100, 600, 1200, final_tick}
        required.update(range(1200, final_tick + 1, 1200))
        check(required.issubset(ticks), f"seed {seed}: missing required ticks {sorted(required - set(ticks))}")
        check(checkpoints[-1]["tick"] == final_tick, f"seed {seed}: last checkpoint is not final")
        check("final" in checkpoints[-1]["reasons"], f"seed {seed}: final reason missing")
        arrival_ticks = {
            event["tick"] + 1
            for event in aar["timeline"]
            if event["type"] == "reinforcement_arrived"
        }
        check(arrival_ticks.issubset(ticks), f"seed {seed}: missing arrival checkpoints {arrival_ticks - set(ticks)}")
        for tick in arrival_ticks:
            entry = checkpoints[ticks.index(tick)]
            check("reinforcement_arrival" in entry["reasons"], f"seed {seed}: arrival reason missing at {tick}")
            check(tick - 1 in entry["arrival_event_ticks"], f"seed {seed}: arrival event tick missing at {tick}")

        detail_count = 0
        for entry in checkpoints:
            relative = entry["detail_file"]
            if seed_info["detailed_snapshots"]:
                check(bool(relative), f"seed {seed}: missing detail path at {entry['tick']}")
            if not relative:
                continue
            snapshot, snapshot_raw = read_artifact(seed_dir / relative)
            detail_count += 1
            detailed_files += 1
            if snapshot is None:
                continue
            check(len(snapshot_raw) == entry["bytes"], f"seed {seed}: snapshot size mismatch at {entry['tick']}")
            check(sha256(snapshot_raw) == entry["sha256"], f"seed {seed}: snapshot hash mismatch at {entry['tick']}")
            check(snapshot["seed"] == seed, f"seed {seed}: snapshot seed mismatch at {entry['tick']}")
            check(snapshot["checkpoint"]["tick"] == entry["tick"], f"seed {seed}: checkpoint tick mismatch")
            check(snapshot["clock"]["tick"] == entry["tick"], f"seed {seed}: clock tick mismatch")
            check(snapshot["checkpoint"]["reasons"] == entry["reasons"], f"seed {seed}: reasons mismatch at {entry['tick']}")
            check(
                snapshot["checkpoint"]["arrival_event_ticks"] == entry["arrival_event_ticks"],
                f"seed {seed}: arrival metadata mismatch at {entry['tick']}",
            )
            check(snapshot["engine_hashes"]["world"] == entry["engine_state_hash"], f"seed {seed}: engine hash mismatch at {entry['tick']}")
            check(snapshot_counts(snapshot) == entry["counts"], f"seed {seed}: key counts mismatch at {entry['tick']}")
        if seed_info["detailed_snapshots"]:
            check(detail_count == len(checkpoints), f"seed {seed}: detailed checkpoint count mismatch")
        seed_reports.append({
            "seed": seed,
            "winner": seed_info["winner"],
            "reason": seed_info["reason"],
            "result_tick": seed_info["result_tick"],
            "final_tick": final_tick,
            "checkpoints": len(checkpoints),
            "detailed_snapshots": detail_count,
            "arrival_checkpoints": len(arrival_ticks),
            "aar_sha256": sha256(aar_raw),
            "baseline_matches_excluding_real_sec": True,
        })

    for name, raw in (("schema.json", schema_raw), ("run_manifest.json", run_raw)):
        mirror = golden / name
        check(mirror.is_file(), f"missing golden root file: {mirror}")
        if mirror.is_file():
            check(mirror.read_bytes() == raw, f"golden root file differs: {mirror}")
    if detailed_files:
        check(nan_tokens > 0, "no NaN f64 tokens found in detailed fixtures")

    repeat_checked = False
    if args.repeat:
        repeat_error_count = len(errors)
        repeat = Path(args.repeat)
        repeat_run_path = repeat / "run_manifest.json"
        check(repeat_run_path.is_file(), f"missing repeat manifest: {repeat_run_path}")
        if repeat_run_path.is_file():
            repeat_run = load(repeat_run_path)
            check(
                repeat_run.get("runner", {}).get("max_ticks_override") == 0,
                "repeat run used a tick limit",
            )
            for seed_info in run.get("seeds", []):
                seed = seed_info["seed"]
                for file_info in seed_info["files"]:
                    relative = f"seed_{seed}/{file_info['file']}"
                    original = root / relative
                    duplicate = repeat / relative
                    check(duplicate.is_file(), f"missing repeat file: {duplicate}")
                    if original.is_file() and duplicate.is_file():
                        check(original.read_bytes() == duplicate.read_bytes(), f"non-deterministic repeat: {relative}")
            for name in ("schema.json", "run_manifest.json"):
                check((root / name).read_bytes() == (repeat / name).read_bytes(), f"non-deterministic repeat: {name}")
            repeat_checked = len(errors) == repeat_error_count

    report = {
        "schema": "echelon.battle_oracle.validation.v1",
        "ok": not errors,
        "root": str(root).replace("\\", "/"),
        "golden": str(golden).replace("\\", "/"),
        "repeat_root": str(args.repeat).replace("\\", "/") if args.repeat else "",
        "repeat_byte_identical": repeat_checked,
        "files_checked": checked_files,
        "bytes_checked": checked_bytes,
        "detailed_snapshots_checked": detailed_files,
        "provenance_files_checked": provenance_files,
        "nan_f64_tokens_observed": nan_tokens,
        "baseline_comparison": "exact compatibility bytes and semantic JSON after removing only the real_sec line",
        "seeds": seed_reports,
        "errors": errors,
    }
    return report


def main():
    parser = argparse.ArgumentParser(description="Validate Echelon Godot battle oracle fixtures")
    parser.add_argument("--root", default="build/unity_battle_oracle")
    parser.add_argument("--golden", default="tests/golden/battle")
    parser.add_argument("--baselines", default="build/balance/after")
    parser.add_argument("--project", default=".")
    parser.add_argument("--repeat", default="")
    parser.add_argument("--seeds", type=int, default=8)
    parser.add_argument("--no-write", action="store_true")
    args = parser.parse_args()
    try:
        report = validate(args)
    except Exception as exc:
        print(f"battle-oracle validation failed: {exc}", file=sys.stderr)
        return 2
    if not args.no_write:
        raw = canonical_bytes(report)
        Path(args.root, "validation.json").write_bytes(raw)
        if report["ok"]:
            Path(args.golden).mkdir(parents=True, exist_ok=True)
            Path(args.golden, "validation.json").write_bytes(raw)
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0 if report["ok"] else 1


if __name__ == "__main__":
    sys.exit(main())
