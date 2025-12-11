#!/usr/bin/env python3
import csv
import json
import os
import re

INPUT = os.path.join(os.getcwd(), ".github", "copilot-languageModelTools.json")
OUTPUT = os.path.join(os.getcwd(), ".github", "copilot-languageModelTools.csv")

with open(INPUT, "r", encoding="utf-8") as fh:
    data = json.load(fh)

rows = []
for ext in data:
    ext_name = ext.get("name") or ext.get("displayName") or ""
    publisher = ext.get("publisher") or ""
    version = ext.get("version") or ""
    for t in ext.get("tools", []) or []:
        tool_ref = t.get("toolReferenceName", "")
        tool_name = t.get("name", "")
        can_ref = t.get("canBeReferencedInPrompt", False)
        # collect inputSchema properties keys
        input_schema = t.get("inputSchema") or {}
        props = (
            input_schema.get("properties") if isinstance(input_schema, dict) else None
        )
        if isinstance(props, dict):
            prop_keys = "|".join(sorted(k for k in props.keys()))
        else:
            prop_keys = ""
        model_desc = t.get("modelDescription", "")
        rows.append(
            (
                ext_name,
                publisher,
                version,
                tool_ref,
                tool_name,
                str(can_ref).lower(),
                prop_keys,
                model_desc,
            )
        )


def _escape_control_chars(s: str) -> str:
    r"""Escape control characters in s using C-style escapes.

    - \n, \r, \t, \b, \f are escaped
    - backslash is escaped
    - other C0 controls (code < 0x20) are encoded as \xHH
    """
    if not isinstance(s, str):
        s = str(s)
    # escape backslash first
    s = s.replace("\\", "\\\\")
    # common escapes
    s = s.replace("\n", "\\n").replace("\r", "\\r").replace("\t", "\\t")
    s = s.replace("\b", "\\b").replace("\f", "\\f")

    # any remaining control chars (0x00-0x1f) -> \xHH
    def repl(m):
        ch = m.group(0)
        return "\\x%02x" % ord(ch)

    return re.sub(r"[\x00-\x1f]", repl, s)


# write CSV with quoting
with open(OUTPUT, "w", newline="", encoding="utf-8") as csvf:
    writer = csv.writer(csvf)
    writer.writerow(
        [
            " extension_name",
            " publisher",
            " version",
            " toolReferenceName",
            " tool_name",
            " canBeReferencedInPrompt",
            " inputSchema.properties",
            " modelDescription",
        ]
    )
    for r in rows:
        # ensure strings and escape control characters
        out = [
            _escape_control_chars(x)
            for x in (r[0], r[1], r[2], r[3], r[4], r[5], r[6], r[7])
        ]
        writer.writerow(out)

print(f"Wrote {len(rows)} rows to {OUTPUT}")
