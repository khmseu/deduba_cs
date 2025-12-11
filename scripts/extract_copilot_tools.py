#!/usr/bin/env python3
import glob
import json
import os
import re
import sys

home = os.path.expanduser("~")
ext_dirs = []
ext_dirs += glob.glob(os.path.join(home, ".vscode", "extensions", "*"))
ext_dirs += glob.glob(os.path.join(home, ".vscode-server", "extensions", "*"))

# Collect extensions but keep only the newest version per (publisher, name)
ext_map = {}


def _version_tuple(v):
    """Return a tuple for comparing version strings by numeric components.

    Examples: '1.2.3' -> (1,2,3). Non-numeric parts are ignored.
    If no numeric parts, return an empty tuple.
    """
    if not v:
        return ()
    parts = re.findall(r"\d+", str(v))
    try:
        return tuple(int(p) for p in parts)
    except Exception:
        return tuple()


for d in sorted(set(ext_dirs)):
    if not os.path.isdir(d):
        continue
    pj = os.path.join(d, "package.json")
    if not os.path.isfile(pj):
        continue
    try:
        with open(pj, "r", encoding="utf-8") as fh:
            pkg = json.load(fh)
    except Exception as e:
        print(f"warning: failed to load {pj}: {e}", file=sys.stderr)
        continue
    contributes = pkg.get("contributes") or {}
    lm = contributes.get("languageModelTools")
    if not lm:
        continue
    tools = []
    for t in lm:
        # ensure serializable
        try:
            tools.append(t)
        except Exception as e:
            print(
                f"warning: skipping tool in {d} because of error: {e}", file=sys.stderr
            )
            # best-effort: skip problematic tool entry
            continue

    publisher = pkg.get("publisher") or ""
    name = pkg.get("name") or os.path.basename(d)
    version = pkg.get("version") or ""

    key = (publisher, name)
    this_entry = {
        "extension_dir": d,
        "publisher": publisher,
        "name": name,
        "displayName": pkg.get("displayName"),
        "version": version,
        "package_json": pj,
        "tools": tools,
    }

    # compare with existing; keep the one with the greater version tuple
    if key in ext_map:
        existing = ext_map[key]
        if _version_tuple(version) > _version_tuple(existing.get("version")):
            ext_map[key] = this_entry
        else:
            # keep existing (newer or equal)
            pass
    else:
        ext_map[key] = this_entry

results = list(ext_map.values())

# write output into repo .github
out_dir = os.path.join(os.getcwd(), ".github")
os.makedirs(out_dir, exist_ok=True)
out_file = os.path.join(out_dir, "copilot-languageModelTools.json")
with open(out_file, "w", encoding="utf-8") as fh:
    json.dump(results, fh, indent=2, ensure_ascii=False)

count_tools = sum(len(e["tools"]) for e in results)
print(f"Wrote {count_tools} tools from {len(results)} extensions to {out_file}")
