#!/usr/bin/env python3
"""
license-sweep.py — convert Herald.Sinks from MIT/MMP LLC to Apache 2.0/MMPWorks LLC.

Mechanical sweep across every per-file header in the Herald.Sinks tree:
- `// Copyright (c) 2026 MMP LLC` → `// Copyright (c) 2026 MMPWorks LLC`
- `// Licensed under the MIT License. See LICENSE in the project root.` →
  `// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.`
- Same swap for `#` comment prefix (CAPABILITY.yaml).

Idempotent: re-running the script over already-converted files is a no-op.

Targets:
    .cs files       (371 expected)
    CAPABILITY.yaml (97 expected)

Run from anywhere; the script resolves the Herald.Sinks root relative to its own location.
"""
from __future__ import annotations

import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
SINKS_ROOT = HERE.parent  # tools/.. = Herald.Sinks/

# Two-line header substitutions. Order matters: longer/more-specific replacements
# come first so we never partially rewrite a line that the next rule would also
# match.
SUBSTITUTIONS_CS = [
    # New canonical header (already-correct files — skip)
    ("// Copyright (c) 2026 MMPWorks LLC", "// Copyright (c) 2026 MMPWorks LLC"),
    ("// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.",
     "// Licensed under the Apache License, Version 2.0. See LICENSE in the project root."),
    # MMP LLC → MMPWorks LLC
    ("// Copyright (c) 2026 MMP LLC",
     "// Copyright (c) 2026 MMPWorks LLC"),
    # Author-keyed variant (FormBuilderDSL pattern — defensive in case any leaked in)
    ("// Copyright (c) 2026 Steve Muchow",
     "// Copyright (c) 2026 MMPWorks LLC"),
    # MIT → Apache 2.0
    ("// Licensed under the MIT License. See LICENSE in the project root.",
     "// Licensed under the Apache License, Version 2.0. See LICENSE in the project root."),
]

SUBSTITUTIONS_YAML = [
    ("# Copyright (c) 2026 MMPWorks LLC", "# Copyright (c) 2026 MMPWorks LLC"),
    ("# Licensed under the Apache License, Version 2.0. See LICENSE in the project root.",
     "# Licensed under the Apache License, Version 2.0. See LICENSE in the project root."),
    ("# Copyright (c) 2026 MMP LLC",
     "# Copyright (c) 2026 MMPWorks LLC"),
    ("# Copyright (c) 2026 Steve Muchow",
     "# Copyright (c) 2026 MMPWorks LLC"),
    ("# Licensed under the MIT License. See LICENSE in the project root.",
     "# Licensed under the Apache License, Version 2.0. See LICENSE in the project root."),
]

# README and other .md files: HTML-comment-style headers + occasional code-block
# samples in programming-guide.md. The leading-spaces variant ("  Copyright ...")
# matches the existing convention used in per-sink README.md files.
SUBSTITUTIONS_MD = [
    # Author-name swap inside HTML-comment headers
    ("  Copyright (c) 2026 MMP LLC",
     "  Copyright (c) 2026 MMPWorks LLC"),
    ("  Licensed under the MIT License. See LICENSE in the project root.",
     "  Licensed under the Apache License, Version 2.0. See LICENSE in the project root."),
    # Code-block samples inside programming-guide.md (two-space indent or none)
    ("// Copyright (c) 2026 MMP LLC",
     "// Copyright (c) 2026 MMPWorks LLC"),
    ("// Licensed under the MIT License. See LICENSE in the project root.",
     "// Licensed under the Apache License, Version 2.0. See LICENSE in the project root."),
]

# Skip directories we should never touch.
SKIP_DIR_NAMES = {"bin", "obj", "node_modules", ".git", ".vs"}


def iter_target_files(root: Path) -> list[tuple[Path, list[tuple[str, str]]]]:
    """Walk the tree and return (path, substitutions) pairs."""
    results: list[tuple[Path, list[tuple[str, str]]]] = []
    for path in root.rglob("*"):
        if not path.is_file():
            continue
        if any(part in SKIP_DIR_NAMES for part in path.parts):
            continue
        if path.suffix == ".cs":
            results.append((path, SUBSTITUTIONS_CS))
        elif path.name == "CAPABILITY.yaml":
            results.append((path, SUBSTITUTIONS_YAML))
        elif path.suffix == ".md":
            results.append((path, SUBSTITUTIONS_MD))
    return results


def apply_subs(text: str, subs: list[tuple[str, str]]) -> tuple[str, bool]:
    """Apply each substitution. Returns (new_text, changed)."""
    original = text
    for old, new in subs:
        if old == new:
            # No-op (idempotent guard for already-converted text).
            continue
        text = text.replace(old, new)
    return text, text != original


def main() -> int:
    targets = iter_target_files(SINKS_ROOT)
    print(f"license-sweep: scanning {len(targets)} file(s) under {SINKS_ROOT}")

    converted = 0
    skipped = 0
    for path, subs in targets:
        try:
            text = path.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            print(f"  skip (binary): {path.relative_to(SINKS_ROOT)}")
            skipped += 1
            continue
        new_text, changed = apply_subs(text, subs)
        if changed:
            path.write_text(new_text, encoding="utf-8", newline="\n" if "\r\n" not in text else None)
            converted += 1
        else:
            skipped += 1

    print(f"license-sweep: converted {converted} file(s); skipped {skipped} (already-current or unmatched).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
