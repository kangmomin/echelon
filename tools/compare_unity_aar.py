from __future__ import annotations

import argparse
import json
from pathlib import Path


def normalized(value):
    if isinstance(value, dict):
        return {key: normalized(item) for key, item in value.items() if key != "real_sec"}
    if isinstance(value, list):
        return [normalized(item) for item in value]
    return value


def differences(expected, actual, path="$"):
    if type(expected) is not type(actual):
        return [f"{path}: type {type(expected).__name__} != {type(actual).__name__}"]
    if isinstance(expected, dict):
        problems = []
        for key in sorted(set(expected) | set(actual)):
            if key not in expected:
                problems.append(f"{path}.{key}: missing in reference")
            elif key not in actual:
                problems.append(f"{path}.{key}: missing in candidate")
            else:
                problems.extend(differences(expected[key], actual[key], f"{path}.{key}"))
        return problems
    if isinstance(expected, list):
        if len(expected) != len(actual):
            return [f"{path}: length {len(expected)} != {len(actual)}"]
        problems = []
        for index, (left, right) in enumerate(zip(expected, actual)):
            problems.extend(differences(left, right, f"{path}[{index}]"))
        return problems
    if isinstance(expected, (int, float)) and not isinstance(expected, bool) and isinstance(actual, (int, float)) and not isinstance(actual, bool):
        if expected == actual or (expected != expected and actual != actual):
            return []
        difference = abs(expected - actual)
        if difference <= max(1e-9, abs(expected) * 1e-9):
            return []
        return [f"{path}: {expected!r} != {actual!r} (delta {difference:.3g})"]
    if expected != actual:
        return [f"{path}: {expected!r} != {actual!r}"]
    return []


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--reference", default="build/balance/after")
    parser.add_argument("--candidate", default="build/balance/unity")
    parser.add_argument("--max-diffs", type=int, default=30)
    args = parser.parse_args()
    mismatches = 0
    for seed in range(1, 9):
        expected_path = Path(args.reference) / f"aar_{seed}.json"
        actual_path = Path(args.candidate) / f"aar_{seed}.json"
        expected = normalized(json.loads(expected_path.read_text(encoding="utf-8")))
        actual = normalized(json.loads(actual_path.read_text(encoding="utf-8")))
        problems = differences(expected, actual)
        if problems:
            mismatches += 1
            print(f"seed={seed} MISMATCH count={len(problems)}")
            for problem in problems[: args.max_diffs]:
                print("  " + problem)
        else:
            print(f"seed={seed} MATCH")
    print(f"matched={8 - mismatches} mismatched={mismatches}")
    return 1 if mismatches else 0


if __name__ == "__main__":
    raise SystemExit(main())
