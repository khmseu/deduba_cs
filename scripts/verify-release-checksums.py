#!/usr/bin/env python3
"""
verify-release-checksums.py

Verify release artifacts checksums listed in a release metadata file.

Usage:
  python3 scripts/verify-release-checksums.py --meta release-files/release-metadata.json --dir release-files

This script returns exit code 0 on success, and non-zero if checks fail.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
from typing import Optional


def sha512_of_file(path: str) -> str:
    h = hashlib.sha512()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def main(argv: Optional[list[str]] = None) -> int:
    argv = argv or sys.argv[1:]
    parser = argparse.ArgumentParser(description="Verify release artifact checksums")
    parser.add_argument(
        "--meta",
        "--meta-file",
        dest="meta",
        default="release-files/release-metadata.json",
        help="Release metadata JSON file",
    )
    parser.add_argument(
        "--dir",
        dest="dir",
        default="release-files",
        help="Directory containing release files",
    )
    args = parser.parse_args(argv)

    meta_path = args.meta
    artifact_dir = args.dir

    if not os.path.exists(meta_path):
        print(f"No release metadata found at: {meta_path}; skipping verification")
        return 0

    try:
        with open(meta_path, "r", encoding="utf-8") as fh:
            meta = json.load(fh)
    except Exception as e:
        print(f"Failed to read metadata {meta_path}: {e}", file=sys.stderr)
        return 1

    errors = 0
    for entry in meta.get("artifacts", []):
        basename = entry.get("path")
        expected = entry.get("sha512")
        p = os.path.join(artifact_dir, basename)
        if not os.path.exists(p):
            print(f"Missing artifact: {p}")
            errors += 1
            continue
        try:
            actual = sha512_of_file(p)
        except Exception as e:
            print(f"Error computing sha512 for {p}: {e}", file=sys.stderr)
            errors += 1
            continue
        if expected is None:
            print(f"No expected sha512 present for {basename}; skipping")
            continue
        if actual != expected:
            print(
                f"Checksum mismatch for {basename}: expected {expected}, got {actual}"
            )
            errors += 1
        else:
            print(f"Verified: {basename}")

    if errors:
        print("Verification failed")
        return 1

    print("Artifact checksum verification passed")
    return 0


if __name__ == "__main__":
    rc = main()
    sys.exit(rc)
