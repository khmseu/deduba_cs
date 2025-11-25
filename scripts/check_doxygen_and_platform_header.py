#!/usr/bin/env python3
"""
scripts/check_doxygen_and_platform_header.py

Verifies:
  1) Doxygen emits zero warnings
  2) Platform.h is the first include in native shim .cpp files

This script mirrors the behavior of the previous bash script but written
in Python for more robust parsing and clearer error messages.
"""

import argparse
import re
import shutil
import subprocess
import sys
from pathlib import Path

ROOT_DIR = Path(__file__).resolve().parents[1]
DOXYFILE = ROOT_DIR / "docs" / "Doxyfile"

SHIM_DIRS = [
    ROOT_DIR / "OsCallsCommonShim",
    ROOT_DIR / "OsCallsLinuxShim",
    ROOT_DIR / "OsCallsWindowsShim",
]


def run_doxygen(doxyfile: Path) -> subprocess.CompletedProcess:
    cmd = ["doxygen", str(doxyfile)]
    # run doxygen in docs directory (so that INPUT paths are relative)
    proc = subprocess.run(cmd, cwd=doxyfile.parent, capture_output=True, text=True)
    return proc


def find_first_include(file_path: Path) -> str | None:
    """
    Return the first #include line that is not preceded by comments or blank lines.
    If no include is found, return None.
    """
    in_block_comment = False
    include_re = re.compile(r"^[ \t]*#\s*include\s*[<\"]([^>\"]+)[>\"]")
    with file_path.open("r", errors="replace") as fh:
        for line in fh:
            s = line.rstrip("\n")
            # strip trailing CR
            if s.endswith("\r"):
                s = s[:-1]
            # handle block comments
            if in_block_comment:
                if "*/" in s:
                    # remove up to end of block comment and continue processing the rest
                    s = s.split("*/", 1)[1]
                    in_block_comment = False
                else:
                    continue
            # check for start of block comment
            if s.strip().startswith("/*"):
                if "*/" in s:
                    s = s.split("*/", 1)[1]
                else:
                    in_block_comment = True
                    continue
            # strip line comments
            if s.strip().startswith("//"):
                continue
            # if blank, continue
            if s.strip() == "":
                continue
            # check include
            m = include_re.match(s)
            if m:
                return m.group(1)
            # if some other content occurs first (not include), stop
            # i.e., include must be the first non-comment statement
            break
    return None


def check_includes(root: Path) -> list[str]:
    failed = []
    for dir_path in SHIM_DIRS:
        src_dir = dir_path / "src"
        if not src_dir.exists():
            continue
        for cpp_file in src_dir.rglob("*.cpp"):
            header = find_first_include(cpp_file)
            if header is None:
                continue
            if header != "Platform.h":
                failed.append(f"{cpp_file} (first include: {header})")
    return failed


def main() -> int:
    parser = argparse.ArgumentParser(
        description="CI checks: Doxygen warnings and Platform.h include ordering"
    )
    parser.add_argument(
        "--doxygen-path", default=str(DOXYFILE), help="Path to Doxyfile"
    )
    args = parser.parse_args()

    doxyfile_path = Path(args.doxygen_path)
    if not doxyfile_path.exists():
        print(f"Doxygen configuration not found at {doxyfile_path}", file=sys.stderr)
        return 1

    if not shutil.which("doxygen"):
        print(
            "Doxygen is required but not found. Please install it: apt-get install doxygen",
            file=sys.stderr,
        )
        return 1

    print(f"Running doxygen ({doxyfile_path}) — capturing output")
    proc = run_doxygen(doxyfile_path)
    output = proc.stdout + proc.stderr

    if proc.returncode != 0:
        print("Doxygen returned a non-zero status — failing CI. See output below:")
        print(output)
        return 1

    # find 'warning:' in doxygen output (case-insensitive)
    warnings = [line for line in output.splitlines() if "warning:" in line.lower()]
    if warnings:
        print("Doxygen reported warnings — failing CI. Warnings:")
        print("----------------------------------------")
        print("\n".join(warnings))
        print("----------------------------------------")
        return 1

    # Now verify Platform.h is the first include in each native shim .cpp file
    failed_files = check_includes(ROOT_DIR)
    if failed_files:
        print("Found files where the first #include is not Platform.h:")
        for f in failed_files:
            print(" - " + f)
        print(
            'Please ensure that the first include in native shim .cpp files is "Platform.h".'
        )
        return 1

    print(
        "Doxygen produced no warnings and Platform.h is the first include for all shim .cpp files."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
