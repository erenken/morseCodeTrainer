# Copyright © 2026 myNOC LLC. All rights reserved.
"""Fail closed on missing reports; gate total and changed shared C# and JavaScript lines."""
import argparse
import glob
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET


def source_path(value):
    value = value.replace("\\", "/")
    index = value.find("src/")
    return value[index:] if index >= 0 else value


def in_scope(path):
    return path.startswith("src/MorseCodeTrainer.UI/wwwroot/") and path.endswith(".js") or path.endswith(".cs") and "/obj/" not in path and (
        path.startswith("src/MorseCodeTrainer.Core/")
        or path.startswith("src/MorseCodeTrainer.UI/Services/")
    )


def read_coverage(pattern):
    reports = glob.glob(pattern, recursive=True)
    if not reports:
        raise ValueError("No Cobertura report found. Tests must run with coverage enabled.")
    coverage = {}
    for report in reports:
        root = ET.parse(report)
        sources = [source.text or "" for source in root.findall(".//sources/source")]
        for item in root.findall(".//class"):
            path = source_path(item.attrib["filename"])
            if not in_scope(path):
                path = next((source_path(source.rstrip("/\\") + "/" + path) for source in sources
                             if in_scope(source_path(source.rstrip("/\\") + "/" + path))), path)
            if not in_scope(path):
                continue
            lines = coverage.setdefault(path, {})
            for line in item.findall("./lines/line"):
                number = int(line.attrib["number"])
                lines[number] = lines.get(number, False) or int(line.attrib["hits"]) > 0
    if not any(coverage.values()):
        raise ValueError("Report contains no executable shared C# lines.")
    return coverage


def add_lcov(coverage, report):
    path = None
    found = False
    for line in Path(report).read_text(encoding="utf-8").splitlines():
        if line.startswith("SF:"):
            path = source_path(line[3:])
        elif path and in_scope(path) and line.startswith("DA:"):
            number, hits, *_ = line[3:].split(",")
            lines = coverage.setdefault(path, {})
            lines[int(number)] = lines.get(int(number), False) or int(hits) > 0
            found = True
    if not found:
        raise ValueError("No shared JavaScript coverage found in LCOV report.")


def parse_diff(diff):
    changed = {}
    path = None
    for line in diff.splitlines():
        if line.startswith("+++ "):
            path = line[6:] if line.startswith("+++ b/") else None
        elif path and in_scope(path) and line.startswith("@@"):
            match = re.search(r"\+(\d+)(?:,(\d+))? @@", line)
            if not match:
                raise ValueError("Could not parse a Git diff hunk.")
            start = int(match[1])
            count = int(match[2]) if match[2] is not None else 1
            changed.setdefault(path, set()).update(range(start, start + count))
    return changed


def check(coverage, changed, minimum):
    all_lines = [hit for lines in coverage.values() for hit in lines.values()]
    total = 100 * sum(all_lines) / len(all_lines)
    print(f"Shared code line coverage: {total:.2f}% ({sum(all_lines)}/{len(all_lines)})")
    passed = total >= minimum
    if changed is not None:
        missing = [p for p, lines in changed.items() if lines and p not in coverage]
        if missing:
            raise ValueError("Changed source files missing from coverage: " + ", ".join(missing))
        patch = [hit for path, lines in coverage.items() for line, hit in lines.items() if line in changed.get(path, set())]
        if patch:
            rate = 100 * sum(patch) / len(patch)
            print(f"Changed shared code line coverage: {rate:.2f}% ({sum(patch)}/{len(patch)})")
            passed &= rate >= minimum
        else:
            print("No changed executable shared code lines; total coverage still enforced.")
    return passed


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--report", default="TestResults/**/coverage.cobertura.xml")
    parser.add_argument("--base", help="Trusted PR base commit SHA; compared to HEAD using the merge base.")
    parser.add_argument("--lcov", help="JavaScript LCOV coverage report.")
    parser.add_argument("--minimum", type=float, default=90)
    args = parser.parse_args()
    if not 0 <= args.minimum <= 100:
        parser.error("Minimum must be 0–100.")
    try:
        coverage = read_coverage(args.report)
        if args.lcov:
            add_lcov(coverage, args.lcov)
        changed = None
        if args.base:
            if not re.fullmatch(r"[a-fA-F0-9]{40,64}", args.base):
                raise ValueError("Base must be a full commit SHA.")
            diff = subprocess.run(["git", "-c", "core.quotepath=false", "diff", "--no-ext-diff", "--no-textconv", "--no-renames", "--unified=0", args.base + "...HEAD", "--", "src"], check=True, capture_output=True, encoding="utf-8").stdout
            changed = parse_diff(diff)
        return 0 if check(coverage, changed, args.minimum) else 1
    except (ValueError, OSError, ET.ParseError, subprocess.CalledProcessError) as error:
        print(f"Coverage gate failed: {error}")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
